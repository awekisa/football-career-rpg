using System;
using System.Globalization;
using System.Linq;
using FootballRpg.Simulation;
using UnityEngine;

namespace FootballRpg.Presentation
{
    public sealed partial class LaunchScreen
    {
        private bool reportsOpen, weeklyReport;
        private int reportTab, reportReadyFrame;
        private string reportMetricId, reportEventId;
        private Vector2 reportListScroll, reportDetailScroll;
        private GUIStyle reportText;
        public CareerReport CurrentReport => weeklyReport ? Career?.Reports.Week : Career?.Reports.Match;
        public void OpenReports(bool week = false)
        {
            if (preview != null || practiceView != null || clubView != null) return;
            reportsOpen = true; weeklyReport = week; reportTab = 0; reportMetricId = reportEventId = null;
            reportListScroll = reportDetailScroll = Vector2.zero; reportReadyFrame = Time.frameCount + 1;
        }
        private void SetReportTab(int tab)
        { reportTab = tab; reportMetricId = reportEventId = null; reportListScroll = reportDetailScroll = Vector2.zero; }
        private void UpdateReports()
        {
            if (Time.frameCount <= reportReadyFrame) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { reportsOpen = false; return; }
            if (Input.GetKeyDown(KeyCode.Tab)) OpenReports(!weeklyReport);
            for (int i = 0; i < 4; i++) if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) SetReportTab(i);
        }
        private static string Signed(double n) => n.ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture);
        private static string Money(long n) => (n / 100m).ToString("0.00", CultureInfo.InvariantCulture);
        public static string ReportOverview(CareerReport r) => r == null ? "Reports will be available after the scheduled fixture finishes." :
            r.PlayerName + " · " + r.Role.ToUpperInvariant() + " · " + r.Score + (r.Abandoned ? " (abandoned)" : "") +
            "\n" + r.Selection + " · Final participation: " + r.Participation +
            $"\nXP gained {r.XpGained:0.##} · XP lost {r.XpLost:0.##} · Net {Signed(r.XpGained - r.XpLost)}";
        private string ReportDetails(CareerReport r)
        {
            if (r == null) return "Finish the career fixture, including both halves. Benched, injured and absent players also receive a report.";
            if (reportEventId != null)
            {
                var a = r.Actions.Single(x => x.EventId == reportEventId);
                var causes = r.Causes.Where(c => c.EventId == a.EventId || c.EventId.EndsWith(":" + a.EventId)).ToArray();
                return a.ActionId.Replace('-', ' ') + " · " + a.Variant + "\n" + (a.Succeeded ? "Successful check" : "Failed check") +
                    $" · Chance {a.Chance:P0}\nOutcome: {a.Outcome}\nTarget: {a.Target}\nElapsed {TimeSpan.FromSeconds(a.ElapsedSeconds):mm\\:ss}\n\n" +
                    (causes.Length == 0 ? "No career changes recorded for this action." : string.Join("\n\n", causes.Select(c =>
                        r.Metrics.Single(m => m.Id == c.MetricId).Label + ": " + Signed(c.Delta) + " · " + c.Explanation)));
            }
            if (reportMetricId != null)
            {
                var m = r.Metrics.Single(x => x.Id == reportMetricId);
                return m.Label + $"\n{m.Before:0.##} → {m.After:0.##} {m.Unit} · {Signed(m.Delta)}\n\n" +
                    (m.Causes.Count == 0 ? "No recorded changes in this period." : string.Join("\n\n", m.Causes.Select(c => Signed(c.Delta) + " " + m.Unit + " · " + c.Explanation)));
            }
            if (reportTab == 3) return "ACTION STATISTICS\nAttempts · successful · failed\n\n" + string.Join("\n", r.Statistics.Select(s =>
                s.ActionId.Replace('-', ' ') + $": {s.Attempts} · {s.Successful} · {s.Failed}")) +
                "\n\nThese count resolved checks, not personal goals or assists. Inspect an action for its actual outcome and career consequences.\n\n" +
                (r.ParticipationEvents.Count == 0 ? "No substitutions or injuries involving you." : string.Join("\n", r.ParticipationEvents));
            return "Select a row to see what caused its change. Both gains and losses are shown; zero can mean no change or offsetting consequences.\n\n" +
                (weeklyReport ? "Whole week: from joining the club through the final whistle. Includes training, attendance, conversations and the match." :
                    "Match only: from kickoff preparation through the final whistle. Calendar attendance consequences belong to the week report.") +
                "\n\nReports record changes already applied. Opening them never gives another reward.";
        }
        private void DrawReports()
        {
            if (reportText == null) reportText = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            var r = CurrentReport;
            GUI.Label(new Rect(50, 28, 920, 40), weeklyReport ? "WEEK COMPLETION" : "MATCH AFTERMATH", body);
            if (GUI.Button(new Rect(1030, 25, 355, 45), weeklyReport ? "MATCH REPORT [TAB]" : "WEEK REPORT [TAB]", button)) OpenReports(!weeklyReport);
            GUI.Label(new Rect(50, 85, 1310, 120), ReportOverview(r), reportText);
            string[] tabs = { "READINESS", "SKILLS", "RELATIONSHIPS", "ACTIONS / STATS" };
            for (int i = 0; i < tabs.Length; i++)
                if (GUI.Button(new Rect(50 + i * 335, 220, 320, 43), (reportTab == i ? "• " : "") + tabs[i] + " [" + (i + 1) + "]", button)) SetReportTab(i);
            var metrics = r?.Metrics.Where(m => reportTab == 0 ? !m.Id.Contains(":") : reportTab == 1 ? m.Id.StartsWith("xp:") || m.Id.StartsWith("level:") : m.Id.StartsWith("social:")).ToArray();
            int count = r == null ? 0 : reportTab == 3 ? r.Actions.Count : metrics.Length;
            reportListScroll = GUI.BeginScrollView(new Rect(50, 285, 740, 430), reportListScroll, new Rect(0, 0, 710, Math.Max(100, count * 64)));
            if (count == 0) GUI.Label(new Rect(0, 0, 700, 90), r == null ? "No completed fixture yet." : "No actions recorded for this player. Selection: " + r.Participation, reportText);
            for (int i = 0; i < count; i++)
            {
                if (reportTab == 3)
                {
                    var a = r.Actions[i];
                    if (GUI.Button(new Rect(0, i * 64, 704, 59), a.ActionId.Replace('-', ' ') + " · " + a.Variant + " · " + (a.Succeeded ? "SUCCESS" : "FAILED") + "\n" + a.Outcome + $" · {a.Chance:P0} chance", button))
                    { reportEventId = a.EventId; reportMetricId = null; reportDetailScroll = Vector2.zero; }
                }
                else
                {
                    var m = metrics[i];
                    if (GUI.Button(new Rect(0, i * 64, 704, 59), m.Label + $"\n{m.Before:0.##} → {m.After:0.##} {m.Unit} · {Signed(m.Delta)}", button))
                    { reportMetricId = m.Id; reportEventId = null; reportDetailScroll = Vector2.zero; }
                }
            }
            GUI.EndScrollView();
            string detail = ReportDetails(r); float height = Mathf.Max(430, reportText.CalcHeight(new GUIContent(detail), 535));
            reportDetailScroll = GUI.BeginScrollView(new Rect(820, 285, 565, 430), reportDetailScroll, new Rect(0, 0, 545, height));
            GUI.Label(new Rect(0, 0, 535, height), detail, reportText); GUI.EndScrollView();
            if (r != null) GUI.Label(new Rect(50, 735, 1310, 70), r.Currency + " balance " + Money(r.BalanceBefore) + " → " + Money(r.BalanceAfter) +
                " · Weekly wage " + Money(r.WageBefore) + " → " + Money(r.WageAfter) +
                "\nNo payments or spending occur in this prototype; the contract wage is not automatically credited.", reportText);
            if (GUI.Button(new Rect(50, 835, 350, 45), "BACK TO WEEK [ESC]", button)) reportsOpen = false;
            GUI.Label(new Rect(440, 835, 920, 50), "Use Save / Load to keep your career and reports between sessions.", small);
        }
        private void VerifyReports()
        {
            var original = Career;
            try
            {
                BeginCreation(); Balance(creationContent.footballAttributes, creationDraft.attributes, creationRules.footballBudget);
                Balance(creationContent.personalityTraits, creationDraft.traits, creationRules.personalityBudget); JoinCreatedCareer();
                while (Career.Week.Phase == CareerWeekPhase.ClubWeek)
                { PreviewCalendar(CalendarChoiceKind.Wait); ConfirmCalendar(); }
                Career.Week.StartFixture(new CompetitionRules(2, 0, .1)); ContinueWeek(); preview.GetComponent<MatchDriver>().enabled = false;
                for (int i = 0; i < 2000 && Career.Week.Phase != CareerWeekPhase.Complete; i++)
                { if (preview.World.FixturePhase == FixturePhase.HalfTime) preview.CommitChosenPass(); preview.Match.Advance(TimeSpan.FromMilliseconds(50)); }
                if (Career.Reports.Match == null || Career.Reports.Week == null) throw new InvalidOperationException("Reports missing at final whistle.");
                preview.Close(); var saved = CurrentReport;
                if (!reportsOpen || saved != Career.Reports.Match) throw new InvalidOperationException("Match aftermath did not open.");
                int changes = Career.Readiness.Changes.Count; long draws = Career.Week.Match.DrawCount;
                for (int i = 0; i < 10; i++) { OpenReports(true); _ = ReportOverview(CurrentReport); OpenReports(false); _ = ReportDetails(CurrentReport); }
                if (CurrentReport != saved || Career.Readiness.Changes.Count != changes || Career.Week.Match.DrawCount != draws) throw new InvalidOperationException("Opening reports mutated the career.");
                Career.Week.World.Dispose(); Debug.Log("FOOTBALL_RPG_REPORTS_PASSED");
            }
            finally { if (preview != null) preview.Close(); Career = original; reportsOpen = calendarOpen = false; creationDraft = null; }
        }
    }
}
