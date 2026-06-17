using System;
using System.Collections.Generic;

namespace HarmonyPatchScanner.Core
{
    public sealed class PatchScannerOptions
    {
        public bool ExcludeCommonLifecycleMethods { get; set; } = true;

        public bool ExcludeCommunityLibraries { get; set; } = true;

        public HashSet<string> CommonLifecycleMethodNames { get; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static PatchScannerOptions CreateBannerlordDefaults()
        {
            var options = new PatchScannerOptions();
            options.AddLifecycleMethods(
                "OnSubModuleLoadPostfix",
                "OnSubModuleLoad",
                "OnSubModuleUnloadedPostfix",
                "OnSubModuleUnloaded",
                "RegisterSubModuleObjectsPostfix",
                "RegisterSubModuleObjects",
                "AfterRegisterSubModuleObjectsPostfix",
                "AfterRegisterSubModuleObjects",
                "OnGameStartPostfix",
                "OnGameStart",
                "OnGameLoadedPostfix",
                "OnGameLoaded",
                "OnGameEndPostfix",
                "OnGameEnd",
                "OnGameInitializationFinishedPostfix",
                "OnGameInitializationFinished",
                "OnAfterGameInitializationFinishedPostfix",
                "OnAfterGameInitializationFinished",
                "InitializeGameStarterPostfix",
                "InitializeGameStarter",
                "DoLoadingPostfix",
                "DoLoading",
                "OnCampaignStartPostfix",
                "OnCampaignStart",
                "BeginGameStartPostfix",
                "BeginGameStart",
                "OnNewGameCreatedPostfix",
                "OnNewGameCreated",
                "OnBeforeMissionBehaviourInitializePostfix",
                "OnBeforeMissionBehaviourInitialize",
                "OnMissionBehaviourInitializePostfix",
                "OnMissionBehaviourInitialize",
                "OnApplicationTickPostfix",
                "OnApplicationTick",
                "OnBeforeInitialModuleScreenSetAsRootPostfix",
                "OnBeforeInitialModuleScreenSetAsRoot",
                "AfterAsyncTickTickPostfix",
                "AfterAsyncTickTick",
                "OnMultiplayerGameStartPostfix",
                "OnMultiplayerGameStart",
                "OnConfigChangedPostfix",
                "OnConfigChanged",
                "OnInitialStatePostfix",
                "OnInitialState");

            return options;
        }

        public static PatchScannerOptions CreateRimWorldDefaults()
        {
            var options = new PatchScannerOptions();
            options.AddLifecycleMethods(
                "StaticConstructor",
                "StaticConstructorOnStartup",
                "FinalizeInit",
                "ResolveReferences",
                "PostLoad",
                "ExposeData",
                "DoSettingsWindowContents",
                "WriteSettings",
                "DefsLoaded",
                "WorldLoaded",
                "MapLoaded",
                "StartedNewGame",
                "LoadedGame");

            return options;
        }

        public void AddLifecycleMethods(params string[] methodNames)
        {
            foreach (var methodName in methodNames)
            {
                if (!string.IsNullOrWhiteSpace(methodName))
                    CommonLifecycleMethodNames.Add(methodName);
            }
        }
    }
}
