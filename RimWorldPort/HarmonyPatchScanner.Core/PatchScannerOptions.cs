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
