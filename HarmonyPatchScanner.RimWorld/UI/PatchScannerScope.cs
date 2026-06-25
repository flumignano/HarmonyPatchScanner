#if RIMWORLD
namespace HarmonyPatchScanner.RimWorld.UI
{
    // Scope is explicit UI state so identical actions cannot silently switch
    // between the full load order and one selected mod.
    internal enum PatchScannerScope
    {
        AllMods,
        SelectedMod
    }
}
#endif
