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

        // Keep independent result state so changing scope never replaces the report the user
        // was reviewing in the other scope. Selected mods also retain their own latest result.
        private readonly ScanViewState allModsResult = new ScanViewState();
        private readonly Dictionary<string, ScanViewState> selectedModResults =
            new Dictionary<string, ScanViewState>(StringComparer.OrdinalIgnoreCase);
        private PatchScannerUiSummary? moduleListSummary;
        private ModLoadInfo? selectedModule;
        private PatchScannerScope activeScope = PatchScannerScope.AllMods;
        private Vector2 moduleScroll;
        private Vector2 detailsScroll;
        private Vector2 actionsScroll;
        private string searchText = string.Empty;
        private string statusText = "HPS_StatusReady".Translate();
        private string statusTooltipText = "HPS_StatusReady".Translate();
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
            Widgets.Label(new Rect(rect.x, rect.y, 330f, rect.height), "HPS_ModName".Translate());
            Text.Font = GameFont.Small;

            const float scopeLabelWidth = 50f;
            const float scopeButtonWidth = 180f;
            const float clearWidth = 105f;
            var y = rect.y + 5f;
            var clearRect = new Rect(rect.xMax - clearWidth, y, clearWidth, PatchScannerUiConstants.ButtonHeight);
            var selectedRect = new Rect(
                clearRect.x - PatchScannerUiConstants.Gap - scopeButtonWidth,
                y,
                scopeButtonWidth,
                PatchScannerUiConstants.ButtonHeight);
            var allRect = new Rect(
                selectedRect.x - scopeButtonWidth,
                y,
                scopeButtonWidth,
                PatchScannerUiConstants.ButtonHeight);
            var scopeLabelRect = new Rect(
                allRect.x - scopeLabelWidth - PatchScannerUiConstants.Gap,
                y,
                scopeLabelWidth,
                PatchScannerUiConstants.ButtonHeight);

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(scopeLabelRect, "HPS_Scope".Translate());
            Text.Anchor = TextAnchor.UpperLeft;

            if (DrawScopeButton(allRect, "HPS_ScopeAllMods".Translate(), activeScope == PatchScannerScope.AllMods, true))
                SetScope(PatchScannerScope.AllMods);

            var selectedEnabled = selectedModule != null;
            if (DrawScopeButton(
                    selectedRect,
                    "HPS_ScopeSelectedMod".Translate(),
                    activeScope == PatchScannerScope.SelectedMod,
                    selectedEnabled))
            {
                SetScope(PatchScannerScope.SelectedMod);
            }

            if (!selectedEnabled)
                TooltipHandler.TipRegion(selectedRect, "HPS_SelectModForScope".Translate());

            if (Widgets.ButtonText(clearRect, "HPS_Clear".Translate()))
                ClearResults();
        }

        private static bool DrawScopeButton(Rect rect, string label, bool selected, bool enabled)
        {
            if (selected)
                Widgets.DrawHighlightSelected(rect);
            else if (enabled)
                Widgets.DrawHighlightIfMouseover(rect);

            var previousColor = GUI.color;
            if (!enabled)
                GUI.color = Color.gray;

            Widgets.DrawBox(rect);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = previousColor;

            return enabled && Widgets.ButtonInvisible(rect);
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
                moduleListSummary,
                selectedModule,
                searchText,
                OnModuleSelected,
                ref moduleScroll,
                ref searchText);

            PatchScannerDetailsPanel.Draw(detailsRect, detailsText, ref detailsScroll);

            var activeResult = GetActiveResultState(createIfMissing: false);
            var scanAction = activeScope == PatchScannerScope.AllMods ? (Action)RunScanAll : RunModuleScan;
            var conflictAction = activeScope == PatchScannerScope.AllMods ? (Action)RunConflictScan : RunModuleConflictScan;
            PatchScannerActionsPanel.Draw(
                actionsRect,
                settings,
                activeScope,
                selectedModule,
                activeResult?.LastExportPath ?? string.Empty,
                logDirectory,
                scanAction,
                conflictAction,
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
            activeScope = PatchScannerScope.SelectedMod;
            detailsScroll = Vector2.zero;
            actionsScroll = Vector2.zero;
            SetStatus("HPS_StatusSelectedMod".Translate(module.DisplayName));
            RefreshDetailsText();
        }

        private void SetScope(PatchScannerScope scope)
        {
            if (scope == PatchScannerScope.SelectedMod && selectedModule == null)
                return;

            activeScope = scope;
            detailsScroll = Vector2.zero;
            actionsScroll = Vector2.zero;
            SetStatus(scope == PatchScannerScope.AllMods
                ? "HPS_StatusAllModsScope".Translate()
                : "HPS_StatusSelectedModScope".Translate(selectedModule?.DisplayName ?? string.Empty));
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

            return null;
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
            RunExport(
                "HPS_ReportNameFullPatchScan".Translate(),
                PatchScannerReportKind.AllPatches,
                () => scannerService.ExportAllPatches(BuildOptions()),
                allModsResult);
        }

        private void RunConflictScan()
        {
            RunExport(
                "HPS_ReportNameConflictScan".Translate(),
                PatchScannerReportKind.AllConflicts,
                () => scannerService.ExportConflictReport(BuildOptions()),
                allModsResult);
        }

        private void RunModuleScan()
        {
            if (selectedModule == null)
            {
                SetStatus("HPS_StatusSelectModFirst".Translate());
                Messages.Message(statusText, MessageTypeDefOf.RejectInput, false);
                return;
            }

            var resultState = GetActiveResultState(createIfMissing: true)!;
            RunExport(
                "HPS_ReportNameModuleScan".Translate(),
                PatchScannerReportKind.ModulePatches,
                () => scannerService.ExportModuleReport(BuildOptions(), selectedModule.ModId),
                resultState);
        }

        private void RunModuleConflictScan()
        {
            if (selectedModule == null)
            {
                SetStatus("HPS_StatusSelectModFirst".Translate());
                Messages.Message(statusText, MessageTypeDefOf.RejectInput, false);
                return;
            }

            var resultState = GetActiveResultState(createIfMissing: true)!;
            RunExport(
                "HPS_ReportNameModuleConflictScan".Translate(),
                PatchScannerReportKind.ModuleConflicts,
                () => scannerService.ExportModuleConflictReport(BuildOptions(), selectedModule.ModId),
                resultState);
        }

        private void RunExport(
            string reportName,
            PatchScannerReportKind reportKind,
            Func<PatchExportResult> export,
            ScanViewState resultState)
        {
            try
            {
                var result = export();
                resultState.Snapshot = result.Snapshot;
                resultState.Summary = result.Snapshot == null ? null : PatchScannerUiSummary.Build(result.Snapshot, loadOrder);
                resultState.LastExportPath = result.FilePath;
                resultState.ReportKind = reportKind;
                moduleListSummary = resultState.Summary;
                var fileName = Path.GetFileName(result.FilePath);
                SetStatus(
                    "HPS_StatusExportSaved".Translate(reportName, fileName),
                    "HPS_StatusExportSavedPath".Translate(reportName, result.FilePath));
                resultState.CurrentReport = "HPS_StatusReportCompletedAt".Translate(reportName, DateTime.Now.ToString("HH:mm:ss"));
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
            allModsResult.Clear();
            selectedModResults.Clear();
            moduleListSummary = null;
            SetStatus("HPS_StatusCleared".Translate());
            RefreshDetailsText();
            detailsScroll = Vector2.zero;
        }

        private void CopyLastPath()
        {
            var lastExportPath = GetActiveResultState(createIfMissing: false)?.LastExportPath ?? string.Empty;
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
            var resultState = GetActiveResultState(createIfMissing: false);
            var snapshot = resultState?.Snapshot;
            if (snapshot == null)
            {
                SetStatus("HPS_StatusScanBeforeStaticFindings".Translate());
                return;
            }

            IReadOnlyList<StaticPatchFinding> scopedFindings = snapshot.StaticFindings;
            if (activeScope == PatchScannerScope.SelectedMod && selectedModule != null)
                scopedFindings = scannerService.GetModuleStaticFindings(snapshot, selectedModule.ModId);

            var actionableFindings = scopedFindings
                .Where(finding => finding.Confidence == StaticFindingConfidence.Deterministic ||
                                  finding.Confidence == StaticFindingConfidence.Likely)
                .ToList();

            if (actionableFindings.Count == 0)
            {
                SetStatus("HPS_StatusNoActionableStaticFindings".Translate());
                return;
            }

            Find.WindowStack.Add(new Dialog_StaticIlFindings(actionableFindings, scopedFindings.Count));
        }

        private void RefreshDetailsText()
        {
            var resultState = GetActiveResultState(createIfMissing: false);
            detailsText = PatchScannerDetailsPanel.BuildDetailsText(
                activeScope,
                resultState?.Summary,
                activeScope == PatchScannerScope.SelectedMod ? selectedModule : null,
                resultState?.ReportKind ?? PatchScannerReportKind.None,
                resultState?.LastExportPath ?? string.Empty,
                string.IsNullOrEmpty(resultState?.CurrentReport)
                    ? "HPS_StatusNoScanRun".Translate()
                    : resultState!.CurrentReport);
        }

        private ScanViewState? GetActiveResultState(bool createIfMissing)
        {
            if (activeScope == PatchScannerScope.AllMods)
                return allModsResult;

            if (selectedModule == null)
                return null;

            if (selectedModResults.TryGetValue(selectedModule.ModId, out var resultState))
                return resultState;

            if (!createIfMissing)
                return null;

            resultState = new ScanViewState();
            selectedModResults[selectedModule.ModId] = resultState;
            return resultState;
        }

        private void SetStatus(string text, string? tooltip = null)
        {
            statusText = text;
            statusTooltipText = tooltip ?? text;
        }

        private sealed class ScanViewState
        {
            public PatchScanSnapshot? Snapshot { get; set; }
            public PatchScannerUiSummary? Summary { get; set; }
            public string LastExportPath { get; set; } = string.Empty;
            public string CurrentReport { get; set; } = string.Empty;
            public PatchScannerReportKind ReportKind { get; set; } = PatchScannerReportKind.None;

            public void Clear()
            {
                Snapshot = null;
                Summary = null;
                LastExportPath = string.Empty;
                CurrentReport = string.Empty;
                ReportKind = PatchScannerReportKind.None;
            }
        }

    }
}
#endif
