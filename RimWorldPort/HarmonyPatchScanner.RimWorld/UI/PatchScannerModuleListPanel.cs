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
        private const float RowBottomPadding = 8f;
        private const float RowLineGap = 2f;

        public static void Draw(
            Rect rect,
            IReadOnlyList<ModLoadInfo> loadOrder,
            PatchScannerUiSummary? summary,
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
            var rowHeights = modules.Select(module => CalculateRowHeight(module, summary, viewWidth)).ToList();
            var viewHeight = Math.Max(listRect.height, TotalRowHeight(rowHeights));
            var viewRect = new Rect(0f, 0f, viewWidth, viewHeight);

            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);

            var y = 0f;
            for (var i = 0; i < modules.Count; i++)
            {
                var module = modules[i];
                var rowHeight = rowHeights[i];
                var row = new Rect(0f, y, viewRect.width, rowHeight);
                DrawModuleRow(row, module, summary, selectedModule, onSelect);
                y += rowHeight + RowGap;
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

        private static float TotalRowHeight(IReadOnlyList<float> rowHeights)
        {
            if (rowHeights.Count == 0)
                return 0f;

            return rowHeights.Sum() + (rowHeights.Count - 1) * RowGap;
        }

        private static float CalculateRowHeight(ModLoadInfo module, PatchScannerUiSummary? summary, float rowWidth)
        {
            var textWidth = rowWidth - RowHorizontalPadding * 2f;
            var titleHeight = Text.CalcHeight(TitleText(module), textWidth);
            var metaHeight = Text.CalcHeight(ModuleTags(module), textWidth);
            var countHeight = Text.CalcHeight(BuildPatchCounts(module, summary), textWidth);

            return Math.Max(
                PatchScannerUiConstants.RowHeight,
                RowTopPadding + titleHeight + RowLineGap + metaHeight + RowLineGap + countHeight + RowBottomPadding);
        }

        private static void DrawModuleRow(
            Rect rect,
            ModLoadInfo module,
            PatchScannerUiSummary? summary,
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

            var textWidth = rect.width - RowHorizontalPadding * 2f;
            var title = TitleText(module);
            var meta = ModuleTags(module);
            var patchCounts = BuildPatchCounts(module, summary);

            var titleRect = new Rect(
                rect.x + RowHorizontalPadding,
                rect.y + RowTopPadding,
                textWidth,
                Text.CalcHeight(title, textWidth));
            var metaRect = new Rect(
                titleRect.x,
                titleRect.yMax + RowLineGap,
                textWidth,
                Text.CalcHeight(meta, textWidth));
            var countRect = new Rect(
                titleRect.x,
                metaRect.yMax + RowLineGap,
                textWidth,
                Text.CalcHeight(patchCounts, textWidth));

            Widgets.Label(titleRect, title);
            Widgets.Label(metaRect, meta);
            Widgets.Label(countRect, patchCounts);
        }

        private static string TitleText(ModLoadInfo module)
        {
            return "#" + module.Position + " " + module.DisplayName;
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

        private static string BuildPatchCounts(ModLoadInfo module, PatchScannerUiSummary? summary)
        {
            if (summary == null)
                return "No scan data yet.";

            var moduleSummary = summary.ForModule(module);
            if (moduleSummary == null || moduleSummary.PatchCount == 0)
                return "0 patches";

            return moduleSummary.PatchCount + " patches on " + moduleSummary.TargetMethodCount +
                   " methods, " + moduleSummary.SharedTargetCount + " shared target(s)";
        }
    }
}
#endif
