# Harmony Patch Scanner for RimWorld

Harmony Patch Scanner is an in-game RimWorld debugging and mod-development utility.

It lists loaded Harmony patches, identifies methods patched by multiple mods, highlights potential conflict risks, and exports readable diagnostic reports from the mod settings window.

## Features

- Full Harmony patch export to `AllHarmonyPatches.txt`.
- Duplicate/shared target export to `DuplicateHarmonyPatches.txt`.
- Selected-mod export to `ModuleScan_<mod>.txt`.
- Patch classification by prefix, postfix, transpiler, and finalizer.
- Owner, Harmony ID, priority, index, before/after hints, target method, and patch method reporting.
- Short-circuit prefix detection.
- RimWorld load-order and official-code tagging.
- Optional filters for common lifecycle methods and community libraries.

## Build

```powershell
dotnet build .\HarmonyPatchScanner.RimWorld\HarmonyPatchScanner.RimWorld.csproj -c Release /p:_EnableDefaultWindowsPlatform=false
```

Build output is written to `1.6\Assemblies`.

## Workshop Package

The player-facing mod package is:

- `About`
- `1.6`

## Usage

Open RimWorld, enable Harmony and this mod, then go to:

`Options -> Mod Settings -> Harmony Patch Scanner`

Reports are written under RimWorld's config folder:

`Config\HarmonyPatchScanner\logs`

## Attribution

Based on the original Harmony Patch Scanner by TadasTheLithuanian. The project is distributed under the MIT license.
