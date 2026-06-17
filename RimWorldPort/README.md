# RimWorld Port Extraction

This folder contains the isolated Harmony patch scanning feature from the Bannerlord
`HarmonyPatchScanner` project.

## What Was Extracted

- Runtime Harmony patch discovery through `Harmony.GetAllPatchedMethods()` and
  `Harmony.GetPatchInfo()`.
- Patch classification by prefix, postfix, transpiler, and finalizer.
- Patch owner, Harmony owner ID, priority, index, before/after hints, target method,
  patch method, short-circuit prefix detection, and official-code tagging.
- Full patch list export to `AllHarmonyPatches.txt`.
- Cross-mod conflict export to `DuplicateHarmonyPatches.txt`.
- Single-module report export to `ModuleScan_<module>.txt`.
- Load-order header support through a host adapter.
- Common lifecycle method filtering and community-library filtering.

## What Was Removed From The Core

- Bannerlord `MBSubModuleBase` lifecycle hooks.
- MCM settings and dropdown types.
- `TaleWorlds.Library.InformationManager` notifications.
- `TaleWorlds.Engine.Utilities` and `TaleWorlds.ModuleManager` load-order lookup.
- Bannerlord module-folder log path resolution.

Those pieces now belong in a host adapter. The RimWorld template in
`HarmonyPatchScanner.RimWorld` shows the expected shape.

## Core Entry Points

- `PatchScannerService.ExportAllPatches(options)`
- `PatchScannerService.ExportConflictReport(options)`
- `PatchScannerService.ExportModuleReport(options, moduleId)`
- `HarmonyPatchScanEngine.Scan(options)` if you need the in-memory data model.

## Porting Shape

1. Compile or copy `HarmonyPatchScanner.Core` into the RimWorld mod assembly.
2. Build `HarmonyPatchScanner.RimWorld`, which implements `IPatchScannerHost` using
   RimWorld/Verse APIs and exposes the scanner through a Verse settings window.
3. Build options with `PatchScannerOptions.CreateRimWorldDefaults()`.
4. Use the in-game settings button to open the scanner UI.
5. Logs are written under `Config/HarmonyPatchScanner/logs`.

The output file names intentionally match the Bannerlord mod so existing user habits
carry across.
