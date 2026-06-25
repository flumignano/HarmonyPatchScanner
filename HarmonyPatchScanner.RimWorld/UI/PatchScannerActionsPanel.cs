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
            PatchScannerScope scope,
            ModLoadInfo? selectedModule,
            string lastExportPath,
            string logDirectory,
            Action scanPatchList,
            Action findConflicts,
            Action viewStaticFindings,
            Action copyLastPath,
            ref Vector2 scrollPosition)
        {
            Widgets.DrawMenuSection(rect);
            var inner = rect.ContractedBy(8f);
            var viewRect = new Rect(0f, 0f, inner.width - 16f, 670f);

            Widgets.BeginScrollView(inner, ref scrollPosition, viewRect);

            var y = 0f;
            var selectedScope = scope == PatchScannerScope.SelectedMod;
            var hasSelectedModule = selectedModule != null;
            var actionsTitle = selectedScope ? "HPS_SelectedModActions".Translate() : "HPS_AllModsActions".Translate();
            Widgets.Label(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.TextLineHeight), actionsTitle);
            y += PatchScannerUiConstants.TextLineHeight + 6f;

            if (selectedScope)
            {
                var selectedText = selectedModule == null
                    ? "HPS_NoModSelected".Translate().ToString()
                    : "#" + selectedModule.Position + " " + selectedModule.DisplayName + "\n" + selectedModule.ModId;
                var selectedHeight = Text.CalcHeight(selectedText, viewRect.width);
                Widgets.Label(new Rect(0f, y, viewRect.width, selectedHeight), selectedText);
                y += selectedHeight + 8f;

                if (!hasSelectedModule)
                {
                    var hint = "HPS_SelectModOrUseAllScope".Translate().ToString();
                    var hintHeight = Text.CalcHeight(hint, viewRect.width);
                    Widgets.Label(new Rect(0f, y, viewRect.width, hintHeight), hint);
                    y += hintHeight + 8f;
                }
            }
            else
            {
                Widgets.Label(
                    new Rect(0f, y, viewRect.width, PatchScannerUiConstants.TextLineHeight),
                    "HPS_AllLoadedModsScope".Translate());
                y += PatchScannerUiConstants.TextLineHeight + 8f;
            }

            var actionsEnabled = !selectedScope || hasSelectedModule;
            var previousEnabled = GUI.enabled;
            GUI.enabled = actionsEnabled;

            var scanLabel = selectedScope ? "HPS_ScanSelectedMod".Translate() : "HPS_ScanAll".Translate();
            if (Widgets.ButtonText(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.ButtonHeight), scanLabel))
                scanPatchList();
            y += PatchScannerUiConstants.ButtonHeight + PatchScannerUiConstants.Gap;

            var conflictsLabel = selectedScope ? "HPS_FindSelectedModConflicts".Translate() : "HPS_FindConflicts".Translate();
            if (Widgets.ButtonText(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.ButtonHeight), conflictsLabel))
                findConflicts();
            y += PatchScannerUiConstants.ButtonHeight + PatchScannerUiConstants.Gap;

            var findingsLabel = selectedScope ? "HPS_ViewSelectedModStaticFindings".Translate() : "HPS_ViewAllStaticFindings".Translate();
            if (Widgets.ButtonText(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.ButtonHeight), findingsLabel))
                viewStaticFindings();
            y += PatchScannerUiConstants.ButtonHeight + 16f;
            GUI.enabled = previousEnabled;

            Widgets.Label(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.TextLineHeight), "HPS_FiltersAllScans".Translate());
            y += PatchScannerUiConstants.TextLineHeight + 4f;

            var lifecycleRect = new Rect(0f, y, viewRect.width, PatchScannerUiConstants.TextLineHeight);
            Widgets.CheckboxLabeled(lifecycleRect, "HPS_ExcludeLifecycleMethods".Translate(), ref settings.ExcludeCommonLifecycleMethods);
            TooltipHandler.TipRegion(lifecycleRect, "HPS_ExcludeLifecycleMethodsTooltip".Translate());
            y += PatchScannerUiConstants.TextLineHeight + 4f;

            var communityRect = new Rect(0f, y, viewRect.width, PatchScannerUiConstants.TextLineHeight);
            Widgets.CheckboxLabeled(communityRect, "HPS_ExcludeCommunityLibraries".Translate(), ref settings.ExcludeCommunityLibraries);
            TooltipHandler.TipRegion(communityRect, "HPS_ExcludeCommunityLibrariesTooltip".Translate());
            y += PatchScannerUiConstants.TextLineHeight + 16f;

            Widgets.Label(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.TextLineHeight), "HPS_Output".Translate());
            y += PatchScannerUiConstants.TextLineHeight + 4f;
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

            var hasExportPath = !string.IsNullOrEmpty(lastExportPath);
            previousEnabled = GUI.enabled;
            GUI.enabled = hasExportPath;
            if (Widgets.ButtonText(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.ButtonHeight), "HPS_CopyLastLogPath".Translate()))
                copyLastPath();
            GUI.enabled = previousEnabled;

            Widgets.EndScrollView();
        }
    }
}
#endif
