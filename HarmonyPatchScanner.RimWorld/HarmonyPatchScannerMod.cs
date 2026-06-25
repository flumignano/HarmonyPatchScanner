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

            if (listing.ButtonText("HPS_OpenScanner".Translate()))
            {
                Find.WindowStack.Add(new Dialog_HarmonyPatchScanner(settings));
            }

            listing.End();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
        }
    }
}
#endif
