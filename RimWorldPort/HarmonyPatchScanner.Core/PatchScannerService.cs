using System;
using System.IO;
using System.Linq;

namespace HarmonyPatchScanner.Core
{
    public sealed class PatchScannerService
    {
        private readonly IPatchScannerHost _host;
        private readonly HarmonyPatchScanEngine _engine;
        private readonly PatchReportBuilder _reportBuilder;

        public PatchScannerService(IPatchScannerHost host)
        {
            _host = host;
            _engine = new HarmonyPatchScanEngine(host);
            _reportBuilder = new PatchReportBuilder();
        }

        public PatchScanSnapshot Scan(PatchScannerOptions options)
        {
            return _engine.Scan(options);
        }

        public PatchExportResult ExportAllPatches(PatchScannerOptions options)
        {
            var snapshot = Scan(options);
            var report = _reportBuilder.BuildAllPatchesReport(snapshot, options);
            var filePath = WriteReport("AllHarmonyPatches.txt", report);
            var totalPatches = snapshot.Patches.Count;
            var totalTranspilers = snapshot.Patches.Count(p => p.PatchType == HarmonyPatchKind.Transpiler);
            var modCount = snapshot.Patches.Select(p => p.Owner).Distinct().Count();

            return Complete(
                snapshot,
                filePath,
                $"Scan complete! {modCount} mods / {totalPatches} patches. Transpilers: {totalTranspilers}. Results saved to {filePath}",
                PatchScannerNotificationLevel.Success);
        }

        public PatchExportResult ExportConflictReport(PatchScannerOptions options)
        {
            var snapshot = Scan(options);
            var conflicts = _reportBuilder.GetCrossModConflicts(snapshot.Patches);
            var highRisk = conflicts.Count(c => c.RiskLevel == ConflictRiskLevel.High);
            var shortCircuitConflicts = conflicts.Count(c =>
                c.Patches.Count(p => p.PatchType == HarmonyPatchKind.Prefix) > 1 &&
                c.Patches.Any(p => p.CanShortCircuit));

            var report = _reportBuilder.BuildConflictReport(snapshot, options);
            var filePath = WriteReport("DuplicateHarmonyPatches.txt", report);
            var level = highRisk > 0
                ? PatchScannerNotificationLevel.Error
                : conflicts.Count > 0
                    ? PatchScannerNotificationLevel.Warning
                    : PatchScannerNotificationLevel.Success;

            return Complete(
                snapshot,
                filePath,
                $"Conflict scan complete! {conflicts.Count} conflicts ({highRisk} high risk), {shortCircuitConflicts} short-circuit risks. Saved to {filePath}",
                level);
        }

        public PatchExportResult ExportModuleReport(PatchScannerOptions options, string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                throw new ArgumentException("Module id is required.", nameof(moduleId));

            var snapshot = Scan(options);
            var report = _reportBuilder.BuildModuleReport(snapshot, options, moduleId);
            var module = snapshot.LoadOrder.FirstOrDefault(m =>
                string.Equals(m.ModId, moduleId, StringComparison.OrdinalIgnoreCase));
            var displayName = module?.DisplayName ?? moduleId;
            var safeName = MethodNameFormatter.SanitizeFileName(displayName);
            var filePath = WriteReport($"ModuleScan_{safeName}.txt", report);

            var moduleAssemblyNames = module?.AssemblyNames ?? Array.Empty<string>();
            var modulePatchCount = snapshot.Patches.Count(p =>
                string.Equals(p.OwnerModId, moduleId, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(p.PatchAssemblyName) && moduleAssemblyNames.Contains(p.PatchAssemblyName, StringComparer.OrdinalIgnoreCase)));

            var conflictCount = snapshot.Patches
                .GroupBy(p => p.TargetMethod)
                .Count(g => g.Any(p => string.Equals(p.OwnerModId, moduleId, StringComparison.OrdinalIgnoreCase)) &&
                            g.Any(p => !string.Equals(p.OwnerModId, moduleId, StringComparison.OrdinalIgnoreCase)));

            var level = conflictCount > 0 ? PatchScannerNotificationLevel.Warning : PatchScannerNotificationLevel.Success;

            return Complete(
                snapshot,
                filePath,
                $"Module scan complete! {displayName}: {modulePatchCount} patches, {conflictCount} conflicts. Saved to {filePath}",
                level);
        }

        private string WriteReport(string fileName, string report)
        {
            var logDirectory = _host.GetLogDirectory();
            Directory.CreateDirectory(logDirectory);
            var filePath = Path.Combine(logDirectory, fileName);
            File.WriteAllText(filePath, report);
            return filePath;
        }

        private PatchExportResult Complete(
            PatchScanSnapshot snapshot,
            string filePath,
            string message,
            PatchScannerNotificationLevel level)
        {
            _host.Notify(message, level);

            return new PatchExportResult
            {
                Snapshot = snapshot,
                FilePath = filePath,
                Message = message,
                Level = level
            };
        }
    }
}
