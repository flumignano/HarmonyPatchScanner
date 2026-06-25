#if RIMWORLD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyPatchScanner.Core;
using UnityEngine;
using Verse;

namespace HarmonyPatchScanner.RimWorld.UI
{
    internal static class PatchScannerDetailsPanel
    {
        private static string cachedMeasuredText = string.Empty;
        private static float cachedMeasuredWidth = -1f;
        private static float cachedMeasuredHeight;

        public static void Draw(
            Rect rect,
            string detailsText,
            ref Vector2 scrollPosition)
        {
            Widgets.DrawMenuSection(rect);
            var inner = rect.ContractedBy(8f);
            var textWidth = inner.width - 16f;
            var height = Math.Max(inner.height, GetMeasuredHeight(detailsText, textWidth) + 32f);
            var viewRect = new Rect(0f, 0f, inner.width - 16f, height);

            Widgets.BeginScrollView(inner, ref scrollPosition, viewRect);
            Widgets.Label(new Rect(0f, 0f, viewRect.width, height + 8f), detailsText);
            Widgets.EndScrollView();
        }

        public static string BuildDetailsText(
            PatchScannerScope scope,
            PatchScannerUiSummary? summary,
            ModLoadInfo? selectedModule,
            PatchScannerReportKind reportKind,
            string lastExportPath,
            string currentReport)
        {
            var builder = new StringBuilder();
            builder.AppendLine(currentReport);
            builder.AppendLine();

            if (summary == null)
            {
                builder.AppendLine(scope == PatchScannerScope.AllMods
                    ? "HPS_DetailsNoAllScanInstructions".Translate()
                    : "HPS_DetailsNoSelectedScanInstructions".Translate());
                if (scope == PatchScannerScope.SelectedMod)
                {
                    builder.AppendLine();
                    AppendSelectedModule(builder, selectedModule, null, reportKind, lastExportPath);
                }
                return builder.ToString();
            }

            if (scope == PatchScannerScope.SelectedMod)
            {
                AppendSelectedModule(builder, selectedModule, summary, reportKind, lastExportPath);
                AppendWarnings(builder, summary);
                return builder.ToString();
            }

            builder.AppendLine("HPS_DetailsScanSummary".Translate());
            builder.AppendLine("HPS_DetailsScanTime".Translate(summary.Snapshot.ScanTime.ToString("yyyy-MM-dd HH:mm:ss")));
            builder.AppendLine("HPS_DetailsPatchedMethods".Translate(summary.Snapshot.TotalPatchedMethods));
            builder.AppendLine("HPS_DetailsModsWithPatches".Translate(summary.ModCount));
            builder.AppendLine("HPS_DetailsTotalPatches".Translate(summary.Snapshot.Patches.Count));
            builder.AppendLine("HPS_DetailsPrefixes".Translate(summary.TotalPrefixes));
            builder.AppendLine("HPS_DetailsPostfixes".Translate(summary.TotalPostfixes));
            builder.AppendLine("HPS_DetailsTranspilers".Translate(summary.TotalTranspilers));
            builder.AppendLine("HPS_DetailsFinalizers".Translate(summary.TotalFinalizers));
            builder.AppendLine("HPS_DetailsShortCircuitPrefixes".Translate(summary.ShortCircuitPrefixes));
            builder.AppendLine("HPS_DetailsOfficialTargets".Translate(summary.OfficialTargets));
            builder.AppendLine("HPS_DetailsPotentialConflicts".Translate(summary.Conflicts.Count));
            builder.AppendLine("HPS_DetailsStaticFindings".Translate(
                summary.StaticDeterministicFindings,
                summary.StaticLikelyFindings));
            builder.AppendLine("HPS_DetailsPotentialStaticNotes".Translate(summary.StaticPotentialFindings));
            builder.AppendLine();

            AppendWarnings(builder, summary);
            if (reportKind == PatchScannerReportKind.AllConflicts)
                AppendTopConflicts(builder, summary.Conflicts);
            else
                AppendAllPatchScanHint(builder, summary);
            AppendTopPatchOwners(builder, summary);

            return builder.ToString();
        }

        private static void AppendWarnings(StringBuilder builder, PatchScannerUiSummary summary)
        {
            if (summary.Snapshot.Errors.Count == 0)
                return;

            builder.AppendLine("HPS_DetailsScanWarnings".Translate());
            foreach (var warning in summary.Snapshot.Errors)
                builder.AppendLine("- " + warning);
            builder.AppendLine();
        }

