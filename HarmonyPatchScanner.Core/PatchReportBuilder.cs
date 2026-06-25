using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HarmonyPatchScanner.Core
{
    public sealed class PatchReportBuilder
    {
        public string BuildAllPatchesReport(PatchScanSnapshot snapshot, PatchScannerOptions options)
        {
            var results = new StringBuilder();

            AppendTitle(results, "Harmony Patch Scanner - Full Patch List");
            results.AppendLine($"Scan Time : {snapshot.ScanTime:yyyy-MM-dd HH:mm:ss}");
            results.AppendLine();

            AppendLoadOrderHeader(results, snapshot.LoadOrder);
            AppendFilterNotes(results, options);
            AppendErrors(results, snapshot);

            var patchesByMod = snapshot.Patches
                .GroupBy(p => p.Owner)
                .ToDictionary(g => g.Key, g => g.ToList());

            var totalPatches = snapshot.Patches.Count;
            var totalTranspilers = snapshot.Patches.Count(p => p.PatchType == HarmonyPatchKind.Transpiler);
            var totalShortCircuits = snapshot.Patches.Count(p => p.CanShortCircuit);
            var totalOfficialTargets = snapshot.Patches.Count(p => p.TargetsOfficialCode);
            var danglingHints = GetDanglingBeforeAfterHints(snapshot.Patches);

            results.AppendLine($"Total Patched Methods       : {snapshot.TotalPatchedMethods}");
            results.AppendLine($"Total Mods with Patches     : {patchesByMod.Count}");
            results.AppendLine($"Total Patches               : {totalPatches}");
            results.AppendLine($"  - Transpilers             : {totalTranspilers}  (highest risk patch type)");
            results.AppendLine($"  - Prefixes (short-circuit): {totalShortCircuits}  (can skip original method)");
            results.AppendLine($"  - Target official code    : {totalOfficialTargets}");
            if (danglingHints.Count > 0)
                results.AppendLine($"  - Dangling before/after   : {danglingHints.Count}  (reference a mod that is not loaded - see below)");
            results.AppendLine();

            AppendStaticAnalysisSummary(results, snapshot.StaticFindings);
            AppendDanglingHints(results, danglingHints);
            AppendPatchesByMod(results, patchesByMod);
            AppendPatchCountRanking(results, patchesByMod);

            return results.ToString();
        }

        public string BuildConflictReport(PatchScanSnapshot snapshot, PatchScannerOptions options)
        {
            var results = new StringBuilder();

            AppendTitle(results, "Harmony Patch Scanner - Conflict Report");
            results.AppendLine($"Scan Time : {snapshot.ScanTime:yyyy-MM-dd HH:mm:ss}");
            results.AppendLine();

            AppendLoadOrderHeader(results, snapshot.LoadOrder);
            results.AppendLine("This report lists methods patched by more than one mod.");
            results.AppendLine("Structural matches are potential conflicts unless static evidence says otherwise.");
            results.AppendLine();

            AppendFilterNotes(results, options);
            AppendErrors(results, snapshot);

            var patchesByTarget = snapshot.Patches
                .GroupBy(p => p.TargetMethod)
                .ToDictionary(g => g.Key, g => g.ToList());

            var conflictingMethods = GetCrossModConflicts(snapshot.Patches);
            var sameModMultiPatches = GetSameModMultiPatches(snapshot.Patches);

            var highRisk = conflictingMethods.Count(c => c.RiskLevel == ConflictRiskLevel.High);
            var mediumRisk = conflictingMethods.Count(c => c.RiskLevel == ConflictRiskLevel.Medium);
            var lowRisk = conflictingMethods.Count(c => c.RiskLevel == ConflictRiskLevel.Low);
            var officialConflicts = conflictingMethods.Count(c => c.Patches.Any(p => p.TargetsOfficialCode));
            var shortCircuitConflicts = conflictingMethods.Count(c =>
                c.Patches.Count(p => p.PatchType == HarmonyPatchKind.Prefix) > 1 &&
                c.Patches.Any(p => p.CanShortCircuit));

            results.AppendLine($"Total Methods Patched          : {patchesByTarget.Count}");
            results.AppendLine($"Potential Cross-Mod Conflicts  : {conflictingMethods.Count}  ({highRisk} Potential High / {mediumRisk} Potential Medium / {lowRisk} Potential Low)");
            results.AppendLine($"  - Targeting official code    : {officialConflicts}");
            results.AppendLine($"  - Short-circuit prefix risk  : {shortCircuitConflicts}  (one prefix can silence another mod's prefix)");
            results.AppendLine($"Same-Mod Multi-Patch (suspect) : {sameModMultiPatches.Count}");
            results.AppendLine();
            AppendStaticAnalysisSummary(results, snapshot.StaticFindings);

            if (conflictingMethods.Count == 0 && sameModMultiPatches.Count == 0)
            {
                results.AppendLine("No potential cross-mod conflicts detected. All methods are patched by a single mod.");
                return results.ToString();
            }

            AppendConflictTableOfContents(results, conflictingMethods, sameModMultiPatches);
            AppendConflictsByRiskLevel(results, conflictingMethods, ConflictRiskLevel.High, "POTENTIAL HIGH RISK CONFLICTS");
            AppendConflictsByRiskLevel(results, conflictingMethods, ConflictRiskLevel.Medium, "POTENTIAL MEDIUM RISK CONFLICTS");
            AppendConflictsByRiskLevel(results, conflictingMethods, ConflictRiskLevel.Low, "POTENTIAL LOW RISK CONFLICTS");
            AppendSameModSection(results, sameModMultiPatches);
            AppendConflictSummary(results, conflictingMethods, sameModMultiPatches);

            return results.ToString();
        }

        public string BuildModuleReport(PatchScanSnapshot snapshot, PatchScannerOptions options, string moduleId)
        {
            var module = snapshot.LoadOrder.FirstOrDefault(m =>
                string.Equals(m.ModId, moduleId, StringComparison.OrdinalIgnoreCase));

            var moduleName = module?.DisplayName ?? moduleId;
            var moduleAssemblies = GetModuleAssemblies(module);
            var modulePatches = GetModulePatches(snapshot, moduleId);
            var modulePatchesByTarget = modulePatches
                .GroupBy(p => p.TargetMethod)
                .ToDictionary(g => g.Key, g => g.ToList());
            var moduleConflicts = GetModuleConflicts(snapshot, moduleId);
            var conflictFileName = "ModuleConflicts_" + MethodNameFormatter.SanitizeFileName(moduleName) + ".txt";

            var results = new StringBuilder();

            AppendTitle(results, "Harmony Patch Scanner - Module Report");
            results.AppendLine($"Scan Time   : {snapshot.ScanTime:yyyy-MM-dd HH:mm:ss}");
            results.AppendLine($"Module      : {moduleName}");
            results.AppendLine($"Module Id   : {moduleId}");
            results.AppendLine($"Load Pos    : {MethodNameFormatter.FormatLoadOrder(module?.Position)}");
            results.AppendLine($"Assemblies  : {string.Join(", ", moduleAssemblies)}");
            results.AppendLine();

            AppendFilterNotes(results, options);
            AppendErrors(results, snapshot);

            results.AppendLine($"Patched Methods             : {modulePatchesByTarget.Count}");
            results.AppendLine($"Total Patches               : {modulePatches.Count}");
            results.AppendLine($"  - Prefixes                : {modulePatches.Count(p => p.PatchType == HarmonyPatchKind.Prefix)}");
            results.AppendLine($"  - Postfixes               : {modulePatches.Count(p => p.PatchType == HarmonyPatchKind.Postfix)}");
            results.AppendLine($"  - Transpilers             : {modulePatches.Count(p => p.PatchType == HarmonyPatchKind.Transpiler)}  (highest risk patch type)");
            results.AppendLine($"  - Finalizers              : {modulePatches.Count(p => p.PatchType == HarmonyPatchKind.Finalizer)}");
            results.AppendLine($"  - Prefixes (short-circuit): {modulePatches.Count(p => p.CanShortCircuit)}  (can skip original method)");
            results.AppendLine($"  - Target official code    : {modulePatches.Count(p => p.TargetsOfficialCode)}");
            results.AppendLine($"Potential shared targets    : {moduleConflicts.Count}");
            results.AppendLine($"Detailed conflict report    : {conflictFileName}");
            results.AppendLine("  Use Find conflicts in Selected mod scope to create or refresh that report.");
            results.AppendLine();

            AppendStaticAnalysisSummary(
                results,
                snapshot.StaticFindings.Where(f => modulePatches.Any(p => FindingMatchesPatch(f, p))).ToList());

            if (modulePatches.Count == 0)
            {
                results.AppendLine("No Harmony patches found for this module.");
                return results.ToString();
            }

            AppendModulePatchDetails(results, moduleName, modulePatchesByTarget);
            AppendModulePatchTypes(results, modulePatches);
            AppendModuleSummary(
                results,
                moduleName,
                moduleId,
                module,
                moduleAssemblies,
                modulePatchesByTarget,
                modulePatches,
                moduleConflicts.Count,
                conflictFileName);

            return results.ToString();
        }

        public string BuildModuleConflictReport(PatchScanSnapshot snapshot, PatchScannerOptions options, string moduleId)
        {
            var module = snapshot.LoadOrder.FirstOrDefault(m =>
                string.Equals(m.ModId, moduleId, StringComparison.OrdinalIgnoreCase));
            var moduleName = module?.DisplayName ?? moduleId;
            var moduleAssemblies = GetModuleAssemblies(module);
            var modulePatches = GetModulePatches(snapshot, moduleId);
            var conflictsByTarget = GetModuleConflicts(snapshot, moduleId)
                .ToDictionary(conflict => conflict.MethodKey, conflict => conflict.Patches.ToList());
            var conflictTargets = new HashSet<string>(conflictsByTarget.Keys, StringComparer.Ordinal);
            var results = new StringBuilder();

            AppendTitle(results, "Harmony Patch Scanner - Selected Mod Conflict Report");
            results.AppendLine($"Scan Time   : {snapshot.ScanTime:yyyy-MM-dd HH:mm:ss}");
            results.AppendLine($"Module      : {moduleName}");
            results.AppendLine($"Module Id   : {moduleId}");
            results.AppendLine($"Load Pos    : {MethodNameFormatter.FormatLoadOrder(module?.Position)}");
            results.AppendLine($"Assemblies  : {string.Join(", ", moduleAssemblies)}");
            results.AppendLine();
            results.AppendLine("This standalone report covers only targets shared by the selected mod and other mods.");
            results.AppendLine("Structural overlap is a potential conflict, not proof of a malfunction.");
            results.AppendLine("AllHarmonyPatches.txt and DuplicateHarmonyPatches.txt are not required or written.");
            results.AppendLine();

            AppendFilterNotes(results, options);
            AppendErrors(results, snapshot);
            var conflictRisks = conflictsByTarget.Values
                .Select(patches => GetModuleConflictRisk(patches, moduleId, moduleAssemblies))
                .ToList();
            results.AppendLine($"Selected mod patches       : {modulePatches.Count}");
            results.AppendLine($"Potential shared targets   : {conflictsByTarget.Count}");
            results.AppendLine($"  - Potential High         : {conflictRisks.Count(risk => risk == 2)}");
            results.AppendLine($"  - Potential Medium       : {conflictRisks.Count(risk => risk == 1)}");
            results.AppendLine($"  - Potential Low          : {conflictRisks.Count(risk => risk == 0)}");
            results.AppendLine();
            AppendStaticAnalysisSummary(
                results,
                snapshot.StaticFindings.Where(f => conflictTargets.Contains(f.TargetMethod)).ToList());
            AppendModuleConflicts(results, moduleName, moduleId, moduleAssemblies, conflictsByTarget);

            return results.ToString();
        }

        public List<PatchRecord> GetModulePatches(PatchScanSnapshot snapshot, string moduleId)
        {
            var module = snapshot.LoadOrder.FirstOrDefault(m =>
                string.Equals(m.ModId, moduleId, StringComparison.OrdinalIgnoreCase));
            var moduleAssemblies = GetModuleAssemblies(module);

            return snapshot.Patches
                .Where(p => IsFromModule(p, moduleId, moduleAssemblies))
                .ToList();
        }

        public List<ConflictRecord> GetModuleConflicts(PatchScanSnapshot snapshot, string moduleId)
        {
            var module = snapshot.LoadOrder.FirstOrDefault(m =>
                string.Equals(m.ModId, moduleId, StringComparison.OrdinalIgnoreCase));
            var moduleAssemblies = GetModuleAssemblies(module);

            return snapshot.Patches
                .GroupBy(p => p.TargetMethod)
                .Where(g => g.Any(p => IsFromModule(p, moduleId, moduleAssemblies)) &&
                            g.Any(p => !IsFromModule(p, moduleId, moduleAssemblies)))
                .Select(g => new ConflictRecord(g.Key, g.ToList()))
                .OrderByDescending(c => GetModuleConflictRisk(c.Patches, moduleId, moduleAssemblies))
                .ThenBy(c => c.MethodKey)
                .ToList();
        }

        public List<ConflictRecord> GetCrossModConflicts(IEnumerable<PatchRecord> patches)
        {
            return patches
                .GroupBy(p => p.TargetMethod)
                .Where(g => g.Count() > 1 && g.Select(p => p.Owner).Distinct().Count() > 1)
                .Select(g => new ConflictRecord(g.Key, g.ToList()))
                .OrderByDescending(c => c.RiskLevel)
                .ThenByDescending(c => c.Patches.Count)
                .ToList();
        }

        public List<ConflictRecord> GetSameModMultiPatches(IEnumerable<PatchRecord> patches)
        {
            return patches
                .GroupBy(p => p.TargetMethod)
                .Where(g =>
                {
                    var owners = g.Select(p => p.Owner).ToList();
                    return owners.Count > 1 && owners.Distinct().Count() == 1;
                })
                .Select(g => new ConflictRecord(g.Key, g.ToList()))
                .OrderByDescending(c => c.Patches.Count)
                .ToList();
        }

        private static void AppendTitle(StringBuilder sb, string title)
        {
            sb.AppendLine("====================================================");
            sb.AppendLine($"    {title}");
            sb.AppendLine("====================================================");
        }

        private static void AppendLoadOrderHeader(StringBuilder sb, IReadOnlyList<ModLoadInfo> loadOrder)
        {
            sb.AppendLine("====================================================");
            sb.AppendLine("  Load Order (authoritative mod/DLL load sequence)");
            sb.AppendLine("====================================================");

            if (loadOrder.Count == 0)
            {
                sb.AppendLine("  (could not determine load order)");
            }
            else
            {
                foreach (var mod in loadOrder.OrderBy(m => m.Position))
                {
                    var officialTag = mod.IsOfficial ? "  [official]" : string.Empty;
                    var communityTag = mod.IsCommunityLibrary ? "  [community lib]" : string.Empty;
                    sb.AppendLine($"  #{mod.Position,-3} {mod.DisplayName,-40} ({mod.ModId}){officialTag}{communityTag}");
                }
            }

            sb.AppendLine();
        }

        private static void AppendFilterNotes(StringBuilder sb, PatchScannerOptions options)
        {
            if (options.ExcludeCommonLifecycleMethods)
            {
                sb.AppendLine("Note: Common lifecycle method patches are excluded from this scan.");
                sb.AppendLine();
            }

            if (options.ExcludeCommunityLibraries)
            {
                sb.AppendLine("Note: Community library patches are excluded from this scan.");
                sb.AppendLine();
            }
        }

        private static void AppendErrors(StringBuilder sb, PatchScanSnapshot snapshot)
        {
            if (snapshot.Errors.Count == 0)
                return;

            sb.AppendLine("====================================================");
            sb.AppendLine("  Scan Warnings");
            sb.AppendLine("====================================================");

            foreach (var error in snapshot.Errors)
                sb.AppendLine($"  [WARN] {error}");

            sb.AppendLine();
        }

        private static void AppendStaticAnalysisSummary(StringBuilder sb, IReadOnlyList<StaticPatchFinding> findings)
        {
            sb.AppendLine("====================================================");
            sb.AppendLine("  Static IL Analysis");
            sb.AppendLine("====================================================");

            if (findings.Count == 0)
            {
                sb.AppendLine("  No static IL findings.");
                sb.AppendLine();
                return;
            }

            sb.AppendLine($"  Findings        : {findings.Count}");
            sb.AppendLine($"    Deterministic : {findings.Count(f => f.Confidence == StaticFindingConfidence.Deterministic)}");
            sb.AppendLine($"    Likely        : {findings.Count(f => f.Confidence == StaticFindingConfidence.Likely)}");
            sb.AppendLine($"    Potential     : {findings.Count(f => f.Confidence == StaticFindingConfidence.Potential)}");
            sb.AppendLine($"    Unreadable    : {findings.Count(f => f.Kind == StaticFindingKind.UnreadableBody)}");
            sb.AppendLine();
        }

        private static void AppendDanglingHints(StringBuilder sb, List<(string PatchMethod, string HintType, string ReferencedId)> danglingHints)
        {
            if (danglingHints.Count == 0)
                return;

            sb.AppendLine("====================================================");
            sb.AppendLine("  WARNING: DANGLING before/after HINTS");
            sb.AppendLine("====================================================");
            sb.AppendLine("  These patches declare HarmonyBefore or HarmonyAfter targeting");
            sb.AppendLine("  a Harmony owner ID that is not present in the loaded patch list.");
            sb.AppendLine("  The hint is silently ignored by Harmony - ordering may be wrong.");
            sb.AppendLine();

            foreach (var hint in danglingHints)
            {
                sb.AppendLine($"  Patch  : {MethodNameFormatter.FormatMethodName(hint.PatchMethod, verbose: false)}");
                sb.AppendLine($"  [{hint.HintType}] references : {hint.ReferencedId}  (not loaded)");
                sb.AppendLine();
            }
        }

        private static void AppendPatchesByMod(StringBuilder sb, Dictionary<string, List<PatchRecord>> patchesByMod)
        {
            foreach (var modGroup in patchesByMod.OrderBy(x => x.Key))
            {
                var modTranspilers = modGroup.Value.Count(p => p.PatchType == HarmonyPatchKind.Transpiler);
                var modShortCircuits = modGroup.Value.Count(p => p.CanShortCircuit);
                var modOfficial = modGroup.Value.Count(p => p.TargetsOfficialCode);

                sb.AppendLine("----------------------------------------------------");
                sb.AppendLine($"  Mod          : {modGroup.Key}");
                sb.AppendLine($"  Patches      : {modGroup.Value.Count}  (transpilers: {modTranspilers}  |  short-circuit prefixes: {modShortCircuits}  |  targets official code: {modOfficial})");
                sb.AppendLine("----------------------------------------------------");
                sb.AppendLine();

                foreach (var targetGroup in modGroup.Value.GroupBy(p => p.TargetMethod).OrderBy(g => g.Key))
                {
                    var allPatches = targetGroup.ToList();
                    var officialTag = allPatches.First().TargetsOfficialCode ? "  [official code]" : string.Empty;

                    sb.AppendLine($"  Target : {MethodNameFormatter.FormatMethodName(targetGroup.Key, verbose: false)}{officialTag}");

                    if (allPatches.Any(p => p.CanShortCircuit) &&
                        allPatches.Count(p => p.PatchType == HarmonyPatchKind.Prefix) > 1)
                    {
                        sb.AppendLine("    Potential short-circuit chain: a prefix here can return false,");
                        sb.AppendLine("    which skips the original and all lower-priority prefixes.");
                    }

                    foreach (var patch in allPatches)
                        AppendPatchDetail(sb, patch, includeOwner: false);
                }
            }
        }

        private static void AppendPatchCountRanking(StringBuilder sb, Dictionary<string, List<PatchRecord>> patchesByMod)
        {
            sb.AppendLine("====================================================");
            sb.AppendLine("  Mod Ranking by Patch Count (most patches first)");
            sb.AppendLine("====================================================");
            sb.AppendLine();

            var rank = 1;
            foreach (var modGroup in patchesByMod.OrderByDescending(x => x.Value.Count))
            {
                var pos = modGroup.Value.FirstOrDefault()?.LoadOrderPosition;
                var posStr = pos.HasValue ? $"  load #{pos.Value}" : string.Empty;
                sb.AppendLine($"  #{rank,-3} {modGroup.Key}  ({modGroup.Value.Count} patches){posStr}");
                rank++;
            }

            sb.AppendLine();
        }

        private static void AppendConflictTableOfContents(
            StringBuilder sb,
            IReadOnlyList<ConflictRecord> conflicts,
            IReadOnlyList<ConflictRecord> sameModConflicts)
        {
            sb.AppendLine("====================================================");
            sb.AppendLine("  Table of Contents");
            sb.AppendLine("====================================================");
            sb.AppendLine();

            AppendTocSection(sb, conflicts, ConflictRiskLevel.High, "POTENTIAL HIGH RISK");
            AppendTocSection(sb, conflicts, ConflictRiskLevel.Medium, "POTENTIAL MEDIUM RISK");
            AppendTocSection(sb, conflicts, ConflictRiskLevel.Low, "POTENTIAL LOW RISK");

            if (sameModConflicts.Count > 0)
            {
                sb.AppendLine("  [SAME-MOD MULTI-PATCH]");
                foreach (var conflict in sameModConflicts)
                    sb.AppendLine($"    - {MethodNameFormatter.FormatMethodName(conflict.MethodKey, verbose: false)}  ({conflict.Patches.Count} patches)");
                sb.AppendLine();
            }
        }

        private static void AppendTocSection(
            StringBuilder sb,
            IReadOnlyList<ConflictRecord> conflicts,
            ConflictRiskLevel level,
            string label)
        {
            var subset = conflicts.Where(c => c.RiskLevel == level).ToList();
            if (subset.Count == 0)
                return;

            sb.AppendLine($"  [{label}]  ({subset.Count})");
            foreach (var conflict in subset)
            {
                var officialTag = conflict.Patches.Any(p => p.TargetsOfficialCode) ? " [official]" : string.Empty;
                var scTag = conflict.Patches.Any(p => p.CanShortCircuit) ? " [short-circuit]" : string.Empty;
                var modCount = conflict.Patches.Select(p => p.Owner).Distinct().Count();
                sb.AppendLine($"    - {MethodNameFormatter.FormatMethodName(conflict.MethodKey, verbose: false)}  ({conflict.Patches.Count} patches from {modCount} mods){officialTag}{scTag}");
            }

            sb.AppendLine();
        }

        private static void AppendConflictsByRiskLevel(
            StringBuilder sb,
            IReadOnlyList<ConflictRecord> conflicts,
            ConflictRiskLevel riskLevel,
            string header)
        {
            var subset = conflicts.Where(c => c.RiskLevel == riskLevel).ToList();
            if (subset.Count == 0)
                return;

            sb.AppendLine();
            sb.AppendLine("====================================================");
            sb.AppendLine($"  {header}  ({subset.Count})");
            sb.AppendLine("====================================================");
            sb.AppendLine();

            foreach (var conflict in subset)
                AppendConflictDetails(sb, conflict);
        }

        private static void AppendConflictDetails(StringBuilder sb, ConflictRecord conflict)
        {
            var uniqueMods = conflict.Patches.Select(p => p.Owner).Distinct().Count();
            var targetsOfficial = conflict.Patches.Any(p => p.TargetsOfficialCode);
            var hasShortCircuit = conflict.Patches.Any(p => p.CanShortCircuit && p.PatchType == HarmonyPatchKind.Prefix);

            sb.AppendLine($"  Target  : {MethodNameFormatter.FormatMethodName(conflict.MethodKey, verbose: false)}{(targetsOfficial ? "  [official code]" : string.Empty)}");
            sb.AppendLine($"  Patches : {conflict.Patches.Count} from {uniqueMods} mod(s)");

            if (conflict.HasMultipleTranspilers)
            {
                sb.AppendLine("  POTENTIAL HIGH RISK: Multiple transpilers detected.");
                sb.AppendLine("    Each transpiler sees IL already modified by the previous one.");
                sb.AppendLine("    Execution order shown below - first in list sees the original IL.");
            }
            else if (conflict.HasMultiplePrefixesWithSamePriority)
            {
                sb.AppendLine("  POTENTIAL MEDIUM RISK: Multiple prefixes share the same priority.");
                sb.AppendLine("    Tie-broken by before/after hints, Harmony index, then load position.");
                if (conflict.HasIndeterminateOrder)
                    sb.AppendLine("  INDETERMINATE: Patches share priority, index, load position, and no before/after hints.");
            }

            if (hasShortCircuit)
            {
                sb.AppendLine("  POTENTIAL SHORT-CIRCUIT RISK: At least one prefix returns bool.");
                sb.AppendLine("    If it returns false, the original method and all lower-priority prefixes");
                sb.AppendLine("    from other mods are silently skipped.");
            }

            sb.AppendLine();

            foreach (var typeGroup in conflict.Patches.GroupBy(p => p.PatchType).OrderBy(g => g.Key))
            {
                var ordered = PatchOrderHelper.GetPatchesInExecutionOrder(typeGroup.Key, typeGroup);
                sb.AppendLine($"  {typeGroup.Key} Patches - Execution Order:");

                var step = 1;
                foreach (var patch in ordered)
                {
                    AppendOrderedPatch(sb, patch, step, markThisMod: false);
                    step++;
                }

                sb.AppendLine();
            }

            sb.AppendLine("----------------------------------------------------");
            sb.AppendLine();
        }

        private static void AppendSameModSection(StringBuilder sb, IReadOnlyList<ConflictRecord> sameModConflicts)
        {
            if (sameModConflicts.Count == 0)
                return;

            sb.AppendLine();
            sb.AppendLine("====================================================");
            sb.AppendLine($"  SAME-MOD MULTI-PATCH SUSPECTS  ({sameModConflicts.Count})");
            sb.AppendLine("====================================================");
            sb.AppendLine("  These methods are patched more than once by the same mod.");
            sb.AppendLine("  This may be intentional but could also indicate a bug.");
            sb.AppendLine();

            foreach (var conflict in sameModConflicts)
                AppendConflictDetails(sb, conflict);
        }

        private static void AppendConflictSummary(
            StringBuilder sb,
            IReadOnlyList<ConflictRecord> conflicts,
            IReadOnlyList<ConflictRecord> sameModConflicts)
        {
            sb.AppendLine("====================================================");
            sb.AppendLine("  Conflict Summary");
            sb.AppendLine("====================================================");
            sb.AppendLine();
            sb.AppendLine($"  Potential Cross-Mod  : {conflicts.Count}");
            sb.AppendLine($"    High   : {conflicts.Count(c => c.RiskLevel == ConflictRiskLevel.High)}");
            sb.AppendLine($"    Medium : {conflicts.Count(c => c.RiskLevel == ConflictRiskLevel.Medium)}");
            sb.AppendLine($"    Low    : {conflicts.Count(c => c.RiskLevel == ConflictRiskLevel.Low)}");
            sb.AppendLine($"  Same-Mod Multi-Patch : {sameModConflicts.Count}");
            sb.AppendLine();
            sb.AppendLine("  Execution Order Tiebreak Chain (Harmony + host game)");
            sb.AppendLine("  ----------------------------------------------------");
            sb.AppendLine("  1. Priority        - higher wins (Prefix/Transpiler), lower wins (Postfix/Finalizer)");
            sb.AppendLine("  2. before/after    - HarmonyBefore / HarmonyAfter explicit ordering hints");
            sb.AppendLine("  3. Harmony Index   - assigned when each mod calls Harmony.Patch() / PatchAll()");
            sb.AppendLine("  4. Load Position   - the order mods were loaded by the host game");
            sb.AppendLine("  5. INDETERMINATE   - if all of the above are identical, order is undefined");
            sb.AppendLine();
            sb.AppendLine("  Patch Type Risk (highest to lowest)");
            sb.AppendLine("  -----------------------------------");
            sb.AppendLine("  Transpiler  - rewrites IL; each one sees the already-modified code");
            sb.AppendLine("  Prefix      - runs before original; bool return can skip original and later prefixes");
            sb.AppendLine("  Finalizer   - always runs, even on exception; wraps the original");
            sb.AppendLine("  Postfix     - runs after original; generally the safest");
            sb.AppendLine();
        }

        private static void AppendModulePatchDetails(
            StringBuilder sb,
            string moduleName,
            Dictionary<string, List<PatchRecord>> modulePatchesByTarget)
        {
            sb.AppendLine("====================================================");
            sb.AppendLine($"  All Patches by {moduleName}");
            sb.AppendLine("====================================================");
            sb.AppendLine();

            foreach (var targetGroup in modulePatchesByTarget.OrderBy(x => x.Key))
            {
                var patches = targetGroup.Value;
                var officialTag = patches.First().TargetsOfficialCode ? "  [official code]" : string.Empty;

                sb.AppendLine($"  Target : {MethodNameFormatter.FormatMethodName(targetGroup.Key, verbose: false)}{officialTag}");

                foreach (var patch in patches)
                    AppendPatchDetail(sb, patch, includeOwner: false);
            }
        }

        private static void AppendModulePatchTypes(StringBuilder sb, IReadOnlyList<PatchRecord> modulePatches)
        {
            sb.AppendLine("====================================================");
            sb.AppendLine("  Patches by Type");
            sb.AppendLine("====================================================");
            sb.AppendLine();

            foreach (var typeGroup in modulePatches.GroupBy(p => p.PatchType).OrderBy(g => PatchOrderHelper.GetTypeOrder(g.Key)))
            {
                var patches = typeGroup.ToList();
                sb.AppendLine($"  -- {typeGroup.Key} ({patches.Count}) ------------------------------");
                sb.AppendLine();

                foreach (var patch in patches.OrderBy(p => p.TargetMethod))
                {
                    var shortCircuitNote = patch.CanShortCircuit ? " [can skip original]" : string.Empty;
                    var officialTag = patch.TargetsOfficialCode ? "  [official code]" : string.Empty;

                    sb.AppendLine($"    Target        : {MethodNameFormatter.FormatMethodName(patch.TargetMethod, verbose: false)}{officialTag}");
                    sb.AppendLine($"    Patch Method  : {MethodNameFormatter.FormatMethodName(patch.PatchMethod, verbose: false)}{shortCircuitNote}");
                    sb.AppendLine($"    Priority      : {MethodNameFormatter.FormatPriority(patch.Priority)}");
                    sb.AppendLine($"    Harmony Index : {MethodNameFormatter.FormatIndex(patch.Index)}");
                    sb.AppendLine($"    Harmony ID    : {patch.HarmonyOwner}");
                    AppendStaticFindings(sb, patch, "    ");
                    sb.AppendLine();
                }
            }
        }

        private static void AppendModuleConflicts(
            StringBuilder sb,
            string moduleName,
            string moduleId,
            HashSet<string> moduleAssemblies,
            Dictionary<string, List<PatchRecord>> conflictsByTarget)
        {
            if (conflictsByTarget.Count == 0)
            {
                sb.AppendLine("====================================================");
                sb.AppendLine("  No Potential Conflicts Detected");
                sb.AppendLine("====================================================");
                sb.AppendLine();
                sb.AppendLine($"  No other mod patches the same methods as {moduleName}.");
                sb.AppendLine();
                return;
            }

            sb.AppendLine("====================================================");
            sb.AppendLine("  Potential Conflicts - Methods Also Patched by Other Mods");
            sb.AppendLine("====================================================");
            sb.AppendLine();
            sb.AppendLine($"  {conflictsByTarget.Count} method(s) are patched by both {moduleName}");
            sb.AppendLine("  and at least one other mod. Review these for potential issues.");
            sb.AppendLine();

            var sortedConflicts = conflictsByTarget
                .Select(kvp => new
                {
                    MethodKey = kvp.Key,
                    AllPatches = kvp.Value,
                    ModulePatches = kvp.Value.Where(p => IsFromModule(p, moduleId, moduleAssemblies)).ToList(),
                    OtherPatches = kvp.Value.Where(p => !IsFromModule(p, moduleId, moduleAssemblies)).ToList(),
                    Risk = GetModuleConflictRisk(kvp.Value, moduleId, moduleAssemblies)
                })
                .OrderByDescending(c => c.Risk)
                .ThenBy(c => c.MethodKey)
                .ToList();

            sb.AppendLine("  Table of Contents:");
            sb.AppendLine();
            foreach (var conflict in sortedConflicts)
            {
                var riskLabel = conflict.Risk == 2 ? "[POTENTIAL HIGH]" : conflict.Risk == 1 ? "[POTENTIAL MEDIUM]" : "[POTENTIAL LOW]";
                var otherMods = string.Join(", ", conflict.OtherPatches.Select(p => p.Owner).Distinct());
                sb.AppendLine($"    {riskLabel,-8} {MethodNameFormatter.FormatMethodName(conflict.MethodKey, verbose: false)}  - also patched by: {otherMods}");
            }

            sb.AppendLine();

            foreach (var conflict in sortedConflicts)
            {
                var otherMods = conflict.OtherPatches.Select(p => p.Owner).Distinct().ToList();
                var hasTranspilers = conflict.AllPatches.Count(p => p.PatchType == HarmonyPatchKind.Transpiler) > 1;
                var hasShortCircuit = conflict.AllPatches.Any(p => p.CanShortCircuit && p.PatchType == HarmonyPatchKind.Prefix)
                                      && conflict.AllPatches.Count(p => p.PatchType == HarmonyPatchKind.Prefix) > 1;
                var targetsOfficial = conflict.ModulePatches.Any(p => p.TargetsOfficialCode);
                var riskLabel = conflict.Risk == 2 ? "POTENTIAL HIGH" : conflict.Risk == 1 ? "POTENTIAL MEDIUM" : "POTENTIAL LOW";

                sb.AppendLine("----------------------------------------------------");
                sb.AppendLine($"  Target     : {MethodNameFormatter.FormatMethodName(conflict.MethodKey, verbose: false)}{(targetsOfficial ? "  [official code]" : string.Empty)}");
                sb.AppendLine($"  Risk       : {riskLabel}");
                sb.AppendLine($"  This mod   : {conflict.ModulePatches.Count} patch(es)");
                sb.AppendLine($"  Other mods : {conflict.OtherPatches.Count} patch(es) from {otherMods.Count} mod(s): {string.Join(", ", otherMods)}");
                sb.AppendLine();

                if (hasTranspilers)
                {
                    sb.AppendLine("  POTENTIAL HIGH RISK: Multiple transpilers on this method.");
                    sb.AppendLine("    Each transpiler sees IL already modified by the previous one.");
                    sb.AppendLine();
                }

                if (hasShortCircuit)
                {
                    sb.AppendLine("  POTENTIAL SHORT-CIRCUIT RISK: A prefix returns bool - if it returns false,");
                    sb.AppendLine("    the original method and all lower-priority prefixes are skipped.");
                    sb.AppendLine();
                }

                foreach (var typeGroup in conflict.AllPatches.GroupBy(p => p.PatchType).OrderBy(g => PatchOrderHelper.GetTypeOrder(g.Key)))
                {
                    var ordered = PatchOrderHelper.GetPatchesInExecutionOrder(typeGroup.Key, typeGroup);
                    sb.AppendLine($"  {typeGroup.Key} Patches - Execution Order:");

                    var step = 1;
                    foreach (var patch in ordered)
                    {
                        var isThisMod = IsFromModule(patch, moduleId, moduleAssemblies);
                        AppendOrderedPatch(sb, patch, step, isThisMod);
                        step++;
                    }

                    sb.AppendLine();
                }
            }
        }

        private static void AppendModuleSummary(
            StringBuilder sb,
            string moduleName,
            string moduleId,
            ModLoadInfo? module,
            HashSet<string> moduleAssemblies,
            Dictionary<string, List<PatchRecord>> modulePatchesByTarget,
            IReadOnlyList<PatchRecord> modulePatches,
            int conflictCount,
            string conflictFileName)
        {
            sb.AppendLine("====================================================");
            sb.AppendLine($"  Module Summary - {moduleName}");
            sb.AppendLine("====================================================");
            sb.AppendLine();
            sb.AppendLine($"  Module Id              : {moduleId}");
            sb.AppendLine($"  Load Position          : {MethodNameFormatter.FormatLoadOrder(module?.Position)}");
            sb.AppendLine($"  Assemblies             : {string.Join(", ", moduleAssemblies)}");
            sb.AppendLine($"  Total Patched Methods  : {modulePatchesByTarget.Count}");
            sb.AppendLine($"  Total Patches          : {modulePatches.Count}");
            sb.AppendLine($"    Prefixes             : {modulePatches.Count(p => p.PatchType == HarmonyPatchKind.Prefix)}");
            sb.AppendLine($"    Postfixes            : {modulePatches.Count(p => p.PatchType == HarmonyPatchKind.Postfix)}");
            sb.AppendLine($"    Transpilers          : {modulePatches.Count(p => p.PatchType == HarmonyPatchKind.Transpiler)}");
            sb.AppendLine($"    Finalizers           : {modulePatches.Count(p => p.PatchType == HarmonyPatchKind.Finalizer)}");
            sb.AppendLine($"  Short-circuit Prefixes : {modulePatches.Count(p => p.CanShortCircuit)}");
            sb.AppendLine($"  Targets Official Code  : {modulePatches.Count(p => p.TargetsOfficialCode)}");
            sb.AppendLine($"  Potential Conflicts    : {conflictCount} method(s) shared with other mods");
            sb.AppendLine($"  Detailed Report        : {conflictFileName}");
            sb.AppendLine();
        }

        private static void AppendPatchDetail(StringBuilder sb, PatchRecord patch, bool includeOwner)
        {
            var shortCircuitNote = patch.CanShortCircuit ? " [can skip original]" : string.Empty;
            var beforeStr = patch.Before.Length > 0 ? string.Join(", ", patch.Before) : "none";
            var afterStr = patch.After.Length > 0 ? string.Join(", ", patch.After) : "none";

            if (includeOwner)
                sb.AppendLine($"    Mod           : {patch.Owner}");

            sb.AppendLine($"    Type          : {patch.PatchType}{shortCircuitNote}");
            sb.AppendLine($"    Priority      : {MethodNameFormatter.FormatPriority(patch.Priority)}");
            sb.AppendLine($"    Harmony Index : {MethodNameFormatter.FormatIndex(patch.Index)}");
            sb.AppendLine($"    Load Pos      : {MethodNameFormatter.FormatLoadOrder(patch.LoadOrderPosition)}");
            sb.AppendLine($"    Patch Method  : {MethodNameFormatter.FormatMethodName(patch.PatchMethod, verbose: false)}");
            sb.AppendLine($"    Harmony ID    : {patch.HarmonyOwner}");
            sb.AppendLine($"    Before        : {beforeStr}");
            sb.AppendLine($"    After         : {afterStr}");
            AppendStaticFindings(sb, patch, "    ");
            sb.AppendLine();
        }

        private static void AppendOrderedPatch(StringBuilder sb, PatchRecord patch, int step, bool markThisMod)
        {
            var marker = markThisMod ? " <- THIS MOD" : string.Empty;
            var scNote = patch.CanShortCircuit ? "  <- can return false to skip original + later prefixes" : string.Empty;
            var beforeStr = patch.Before.Length > 0 ? string.Join(", ", patch.Before) : "none";
            var afterStr = patch.After.Length > 0 ? string.Join(", ", patch.After) : "none";

            sb.AppendLine($"    [{step}] Mod          : {patch.Owner}{marker}");
            sb.AppendLine($"        Method       : {MethodNameFormatter.FormatMethodName(patch.PatchMethod, verbose: false)}");
            sb.AppendLine($"        Harmony ID   : {patch.HarmonyOwner}");
            sb.AppendLine($"        Priority     : {MethodNameFormatter.FormatPriority(patch.Priority)}");
            sb.AppendLine($"        Harmony Idx  : {MethodNameFormatter.FormatIndex(patch.Index)}");
            sb.AppendLine($"        Load Pos     : {MethodNameFormatter.FormatLoadOrder(patch.LoadOrderPosition)}{scNote}");
            sb.AppendLine($"        Before       : {beforeStr}");
            sb.AppendLine($"        After        : {afterStr}");
            AppendStaticFindings(sb, patch, "        ");
        }

        private static void AppendStaticFindings(StringBuilder sb, PatchRecord patch, string indent)
        {
            if (patch.StaticFindings.Count == 0)
                return;

            sb.AppendLine($"{indent}Static IL    :");
            foreach (var finding in patch.StaticFindings)
            {
                sb.AppendLine($"{indent}  [{finding.Confidence}] {FormatFindingKind(finding.Kind)} - {finding.Explanation}");
            }
        }

        private static string FormatFindingKind(StaticFindingKind kind)
        {
            switch (kind)
            {
                case StaticFindingKind.UnconditionalSkipOriginal:
                    return "original skip";
                case StaticFindingKind.ResultWrite:
                    return "result write";
                case StaticFindingKind.RefArgumentMutation:
                    return "ref argument mutation";
                case StaticFindingKind.PrivateFieldAccess:
                    return "private field access";
                case StaticFindingKind.UnreadableBody:
                    return "unreadable body";
                case StaticFindingKind.UnsupportedPattern:
                    return "unsupported pattern";
                default:
                    return kind.ToString();
            }
        }

        private static bool FindingMatchesPatch(StaticPatchFinding finding, PatchRecord patch)
        {
            return string.Equals(finding.TargetMethod, patch.TargetMethod, StringComparison.Ordinal) &&
                   string.Equals(finding.PatchMethod, patch.PatchMethod, StringComparison.Ordinal) &&
                   string.Equals(finding.PatchOwner, patch.Owner, StringComparison.Ordinal) &&
                   finding.PatchKind == patch.PatchType &&
                   finding.HarmonyIndex == patch.Index;
        }

        private static List<(string PatchMethod, string HintType, string ReferencedId)> GetDanglingBeforeAfterHints(
            IEnumerable<PatchRecord> patches)
        {
            var patchList = patches.ToList();
            var allHarmonyOwners = new HashSet<string>(
                patchList.Where(p => !string.IsNullOrEmpty(p.HarmonyOwner)).Select(p => p.HarmonyOwner),
                StringComparer.OrdinalIgnoreCase);

            var dangling = new List<(string, string, string)>();
            foreach (var patch in patchList)
            {
                foreach (var id in patch.Before)
                {
                    if (!allHarmonyOwners.Contains(id))
                        dangling.Add((patch.PatchMethod, "HarmonyBefore", id));
                }

                foreach (var id in patch.After)
                {
                    if (!allHarmonyOwners.Contains(id))
                        dangling.Add((patch.PatchMethod, "HarmonyAfter", id));
                }
            }

            return dangling;
        }

        private static bool IsFromModule(PatchRecord patch, string moduleId, HashSet<string> moduleAssemblies)
        {
            if (!string.IsNullOrEmpty(patch.OwnerModId) &&
                string.Equals(patch.OwnerModId, moduleId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var patchAssemblyName = patch.PatchAssemblyName;
            if (!string.IsNullOrEmpty(patchAssemblyName) &&
                moduleAssemblies.Contains(patchAssemblyName!))
            {
                return true;
            }

            return patch.Owner.IndexOf(moduleId, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static HashSet<string> GetModuleAssemblies(ModLoadInfo? module)
        {
            return new HashSet<string>(
                module?.AssemblyNames ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
        }

        private static int GetModuleConflictRisk(
            IReadOnlyList<PatchRecord> allPatches,
            string moduleId,
            HashSet<string> moduleAssemblies)
        {
            if (allPatches.Count(p => p.PatchType == HarmonyPatchKind.Transpiler) > 1)
                return 2;

            var prefixes = allPatches.Where(p => p.PatchType == HarmonyPatchKind.Prefix).ToList();
            var modulePrefixes = prefixes.Where(p => IsFromModule(p, moduleId, moduleAssemblies)).ToList();
            var otherPrefixes = prefixes.Where(p => !IsFromModule(p, moduleId, moduleAssemblies)).ToList();

            if (modulePrefixes.Count > 0 &&
                otherPrefixes.Count > 0 &&
                modulePrefixes.Any(mp => otherPrefixes.Any(op => op.Priority == mp.Priority)))
            {
                return 1;
            }

            return 0;
        }
    }
}
