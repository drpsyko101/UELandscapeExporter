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
				Console.WriteLine("UE Version: {0}\n", game_ver);

				var provider = new DefaultFileProvider(pakDir, System.IO.SearchOption.TopDirectoryOnly, true, new VersionContainer(game_ver));
				//provider.MappingsContainer = new FileUsmapTypeMappingsProvider(_mapping);

				provider.Initialize();
				provider.SubmitKey(new FGuid(), new FAesKey(aesKey)); // decrypt basic info (1 guid - 1 key)
																	  //provider.LoadLocalization(ELanguage.English); // explicit enough

				ZlibHelper.DownloadDll();
				ZlibHelper.Initialize("zlib-ng2.dll");

				OodleHelper.DownloadOodleDll();
				OodleHelper.Initialize("oo2core_9_win64.dll");

				var umap_exports = provider.Files.Where(file => file.Key.Contains(".umap"));
				Console.WriteLine("Processing {0} maps", umap_exports.Count());

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
						continue;

					var umap_name = umap.Value.ToString().Split('/').Last().Split('.').First();
					Console.WriteLine(System.IO.Path.Combine(outDir, umap_name, umap_name + ".json"));

					if (landscapes.Count() > 1)
						Console.WriteLine("Found multiple landscapes!");

					var landscape_name = landscapes.First().ObjectName;
					// export only landscape and child components
					var landscape_relevant = provider.LoadAllObjects(pkg.Name).Where(export => export.Outer.Name == landscape_name || export.Name == landscape_name);
					//var pkgJson = JsonConvert.SerializeObject(pkg, Formatting.Indented);

					var out_path = System.IO.Path.Combine(outDir, umap_name.Split('.').First());
					System.IO.Directory.CreateDirectory(System.IO.Path.Combine(out_path, "Heightmaps"));
					System.IO.Directory.CreateDirectory(System.IO.Path.Combine(out_path, "Weightmaps"));

					var variantJson = JsonConvert.SerializeObject(landscape_relevant, Formatting.Indented);
					File.WriteAllText(System.IO.Path.Combine(outDir, umap_name, umap_name + ".json"), variantJson);

					var textures = pkg.ExportMap.Where(export => export.ClassName == "Texture2D");

					int heightmaps = 0;
					int weightmaps = 0;

					foreach (var texExport in textures)
					{
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
							TextureDecoder.Decode(tex).Encode(SkiaSharp.SKEncodedImageFormat.Png, 100).SaveTo(s);
					}

					Console.WriteLine("Heightmaps: {0} Weightmaps: {1}", heightmaps, weightmaps);
				}
			}, pakDirOption, outDirOption, aesKeyOption, gameVerOption);


			return await rootCommand.InvokeAsync(args);


		}
	}
}
