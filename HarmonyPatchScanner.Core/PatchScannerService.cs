using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HarmonyPatchScanner.Core
{
    public sealed class PatchScannerService
    {
        private readonly IPatchScannerHost _host;
        private readonly HarmonyPatchScanEngine _engine;
        private readonly StaticPatchAnalyzer _staticAnalyzer;
        private readonly PatchReportBuilder _reportBuilder;

        public PatchScannerService(IPatchScannerHost host)
        {
            _host = host;
            _engine = new HarmonyPatchScanEngine(host);
            _staticAnalyzer = new StaticPatchAnalyzer();
            _reportBuilder = new PatchReportBuilder();
        }

        public PatchScanSnapshot Scan(PatchScannerOptions options)
        {
            var snapshot = _engine.Scan(options);
            var staticFindings = _staticAnalyzer.Analyze(snapshot);
            return snapshot.WithStaticFindings(staticFindings);
        }

        public IReadOnlyList<StaticPatchFinding> GetModuleStaticFindings(PatchScanSnapshot snapshot, string moduleId)
        {
            return _reportBuilder.GetModulePatches(snapshot, moduleId)
                .SelectMany(patch => patch.StaticFindings)
                .ToList();
        }

        public PatchExportResult ExportAllPatches(PatchScannerOptions options)
        {
            var snapshot = Scan(options);
            var report = _reportBuilder.BuildAllPatchesReport(snapshot, options);
            var filePath = WriteReport("AllHarmonyPatches.txt", report);
            var totalPatches = snapshot.Patches.Count;
            var totalTranspilers = snapshot.Patches.Count(p => p.PatchType == HarmonyPatchKind.Transpiler);
            var deterministicFindings = snapshot.StaticFindings.Count(f => f.Confidence == StaticFindingConfidence.Deterministic);
            var likelyFindings = snapshot.StaticFindings.Count(f => f.Confidence == StaticFindingConfidence.Likely);
            var modCount = snapshot.Patches.Select(p => p.Owner).Distinct().Count();

            return Complete(
                snapshot,
                filePath,
                _host.Translate(
                    "HPS_NotificationScanComplete",
                    modCount,
                    totalPatches,
                    totalTranspilers,
                    deterministicFindings,
                    likelyFindings,
                    filePath),
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
            var level = conflicts.Count > 0
                ? PatchScannerNotificationLevel.Warning
                : PatchScannerNotificationLevel.Success;

            return Complete(
                snapshot,
                filePath,
                _host.Translate(
                    "HPS_NotificationConflictScanComplete",
                    conflicts.Count,
                    highRisk,
                    shortCircuitConflicts,
                    filePath),
                level);
        }

        public PatchExportResult ExportModuleReport(PatchScannerOptions options, string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                throw new ArgumentException("Module id is required.", nameof(moduleId));

            var snapshot = ScanModule(options, moduleId);
            var report = _reportBuilder.BuildModuleReport(snapshot, options, moduleId);
            var module = snapshot.LoadOrder.FirstOrDefault(m =>
                string.Equals(m.ModId, moduleId, StringComparison.OrdinalIgnoreCase));
            var displayName = module?.DisplayName ?? moduleId;
            var safeName = MethodNameFormatter.SanitizeFileName(displayName);
            var filePath = WriteReport($"ModuleScan_{safeName}.txt", report);

            var modulePatchCount = _reportBuilder.GetModulePatches(snapshot, moduleId).Count;
            var conflictCount = _reportBuilder.GetModuleConflicts(snapshot, moduleId).Count;

            var level = conflictCount > 0 ? PatchScannerNotificationLevel.Warning : PatchScannerNotificationLevel.Success;

            return Complete(
                snapshot,
                filePath,
                _host.Translate(
                    "HPS_NotificationModuleScanComplete",
                    displayName,
                    modulePatchCount,
                    conflictCount,
                    filePath),
                level);
        }

        public PatchExportResult ExportModuleConflictReport(PatchScannerOptions options, string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                throw new ArgumentException("Module id is required.", nameof(moduleId));

            var snapshot = ScanModule(options, moduleId);
            var report = _reportBuilder.BuildModuleConflictReport(snapshot, options, moduleId);
            var module = snapshot.LoadOrder.FirstOrDefault(m =>
                string.Equals(m.ModId, moduleId, StringComparison.OrdinalIgnoreCase));
            var displayName = module?.DisplayName ?? moduleId;
            var safeName = MethodNameFormatter.SanitizeFileName(displayName);
            var filePath = WriteReport($"ModuleConflicts_{safeName}.txt", report);
            var modulePatchCount = _reportBuilder.GetModulePatches(snapshot, moduleId).Count;
            var conflictCount = _reportBuilder.GetModuleConflicts(snapshot, moduleId).Count;
            var level = conflictCount > 0 ? PatchScannerNotificationLevel.Warning : PatchScannerNotificationLevel.Success;

            return Complete(
                snapshot,
                filePath,
                _host.Translate(
                    "HPS_NotificationModuleConflictScanComplete",
                    displayName,
                    modulePatchCount,
                    conflictCount,
                    filePath),
                level);
        }

        private PatchScanSnapshot ScanModule(PatchScannerOptions options, string moduleId)
        {
            var snapshot = _engine.Scan(options);
            var moduleTargets = new HashSet<string>(
                _reportBuilder.GetModulePatches(snapshot, moduleId).Select(p => p.TargetMethod),
                StringComparer.Ordinal);

            // Conflict detection must inspect all loaded patch metadata, but module exports do
            // not need to decode IL for unrelated targets. This keeps the operation standalone
            // and focused without invoking patch methods or writing an all-mod report.
            var relevantPatches = snapshot.Patches
                .Where(p => moduleTargets.Contains(p.TargetMethod))
                .ToList();
            var focusedSnapshot = new PatchScanSnapshot(
                snapshot.ScanTime,
                snapshot.TotalPatchedMethods,
                relevantPatches,
                snapshot.LoadOrder,
                snapshot.Errors);
            var staticFindings = _staticAnalyzer.Analyze(focusedSnapshot);

            return snapshot.WithStaticFindings(staticFindings);
        }

        private string WriteReport(string fileName, string report)
        {
            var logDirectory = _host.GetLogDirectory();
            Directory.CreateDirectory(logDirectory);
            var filePath = Path.Combine(logDirectory, fileName);
            File.WriteAllText(filePath, report);
            return NormalizePath(filePath);
        }

        private static string NormalizePath(string path)
        {
            var separator = Path.DirectorySeparatorChar;
            var alternateSeparator = separator == '\\' ? '/' : '\\';
            var normalized = Path.GetFullPath(path).Replace(alternateSeparator, separator);
            var preservedPrefix = string.Empty;

            if (separator == '\\' && normalized.StartsWith(@"\\", StringComparison.Ordinal))
            {
                preservedPrefix = @"\\";
                normalized = normalized.Substring(2);
            }

            var doubledSeparator = new string(separator, 2);
            while (normalized.Contains(doubledSeparator))
                normalized = normalized.Replace(doubledSeparator, separator.ToString());

            return preservedPrefix + normalized;
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
