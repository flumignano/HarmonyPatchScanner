# RimWorld UI

This project is the RimWorld-facing mod layer for Harmony Patch Scanner. It uses
Verse `Mod` and `Window` APIs to expose the scanner through RimWorld's mod
settings UI.

Available actions:

- In `All mods` scope, `Scan all Harmony patches` exports `AllHarmonyPatches.txt`.
- In `All mods` scope, `Find duplicate/conflicting patches` exports `DuplicateHarmonyPatches.txt`.
- In `Selected mod` scope, `Scan selected mod` exports `ModuleScan_<mod>.txt`.
- In `Selected mod` scope, `Find selected mod conflicts` exports `ModuleConflicts_<mod>.txt`.
- Static IL findings follow the active scope.
- `Exclude common lifecycle methods`.
- `Exclude community libraries`.

Build with:

```powershell
dotnet build .\HarmonyPatchScanner.RimWorld\HarmonyPatchScanner.RimWorld.csproj -c Release /p:_EnableDefaultWindowsPlatform=false
```

The output path is `1.6\Assemblies`, so the repository root can be used as the
RimWorld mod folder once `About\About.xml` is present.
