#if RIMWORLD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyPatchScanner.Core;
using RimWorld;
using Verse;

namespace HarmonyPatchScanner.RimWorld
{
    public sealed class RimWorldPatchScannerAdapter : IPatchScannerHost
    {
        private static readonly HashSet<string> CommunityLibraryPackageIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "brrainz.harmony",
                "unlimitedhugs.hugslib",
                "me.samboycoding.betterloading",
                "krkr.rocketman",
                "dubwise.dubsperformanceanalyzer",
                "owlchemist.performanceoptimizer",
                "com.github.rimpy-custom.rimpy-mod-manager-database"
            };

        private readonly List<ModLoadInfo> _loadOrder;
        private readonly Dictionary<string, ModLoadInfo> _modByAssembly;

        public RimWorldPatchScannerAdapter()
        {
            _loadOrder = BuildLoadOrder();
            _modByAssembly = _loadOrder
                .SelectMany(mod => mod.AssemblyNames.Select(assembly => new { assembly, mod }))
                .GroupBy(x => x.assembly, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().mod, StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyList<ModLoadInfo> GetLoadOrder()
        {
            return _loadOrder;
        }

        public string? GetModIdForAssembly(string? assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return null;

            return _modByAssembly.TryGetValue(assemblyName!, out var mod) ? mod.ModId : null;
        }

        public int? GetLoadOrderPositionForAssembly(string? assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return null;

            return _modByAssembly.TryGetValue(assemblyName!, out var mod) ? mod.Position : null;
        }

        public string GetOwnerDisplayName(Assembly? patchAssembly, string? harmonyOwner)
        {
            var assemblyName = patchAssembly?.GetName()?.Name;
            if (!string.IsNullOrEmpty(assemblyName) &&
                _modByAssembly.TryGetValue(assemblyName!, out var mod))
            {
                return $"{mod.DisplayName} ({assemblyName})";
            }

            return assemblyName ?? harmonyOwner ?? "Unknown";
        }

        public bool IsCommunityLibrary(string? modId, string? assemblyName)
        {
            if (!string.IsNullOrEmpty(modId) && CommunityLibraryPackageIds.Contains(modId!))
                return true;

            return string.Equals(assemblyName, "0Harmony", StringComparison.OrdinalIgnoreCase);
        }

        public bool IsOfficialTarget(MethodBase originalMethod)
        {
            var assemblyName = originalMethod.DeclaringType?.Assembly?.GetName()?.Name;
            if (string.IsNullOrEmpty(assemblyName))
                return false;

            return string.Equals(assemblyName, "Assembly-CSharp", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(assemblyName, "Verse", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(assemblyName, "RimWorld", StringComparison.OrdinalIgnoreCase);
        }

        public string GetLogDirectory()
        {
            var directory = Path.Combine(GenFilePaths.ConfigFolderPath, "HarmonyPatchScanner", "logs");
            Directory.CreateDirectory(directory);
            return directory;
        }

        public void Notify(string message, PatchScannerNotificationLevel level)
        {
            var messageType = level == PatchScannerNotificationLevel.Error
                ? MessageTypeDefOf.RejectInput
                : MessageTypeDefOf.TaskCompletion;

            Messages.Message(message, messageType, false);

            if (level == PatchScannerNotificationLevel.Error)
                Log.Error(message);
            else if (level == PatchScannerNotificationLevel.Warning)
                Log.Warning(message);
            else
                Log.Message(message);
        }

        private static List<ModLoadInfo> BuildLoadOrder()
        {
            var result = new List<ModLoadInfo>();
            var runningMods = LoadedModManager.RunningModsListForReading;

            for (var i = 0; i < runningMods.Count; i++)
            {
                var mod = runningMods[i];
                var packageId = GetPackageId(mod);
                var assemblies = mod.assemblies?.loadedAssemblies?
                    .Select(assembly => assembly.GetName().Name)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>();

                result.Add(new ModLoadInfo
                {
                    Position = i + 1,
                    ModId = packageId,
                    DisplayName = mod.Name ?? packageId,
                    IsOfficial = IsOfficialPackage(packageId, assemblies),
                    IsCommunityLibrary = CommunityLibraryPackageIds.Contains(packageId),
                    AssemblyNames = assemblies
                });
            }

            return result;
        }

        private static string GetPackageId(ModContentPack mod)
        {
            var type = mod.GetType();
            var property =
                type.GetProperty("PackageIdPlayerFacing", BindingFlags.Instance | BindingFlags.Public) ??
                type.GetProperty("PackageId", BindingFlags.Instance | BindingFlags.Public);

            var value = property?.GetValue(mod, null) as string;
            if (!string.IsNullOrWhiteSpace(value))
                return value!;

            var displayName = mod.Name;
            return string.IsNullOrWhiteSpace(displayName) ? "Unknown" : displayName!;
        }

        private static bool IsOfficialPackage(string packageId, IReadOnlyCollection<string> assemblies)
        {
            return packageId.StartsWith("ludeon.", StringComparison.OrdinalIgnoreCase) ||
                   assemblies.Contains("Assembly-CSharp", StringComparer.OrdinalIgnoreCase) ||
                   assemblies.Contains("Verse", StringComparer.OrdinalIgnoreCase) ||
                   assemblies.Contains("RimWorld", StringComparer.OrdinalIgnoreCase);
        }
    }
}
#endif
