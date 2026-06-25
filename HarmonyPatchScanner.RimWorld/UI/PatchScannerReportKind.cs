#if RIMWORLD
namespace HarmonyPatchScanner.RimWorld.UI
{
    // The visible details panel stays scoped to the last action in the active scope.
    // Tracking that action avoids guessing from localized status text.
    internal enum PatchScannerReportKind
    {
        None,
        AllPatches,
        AllConflicts,
        ModulePatches,
        ModuleConflicts
    }
}
#endif
