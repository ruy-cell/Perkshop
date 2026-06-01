# Build PerkShop

PerkShop targets V Rising's BepInEx IL2CPP modding environment and requires local game/modding DLL references.

## Requirements

- .NET 6 SDK
- V Rising dedicated server with BepInExPack installed
- VampireCommandFramework DLL
- Generated interop DLLs from your server environment

## Expected local folders

The project references local dependency folders beside `PerkShop.csproj`:

```text
core/
interop/
libs/
```

These folders are intentionally ignored by git.

### `core/`

Copy BepInEx/IL2CPP support DLLs into:

```text
core/core/
```

Typical source:

```text
VRisingDedicatedServer/BepInEx/core/
```

### `interop/`

Copy generated interop DLLs into:

```text
interop/
```

Typical source:

```text
VRisingDedicatedServer/BepInEx/interop/
```

### `libs/`

Copy VampireCommandFramework into:

```text
libs/VampireCommandFramework.dll
```

## Build

```bash
dotnet restore PerkShop.csproj
dotnet build PerkShop.csproj -c Release
```

Output:

```text
bin/Release/net6.0/PerkShop.dll
```

## Server installation

```text
VRisingDedicatedServer/BepInEx/plugins/PerkShop/PerkShop.dll
```

Restart the server after replacing the DLL.


## Thunderstore release package

After a successful local build, create the upload zip:

```powershell
.\pack-release.ps1
```

or:

```bash
./pack-release.sh
```

The resulting `dist/PerkShop-0.1.2-Thunderstore.zip` should contain only:

```text
PerkShop.dll
manifest.json
README.md
CHANGELOG.md
icon.png
```
