#if RIMWORLD
using System;
using HarmonyPatchScanner.Core;
using UnityEngine;
using Verse;

namespace HarmonyPatchScanner.RimWorld.UI
{
    internal static class PatchScannerActionsPanel
    {
        public static void Draw(
            Rect rect,
            HarmonyPatchScannerSettings settings,
            ModLoadInfo? selectedModule,
            string lastExportPath,
            string logDirectory,
            Action scanAll,
            Action findConflicts,
            Action scanModule,
            Action viewStaticFindings,
            Action copyLastPath,
            ref Vector2 scrollPosition)
        {
            Widgets.DrawMenuSection(rect);
            var inner = rect.ContractedBy(8f);
            var viewRect = new Rect(0f, 0f, inner.width - 16f, 670f);

            Widgets.BeginScrollView(inner, ref scrollPosition, viewRect);

            var y = 0f;
            Widgets.Label(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.TextLineHeight), "HPS_Actions".Translate());
            y += PatchScannerUiConstants.TextLineHeight + 6f;

            if (Widgets.ButtonText(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.ButtonHeight), "HPS_ScanAll".Translate()))
                scanAll();
            y += PatchScannerUiConstants.ButtonHeight + PatchScannerUiConstants.Gap;

            if (Widgets.ButtonText(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.ButtonHeight), "HPS_FindConflicts".Translate()))
                findConflicts();
            y += PatchScannerUiConstants.ButtonHeight + PatchScannerUiConstants.Gap;

            if (Widgets.ButtonText(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.ButtonHeight), "HPS_ScanSelectedMod".Translate()))
                scanModule();
            y += PatchScannerUiConstants.ButtonHeight + PatchScannerUiConstants.Gap;

            if (Widgets.ButtonText(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.ButtonHeight), "HPS_ViewStaticFindings".Translate()))
                viewStaticFindings();
            y += PatchScannerUiConstants.ButtonHeight + 16f;

            Widgets.Label(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.TextLineHeight), "HPS_Filters".Translate());
            y += PatchScannerUiConstants.TextLineHeight + 4f;

            var lifecycleRect = new Rect(0f, y, viewRect.width, PatchScannerUiConstants.TextLineHeight);
            Widgets.CheckboxLabeled(lifecycleRect, "HPS_ExcludeLifecycleMethods".Translate(), ref settings.ExcludeCommonLifecycleMethods);
            TooltipHandler.TipRegion(lifecycleRect, "HPS_ExcludeLifecycleMethodsTooltip".Translate());
            y += PatchScannerUiConstants.TextLineHeight + 4f;

            var communityRect = new Rect(0f, y, viewRect.width, PatchScannerUiConstants.TextLineHeight);
            Widgets.CheckboxLabeled(communityRect, "HPS_ExcludeCommunityLibraries".Translate(), ref settings.ExcludeCommunityLibraries);
            TooltipHandler.TipRegion(communityRect, "HPS_ExcludeCommunityLibrariesTooltip".Translate());
            y += PatchScannerUiConstants.TextLineHeight + 16f;

            Widgets.Label(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.TextLineHeight), "HPS_SelectedMod".Translate());
            y += PatchScannerUiConstants.TextLineHeight;
            var selectedText = selectedModule == null
                ? "HPS_NoModSelected".Translate().ToString()
                : "#" + selectedModule.Position + " " + selectedModule.DisplayName + "\n" + selectedModule.ModId;
            var selectedHeight = Text.CalcHeight(selectedText, viewRect.width);
            Widgets.Label(new Rect(0f, y, viewRect.width, selectedHeight), selectedText);
            y += selectedHeight + 16f;

            Widgets.Label(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.TextLineHeight), "HPS_ExportFolder".Translate());
            y += PatchScannerUiConstants.TextLineHeight;
            var folderHeight = Text.CalcHeight(logDirectory, viewRect.width);
            Widgets.Label(new Rect(0f, y, viewRect.width, folderHeight), logDirectory);
            y += folderHeight + 16f;

            Widgets.Label(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.TextLineHeight), "HPS_LastExportedFile".Translate());
            y += PatchScannerUiConstants.TextLineHeight;
            var pathText = string.IsNullOrEmpty(lastExportPath) ? "HPS_NoReportExported".Translate().ToString() : lastExportPath;
            var pathHeight = Text.CalcHeight(pathText, viewRect.width);
            Widgets.Label(new Rect(0f, y, viewRect.width, pathHeight), pathText);
            y += pathHeight + PatchScannerUiConstants.Gap;

            if (Widgets.ButtonText(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.ButtonHeight), "HPS_CopyLastLogPath".Translate()))
                copyLastPath();
            y += PatchScannerUiConstants.ButtonHeight + 16f;

            Widgets.Label(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.TextLineHeight), "HPS_ReportFiles".Translate());
            y += PatchScannerUiConstants.TextLineHeight;
            var files = "AllHarmonyPatches.txt\nDuplicateHarmonyPatches.txt\nModuleScan_<mod>.txt";
            Widgets.Label(new Rect(0f, y, viewRect.width, Text.CalcHeight(files, viewRect.width)), files);

            Widgets.EndScrollView();
        }
    }
}
#endif
