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
            return "HPS_ModName".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("HPS_ModName".Translate());
            listing.Gap();

            listing.CheckboxLabeled(
                "HPS_ExcludeLifecycleMethods".Translate(),
                ref settings.ExcludeCommonLifecycleMethods,
                "HPS_ExcludeLifecycleMethodsTooltip".Translate());

            listing.CheckboxLabeled(
                "HPS_ExcludeCommunityLibraries".Translate(),
                ref settings.ExcludeCommunityLibraries,
                "HPS_ExcludeCommunityLibrariesTooltip".Translate());

            listing.Gap();

            if (listing.ButtonText("HPS_OpenScanner".Translate()))
            {
                Find.WindowStack.Add(new Dialog_HarmonyPatchScanner(settings));
            }

            listing.GapLine();
            listing.Label("HPS_ReportsLocation".Translate());
            listing.Label("HPS_ExportedFiles".Translate());

            listing.End();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
        }
    }
}
#endif
