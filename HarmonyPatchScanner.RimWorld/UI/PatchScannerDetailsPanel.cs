#if RIMWORLD
using System;
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

        public static string BuildDetailsText(PatchScannerUiSummary? summary, ModLoadInfo? selectedModule, string currentReport)
        {
            var builder = new StringBuilder();
            builder.AppendLine(currentReport);
            builder.AppendLine();

            if (summary == null)
            {
                builder.AppendLine("HPS_DetailsNoScanInstructions".Translate());
                builder.AppendLine();
                builder.AppendLine("HPS_DetailsNoScanLoadOrderHint".Translate());
                AppendSelectedModule(builder, selectedModule, null);
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

            if (summary.Snapshot.Errors.Count > 0)
            {
                builder.AppendLine("HPS_DetailsScanWarnings".Translate());
                foreach (var warning in summary.Snapshot.Errors)
                    builder.AppendLine("- " + warning);
                builder.AppendLine();
            }

            AppendSelectedModule(builder, selectedModule, summary);
            AppendTopConflicts(builder, summary.Conflicts);
            AppendTopPatchOwners(builder, summary);

            return builder.ToString();
        }

        private static void AppendSelectedModule(StringBuilder builder, ModLoadInfo? module, PatchScannerUiSummary? summary)
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

                if (moduleSummary != null && moduleSummary.SharedTargetCount > 0)
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
    }
}
#endif
