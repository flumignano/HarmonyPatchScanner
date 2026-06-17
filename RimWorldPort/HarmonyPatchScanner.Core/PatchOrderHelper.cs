using System.Collections.Generic;
using System.Linq;

namespace HarmonyPatchScanner.Core
{
    public static class PatchOrderHelper
    {
        public static List<PatchRecord> GetPatchesInExecutionOrder(
            HarmonyPatchKind patchType,
            IEnumerable<PatchRecord> patches)
        {
            switch (patchType)
            {
                case HarmonyPatchKind.Prefix:
                case HarmonyPatchKind.Transpiler:
                    return patches.OrderByDescending(p => p.Priority).ThenBy(p => p.Index).ToList();

                case HarmonyPatchKind.Postfix:
                case HarmonyPatchKind.Finalizer:
                    return patches.OrderBy(p => p.Priority).ThenByDescending(p => p.Index).ToList();

                default:
                    return patches.OrderByDescending(p => p.Priority).ThenBy(p => p.Index).ToList();
            }
        }

        public static int GetTypeOrder(HarmonyPatchKind patchType)
        {
            switch (patchType)
            {
                case HarmonyPatchKind.Transpiler:
                    return 0;
                case HarmonyPatchKind.Prefix:
                    return 1;
                case HarmonyPatchKind.Finalizer:
                    return 2;
                case HarmonyPatchKind.Postfix:
                    return 3;
                default:
                    return 4;
            }
        }
    }
}
