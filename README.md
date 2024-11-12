### LandscapeExporter
Exports heightmap and weightmap tiles as png from all .umaps containing Landscape data.

To stitch the tiles and split weightmap layers use [python scripts in UnrealStuff](https://github.com/Knutschbert/UnrealStuff/tree/main/Modding/SDK%20Generation/UMap/Landscape/ue4parse)

Tested with UE 4.25 and 4.27 games. Does not support IoStore

Usage:

`LandscapeExtractor.exe -p "C:\Game\XYZ\Content\Paks" -u 4_25`

Options:

```bash
  -u, --ue4 <ue4> (REQUIRED)        game version (EGame suffix) [default: 4_27]
  -p, --pakdir <pakdir> (REQUIRED)  PAK directory path
  -a, --aes <aes>                   AES key [default:
                                    0x0000000000000000000000000000000000000000000000000000000000000000]
  -o, --outdir <outdir> (REQUIRED)  Output directory path [default: output]
  --version                         Show version information
  -?, -h, --help                    Show help and usage information
```

Output structure:

```
│   LandscapeExtractor.exe
└───output
    ├───Courtyard_Landscape2
    │   │   Courtyard_Landscape2.json          <- Landscape + components
    │   ├───Heightmaps                         <- Still in internal 32bit RGBE format
    │   │       Texture2D_1.png
    │   │       Texture2D_142.png
    │   │       Texture2D_154.png
    │   │       Texture2D_156.png
    │   │
    │   └───Weightmaps                         <- RGBA format, merged layers
    │           Texture2D_10.png
    │           Texture2D_163.png
    │           Texture2D_239.png
```

CUE4Parse - An Unreal Engine Archives & Packages Parsing Library in C#
------------------------------------------
Further detailed documentation is available in the [wiki](https://github.com/FabianFG/CUE4Parse/wiki)

### License:
CUE4Parse is licensed under [Apache License 2.0](https://github.com/FabianFG/CUE4Parse/blob/master/LICENSE), and licenses of third-party libraries used are listed [here](https://github.com/FabianFG/CUE4Parse/blob/master/NOTICE).
