using System.Collections.Generic;
using System.Reflection;

namespace HarmonyPatchScanner.Core
{
    public interface IPatchScannerHost
    {
        IReadOnlyList<ModLoadInfo> GetLoadOrder();

        string? GetModIdForAssembly(string? assemblyName);

        int? GetLoadOrderPositionForAssembly(string? assemblyName);

        string GetOwnerDisplayName(Assembly? patchAssembly, string? harmonyOwner);

        bool IsCommunityLibrary(string? modId, string? assemblyName);

        bool IsOfficialTarget(MethodBase originalMethod);

        string GetLogDirectory();

        void Notify(string message, PatchScannerNotificationLevel level);
    }

    public enum PatchScannerNotificationLevel
    {
        Info,
        Success,
        Warning,
        Error
    }
}
