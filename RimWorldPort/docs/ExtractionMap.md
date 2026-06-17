# Harmony Patch Scanner Extraction Map

Source reviewed: local Bannerlord project plus the Nexus description for mod 9179.

## Required Behavior

The Nexus page describes the utility as a modder tool that scans all loaded Harmony
patches, shows patched methods, detects multiple mods patching the same methods, and
writes text logs. Version notes also call out load-order reporting, first-run ordering
in conflicts, community-library filtering, prefix short-circuit warnings, and full mod
load-order headers.

## Original File Roles

| File | Role | Port decision |
| --- | --- | --- |
| `SubModule.cs` | Bannerlord lifecycle and `Harmony.PatchAll()` bootstrap. | Replace with RimWorld `Mod`/static constructor/bootstrap. |
| `ScannerSettings.cs` | MCM settings, buttons, filters, module dropdown. | Replace with RimWorld settings/debug UI. |
| `ModuleWrapper.cs` | MCM dropdown display wrapper. | Drop. |
| `ModuleLoadOrderHelper.cs` | Bannerlord launcher load order and module-to-assembly map. | Replace with `IPatchScannerHost.GetLoadOrder()`. |
| `FileHelper.cs` | Bannerlord module log folder. | Replace with `IPatchScannerHost.GetLogDirectory()`. |
| `FilterHelper.cs` | Lifecycle and community-library filters. | Extracted into options plus host community-library classification. |
| `PatchInfo.cs` | Data model for one Harmony patch. | Extracted as `PatchRecord`. |
| `PatchProcessor.cs` | Converts Harmony `Patch` objects to records. | Extracted as `HarmonyPatchScanEngine`. |
| `PatchDisplayHelper.cs` | Priority/index/load-order/method formatting. | Extracted as `MethodNameFormatter`. |
| `PatchScanner.cs` | Full patch list report and export. | Extracted as `PatchReportBuilder.BuildAllPatchesReport()` and service export. |
| `ConflictScanner.cs` | Cross-mod conflict report and export. | Extracted as `PatchReportBuilder.BuildConflictReport()` and service export. |
| `ModuleScanner.cs` | Single selected-module report and conflict view. | Extracted as `BuildModuleReport()` and service export. |

## Core Boundary

The portable core is allowed to know about Harmony and reflection only. It must not
reference Bannerlord, Verse, Unity, RimWorld, or any settings UI framework.

The host adapter owns:

- authoritative mod load order;
- mapping assembly name to mod/package ID;
- display name for a patch owner;
- official game-code classification;
- community-library classification;
- writable log directory;
- user notification.

## RimWorld Adapter Notes

For RimWorld, the adapter should use `LoadedModManager.RunningModsListForReading` as
the load-order source, map each `ModContentPack.assemblies.loadedAssemblies` entry to
its package ID, treat `Verse`, `RimWorld`, and `Assembly-CSharp` as official targets,
and write logs somewhere stable such as `GenFilePaths.ConfigFolderPath`.

Community-library filtering should be tuned to the mod list you care about. The
template includes common starting IDs, but this list should stay easy to edit.

## Output Compatibility

The extracted core preserves the Bannerlord report concepts and output file names:

- `AllHarmonyPatches.txt`
- `DuplicateHarmonyPatches.txt`
- `ModuleScan_<module>.txt`

The text uses ASCII separators rather than Bannerlord's original box-drawing glyphs,
but keeps the same sections, counts, risk labels, ordering data, and warning types.
