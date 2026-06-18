using System;
using System.Reflection;
using HarmonyLib;

namespace HarmonyPatchScanner.Core
{
    public static class MethodNameFormatter
    {
        public static string GetFullMethodName(MethodBase method)
        {
            return $"{method.DeclaringType?.FullName ?? "Unknown"}.{method.Name}";
        }

        public static string GetFullPatchMethodName(MethodInfo? method)
        {
            return $"{method?.DeclaringType?.FullName ?? "Unknown"}.{method?.Name ?? "Unknown"}";
        }

        public static string FormatMethodName(string fullName, bool verbose)
        {
            if (verbose || string.IsNullOrEmpty(fullName))
                return fullName;

            var parts = fullName.Split('.');
            return parts.Length >= 2
                ? $"{parts[parts.Length - 2]}.{parts[parts.Length - 1]}"
                : fullName;
        }

        public static string FormatPriority(int priority)
        {
            string name;
            if (priority == Priority.First) name = "First";
            else if (priority == Priority.VeryHigh) name = "VeryHigh";
            else if (priority == Priority.High) name = "High";
            else if (priority == Priority.HigherThanNormal) name = "HigherThanNormal";
            else if (priority == Priority.Normal) name = "Normal";
            else if (priority == Priority.LowerThanNormal) name = "LowerThanNormal";
            else if (priority == Priority.Low) name = "Low";
            else if (priority == Priority.VeryLow) name = "VeryLow";
            else if (priority == Priority.Last) name = "Last";
            else name = "Custom";

            return $"{priority} ({name})";
        }

        public static string FormatIndex(int index)
        {
            return $"#{index + 1} (index {index})";
        }

        public static string FormatLoadOrder(int? loadOrderPosition)
        {
            return loadOrderPosition.HasValue
                ? $"Load order position #{loadOrderPosition.Value}"
                : "Load order position unknown";
        }

        public static string SanitizeFileName(string name)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var chars = name.ToCharArray();

            for (var i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0)
                    chars[i] = '_';
            }

            return new string(chars);
        }
    }
}
