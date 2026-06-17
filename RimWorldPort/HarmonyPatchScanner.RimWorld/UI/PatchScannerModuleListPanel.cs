#if RIMWORLD
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyPatchScanner.Core;
using UnityEngine;
using Verse;

namespace HarmonyPatchScanner.RimWorld.UI
{
    internal static class PatchScannerModuleListPanel
    {
        private const float RowGap = 6f;
        private const float RowHorizontalPadding = 8f;
        private const float RowTopPadding = 7f;

        public static void Draw(
            Rect rect,
            IReadOnlyList<ModLoadInfo> loadOrder,
            PatchScanSnapshot? snapshot,
            ModLoadInfo? selectedModule,
            string searchText,
            Action<ModLoadInfo> onSelect,
            ref Vector2 scrollPosition,
            ref string mutableSearchText)
        {
            Widgets.DrawMenuSection(rect);
            var inner = rect.ContractedBy(8f);

            Widgets.Label(new Rect(inner.x, inner.y, inner.width, PatchScannerUiConstants.TextLineHeight), "Loaded mods");

            var searchRect = new Rect(inner.x, inner.y + PatchScannerUiConstants.TextLineHeight + 4f, inner.width, PatchScannerUiConstants.TextFieldHeight);
            mutableSearchText = Widgets.TextField(searchRect, mutableSearchText ?? string.Empty);

            var listRect = new Rect(inner.x, searchRect.yMax + 8f, inner.width, inner.yMax - searchRect.yMax - 8f);
            var modules = loadOrder.Where(mod => MatchesSearch(mod, searchText)).ToList();
            var viewWidth = listRect.width - 16f;
            var viewHeight = Math.Max(listRect.height, modules.Count * (PatchScannerUiConstants.RowHeight + RowGap));
            var viewRect = new Rect(0f, 0f, viewWidth, viewHeight);

            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);

            var y = 0f;
            foreach (var module in modules)
            {
                var row = new Rect(0f, y, viewRect.width, PatchScannerUiConstants.RowHeight);
                DrawModuleRow(row, module, snapshot, selectedModule, onSelect);
                y += PatchScannerUiConstants.RowHeight + RowGap;
            }

            if (modules.Count == 0)
                Widgets.Label(new Rect(0f, 0f, viewRect.width, 60f), "No loaded mods match the search filter.");

            Widgets.EndScrollView();
        }

        private static bool MatchesSearch(ModLoadInfo module, string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
                return true;

            var term = searchText.ToLowerInvariant();
            return (module.DisplayName ?? string.Empty).ToLowerInvariant().Contains(term) ||
                   (module.ModId ?? string.Empty).ToLowerInvariant().Contains(term) ||
                   module.AssemblyNames.Any(assembly => assembly.ToLowerInvariant().Contains(term));
        }

        private static void DrawModuleRow(
            Rect rect,
            ModLoadInfo module,
            PatchScanSnapshot? snapshot,
            ModLoadInfo? selectedModule,
            Action<ModLoadInfo> onSelect)
        {
            var selected = selectedModule != null &&
                           string.Equals(selectedModule.ModId, module.ModId, StringComparison.OrdinalIgnoreCase);

            if (selected)
                Widgets.DrawHighlightSelected(rect);
            else
                Widgets.DrawHighlightIfMouseover(rect);

            Widgets.DrawBox(rect);
            if (Widgets.ButtonInvisible(rect))
                onSelect(module);

            var titleRect = new Rect(
                rect.x + RowHorizontalPadding,
                rect.y + RowTopPadding,
                rect.width - RowHorizontalPadding * 2f,
                PatchScannerUiConstants.TextLineHeight);
            var metaRect = new Rect(
                titleRect.x,
                titleRect.yMax + 2f,
                titleRect.width,
                PatchScannerUiConstants.TextLineHeight);
            var countRect = new Rect(
                titleRect.x,
                metaRect.yMax + 2f,
                titleRect.width,
                PatchScannerUiConstants.TextLineHeight);

            Widgets.Label(titleRect, "#" + module.Position + " " + module.DisplayName);
            Widgets.Label(metaRect, ModuleTags(module));
            Widgets.Label(countRect, BuildPatchCounts(module, snapshot));
        }

        private static string ModuleTags(ModLoadInfo module)
        {
            var tags = module.ModId;
            if (module.IsOfficial)
                tags += "  [official]";
            if (module.IsCommunityLibrary)
                tags += "  [community lib]";
            return tags;
        }

        private static string BuildPatchCounts(ModLoadInfo module, PatchScanSnapshot? snapshot)
        {
            if (snapshot == null)
                return "No scan data yet.";

            var patches = snapshot.Patches.Where(p => IsPatchFromModule(p, module)).ToList();
            if (patches.Count == 0)
                return "0 patches";

            var targets = patches.Select(p => p.TargetMethod).Distinct().Count();
            var conflicts = snapshot.Patches
                .GroupBy(p => p.TargetMethod)
                .Count(group => group.Any(p => IsPatchFromModule(p, module)) &&
                                group.Select(p => p.Owner).Distinct().Count() > 1);

            return patches.Count + " patches on " + targets + " methods, " + conflicts + " shared target(s)";
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
    }
}
#endif
