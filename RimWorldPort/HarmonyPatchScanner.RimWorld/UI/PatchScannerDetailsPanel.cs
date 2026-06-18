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
        public static void Draw(
            Rect rect,
            PatchScannerUiSummary? summary,
            ModLoadInfo? selectedModule,
            string currentReport,
            ref Vector2 scrollPosition)
        {
            Widgets.DrawMenuSection(rect);
            var inner = rect.ContractedBy(8f);
            var text = BuildDetailsText(summary, selectedModule, currentReport);
            var height = Math.Max(inner.height, Text.CalcHeight(text, inner.width - 16f) + 32f);
            var viewRect = new Rect(0f, 0f, inner.width - 16f, height);

            Widgets.BeginScrollView(inner, ref scrollPosition, viewRect);
            Widgets.Label(new Rect(0f, 0f, viewRect.width, height + 8f), text);
            Widgets.EndScrollView();
        }

        private static string BuildDetailsText(PatchScannerUiSummary? summary, ModLoadInfo? selectedModule, string currentReport)
        {
            var builder = new StringBuilder();
            builder.AppendLine(currentReport);
            builder.AppendLine();

            if (summary == null)
            {
                builder.AppendLine("Use Scan all, Find conflicts, or Scan mod to export the same report files as the Bannerlord version.");
                builder.AppendLine();
                builder.AppendLine("The selected mod panel is still useful before a scan because it shows RimWorld's active load order.");
                AppendSelectedModule(builder, selectedModule, null);
                return builder.ToString();
            }

            builder.AppendLine("Scan summary");
            builder.AppendLine("Scan time: " + summary.Snapshot.ScanTime.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendLine("Patched methods seen by Harmony: " + summary.Snapshot.TotalPatchedMethods);
            builder.AppendLine("Mods with patches: " + summary.ModCount);
            builder.AppendLine("Total patches: " + summary.Snapshot.Patches.Count);
            builder.AppendLine("Prefixes: " + summary.TotalPrefixes);
            builder.AppendLine("Postfixes: " + summary.TotalPostfixes);
            builder.AppendLine("Transpilers: " + summary.TotalTranspilers);
            builder.AppendLine("Finalizers: " + summary.TotalFinalizers);
            builder.AppendLine("Short-circuit prefixes: " + summary.ShortCircuitPrefixes);
            builder.AppendLine("Targets official code: " + summary.OfficialTargets);
            builder.AppendLine("Cross-mod conflicts: " + summary.Conflicts.Count);
            builder.AppendLine();

            if (summary.Snapshot.Errors.Count > 0)
            {
                builder.AppendLine("Scan warnings");
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
            builder.AppendLine("Selected mod");

            if (module == null)
            {
                builder.AppendLine("No mod selected.");
                builder.AppendLine();
                return;
            }

            builder.AppendLine("#" + module.Position + " " + module.DisplayName);
            builder.AppendLine("Package ID: " + module.ModId);
            builder.AppendLine("Assemblies: " + (module.AssemblyNames.Count == 0 ? "(none)" : string.Join(", ", module.AssemblyNames)));
            builder.AppendLine("Official: " + YesNo(module.IsOfficial));
            builder.AppendLine("Community library: " + YesNo(module.IsCommunityLibrary));

            var moduleSummary = summary?.ForModule(module);
            if (summary != null)
            {
                builder.AppendLine("Patches in last scan: " + (moduleSummary?.PatchCount ?? 0));
                builder.AppendLine("Target methods: " + (moduleSummary?.TargetMethodCount ?? 0));
                builder.AppendLine("Shared targets: " + (moduleSummary?.SharedTargetCount ?? 0));

                if (moduleSummary != null)
                {
                    if (moduleSummary.PrefixCount > 0)
                        builder.AppendLine("  Prefix: " + moduleSummary.PrefixCount);
                    if (moduleSummary.FinalizerCount > 0)
                        builder.AppendLine("  Finalizer: " + moduleSummary.FinalizerCount);
                    if (moduleSummary.TranspilerCount > 0)
                        builder.AppendLine("  Transpiler: " + moduleSummary.TranspilerCount);
                    if (moduleSummary.PostfixCount > 0)
                        builder.AppendLine("  Postfix: " + moduleSummary.PostfixCount);
                }

                if (moduleSummary != null && moduleSummary.SharedTargetCount > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("Selected mod shared targets");
                    foreach (var sharedTarget in moduleSummary.SharedTargets.Take(12))
                    {
                        builder.AppendLine("- " + MethodNameFormatter.FormatMethodName(sharedTarget.TargetMethod, false) +
                                           " with " + sharedTarget.OtherOwners);
                    }

                    if (moduleSummary.SharedTargetCount > 12)
                        builder.AppendLine("- ... " + (moduleSummary.SharedTargetCount - 12) + " more in the exported module report");
                }
            }

            builder.AppendLine();
        }

        private static void AppendTopConflicts(StringBuilder builder, System.Collections.Generic.IReadOnlyList<ConflictRecord> conflicts)
        {
            builder.AppendLine("Highest-risk shared targets");

            if (conflicts.Count == 0)
            {
                builder.AppendLine("No cross-mod conflicts in the last scan.");
                builder.AppendLine();
                return;
            }

            foreach (var conflict in conflicts.Take(12))
            {
                var owners = conflict.Patches.Select(p => p.Owner).Distinct().Count();
                builder.AppendLine("- [" + conflict.RiskLevel + "] " +
                                   MethodNameFormatter.FormatMethodName(conflict.MethodKey, false) +
                                   " - " + conflict.Patches.Count + " patches from " + owners + " mods");
            }

            if (conflicts.Count > 12)
                builder.AppendLine("- ... " + (conflicts.Count - 12) + " more in DuplicateHarmonyPatches.txt");

            builder.AppendLine();
        }

        private static void AppendTopPatchOwners(StringBuilder builder, PatchScannerUiSummary summary)
        {
            builder.AppendLine("Top patch owners");
            foreach (var owner in summary.TopPatchOwners)
                builder.AppendLine("- " + owner.Owner + ": " + owner.Count + " patches");
            builder.AppendLine();
        }

        private static string YesNo(bool value)
        {
            return value ? "yes" : "no";
        }
    }
}
#endif
