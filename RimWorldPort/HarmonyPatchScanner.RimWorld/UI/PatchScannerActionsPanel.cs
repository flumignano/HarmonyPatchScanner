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
            Action copyLastPath,
            ref Vector2 scrollPosition)
        {
            Widgets.DrawMenuSection(rect);
            var inner = rect.ContractedBy(8f);
            var viewRect = new Rect(0f, 0f, inner.width - 16f, 620f);

            Widgets.BeginScrollView(inner, ref scrollPosition, viewRect);

            var y = 0f;
            Widgets.Label(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.TextLineHeight), "Actions");
            y += PatchScannerUiConstants.TextLineHeight + 6f;

            if (Widgets.ButtonText(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.ButtonHeight), "Scan all Harmony patches"))
                scanAll();
            y += PatchScannerUiConstants.ButtonHeight + PatchScannerUiConstants.Gap;

            if (Widgets.ButtonText(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.ButtonHeight), "Find duplicate/conflicting patches"))
                findConflicts();
            y += PatchScannerUiConstants.ButtonHeight + PatchScannerUiConstants.Gap;

            if (Widgets.ButtonText(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.ButtonHeight), "Scan selected mod"))
                scanModule();
            y += PatchScannerUiConstants.ButtonHeight + 16f;

            Widgets.Label(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.TextLineHeight), "Filters");
            y += PatchScannerUiConstants.TextLineHeight + 4f;

            var lifecycleRect = new Rect(0f, y, viewRect.width, PatchScannerUiConstants.TextLineHeight);
            Widgets.CheckboxLabeled(lifecycleRect, "Exclude common lifecycle methods", ref settings.ExcludeCommonLifecycleMethods);
            TooltipHandler.TipRegion(lifecycleRect, "Hides routine startup, loading, settings, and save/load patches from the reports.");
            y += PatchScannerUiConstants.TextLineHeight + 4f;

            var communityRect = new Rect(0f, y, viewRect.width, PatchScannerUiConstants.TextLineHeight);
            Widgets.CheckboxLabeled(communityRect, "Exclude community libraries", ref settings.ExcludeCommunityLibraries);
            TooltipHandler.TipRegion(communityRect, "Hides common framework/library mods such as Harmony and HugsLib when possible.");
            y += PatchScannerUiConstants.TextLineHeight + 16f;

            Widgets.Label(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.TextLineHeight), "Selected mod");
            y += PatchScannerUiConstants.TextLineHeight;
            var selectedText = selectedModule == null
                ? "No mod selected."
                : "#" + selectedModule.Position + " " + selectedModule.DisplayName + "\n" + selectedModule.ModId;
            var selectedHeight = Text.CalcHeight(selectedText, viewRect.width);
            Widgets.Label(new Rect(0f, y, viewRect.width, selectedHeight), selectedText);
            y += selectedHeight + 16f;

            Widgets.Label(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.TextLineHeight), "Export folder");
            y += PatchScannerUiConstants.TextLineHeight;
            var folderHeight = Text.CalcHeight(logDirectory, viewRect.width);
            Widgets.Label(new Rect(0f, y, viewRect.width, folderHeight), logDirectory);
            y += folderHeight + 16f;

            Widgets.Label(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.TextLineHeight), "Last exported file");
            y += PatchScannerUiConstants.TextLineHeight;
            var pathText = string.IsNullOrEmpty(lastExportPath) ? "No report exported yet." : lastExportPath;
            var pathHeight = Text.CalcHeight(pathText, viewRect.width);
            Widgets.Label(new Rect(0f, y, viewRect.width, pathHeight), pathText);
            y += pathHeight + PatchScannerUiConstants.Gap;

            if (Widgets.ButtonText(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.ButtonHeight), "Copy last log path"))
                copyLastPath();
            y += PatchScannerUiConstants.ButtonHeight + 16f;

            Widgets.Label(new Rect(0f, y, viewRect.width, PatchScannerUiConstants.TextLineHeight), "Report files");
            y += PatchScannerUiConstants.TextLineHeight;
            var files = "AllHarmonyPatches.txt\nDuplicateHarmonyPatches.txt\nModuleScan_<mod>.txt";
            Widgets.Label(new Rect(0f, y, viewRect.width, Text.CalcHeight(files, viewRect.width)), files);

            Widgets.EndScrollView();
        }
    }
}
#endif
