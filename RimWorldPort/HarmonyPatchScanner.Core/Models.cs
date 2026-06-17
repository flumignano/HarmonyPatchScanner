using System;
using System.Collections.Generic;
using System.Linq;

namespace HarmonyPatchScanner.Core
{
    public enum HarmonyPatchKind
    {
        Prefix,
        Postfix,
        Transpiler,
        Finalizer
    }

    public enum ConflictRiskLevel
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    public sealed class ModLoadInfo
    {
        public int Position { get; set; }

        public string ModId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public bool IsOfficial { get; set; }

        public bool IsCommunityLibrary { get; set; }

        public IReadOnlyList<string> AssemblyNames { get; set; } = Array.Empty<string>();
    }

    public sealed class PatchRecord
    {
        public string TargetMethod { get; set; } = string.Empty;

        public HarmonyPatchKind PatchType { get; set; }

        public int Priority { get; set; }

        public string Owner { get; set; } = string.Empty;

        public string? OwnerModId { get; set; }

        public string? PatchAssemblyName { get; set; }

        public string PatchMethod { get; set; } = string.Empty;

        public int Index { get; set; }

        public string HarmonyOwner { get; set; } = string.Empty;

        public string[] Before { get; set; } = Array.Empty<string>();

        public string[] After { get; set; } = Array.Empty<string>();

        public bool CanShortCircuit { get; set; }

        public int? LoadOrderPosition { get; set; }

        public bool TargetsOfficialCode { get; set; }
    }

    public sealed class PatchScanSnapshot
    {
        public PatchScanSnapshot(
            DateTime scanTime,
            int totalPatchedMethods,
            IReadOnlyList<PatchRecord> patches,
            IReadOnlyList<ModLoadInfo> loadOrder,
            IReadOnlyList<string> errors)
        {
            ScanTime = scanTime;
            TotalPatchedMethods = totalPatchedMethods;
            Patches = patches;
            LoadOrder = loadOrder;
            Errors = errors;
        }

        public DateTime ScanTime { get; }

        public int TotalPatchedMethods { get; }

        public IReadOnlyList<PatchRecord> Patches { get; }

        public IReadOnlyList<ModLoadInfo> LoadOrder { get; }

        public IReadOnlyList<string> Errors { get; }
    }

    public sealed class ConflictRecord
    {
        public ConflictRecord(string methodKey, IReadOnlyList<PatchRecord> patches)
        {
            MethodKey = methodKey;
            Patches = patches;

            HasMultipleTranspilers = patches.Count(p => p.PatchType == HarmonyPatchKind.Transpiler) > 1;
            HasMultiplePrefixes = patches.Count(p => p.PatchType == HarmonyPatchKind.Prefix) > 1;
            HasMultiplePrefixesWithSamePriority =
                HasMultiplePrefixes &&
                patches.Where(p => p.PatchType == HarmonyPatchKind.Prefix)
                    .GroupBy(p => p.Priority)
                    .Any(g => g.Count() > 1);

            HasIndeterminateOrder = patches
                .GroupBy(p => new { p.PatchType, p.Priority, p.Index, p.LoadOrderPosition })
                .Any(g => g.Count() > 1 && g.All(p => p.Before.Length == 0 && p.After.Length == 0));

            if (HasMultipleTranspilers)
                RiskLevel = ConflictRiskLevel.High;
            else if (HasMultiplePrefixesWithSamePriority)
                RiskLevel = ConflictRiskLevel.Medium;
            else
                RiskLevel = ConflictRiskLevel.Low;
        }

        public string MethodKey { get; }

        public IReadOnlyList<PatchRecord> Patches { get; }

        public bool HasMultipleTranspilers { get; }

        public bool HasMultiplePrefixes { get; }

        public bool HasMultiplePrefixesWithSamePriority { get; }

        public bool HasIndeterminateOrder { get; }

        public ConflictRiskLevel RiskLevel { get; }
    }

    public sealed class PatchExportResult
    {
        public string FilePath { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public PatchScannerNotificationLevel Level { get; set; } = PatchScannerNotificationLevel.Info;

        public PatchScanSnapshot? Snapshot { get; set; }
    }
}
