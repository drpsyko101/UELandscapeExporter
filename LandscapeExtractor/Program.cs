using System;
using System.IO;
using System.Linq;
using System.CommandLine;
using CUE4Parse.Compression;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using CUE4Parse_Conversion.Textures;
using Newtonsoft.Json;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using System.Threading.Tasks;
using Org.BouncyCastle.Asn1;
using static SharpGLTF.Scenes.LightBuilder;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Assets.Objects.Properties;
using System.ComponentModel;
using System.Collections.Generic;

namespace LandscapeExtractor
{
	public class Program
	{
		static async Task<int> Main(string[] args)
		{
			var gameVerOption = new Option<string>(["--ue4", "-u"], () => "4_27", "game version (EGame suffix)") { IsRequired = true };
			//var pakDirOption = new Option<string>("--pakdir", () => "I:\\Epic Games\\Chivalry2_c\\TBL\\Content\\Paks", "PAK directory path") { IsRequired = true };
			var pakDirOption = new Option<string>(["--pakdir", "-p"], "PAK directory path") { IsRequired = true }; 
			var aesKeyOption = new Option<string>(["--aes", "-a"], () => "0x0000000000000000000000000000000000000000000000000000000000000000", "AES key") { IsRequired = false };
			var outDirOption = new Option<string>(["--outdir", "-o"], () => "output", "Output directory path") { IsRequired = true };

			var rootCommand = new RootCommand("Extracts heightmaps, weightmaps and Landscape json from all .umaps");
			rootCommand.AddOption(gameVerOption);
			rootCommand.AddOption(pakDirOption);
			rootCommand.AddOption(aesKeyOption);
			rootCommand.AddOption(outDirOption);

			Log.Logger = new LoggerConfiguration().WriteTo.Console(Serilog.Events.LogEventLevel.Fatal, theme: AnsiConsoleTheme.Literate).CreateLogger();

			rootCommand.SetHandler((pakDir, outDir, aesKey, gameVersionStr) =>
			{
				gameVersionStr = (String)gameVersionStr.Replace('.', '_');
				Enum.TryParse("GAME_UE" + gameVersionStr, out EGame game_ver);
				Console.WriteLine("Input directory: {0}", pakDir);
				Console.WriteLine("Output directory: {0}", System.IO.Path.GetFullPath(outDir));
				Console.WriteLine("UE Version: {0}", game_ver);

				var provider = new DefaultFileProvider(pakDir, System.IO.SearchOption.TopDirectoryOnly, true, new VersionContainer(game_ver));
				//provider.MappingsContainer = new FileUsmapTypeMappingsProvider(_mapping);

				provider.Initialize();
				provider.SubmitKey(new FGuid(), new FAesKey(aesKey)); // decrypt basic info (1 guid - 1 key)
																	  //provider.LoadLocalization(ELanguage.English); // explicit enough
				provider.LoadVirtualPaths();

				ZlibHelper.DownloadDll();
				ZlibHelper.Initialize("zlib-ng2.dll");

				OodleHelper.DownloadOodleDll();
				OodleHelper.Initialize("oo2core_9_win64.dll");

				var umap_exports = provider.Files.Where(file => file.Key.Contains(".umap"));
				Console.WriteLine("Processing {0} maps", umap_exports.Count());
				
				var validLandscapeMaps = new List<string>();

				foreach (var umap in umap_exports)
				{
					var val = umap.Value;
					//var package = provider.LoadPackage(val);

					//switch (package)
					//{
					//	case IoPackage ip:
					//		var variantJson2 = JsonConvert.SerializeObject(ip, Formatting.Indented);

					//		foreach (var import in ip.ImportMap)
					//		{
					//			var resolved = ip.ResolveObjectIndex(import);
					//			if (resolved?.Class == null) continue;
					//			var has = (resolved?.Class.Name.PlainText == ("Landscape"));
					//		}

					//		foreach (var import in ip.ExportMap)
					//		{
					//			continue;
					//			var resolved2 = ip.ResolveObjectIndex(import.GlobalImportIndex);
					//			var resolved = ip.ResolveObjectIndex(import.ClassIndex);
					//			var resolved3 = ip.ResolveObjectIndex(import.OuterIndex);
					//			if (resolved?.Class == null) continue;
					//			if (resolved3?.Name == null) continue;

					//		}
					//		break;
					//	case Package pk:
					//		var landscape2 = pk.ExportMap.Where(export => export.ClassName == "Landscape");
					//		if (!landscape2.Any())
					//			continue;
					//		break;
					//}

					Package pkg = (Package)provider.LoadPackage(val);
					var landscapes = pkg.ExportMap.Where(export => export.ClassName == "Landscape");

					if (!landscapes.Any())
					{
						Console.WriteLine("Skipping {0}", umap.Value.Name);
						continue;
					}
					validLandscapeMaps.Append(umap.Value.Name);

					var umap_name = umap.Value.ToString().Split('/').Last().Split('.').First();
					Console.WriteLine(System.IO.Path.Combine(outDir, umap_name, umap_name + ".json"));

					if (landscapes.Count() > 1)
					{
						Console.WriteLine("Found multiple landscapes ({0})!", landscapes.Count());
						foreach (var landscape in landscapes) { Console.WriteLine(landscape.ObjectName); }
					}
					var pkg_data = provider.LoadAllObjects(pkg.Name);
					var obj = provider.LoadObject(pkg.Name);
					foreach (var landscape in landscapes)
					{
						var landscape_name = landscape.ObjectName;
						// export only landscape and child components
						var landscape_relevant = pkg_data.Where(export => (export.Outer.Name == landscape_name.Text || export.Name == landscape_name.Text)).ToList();

						var landscape_components = landscape_relevant.Where(lsc => lsc.Class.Name == "LandscapeComponent");


						var textures_relevant = pkg_data.Where(export => export.Class.Name == "Texture2D" && export.Outer.Name == landscape_name.Text);
						//var textures = landscape_relevant.Where(asd => true);
						var textures = pkg.ExportMap.Where(export => export.ClassName == "Texture2D" && export.OuterIndex.Name == landscape_name.Text);
						//var texture_names = new List<string>();
						if (textures_relevant.Count() == 0)
						{
							Console.WriteLine("Found no assigned textures");
							//foreach (var lsc in landscape_components)
							//{
							//	var heightmap_name = lsc.GetOrDefault<FPackageIndex>("HeightmapTexture").Name;
							//	var weightmap_names = lsc.GetOrDefault<FPackageIndex[]>("WeightmapTextures").Select(h => h.Name);
							//	texture_names.AddRange(weightmap_names.Append(heightmap_name).ToList());
							//}
							//landscape_relevant.AddRange(pkg_data.Where(export => texture_names.Contains(export.Name)));
							//var asd = textures_relevant.Count();
						}
						//var textures = pkg.ExportMap.Where(export => export.ClassName == "Texture2D" && texture_names.Contains(export.ObjectName.Text));

						// Grab textures that weren't registerd
						//var textures_relevant = provider.LoadAllObjects(pkg.Name).Where(export => export.Class.Name == "Texture2D" && export.Outer.Name != landscape_name);
						//Console.WriteLine("Added {0} trailing textures", textures_relevant.Count());
						//landscape_relevant = landscape_relevant.Concat(textures_relevant);
						//var pkgJson = JsonConvert.SerializeObject(pkg, Formatting.Indented);


						var variantJson = JsonConvert.SerializeObject(landscape_relevant, Formatting.Indented);
						var file_path = System.IO.Path.Combine(outDir, umap_name, umap_name + ".json");
						var out_path = System.IO.Path.Combine(outDir, umap_name.Split('.').First());
						if (landscapes.Count() > 1)
						{
							file_path = System.IO.Path.Combine(outDir, umap_name, landscape_name.Text, umap_name + ".json");
							out_path = System.IO.Path.Combine(outDir, umap_name.Split('.').First(), landscape_name.Text);
						}
						System.IO.Directory.CreateDirectory(System.IO.Path.Combine(out_path, "Heightmaps"));
						System.IO.Directory.CreateDirectory(System.IO.Path.Combine(out_path, "Weightmaps"));

						File.WriteAllText(file_path, variantJson);

						//var textures = pkg.ExportMap.Where(export => export.ClassName == "Texture2D").ToList();

						int heightmaps = 0;
						int weightmaps = 0;

						foreach (var texExport in textures)
						{
							if (texExport.OuterIndex.Name != landscape_name.Text)
								Console.WriteLine("WTFFF");
							var tex = (UTexture2D)texExport.ExportObject.Value;
							var lodGrp = tex.Properties.FirstOrDefault(it => it.Name.Text.Equals("LODGroup"))?.Tag;
							var target_dir = lodGrp.GenericValue.ToString();
							switch (target_dir)
							{
								case "TEXTUREGROUP_Terrain_Heightmap":
									target_dir = System.IO.Path.Combine(out_path, "Heightmaps");
									heightmaps++;
									break;
								case "TEXTUREGROUP_Terrain_Weightmap":
									target_dir = System.IO.Path.Combine(out_path, "Weightmaps");
									weightmaps++;
									break;
								default:
									target_dir = out_path;
									break;

							}

							using (Stream s = File.OpenWrite(System.IO.Path.Combine(target_dir, tex.Name + ".png")))
							{
								var bitmap = TextureDecoder.Decode(tex);
								if (bitmap != null)
									bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100).SaveTo(s);
								else
									Console.WriteLine("Failed to decode {0}!", tex.Name);

							}

						}
						Console.WriteLine("{3} Components: {2} Heightmaps: {0} Weightmaps: {1}", heightmaps, weightmaps, landscape_components.Count(), landscape_name.Text);
					}

				}
				Console.WriteLine("Handled {0} maps:", validLandscapeMaps.Count());
				foreach (var map in validLandscapeMaps)
					Console.WriteLine(map);

			}, pakDirOption, outDirOption, aesKeyOption, gameVerOption);


			return await rootCommand.InvokeAsync(args);


		}
	}
}
