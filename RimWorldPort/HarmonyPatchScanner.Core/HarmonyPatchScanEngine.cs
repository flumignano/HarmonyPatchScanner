using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace HarmonyPatchScanner.Core
{
    public sealed class HarmonyPatchScanEngine
    {
        private readonly IPatchScannerHost _host;

        public HarmonyPatchScanEngine(IPatchScannerHost host)
        {
            _host = host;
        }

        public PatchScanSnapshot Scan(PatchScannerOptions options)
        {
            var scanTime = DateTime.Now;
            var errors = new List<string>();
            var patches = new List<PatchRecord>();
            var loadOrder = SafeGetLoadOrder(errors);
            var allPatchedMethods = Harmony.GetAllPatchedMethods().ToList();

            foreach (var originalMethod in allPatchedMethods)
            {
                try
                {
                    if (ShouldExcludeMethod(originalMethod.Name, options))
                        continue;

                    var patchInfo = Harmony.GetPatchInfo(originalMethod);
                    if (patchInfo == null)
                        continue;

                    CollectPatches(patchInfo.Prefixes, HarmonyPatchKind.Prefix, originalMethod, options, patches);
                    CollectPatches(patchInfo.Postfixes, HarmonyPatchKind.Postfix, originalMethod, options, patches);
                    CollectPatches(patchInfo.Transpilers, HarmonyPatchKind.Transpiler, originalMethod, options, patches);
                    CollectPatches(patchInfo.Finalizers, HarmonyPatchKind.Finalizer, originalMethod, options, patches);
                }
                catch (Exception ex)
                {
                    errors.Add($"Could not process {MethodNameFormatter.GetFullMethodName(originalMethod)}: {ex.Message}");
                }
            }

            return new PatchScanSnapshot(scanTime, allPatchedMethods.Count, patches, loadOrder, errors);
        }

        private IReadOnlyList<ModLoadInfo> SafeGetLoadOrder(List<string> errors)
        {
            try
            {
                return _host.GetLoadOrder();
            }
            catch (Exception ex)
            {
                errors.Add($"Could not read load order: {ex.Message}");
                return Array.Empty<ModLoadInfo>();
            }
        }

        private void CollectPatches(
            IEnumerable<Patch> harmonyPatches,
            HarmonyPatchKind patchType,
            MethodBase originalMethod,
            PatchScannerOptions options,
            List<PatchRecord> output)
        {
            if (harmonyPatches == null)
                return;

            foreach (var patch in harmonyPatches)
            {
                try
                {
                    if (ShouldExcludePatch(patch, options))
                        continue;

                    output.Add(BuildPatchRecord(patch, patchType, originalMethod));
                }
                catch
                {
                    // Match the Bannerlord scanner: one malformed patch must not stop the report.
                }
            }
        }

        private bool ShouldExcludeMethod(string methodName, PatchScannerOptions options)
        {
            return options.ExcludeCommonLifecycleMethods &&
                   options.CommonLifecycleMethodNames.Contains(methodName);
        }

        private bool ShouldExcludePatch(Patch patch, PatchScannerOptions options)
        {
            var patchMethod = patch.PatchMethod;
            if (patchMethod != null &&
                options.ExcludeCommonLifecycleMethods &&
                options.CommonLifecycleMethodNames.Contains(patchMethod.Name))
            {
                return true;
            }

            if (!options.ExcludeCommunityLibraries)
                return false;

            var assemblyName = patchMethod?.DeclaringType?.Assembly?.GetName()?.Name;
            var modId = _host.GetModIdForAssembly(assemblyName);
            return _host.IsCommunityLibrary(modId, assemblyName);
        }

        private PatchRecord BuildPatchRecord(Patch patch, HarmonyPatchKind patchType, MethodBase originalMethod)
        {
            var patchMethod = patch.PatchMethod;
            var patchAssembly = patchMethod?.DeclaringType?.Assembly;
            var assemblyName = patchAssembly?.GetName()?.Name;
            var modId = _host.GetModIdForAssembly(assemblyName);

            return new PatchRecord
            {
                TargetMethod = MethodNameFormatter.GetFullMethodName(originalMethod),
                PatchType = patchType,
                Priority = patch.priority,
                Owner = _host.GetOwnerDisplayName(patchAssembly, patch.owner),
                OwnerModId = modId,
                PatchAssemblyName = assemblyName,
                HarmonyOwner = patch.owner ?? string.Empty,
                PatchMethod = MethodNameFormatter.GetFullPatchMethodName(patchMethod),
                Index = patch.index,
                Before = patch.before ?? Array.Empty<string>(),
                After = patch.after ?? Array.Empty<string>(),
                CanShortCircuit = patchType == HarmonyPatchKind.Prefix && patchMethod?.ReturnType == typeof(bool),
                LoadOrderPosition = _host.GetLoadOrderPositionForAssembly(assemblyName),
                TargetsOfficialCode = _host.IsOfficialTarget(originalMethod)
            };
        }
    }
}