        private static void AppendSelectedModule(
            StringBuilder builder,
            ModLoadInfo? module,
            PatchScannerUiSummary? summary,
            PatchScannerReportKind reportKind,
            string lastExportPath)
        {
            builder.AppendLine("HPS_SelectedMod".Translate());

            if (module == null)
            {
                builder.AppendLine("HPS_NoModSelected".Translate());
                builder.AppendLine();
                return;
            }

            builder.AppendLine("#" + module.Position + " " + module.DisplayName);
            builder.AppendLine("HPS_DetailsPackageId".Translate(module.ModId));
            builder.AppendLine("HPS_DetailsAssemblies".Translate(
                module.AssemblyNames.Count == 0 ? "HPS_CommonNone".Translate().ToString() : string.Join(", ", module.AssemblyNames)));
            builder.AppendLine("HPS_DetailsOfficial".Translate(YesNo(module.IsOfficial)));
            builder.AppendLine("HPS_DetailsCommunityLibrary".Translate(YesNo(module.IsCommunityLibrary)));

            var moduleSummary = summary?.ForModule(module);
            if (summary != null)
            {
                builder.AppendLine("HPS_DetailsPatchesLastScan".Translate(moduleSummary?.PatchCount ?? 0));
                builder.AppendLine("HPS_DetailsTargetMethods".Translate(moduleSummary?.TargetMethodCount ?? 0));
                builder.AppendLine("HPS_DetailsSharedTargets".Translate(moduleSummary?.SharedTargetCount ?? 0));

                if (moduleSummary != null)
                {
                    if (moduleSummary.PrefixCount > 0)
                        builder.AppendLine("HPS_DetailsPrefixCount".Translate(moduleSummary.PrefixCount));
                    if (moduleSummary.FinalizerCount > 0)
                        builder.AppendLine("HPS_DetailsFinalizerCount".Translate(moduleSummary.FinalizerCount));
                    if (moduleSummary.TranspilerCount > 0)
                        builder.AppendLine("HPS_DetailsTranspilerCount".Translate(moduleSummary.TranspilerCount));
                    if (moduleSummary.PostfixCount > 0)
                        builder.AppendLine("HPS_DetailsPostfixCount".Translate(moduleSummary.PostfixCount));
                }

                if (moduleSummary != null &&
                    moduleSummary.SharedTargetCount > 0 &&
                    reportKind == PatchScannerReportKind.ModuleConflicts)
                {
                    AppendSelectedModuleConflictPreview(builder, module, summary, lastExportPath);
                }
                else if (moduleSummary != null && moduleSummary.SharedTargetCount > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("HPS_DetailsSelectedModSharedTargets".Translate());
                    foreach (var sharedTarget in moduleSummary.SharedTargets.Take(12))
                    {
                        builder.AppendLine("HPS_DetailsSharedTargetRow".Translate(
                            MethodNameFormatter.FormatMethodName(sharedTarget.TargetMethod, false),
                            sharedTarget.OtherOwners));
                    }

                    if (moduleSummary.SharedTargetCount > 12)
                        builder.AppendLine("HPS_DetailsMoreModuleTargets".Translate(moduleSummary.SharedTargetCount - 12));
                }
            }

            builder.AppendLine();
        }

        private static void AppendSelectedModuleConflictPreview(
            StringBuilder builder,
            ModLoadInfo module,
            PatchScannerUiSummary summary,
            string lastExportPath)
        {
            var conflicts = GetSelectedModuleConflicts(summary, module);
            var fileName = string.IsNullOrEmpty(lastExportPath)
                ? "HPS_CommonUnknown".Translate().ToString()
                : Path.GetFileName(lastExportPath);

            builder.AppendLine();
            builder.AppendLine("HPS_DetailsSelectedConflictSummary".Translate());
            builder.AppendLine("HPS_DetailsSelectedConflictCount".Translate(conflicts.Count));
            builder.AppendLine("HPS_DetailsSelectedConflictRiskCounts".Translate(
                conflicts.Count(c => c.RiskLevel == ConflictRiskLevel.High),
                conflicts.Count(c => c.RiskLevel == ConflictRiskLevel.Medium),
                conflicts.Count(c => c.RiskLevel == ConflictRiskLevel.Low)));
            builder.AppendLine("HPS_DetailsSelectedConflictReportFile".Translate(fileName));

            if (conflicts.Count == 0)
            {
                builder.AppendLine("HPS_DetailsNoConflicts".Translate());
                builder.AppendLine();
                return;
            }

            builder.AppendLine();
            builder.AppendLine("HPS_DetailsSelectedConflictTargets".Translate());
            foreach (var conflict in conflicts.Take(12))
            {
                var otherOwners = conflict.Conflict.Patches
                    .Where(patch => !PatchBelongsToModule(patch, module))
                    .Select(patch => patch.Owner)
                    .Distinct()
                    .ToList();
                builder.AppendLine("HPS_DetailsSelectedConflictRow".Translate(
                    TranslateRiskLevel(conflict.RiskLevel),
                    MethodNameFormatter.FormatMethodName(conflict.Conflict.MethodKey, false),
                    otherOwners.Count,
                    string.Join(", ", otherOwners)));
            }

            if (conflicts.Count > 12)
                builder.AppendLine("HPS_DetailsMoreSelectedConflicts".Translate(conflicts.Count - 12, fileName));
        }

