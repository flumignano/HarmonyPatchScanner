# RimWorld Source

This folder contains the buildable RimWorld implementation of Harmony Patch Scanner.

## Projects

- `HarmonyPatchScanner.Core` contains host-independent Harmony scanning, conflict analysis, report building, and log exporting.
- `HarmonyPatchScanner.RimWorld` contains the Verse/RimWorld adapter, settings window, mod list UI, and RimWorld-specific paths/load-order integration.

## Core Entry Points

- `PatchScannerService.ExportAllPatches(options)`
- `PatchScannerService.ExportConflictReport(options)`
- `PatchScannerService.ExportModuleReport(options, moduleId)`
- `HarmonyPatchScanEngine.Scan(options)` for callers that need the in-memory scan model.

## Build

```powershell
dotnet build .\RimWorldPort\HarmonyPatchScanner.RimWorld\HarmonyPatchScanner.RimWorld.csproj -c Release /p:_EnableDefaultWindowsPlatform=false
```

The RimWorld project writes release assemblies to `1.6\Assemblies` at the repository root.

## Reports

The in-game UI exports:

- `AllHarmonyPatches.txt`
- `DuplicateHarmonyPatches.txt`
- `ModuleScan_<mod>.txt`

Reports are saved under `Config\HarmonyPatchScanner\logs` in RimWorld's user config directory.
