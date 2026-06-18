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
        private static ModuleListLayout? cachedLayout;

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
            var viewWidth = listRect.width - 16f;
            var layout = GetLayout(loadOrder, summary, mutableSearchText, viewWidth);
            var viewRect = new Rect(0f, 0f, viewWidth, Math.Max(listRect.height, layout.ViewHeight));

            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);

            var y = 0f;
            var visibleTop = Math.Max(0f, scrollPosition.y - RowGap);
            var visibleBottom = scrollPosition.y + listRect.height + RowGap;
            for (var i = 0; i < layout.Rows.Count; i++)
            {
                var rowLayout = layout.Rows[i];
                var rowHeight = rowLayout.Height;
                var row = new Rect(0f, y, viewRect.width, rowHeight);

                if (row.yMax >= visibleTop && row.y <= visibleBottom)
                    DrawModuleRow(row, rowLayout, selectedModule, onSelect);

                y += rowHeight + RowGap;
            }

            if (layout.Rows.Count == 0)
                Widgets.Label(new Rect(0f, 0f, viewRect.width, 60f), "No loaded mods match the search filter.");

            Widgets.EndScrollView();
        }

        private static ModuleListLayout GetLayout(
            IReadOnlyList<ModLoadInfo> loadOrder,
            PatchScannerUiSummary? summary,
            string searchText,
            float viewWidth)
        {
            var normalizedSearch = searchText ?? string.Empty;
            if (cachedLayout != null &&
                ReferenceEquals(cachedLayout.LoadOrder, loadOrder) &&
                ReferenceEquals(cachedLayout.Summary, summary) &&
                string.Equals(cachedLayout.SearchText, normalizedSearch, StringComparison.Ordinal) &&
                Math.Abs(cachedLayout.ViewWidth - viewWidth) < 0.5f)
            {
                return cachedLayout;
            }

            var rows = loadOrder
                .Where(mod => MatchesSearch(mod, normalizedSearch))
                .Select(module => BuildRowLayout(module, summary, viewWidth))
                .ToList();

            cachedLayout = new ModuleListLayout(
                loadOrder,
                summary,
                normalizedSearch,
                viewWidth,
                rows,
                TotalRowHeight(rows));

            return cachedLayout;
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

        private static float TotalRowHeight(IReadOnlyList<ModuleRowLayout> rows)
        {
            if (rows.Count == 0)
                return 0f;

            return rows.Sum(row => row.Height) + (rows.Count - 1) * RowGap;
        }

        private static ModuleRowLayout BuildRowLayout(ModLoadInfo module, PatchScannerUiSummary? summary, float rowWidth)
        {
            var textWidth = rowWidth - RowHorizontalPadding * 2f;
            var title = TitleText(module);
            var meta = ModuleTags(module);
            var patchCounts = BuildPatchCounts(module, summary);
            var titleHeight = Text.CalcHeight(title, textWidth);
            var metaHeight = Text.CalcHeight(meta, textWidth);
            var countHeight = Text.CalcHeight(patchCounts, textWidth);

            var height = Math.Max(
                PatchScannerUiConstants.RowHeight,
                RowTopPadding + titleHeight + RowLineGap + metaHeight + RowLineGap + countHeight + RowBottomPadding);

            return new ModuleRowLayout(module, title, meta, patchCounts, titleHeight, metaHeight, countHeight, height);
        }

        private static void DrawModuleRow(
            Rect rect,
            ModuleRowLayout layout,
            ModLoadInfo? selectedModule,
            Action<ModLoadInfo> onSelect)
        {
            var selected = selectedModule != null &&
                           string.Equals(selectedModule.ModId, layout.Module.ModId, StringComparison.OrdinalIgnoreCase);

            if (selected)
                Widgets.DrawHighlightSelected(rect);
            else
                Widgets.DrawHighlightIfMouseover(rect);

            Widgets.DrawBox(rect);
            if (Widgets.ButtonInvisible(rect))
                onSelect(layout.Module);

            var textWidth = rect.width - RowHorizontalPadding * 2f;

            var titleRect = new Rect(
                rect.x + RowHorizontalPadding,
                rect.y + RowTopPadding,
                textWidth,
                layout.TitleHeight);
            var metaRect = new Rect(
                titleRect.x,
                titleRect.yMax + RowLineGap,
                textWidth,
                layout.MetaHeight);
            var countRect = new Rect(
                titleRect.x,
                metaRect.yMax + RowLineGap,
                textWidth,
                layout.PatchCountsHeight);

            Widgets.Label(titleRect, layout.Title);
            Widgets.Label(metaRect, layout.Meta);
            Widgets.Label(countRect, layout.PatchCounts);
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

        private sealed class ModuleListLayout
        {
            public ModuleListLayout(
                IReadOnlyList<ModLoadInfo> loadOrder,
                PatchScannerUiSummary? summary,
                string searchText,
                float viewWidth,
                IReadOnlyList<ModuleRowLayout> rows,
                float viewHeight)
            {
                LoadOrder = loadOrder;
                Summary = summary;
                SearchText = searchText;
                ViewWidth = viewWidth;
                Rows = rows;
                ViewHeight = viewHeight;
            }

            public IReadOnlyList<ModLoadInfo> LoadOrder { get; }
            public PatchScannerUiSummary? Summary { get; }
            public string SearchText { get; }
            public float ViewWidth { get; }
            public IReadOnlyList<ModuleRowLayout> Rows { get; }
            public float ViewHeight { get; }
        }

        private sealed class ModuleRowLayout
        {
            public ModuleRowLayout(
                ModLoadInfo module,
                string title,
                string meta,
                string patchCounts,
                float titleHeight,
                float metaHeight,
                float patchCountsHeight,
                float height)
            {
                Module = module;
                Title = title;
                Meta = meta;
                PatchCounts = patchCounts;
                TitleHeight = titleHeight;
                MetaHeight = metaHeight;
                PatchCountsHeight = patchCountsHeight;
                Height = height;
            }

            public ModLoadInfo Module { get; }
            public string Title { get; }
            public string Meta { get; }
            public string PatchCounts { get; }
            public float TitleHeight { get; }
            public float MetaHeight { get; }
            public float PatchCountsHeight { get; }
            public float Height { get; }
        }
    }
}
#endif
