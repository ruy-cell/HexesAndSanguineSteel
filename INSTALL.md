# Install

## Requirements

- V Rising dedicated server
- BepInEx IL2CPP
- VampireCommandFramework

## Server Install

1. Copy `HexesAndSanguineSteel.dll` into:

```text
BepInEx/plugins/HexesAndSanguineSteel/
```

2. Start the server once.
3. Edit the generated config:

```text
BepInEx/config/HexesAndSanguineSteel/weapons.json
```

4. Reload in game:

```text
.csw reload
.csw validate
```

## Build From Source

```bash
dotnet restore HexesAndSanguineSteel.csproj
dotnet build HexesAndSanguineSteel.csproj -c Release
```

The DLL will be in:

```text
bin/Release/net6.0/
```
