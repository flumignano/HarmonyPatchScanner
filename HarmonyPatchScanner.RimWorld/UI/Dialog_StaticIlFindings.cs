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

        public Dialog_StaticIlFindings(IReadOnlyList<StaticPatchFinding> findings, int totalFindingCount)
        {
            detailsText = BuildDetailsText(findings, totalFindingCount);
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
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "HPS_StaticFindingsTitle".Translate());
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

        private static string BuildDetailsText(IReadOnlyList<StaticPatchFinding> findings, int totalFindingCount)
        {
            var builder = new StringBuilder();

            // Keep the explanation window read-only: the scan already happened, and this
            // text only explains actionable passive findings attached to that snapshot.
            builder.AppendLine("HPS_StaticFindingsPassiveOnly".Translate());
            builder.AppendLine("HPS_StaticFindingsNoExecution".Translate());
            builder.AppendLine("HPS_StaticFindingsShownConfidence".Translate());
            builder.AppendLine("HPS_StaticFindingsPotentialInReports".Translate());
            builder.AppendLine("HPS_StaticFindingsShownCount".Translate(findings.Count, totalFindingCount));
            builder.AppendLine();

            foreach (var confidenceGroup in findings
                         .OrderByDescending(f => f.Confidence)
                         .ThenBy(f => f.PatchOwner)
                         .ThenBy(f => f.TargetMethod)
                         .GroupBy(f => f.Confidence))
            {
                builder.AppendLine("HPS_StaticFindingsGroup".Translate(
                    TranslateConfidence(confidenceGroup.Key),
                    confidenceGroup.Count()));
                builder.AppendLine("----------------------------------------------------");

                foreach (var finding in confidenceGroup)
                {
                    builder.AppendLine("HPS_StaticFindingsOwner".Translate(finding.PatchOwner));
                    builder.AppendLine("HPS_StaticFindingsKind".Translate(FormatFindingKind(finding.Kind)));
                    builder.AppendLine("HPS_StaticFindingsPatch".Translate(MethodNameFormatter.FormatMethodName(finding.PatchMethod, false)));
                    builder.AppendLine("HPS_StaticFindingsTarget".Translate(MethodNameFormatter.FormatMethodName(finding.TargetMethod, false)));
                    builder.AppendLine("HPS_StaticFindingsWhy".Translate(finding.Explanation));
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
                    return "HPS_StaticKindUnconditionalSkip".Translate();
                case StaticFindingKind.ResultWrite:
                    return "HPS_StaticKindResultWrite".Translate();
                case StaticFindingKind.RefArgumentMutation:
                    return "HPS_StaticKindRefArgumentMutation".Translate();
                case StaticFindingKind.PrivateFieldAccess:
                    return "HPS_StaticKindPrivateFieldAccess".Translate();
                case StaticFindingKind.UnreadableBody:
                    return "HPS_StaticKindUnreadableBody".Translate();
                case StaticFindingKind.UnsupportedPattern:
                    return "HPS_StaticKindUnsupportedPattern".Translate();
                default:
                    return kind.ToString();
            }
        }

        private static string TranslateConfidence(StaticFindingConfidence confidence)
        {
            switch (confidence)
            {
                case StaticFindingConfidence.Deterministic:
                    return "HPS_ConfidenceDeterministic".Translate();
                case StaticFindingConfidence.Observed:
                    return "HPS_ConfidenceObserved".Translate();
                case StaticFindingConfidence.Likely:
                    return "HPS_ConfidenceLikely".Translate();
                default:
                    return "HPS_ConfidencePotential".Translate();
            }
        }
    }
}
#endif
