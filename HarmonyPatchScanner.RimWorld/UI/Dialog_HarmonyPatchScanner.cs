#if RIMWORLD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyPatchScanner.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace HarmonyPatchScanner.RimWorld.UI
{
    public sealed class Dialog_HarmonyPatchScanner : Window
    {
        private readonly HarmonyPatchScannerSettings settings;
        private readonly RimWorldPatchScannerAdapter adapter;
        private readonly PatchScannerService scannerService;
        private readonly IReadOnlyList<ModLoadInfo> loadOrder;
        private readonly string logDirectory;

        private PatchScanSnapshot? snapshot;
        private PatchScannerUiSummary? uiSummary;
        private ModLoadInfo? selectedModule;
        private Vector2 moduleScroll;
        private Vector2 detailsScroll;
        private Vector2 actionsScroll;
        private string searchText = string.Empty;
        private string statusText = "HPS_StatusReady".Translate();
        private string statusTooltipText = "HPS_StatusReady".Translate();
        private string lastExportPath = string.Empty;
        private string currentReport = "HPS_StatusNoScanRun".Translate();
        private string detailsText = string.Empty;

        public Dialog_HarmonyPatchScanner(HarmonyPatchScannerSettings settings)
        {
            this.settings = settings;
            adapter = new RimWorldPatchScannerAdapter();
            scannerService = new PatchScannerService(adapter);
            loadOrder = adapter.GetLoadOrder();
            logDirectory = adapter.GetLogDirectory();
            selectedModule = ResolveInitialModule();
            RefreshDetailsText();

            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
        }

        public override Vector2 InitialSize
        {
            get { return new Vector2(1500f, 850f); }
        }

        public override void DoWindowContents(Rect inRect)
        {
            var topRect = new Rect(inRect.x, inRect.y, inRect.width, PatchScannerUiConstants.TopBarHeight);
            var statusRect = new Rect(inRect.x, inRect.yMax - PatchScannerUiConstants.StatusHeight, inRect.width, PatchScannerUiConstants.StatusHeight);
            var bodyRect = new Rect(
                inRect.x,
                topRect.yMax + PatchScannerUiConstants.Gap,
                inRect.width,
                inRect.height - PatchScannerUiConstants.TopBarHeight - PatchScannerUiConstants.StatusHeight - PatchScannerUiConstants.Gap * 2f);

            DrawTopBar(topRect);
            DrawBody(bodyRect);
            DrawStatus(statusRect);
        }

        private void DrawTopBar(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, 360f, rect.height), "HPS_ModName".Translate());
            Text.Font = GameFont.Small;

            const float scanAllWidth = 115f;
            const float conflictsWidth = 130f;
            const float moduleWidth = 125f;
            const float clearWidth = 80f;
            var totalWidth = scanAllWidth + conflictsWidth + moduleWidth + clearWidth + PatchScannerUiConstants.Gap * 3f;
            var x = rect.xMax - totalWidth;
            var y = rect.y + 5f;

            var scanRect = new Rect(x, y, scanAllWidth, PatchScannerUiConstants.ButtonHeight);
            var conflictRect = new Rect(scanRect.xMax + PatchScannerUiConstants.Gap, y, conflictsWidth, PatchScannerUiConstants.ButtonHeight);
            var moduleRect = new Rect(conflictRect.xMax + PatchScannerUiConstants.Gap, y, moduleWidth, PatchScannerUiConstants.ButtonHeight);
            var clearRect = new Rect(moduleRect.xMax + PatchScannerUiConstants.Gap, y, clearWidth, PatchScannerUiConstants.ButtonHeight);

            if (Widgets.ButtonText(scanRect, "HPS_ScanAllShort".Translate()))
                RunScanAll();

            if (Widgets.ButtonText(conflictRect, "HPS_FindConflictsShort".Translate()))
                RunConflictScan();

            if (Widgets.ButtonText(moduleRect, "HPS_ScanModShort".Translate()))
                RunModuleScan();

            if (Widgets.ButtonText(clearRect, "HPS_Clear".Translate()))
                ClearResults();
        }

        private void DrawBody(Rect rect)
        {
            var moduleListRect = new Rect(rect.x, rect.y, PatchScannerUiConstants.ModuleListWidth, rect.height);
            var actionsRect = new Rect(rect.xMax - PatchScannerUiConstants.ActionsPanelWidth, rect.y, PatchScannerUiConstants.ActionsPanelWidth, rect.height);
            var detailsRect = new Rect(
                moduleListRect.xMax + PatchScannerUiConstants.Gap,
                rect.y,
                rect.width - moduleListRect.width - actionsRect.width - PatchScannerUiConstants.Gap * 2f,
                rect.height);

            PatchScannerModuleListPanel.Draw(
                moduleListRect,
                loadOrder,
                uiSummary,
                selectedModule,
                searchText,
                OnModuleSelected,
                ref moduleScroll,
                ref searchText);

            PatchScannerDetailsPanel.Draw(detailsRect, detailsText, ref detailsScroll);

            PatchScannerActionsPanel.Draw(
                actionsRect,
                settings,
                selectedModule,
                lastExportPath,
                logDirectory,
                RunScanAll,
                RunConflictScan,
                RunModuleScan,
                ShowStaticFindings,
                CopyLastPath,
                ref actionsScroll);
        }

        private void DrawStatus(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            var labelRect = rect.ContractedBy(6f);
            labelRect.height = PatchScannerUiConstants.TextLineHeight;

            var previousWordWrap = Text.WordWrap;
            Text.WordWrap = false;
            try
            {
                Widgets.Label(labelRect, statusText);
            }
            finally
            {
                Text.WordWrap = previousWordWrap;
            }

            if (!string.IsNullOrEmpty(statusTooltipText))
                TooltipHandler.TipRegion(rect, statusTooltipText);
        }

        private void OnModuleSelected(ModLoadInfo module)
        {
            selectedModule = module;
            settings.SelectedModuleId = module.ModId;
            detailsScroll = Vector2.zero;
            actionsScroll = Vector2.zero;
            SetStatus("HPS_StatusSelectedMod".Translate(module.DisplayName));
            RefreshDetailsText();
        }

        private ModLoadInfo? ResolveInitialModule()
        {
            if (!string.IsNullOrEmpty(settings.SelectedModuleId))
            {
                var saved = loadOrder.FirstOrDefault(mod =>
                    string.Equals(mod.ModId, settings.SelectedModuleId, StringComparison.OrdinalIgnoreCase));
                if (saved != null)
                    return saved;
            }

            return loadOrder.FirstOrDefault(mod => !mod.IsOfficial && !mod.IsCommunityLibrary) ??
                   loadOrder.FirstOrDefault();
        }

        private PatchScannerOptions BuildOptions()
        {
            var options = PatchScannerOptions.CreateRimWorldDefaults();
            options.ExcludeCommonLifecycleMethods = settings.ExcludeCommonLifecycleMethods;
            options.ExcludeCommunityLibraries = settings.ExcludeCommunityLibraries;
            return options;
        }

        private void RunScanAll()
        {
            RunExport("HPS_ReportNameFullPatchScan".Translate(), () => scannerService.ExportAllPatches(BuildOptions()));
        }

        private void RunConflictScan()
        {
            RunExport("HPS_ReportNameConflictScan".Translate(), () => scannerService.ExportConflictReport(BuildOptions()));
        }

        private void RunModuleScan()
        {
            if (selectedModule == null)
            {
                SetStatus("HPS_StatusSelectModFirst".Translate());
                Messages.Message(statusText, MessageTypeDefOf.RejectInput, false);
                return;
            }

            RunExport("HPS_ReportNameModuleScan".Translate(), () => scannerService.ExportModuleReport(BuildOptions(), selectedModule.ModId));
        }

        private void RunExport(string reportName, Func<PatchExportResult> export)
        {
            try
            {
                var result = export();
                snapshot = result.Snapshot;
                uiSummary = snapshot == null ? null : PatchScannerUiSummary.Build(snapshot, loadOrder);
                lastExportPath = result.FilePath;
                var fileName = Path.GetFileName(result.FilePath);
                SetStatus(
                    "HPS_StatusExportSaved".Translate(reportName, fileName),
                    "HPS_StatusExportSavedPath".Translate(reportName, result.FilePath));
                currentReport = "HPS_StatusReportCompletedAt".Translate(reportName, DateTime.Now.ToString("HH:mm:ss"));
                RefreshDetailsText();
                detailsScroll = Vector2.zero;
            }
            catch (Exception ex)
            {
                SetStatus("HPS_StatusReportFailed".Translate(reportName, ex.Message));
                Messages.Message(statusText, MessageTypeDefOf.RejectInput, false);
                Log.Error("[HarmonyPatchScanner] " + ex);
            }
        }

        private void ClearResults()
        {
            snapshot = null;
            uiSummary = null;
            lastExportPath = string.Empty;
            currentReport = "HPS_StatusNoScanRun".Translate();
            SetStatus("HPS_StatusCleared".Translate());
            RefreshDetailsText();
            detailsScroll = Vector2.zero;
        }

        private void CopyLastPath()
        {
            if (string.IsNullOrEmpty(lastExportPath))
            {
                SetStatus("HPS_StatusNoLogPath".Translate());
                return;
            }

            GUIUtility.systemCopyBuffer = lastExportPath;
            SetStatus("HPS_StatusCopiedLogPath".Translate());
        }

        private void ShowStaticFindings()
        {
            if (snapshot == null)
            {
                SetStatus("HPS_StatusScanBeforeStaticFindings".Translate());
                return;
            }

            var actionableFindings = snapshot.StaticFindings
                .Where(finding => finding.Confidence == StaticFindingConfidence.Deterministic ||
                                  finding.Confidence == StaticFindingConfidence.Likely)
                .ToList();

            if (actionableFindings.Count == 0)
            {
                SetStatus("HPS_StatusNoActionableStaticFindings".Translate());
                return;
            }

            Find.WindowStack.Add(new Dialog_StaticIlFindings(actionableFindings, snapshot.StaticFindings.Count));
        }

        private void RefreshDetailsText()
        {
            detailsText = PatchScannerDetailsPanel.BuildDetailsText(uiSummary, selectedModule, currentReport);
        }

        private void SetStatus(string text, string? tooltip = null)
        {
            statusText = text;
            statusTooltipText = tooltip ?? text;
        }

    }
}
#endif
