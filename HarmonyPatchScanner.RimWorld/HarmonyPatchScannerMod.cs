#if RIMWORLD
using HarmonyPatchScanner.RimWorld.UI;
using UnityEngine;
using Verse;

namespace HarmonyPatchScanner.RimWorld
{
    public sealed class HarmonyPatchScannerMod : Mod
    {
        private readonly HarmonyPatchScannerSettings settings;

        public HarmonyPatchScannerMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<HarmonyPatchScannerSettings>();
        }

        public override string SettingsCategory()
        {
            return "Harmony Patch Scanner";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("Harmony Patch Scanner");
            listing.Gap();

            listing.CheckboxLabeled(
                "Exclude common lifecycle methods",
                ref settings.ExcludeCommonLifecycleMethods,
                "Hides routine startup, loading, settings, and save/load patches from the reports.");

            listing.CheckboxLabeled(
                "Exclude community libraries",
                ref settings.ExcludeCommunityLibraries,
                "Hides common framework/library mods such as Harmony and HugsLib when possible.");

            listing.Gap();

            if (listing.ButtonText("Open Harmony Patch Scanner"))
            {
                Find.WindowStack.Add(new Dialog_HarmonyPatchScanner(settings));
            }

            listing.GapLine();
            listing.Label("Reports are written to Config/HarmonyPatchScanner/logs.");
            listing.Label("Exported files: AllHarmonyPatches.txt, DuplicateHarmonyPatches.txt, and ModuleScan_<mod>.txt.");

            listing.End();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
        }
    }
}
#endif
