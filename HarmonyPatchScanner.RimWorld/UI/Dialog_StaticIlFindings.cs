#if RIMWORLD
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyPatchScanner.Core;
using UnityEngine;
using Verse;

namespace HarmonyPatchScanner.RimWorld.UI
{
    internal sealed class Dialog_StaticIlFindings : Window
    {
        private readonly string detailsText;
        private Vector2 scrollPosition;

        public Dialog_StaticIlFindings(IReadOnlyList<StaticPatchFinding> findings)
        {
            detailsText = BuildDetailsText(findings);
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
        }

        public override Vector2 InitialSize
        {
            get { return new Vector2(920f, 680f); }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "Static IL Findings");
            Text.Font = GameFont.Small;

            var bodyRect = new Rect(inRect.x, inRect.y + 42f, inRect.width, inRect.height - 42f);
            Widgets.DrawMenuSection(bodyRect);

            var inner = bodyRect.ContractedBy(8f);
            var textWidth = inner.width - 16f;
            var textHeight = Text.CalcHeight(detailsText, textWidth) + 24f;
            var viewRect = new Rect(0f, 0f, textWidth, textHeight);

            Widgets.BeginScrollView(inner, ref scrollPosition, viewRect);
            Widgets.Label(new Rect(0f, 0f, textWidth, textHeight), detailsText);
            Widgets.EndScrollView();
        }

        private static string BuildDetailsText(IReadOnlyList<StaticPatchFinding> findings)
        {
            var builder = new StringBuilder();

            // Keep the explanation window read-only: the scan already happened, and this
            // text only explains passive findings attached to that completed snapshot.
            builder.AppendLine("These findings come from passive IL inspection only.");
            builder.AppendLine("The analyzer does not execute patch methods or replay transpilers.");
            builder.AppendLine();

            foreach (var confidenceGroup in findings
                         .OrderByDescending(f => f.Confidence)
                         .ThenBy(f => f.PatchOwner)
                         .ThenBy(f => f.TargetMethod)
                         .GroupBy(f => f.Confidence))
            {
                builder.AppendLine(confidenceGroup.Key + " findings (" + confidenceGroup.Count() + ")");
                builder.AppendLine("----------------------------------------------------");

                foreach (var finding in confidenceGroup)
                {
                    builder.AppendLine("Owner   : " + finding.PatchOwner);
                    builder.AppendLine("Kind    : " + FormatFindingKind(finding.Kind));
                    builder.AppendLine("Patch   : " + MethodNameFormatter.FormatMethodName(finding.PatchMethod, false));
                    builder.AppendLine("Target  : " + MethodNameFormatter.FormatMethodName(finding.TargetMethod, false));
                    builder.AppendLine("Why     : " + finding.Explanation);
                    builder.AppendLine();
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }

        private static string FormatFindingKind(StaticFindingKind kind)
        {
            switch (kind)
            {
                case StaticFindingKind.UnconditionalSkipOriginal:
                    return "Deterministic original skip";
                case StaticFindingKind.ResultWrite:
                    return "Likely result write";
                case StaticFindingKind.RefArgumentMutation:
                    return "Likely ref argument mutation";
                case StaticFindingKind.PrivateFieldAccess:
                    return "Private field access";
                case StaticFindingKind.UnreadableBody:
                    return "Unreadable patch body";
                case StaticFindingKind.UnsupportedPattern:
                    return "Unsupported IL pattern";
                default:
                    return kind.ToString();
            }
        }
    }
}
#endif
