# RimWorld Mod UI

This project is the RimWorld-facing mod layer for the extracted scanner core. It uses
the same tested Verse `Mod` + `Window` pattern as PatchDoctor and exposes the original
Bannerlord actions in-game:

- `Scan all Harmony patches` -> `AllHarmonyPatches.txt`
- `Find duplicate/conflicting patches` -> `DuplicateHarmonyPatches.txt`
- `Scan selected mod` -> `ModuleScan_<mod>.txt`
- `Exclude common lifecycle methods`
- `Exclude community libraries`

Build with:

```powershell
dotnet build .\RimWorldPort\HarmonyPatchScanner.RimWorld\HarmonyPatchScanner.RimWorld.csproj
```

The output path is `1.6/Assemblies`, so the repository root can be used as the
RimWorld mod folder once `About/About.xml` is present.
