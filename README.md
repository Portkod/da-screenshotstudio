# DA Screenshot studio

Dark Ages Screenshot Studio: stage and render Dark Ages 7.41 scenes, and compare them with real screenshots.

Authored and produced by clankers given full access to my dev folder for 2 hours.

## Features

* Unfriendly interface
* Bugs
* Unfinished functionality
* Random fun tidbits yoinked into the project
* 100% unreviewed code
* Probably safe

## Requirements

- A Dark Ages 7.41 client install. The studio only reads its art, it never changes it.
- To build: the [.NET 10 SDK](https://dotnet.microsoft.com/download) or newer.
- To publish with native AOT on Windows: Visual Studio or the Build Tools with the **Desktop development with C++** workload.

## Build and run

```bash
dotnet run --project src/DaScreenshotStudio.App
```

## First run

Until a client folder is set, the studio opens on the **Settings** tab. Use **Browse...** next to *Client folder* and pick
the folder `Darkages.exe` is in. The maps and metafile folders inside it are filled in as you go. Each path is checked
as you type or browse, so a wrong folder shows before you apply. Then press **Apply and save**.

The settings are saved to `dass.json`. At startup the studio looks for it in the working directory and the folders
above it, and when none is found it is created in the working directory. Relative paths in it are resolved against the
folder holding it.

| Setting | Default | Notes |
| --- | --- | --- |
| Client folder | *(none)* | Required. Must hold `setoa.dat`, `national.dat` and the map archives. |
| Maps folder | `maps` in the client folder | Holds the `lod*.map` files. |
| Metafile folder | `metafile` in the client folder | Light profiles and the other metafile tables. |
| Map catalog file | `maps.json` | Created when you remember a map. |
| Items database | `Items.json` | Copied next to the exe when publishing. In the repository it is `data/Items.json`. |
| Character library file | `characters.json` | Created when you save a character. |

## Publishing

There are two ways to publish. Both are self-contained, so the target machine does not need .NET installed. Both put
`Items.json` next to the exe and leave out debug symbols. Run the commands from the repository root. Replace `win-x64`
with another [runtime identifier](https://learn.microsoft.com/dotnet/core/rid-catalog) such as `win-arm64`,
`linux-x64` or `osx-arm64` for another platform. Only `win-x64` has been tested.

### Native AOT

Compiles the app ahead of time to native code: fastest startup, no JIT.

```bash
dotnet publish src/DaScreenshotStudio.App -c Release -r win-x64 -p:PublishAot=true -o artifacts/aot
```

The output is `DaScreenshotStudio.exe` plus two native libraries, `libSkiaSharp.dll` (drawing) and
`libHarfBuzzSharp.dll` (text shaping), which have to stay next to it. Their packages ship them only as DLLs, so they
cannot be linked into the exe. Native AOT cannot cross-compile between operating systems, so build it on the OS you
publish for.

### Single file

Bundles the trimmed app, the .NET runtime and the native libraries into one exe:

```bash
dotnet publish src/DaScreenshotStudio.App -c Release -r win-x64 -p:PublishSingleFile=true -o artifacts/single
```

The output is `DaScreenshotStudio.exe` and `Items.json`. The native libraries are unpacked to a cache folder on
first start. A single-file build can be published for any OS from any OS.

### Distributing

Copy the whole output folder. Starting the exe from that folder, for example by double-clicking it, makes it the
working directory, so the bundled `Items.json` is found and `dass.json` is saved beside the exe.
