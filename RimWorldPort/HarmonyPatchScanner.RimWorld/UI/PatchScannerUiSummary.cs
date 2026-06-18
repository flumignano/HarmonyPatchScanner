#if RIMWORLD
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyPatchScanner.Core;

namespace HarmonyPatchScanner.RimWorld.UI
{
    internal sealed class PatchScannerUiSummary
    {
        private readonly Dictionary<string, PatchScannerModuleSummary> modulesById;

        private PatchScannerUiSummary(
            PatchScanSnapshot snapshot,
            Dictionary<string, PatchScannerModuleSummary> modulesById,
            IReadOnlyList<ConflictRecord> conflicts,
            IReadOnlyList<PatchOwnerSummary> topPatchOwners)
        {
            Snapshot = snapshot;
            this.modulesById = modulesById;
            Conflicts = conflicts;
            TopPatchOwners = topPatchOwners;
            TotalPrefixes = snapshot.Patches.Count(p => p.PatchType == HarmonyPatchKind.Prefix);
            TotalPostfixes = snapshot.Patches.Count(p => p.PatchType == HarmonyPatchKind.Postfix);
            TotalTranspilers = snapshot.Patches.Count(p => p.PatchType == HarmonyPatchKind.Transpiler);
            TotalFinalizers = snapshot.Patches.Count(p => p.PatchType == HarmonyPatchKind.Finalizer);
            ShortCircuitPrefixes = snapshot.Patches.Count(p => p.CanShortCircuit);
            OfficialTargets = snapshot.Patches.Count(p => p.TargetsOfficialCode);
            ModCount = snapshot.Patches.Select(p => p.Owner).Distinct().Count();
        }

        public PatchScanSnapshot Snapshot { get; }
        public IReadOnlyList<ConflictRecord> Conflicts { get; }
        public IReadOnlyList<PatchOwnerSummary> TopPatchOwners { get; }
        public int TotalPrefixes { get; }
        public int TotalPostfixes { get; }
        public int TotalTranspilers { get; }
        public int TotalFinalizers { get; }
        public int ShortCircuitPrefixes { get; }
        public int OfficialTargets { get; }
        public int ModCount { get; }

        public static PatchScannerUiSummary Build(PatchScanSnapshot snapshot, IReadOnlyList<ModLoadInfo> loadOrder)
        {
            var modulesById = loadOrder.ToDictionary(
                mod => mod.ModId,
                mod => new PatchScannerModuleSummary(),
                StringComparer.OrdinalIgnoreCase);

            var moduleIdByAssembly = loadOrder
                .SelectMany(mod => mod.AssemblyNames.Select(assembly => new { assembly, mod.ModId }))
                .GroupBy(x => x.assembly, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().ModId, StringComparer.OrdinalIgnoreCase);

            var patchesByModuleId = new Dictionary<string, List<PatchRecord>>(StringComparer.OrdinalIgnoreCase);
            foreach (var patch in snapshot.Patches)
            {
                var moduleId = ResolveModuleId(patch, modulesById, moduleIdByAssembly);
                if (moduleId == null)
                    continue;

                if (!patchesByModuleId.TryGetValue(moduleId, out var patches))
                {
                    patches = new List<PatchRecord>();
                    patchesByModuleId[moduleId] = patches;
                }

                patches.Add(patch);
            }

            foreach (var entry in patchesByModuleId)
                modulesById[entry.Key].SetPatchCounts(entry.Value);

            var conflicts = snapshot.Patches
                .GroupBy(p => p.TargetMethod)
                .Where(group => group.Select(p => p.Owner).Distinct().Count() > 1)
                .Select(group => new ConflictRecord(group.Key, group.ToList()))
                .OrderByDescending(conflict => conflict.RiskLevel)
                .ThenByDescending(conflict => conflict.Patches.Count)
                .ToList();

            foreach (var conflict in conflicts)
            {
                var patchModuleIds = conflict.Patches
                    .Select(p => new { Patch = p, ModuleId = ResolveModuleId(p, modulesById, moduleIdByAssembly) })
                    .Where(x => x.ModuleId != null)
                    .ToList();

                foreach (var moduleId in patchModuleIds.Select(x => x.ModuleId!).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var otherOwners = patchModuleIds
                        .Where(x => !string.Equals(x.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
                        .Select(x => x.Patch.Owner)
                        .Distinct()
                        .ToList();

                    if (otherOwners.Count > 0)
                        modulesById[moduleId].AddSharedTarget(conflict.MethodKey, string.Join(", ", otherOwners));
                }
            }

            var topPatchOwners = snapshot.Patches
                .GroupBy(p => p.Owner)
                .OrderByDescending(g => g.Count())
                .Take(12)
                .Select(g => new PatchOwnerSummary(g.Key, g.Count()))
                .ToList();

            return new PatchScannerUiSummary(snapshot, modulesById, conflicts, topPatchOwners);
        }

        public PatchScannerModuleSummary? ForModule(ModLoadInfo? module)
        {
            if (module == null)
                return null;

            return modulesById.TryGetValue(module.ModId, out var summary) ? summary : null;
        }

        private static string? ResolveModuleId(
            PatchRecord patch,
            Dictionary<string, PatchScannerModuleSummary> modulesById,
            Dictionary<string, string> moduleIdByAssembly)
        {
            if (!string.IsNullOrEmpty(patch.OwnerModId) && modulesById.ContainsKey(patch.OwnerModId!))
                return patch.OwnerModId;

            var assemblyName = patch.PatchAssemblyName;
            if (!string.IsNullOrEmpty(assemblyName) &&
                moduleIdByAssembly.TryGetValue(assemblyName!, out var moduleId))
            {
                return moduleId;
            }

            return null;
        }
    }

    internal sealed class PatchScannerModuleSummary
    {
        private readonly List<SharedTargetSummary> sharedTargets = new List<SharedTargetSummary>();

        public int PatchCount { get; private set; }
        public int TargetMethodCount { get; private set; }
        public int PrefixCount { get; private set; }
        public int PostfixCount { get; private set; }
        public int TranspilerCount { get; private set; }
        public int FinalizerCount { get; private set; }
        public int SharedTargetCount { get { return sharedTargets.Count; } }
        public IReadOnlyList<SharedTargetSummary> SharedTargets { get { return sharedTargets; } }

        public void SetPatchCounts(IReadOnlyList<PatchRecord> patches)
        {
            PatchCount = patches.Count;
            TargetMethodCount = patches.Select(p => p.TargetMethod).Distinct().Count();
            PrefixCount = patches.Count(p => p.PatchType == HarmonyPatchKind.Prefix);
            PostfixCount = patches.Count(p => p.PatchType == HarmonyPatchKind.Postfix);
            TranspilerCount = patches.Count(p => p.PatchType == HarmonyPatchKind.Transpiler);
            FinalizerCount = patches.Count(p => p.PatchType == HarmonyPatchKind.Finalizer);
        }

        public void AddSharedTarget(string targetMethod, string otherOwners)
        {
            sharedTargets.Add(new SharedTargetSummary(targetMethod, otherOwners));
        }
    }

    internal sealed class SharedTargetSummary
    {
        public SharedTargetSummary(string targetMethod, string otherOwners)
        {
            TargetMethod = targetMethod;
            OtherOwners = otherOwners;
        }

        public string TargetMethod { get; }
        public string OtherOwners { get; }
    }

    internal sealed class PatchOwnerSummary
    {
        public PatchOwnerSummary(string owner, int count)
        {
            Owner = owner;
            Count = count;
        }

        public string Owner { get; }
        public int Count { get; }
    }
}
#endif