        private static List<SelectedModuleConflictPreview> GetSelectedModuleConflicts(PatchScannerUiSummary summary, ModLoadInfo module)
        {
            return summary.Conflicts
                .Where(conflict =>
                    conflict.Patches.Any(patch => PatchBelongsToModule(patch, module)) &&
                    conflict.Patches.Any(patch => !PatchBelongsToModule(patch, module)))
                .Select(conflict => new SelectedModuleConflictPreview(conflict, GetSelectedModuleRisk(conflict, module)))
                .OrderByDescending(conflict => conflict.RiskLevel)
                .ThenByDescending(conflict => conflict.Conflict.Patches.Count)
                .ThenBy(conflict => conflict.Conflict.MethodKey)
                .ToList();
        }

        private static ConflictRiskLevel GetSelectedModuleRisk(ConflictRecord conflict, ModLoadInfo module)
        {
            // Match the selected-mod conflict report: a medium risk only applies when this
            // mod and another mod both prefix the target at the same Harmony priority.
            if (conflict.Patches.Count(patch => patch.PatchType == HarmonyPatchKind.Transpiler) > 1)
                return ConflictRiskLevel.High;

            var prefixes = conflict.Patches
                .Where(patch => patch.PatchType == HarmonyPatchKind.Prefix)
                .ToList();
            var modulePrefixes = prefixes
                .Where(patch => PatchBelongsToModule(patch, module))
                .ToList();
            var otherPrefixes = prefixes
                .Where(patch => !PatchBelongsToModule(patch, module))
                .ToList();

            if (modulePrefixes.Count > 0 &&
                otherPrefixes.Count > 0 &&
                modulePrefixes.Any(modulePatch => otherPrefixes.Any(otherPatch => otherPatch.Priority == modulePatch.Priority)))
            {
                return ConflictRiskLevel.Medium;
            }

            return ConflictRiskLevel.Low;
        }

        private static bool PatchBelongsToModule(PatchRecord patch, ModLoadInfo module)
        {
            if (!string.IsNullOrEmpty(patch.OwnerModId) &&
                string.Equals(patch.OwnerModId, module.ModId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(patch.PatchAssemblyName) &&
                module.AssemblyNames.Any(assembly => string.Equals(assembly, patch.PatchAssemblyName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return patch.Owner.IndexOf(module.ModId, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AppendAllPatchScanHint(StringBuilder builder, PatchScannerUiSummary summary)
        {
            if (summary.Conflicts.Count == 0)
                return;

            builder.AppendLine("HPS_DetailsAllPatchConflictHint".Translate(summary.Conflicts.Count));
            builder.AppendLine("HPS_DetailsAllPatchConflictAction".Translate());
            builder.AppendLine();
        }

        private static void AppendTopConflicts(StringBuilder builder, System.Collections.Generic.IReadOnlyList<ConflictRecord> conflicts)
        {
            builder.AppendLine("HPS_DetailsHighestRiskTargets".Translate());

            if (conflicts.Count == 0)
            {
                builder.AppendLine("HPS_DetailsNoConflicts".Translate());
                builder.AppendLine();
                return;
            }

            foreach (var conflict in conflicts.Take(12))
            {
                var owners = conflict.Patches.Select(p => p.Owner).Distinct().Count();
                builder.AppendLine("HPS_DetailsConflictRow".Translate(
                    TranslateRiskLevel(conflict.RiskLevel),
                    MethodNameFormatter.FormatMethodName(conflict.MethodKey, false),
                    conflict.Patches.Count,
                    owners));
            }

            if (conflicts.Count > 12)
                builder.AppendLine("HPS_DetailsMoreConflicts".Translate(conflicts.Count - 12));

            builder.AppendLine();
        }

        private static void AppendTopPatchOwners(StringBuilder builder, PatchScannerUiSummary summary)
        {
            builder.AppendLine("HPS_DetailsTopPatchOwners".Translate());
            foreach (var owner in summary.TopPatchOwners)
                builder.AppendLine("HPS_DetailsPatchOwnerRow".Translate(owner.Owner, owner.Count));
            builder.AppendLine();
        }

        private static string YesNo(bool value)
        {
            return value ? "HPS_CommonYes".Translate() : "HPS_CommonNo".Translate();
        }

        private static string TranslateRiskLevel(ConflictRiskLevel riskLevel)
        {
            switch (riskLevel)
            {
                case ConflictRiskLevel.High:
                    return "HPS_RiskHigh".Translate();
                case ConflictRiskLevel.Medium:
                    return "HPS_RiskMedium".Translate();
                default:
                    return "HPS_RiskLow".Translate();
            }
        }

        private static float GetMeasuredHeight(string text, float width)
        {
            if (string.Equals(cachedMeasuredText, text, StringComparison.Ordinal) &&
                Math.Abs(cachedMeasuredWidth - width) < 0.5f)
            {
                return cachedMeasuredHeight;
            }

            cachedMeasuredText = text;
            cachedMeasuredWidth = width;
            cachedMeasuredHeight = Text.CalcHeight(text, width);
            return cachedMeasuredHeight;
        }

        private sealed class SelectedModuleConflictPreview
        {
            public SelectedModuleConflictPreview(ConflictRecord conflict, ConflictRiskLevel riskLevel)
            {
                Conflict = conflict;
                RiskLevel = riskLevel;
            }

            public ConflictRecord Conflict { get; }

            public ConflictRiskLevel RiskLevel { get; }
        }
    }
}
#endif
