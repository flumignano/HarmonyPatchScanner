#if RIMWORLD
using HarmonyPatchScanner.Core;

namespace HarmonyPatchScanner.RimWorld
{
    public static class RimWorldScannerController
    {
        private static readonly PatchScannerOptions Options = PatchScannerOptions.CreateRimWorldDefaults();

        private static PatchScannerService CreateService()
        {
            return new PatchScannerService(new RimWorldPatchScannerAdapter());
        }

        public static PatchExportResult ExportAllPatches()
        {
            return CreateService().ExportAllPatches(Options);
        }

        public static PatchExportResult ExportConflictReport()
        {
            return CreateService().ExportConflictReport(Options);
        }

        public static PatchExportResult ExportModuleReport(string packageId)
        {
            return CreateService().ExportModuleReport(Options, packageId);
        }

        public static PatchExportResult ExportModuleConflictReport(string packageId)
        {
            return CreateService().ExportModuleConflictReport(Options, packageId);
        }
    }
}
#endif
