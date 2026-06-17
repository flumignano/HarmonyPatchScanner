#if RIMWORLD
using Verse;

namespace HarmonyPatchScanner.RimWorld
{
    public sealed class HarmonyPatchScannerSettings : ModSettings
    {
        public bool ExcludeCommonLifecycleMethods = true;
        public bool ExcludeCommunityLibraries = true;
        public string SelectedModuleId = string.Empty;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref ExcludeCommonLifecycleMethods, "excludeCommonLifecycleMethods", true);
            Scribe_Values.Look(ref ExcludeCommunityLibraries, "excludeCommunityLibraries", true);
            Scribe_Values.Look(ref SelectedModuleId, "selectedModuleId", string.Empty);
        }
    }
}
#endif
