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
            PatchScanSnapshot? snapshot,
            ModLoadInfo? selectedModule,
            string currentReport,
            ref Vector2 scrollPosition)
        {
            Widgets.DrawMenuSection(rect);
            var inner = rect.ContractedBy(8f);
            var text = BuildDetailsText(snapshot, selectedModule, currentReport);
            var height = Math.Max(inner.height, Text.CalcHeight(text, inner.width - 16f) + 32f);
            var viewRect = new Rect(0f, 0f, inner.width - 16f, height);

            Widgets.BeginScrollView(inner, ref scrollPosition, viewRect);
            Widgets.Label(new Rect(0f, 0f, viewRect.width, height + 8f), text);
            Widgets.EndScrollView();
        }

        private static string BuildDetailsText(PatchScanSnapshot? snapshot, ModLoadInfo? selectedModule, string currentReport)
        {
            var builder = new StringBuilder();
            builder.AppendLine(currentReport);
            builder.AppendLine();

            if (snapshot == null)
            {
                builder.AppendLine("Use Scan all, Find conflicts, or Scan mod to export the same report files as the Bannerlord version.");
                builder.AppendLine();
                builder.AppendLine("The selected mod panel is still useful before a scan because it shows RimWorld's active load order.");
                AppendSelectedModule(builder, selectedModule, null);
                return builder.ToString();
            }

            var totalTranspilers = snapshot.Patches.Count(p => p.PatchType == HarmonyPatchKind.Transpiler);
            var totalPrefixes = snapshot.Patches.Count(p => p.PatchType == HarmonyPatchKind.Prefix);
            var totalPostfixes = snapshot.Patches.Count(p => p.PatchType == HarmonyPatchKind.Postfix);
            var totalFinalizers = snapshot.Patches.Count(p => p.PatchType == HarmonyPatchKind.Finalizer);
            var shortCircuits = snapshot.Patches.Count(p => p.CanShortCircuit);
            var officialTargets = snapshot.Patches.Count(p => p.TargetsOfficialCode);
            var modCount = snapshot.Patches.Select(p => p.Owner).Distinct().Count();
            var conflicts = snapshot.Patches
                .GroupBy(p => p.TargetMethod)
                .Where(group => group.Select(p => p.Owner).Distinct().Count() > 1)
                .Select(group => new ConflictRecord(group.Key, group.ToList()))
                .OrderByDescending(conflict => conflict.RiskLevel)
                .ThenByDescending(conflict => conflict.Patches.Count)
                .ToList();

            builder.AppendLine("Scan summary");
            builder.AppendLine("Scan time: " + snapshot.ScanTime.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendLine("Patched methods seen by Harmony: " + snapshot.TotalPatchedMethods);
            builder.AppendLine("Mods with patches: " + modCount);
            builder.AppendLine("Total patches: " + snapshot.Patches.Count);
            builder.AppendLine("Prefixes: " + totalPrefixes);
            builder.AppendLine("Postfixes: " + totalPostfixes);
            builder.AppendLine("Transpilers: " + totalTranspilers);
            builder.AppendLine("Finalizers: " + totalFinalizers);
            builder.AppendLine("Short-circuit prefixes: " + shortCircuits);
            builder.AppendLine("Targets official code: " + officialTargets);
            builder.AppendLine("Cross-mod conflicts: " + conflicts.Count);
            builder.AppendLine();

            if (snapshot.Errors.Count > 0)
            {
                builder.AppendLine("Scan warnings");
                foreach (var warning in snapshot.Errors)
                    builder.AppendLine("- " + warning);
                builder.AppendLine();
            }

            AppendSelectedModule(builder, selectedModule, snapshot);
            AppendTopConflicts(builder, conflicts);
            AppendTopPatchOwners(builder, snapshot);

            return builder.ToString();
        }

        private static void AppendSelectedModule(StringBuilder builder, ModLoadInfo? module, PatchScanSnapshot? snapshot)
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

            if (snapshot != null)
            {
                var modulePatches = snapshot.Patches.Where(p => IsPatchFromModule(p, module)).ToList();
                var moduleTargets = modulePatches.Select(p => p.TargetMethod).Distinct().Count();
                var moduleConflicts = snapshot.Patches
                    .GroupBy(p => p.TargetMethod)
                    .Where(group => group.Any(p => IsPatchFromModule(p, module)) &&
                                    group.Select(p => p.Owner).Distinct().Count() > 1)
                    .ToList();

                builder.AppendLine("Patches in last scan: " + modulePatches.Count);
                builder.AppendLine("Target methods: " + moduleTargets);
                builder.AppendLine("Shared targets: " + moduleConflicts.Count);

                var byType = modulePatches
                    .GroupBy(p => p.PatchType)
                    .OrderBy(group => PatchOrderHelper.GetTypeOrder(group.Key));
                foreach (var group in byType)
                    builder.AppendLine("  " + group.Key + ": " + group.Count());

                if (moduleConflicts.Count > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("Selected mod shared targets");
                    foreach (var group in moduleConflicts.Take(12))
                    {
                        var otherMods = group
                            .Where(p => !IsPatchFromModule(p, module))
                            .Select(p => p.Owner)
                            .Distinct()
                            .ToList();

                        builder.AppendLine("- " + MethodNameFormatter.FormatMethodName(group.Key, false) + " with " + string.Join(", ", otherMods));
                    }

                    if (moduleConflicts.Count > 12)
                        builder.AppendLine("- ... " + (moduleConflicts.Count - 12) + " more in the exported module report");
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

        private static void AppendTopPatchOwners(StringBuilder builder, PatchScanSnapshot snapshot)
        {
            builder.AppendLine("Top patch owners");
            foreach (var group in snapshot.Patches.GroupBy(p => p.Owner).OrderByDescending(g => g.Count()).Take(12))
                builder.AppendLine("- " + group.Key + ": " + group.Count() + " patches");
            builder.AppendLine();
        }

        private static bool IsPatchFromModule(PatchRecord patch, ModLoadInfo module)
        {
            if (!string.IsNullOrEmpty(patch.OwnerModId) &&
                string.Equals(patch.OwnerModId, module.ModId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var patchAssemblyName = patch.PatchAssemblyName;
            return !string.IsNullOrEmpty(patchAssemblyName) &&
                   module.AssemblyNames.Contains(patchAssemblyName!, StringComparer.OrdinalIgnoreCase);
        }

        private static string YesNo(bool value)
        {
            return value ? "yes" : "no";
        }
    }
}
#endif
