using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FootballRpg.Content;
using FootballRpg.Presentation;
using FootballRpg.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace FootballRpg.Tests
{
    public sealed class CareerReportsTests
    {
        private static CareerState Create(string role = "am")
        {
            var d = PrototypeContent.Load(); if (role == "gk") d.userPlayerId = "harbour-player-01";
            d.players.Single(p => p.id == d.userPlayerId).positionId = role;
            var c = CareerState.Create(d); c.Week.Begin(); return c;
        }
        private static void ToFixture(CareerState c, bool attend = true)
        {
            while (c.Calendar.Minute < 25 * 360) c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Wait));
            c.Calendar.Commit(c.Calendar.Preview(attend ? CalendarChoiceKind.Attend : CalendarChoiceKind.Skip, "sun-match"));
            c.Week.StartFixture(new CompetitionRules(3, 0, .1), new MatchFitnessRules(injuryRatePerSecond: 0));
        }
        private static void Finish(CareerState c)
        {
            var view = new GameObject("Report Fixture").AddComponent<PitchPreview>();
            try
            {
                view.Initialize(c, careerFixture: true); view.GetComponent<MatchDriver>().enabled = false; view.Match.Resume();
                for (int i = 0; i < 50000 && c.Week.Phase != CareerWeekPhase.Complete; i++)
                {
                    if (view.World.HasPendingDecision || view.UserRestart || view.World.RefereeNoticePending || view.World.OffsideNoticePending || view.World.FixturePhase == FixturePhase.HalfTime) view.CommitChosenPass();
                    view.Match.Advance(TimeSpan.FromMilliseconds(50));
                }
                Assert.That(c.Week.Phase, Is.EqualTo(CareerWeekPhase.Complete));
            }
            finally { view.Close(); c.Week.World.Dispose(); }
        }
        private static void AssertReconciled(CareerReport r)
        {
            foreach (var m in r.Metrics)
            {
                Assert.That(m.Causes.Sum(c => c.Delta), Is.EqualTo(m.Delta).Within(.00001), m.Label);
                Assert.That(m.Causes.All(c => !string.IsNullOrWhiteSpace(c.EventId) && !string.IsNullOrWhiteSpace(c.Explanation)), Is.True);
            }
            Assert.That(r.XpGained - r.XpLost, Is.EqualTo(r.Metrics.Where(m => m.Id.StartsWith("xp:")).Sum(m => m.Delta)).Within(.00001));
        }
        [TestCaseSource(typeof(PositionIds), nameof(PositionIds.All))]
        public void EveryPositionRecordsItsOwnActionsAndReconcilesReports(string role)
        {
            var c = Create(role); ToFixture(c); Finish(c);
            Assert.That(c.Reports.Match.Role, Is.EqualTo(role));
            var diagnostics = c.Week.Match.Diagnostics.Where(d => d.Result.PlayerId == c.UserPlayerId).ToArray();
            Assert.That(c.Reports.Match.Actions.Select(a => a.EventId), Is.EqualTo(diagnostics.Select(d => d.Result.EventId)));
            Assert.That(c.Reports.Match.Statistics.Sum(s => s.Attempts), Is.EqualTo(diagnostics.Length));
            Assert.That(c.Reports.Match.Statistics.Sum(s => s.Successful), Is.EqualTo(diagnostics.Count(d => d.Result.Succeeded)));
            AssertReconciled(c.Reports.Match); AssertReconciled(c.Reports.Week);
        }
        [Test] public void WeeklyTrainingConversationAndAttendanceAreExcludedFromMatchScope()
        {
            var c = Create(); var startXp = c.Progression.ForPlayer(c.UserPlayerId).ToDictionary(s => s.Key, s => s.Value.TotalXp);
            c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training"));
            c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.ExtraPractice, "extra-practice"));
            while (c.Calendar.Minute < 6 * 360) c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Wait));
            c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Attend, "tue-coach")); c.Meetings.Commit(c.Meetings.Preview("role-plan"));
            double training = c.Training.Results.Sum(r => r.TotalXp); ToFixture(c); Finish(c);
            var match = c.Reports.Match; var week = c.Reports.Week;
            Assert.That(week.XpGained - match.XpGained, Is.EqualTo(training).Within(.00001));
            foreach (var skill in startXp) Assert.That(week.Metrics.Single(m => m.Id == "xp:" + skill.Key).Delta,
                Is.EqualTo(c.Progression.ForPlayer(c.UserPlayerId)[skill.Key].TotalXp - skill.Value).Within(.00001));
            Assert.That(week.Causes.Any(x => x.Explanation.Contains("training slot")), Is.True);
            Assert.That(match.Causes.Any(x => x.Explanation.Contains("training slot")), Is.False);
            Assert.That(week.Causes.Any(x => x.Explanation.Contains("meeting")), Is.True);
            Assert.That(week.Causes.Any(x => x.Explanation.Contains("Skipped")), Is.True);
            AssertReconciled(match); AssertReconciled(week);
        }
        [TestCase("save", "gk", "handling")] [TestCase("distribute", "gk", "distribution")] [TestCase("shoot", "st", "finishing")]
        public void SuccessfulAndFailedChecksRecordExactXpAndLevelCauses(string actionId, string role, string skill)
        {
            var c = Create(role); ToFixture(c);
            string sourceMatch = c.Progression.BeginMatch();
            // Controlled resolver facts make gains, losses, outcomes and level drops independently checkable.
            foreach (bool success in new[] { false, true })
            {
                var d = c.Week.Match.ResolveAutomatic(new ActionRequest("report-" + success, c.UserPlayerId, actionId, "harbour-player-07",
                    success ? 1 : 0, "completed", "missed", new Dictionary<string, string> { ["variant"] = "report-fixture" }));
                var applied = c.Progression.Apply(sourceMatch, d.Result, new Dictionary<string, float> { [skill] = 1 });
                Assert.That(c.Progression.Apply(sourceMatch, d.Result, new Dictionary<string, float> { [skill] = 1 }), Is.SameAs(applied));
            }
            Finish(c); var r = c.Reports.Match;
            var source = c.Progression.Changes.Where(x => x.PlayerId == c.UserPlayerId).ToArray();
            Assert.That(r.XpGained, Is.EqualTo(source.SelectMany(x => x.Deltas.Values).Where(x => x > 0).Sum()).Within(.00001));
            Assert.That(r.XpLost, Is.EqualTo(-source.SelectMany(x => x.Deltas.Values).Where(x => x < 0).Sum()).Within(.00001));
            Assert.That(r.Causes.Any(cause => cause.MetricId == "level:" + skill && cause.Delta < 0), Is.True);
            Assert.That(r.Actions.Count(a => a.Variant == "report-fixture"), Is.EqualTo(2));
            Assert.That(r.Actions.Where(a => a.Variant == "report-fixture").Select(a => a.Outcome), Is.EqualTo(new[] { "missed", "completed" }));
            if (role == "gk") Assert.That(r.Statistics.Select(s => s.ActionId), Does.Contain("save").And.Contain("claim").And.Contain("distribute"));
            AssertReconciled(r); AssertReconciled(c.Reports.Week);
        }
        [TestCase("gk")] [TestCase("am")]
        public void RealFootballResolversFeedKeeperAndOutfieldStatistics(string role)
        {
            var c = Create(role);
            while (c.Week.Phase == CareerWeekPhase.ClubWeek)
            {
                var a = c.Calendar.CurrentCommitments.FirstOrDefault();
                c.Calendar.Commit(c.Calendar.Preview(a == null ? CalendarChoiceKind.Replace : CalendarChoiceKind.Attend, a?.Id ?? "rest"));
                if (c.Meetings.Pending != null) c.Meetings.Commit(c.Meetings.Preview(c.Meetings.Pending.Options[0].Id));
            }
            c.Week.StartFixture(new CompetitionRules(600, 0, .1), new MatchFitnessRules(injuryRatePerSecond: 0)); Finish(c);
            var r = c.Reports.Match;
            Assert.That(r.Actions.Count, Is.GreaterThan(0));
            Assert.That(r.Statistics.Sum(s => s.Attempts), Is.EqualTo(r.Actions.Count));
            if (role == "gk") Assert.That(r.Actions.Any(a => a.ActionId == "save" || a.ActionId == "claim" || a.ActionId == "distribute"), Is.True);
            AssertReconciled(r); AssertReconciled(c.Reports.Week);
        }
        [Test] public void AbsentPlayerHasExplicitZeroStatisticsAndUnchangedFinances()
        {
            var c = Create("gk"); ToFixture(c, false); Finish(c); var r = c.Reports.Match;
            Assert.That(r.Participation, Is.EqualTo(ParticipationState.NotSelected)); Assert.That(r.Actions, Is.Empty);
            Assert.That(r.Statistics.All(s => s.Attempts == 0 && s.Successful == 0 && s.Failed == 0), Is.True);
            Assert.That(r.XpGained, Is.Zero); Assert.That(r.XpLost, Is.Zero);
            Assert.That(r.BalanceAfter, Is.EqualTo(r.BalanceBefore)); Assert.That(r.WageAfter, Is.EqualTo(r.WageBefore));
            Assert.That(c.Reports.Week.Causes.Any(x => x.Explanation.Contains("Skipped match")), Is.True);
        }
        [Test] public void ReportReadsAndReopeningCannotMutateOrRecaptureFinalState()
        {
            var c = Create(); ToFixture(c); Finish(c); var report = c.Reports.Week;
            var deltas = report.Metrics.Select(m => m.Delta).ToArray(); var player = c.UserPlayer;
            int changes = c.Readiness.Changes.Count, xp = c.Progression.Changes.Count; long draws = c.Week.Match.DrawCount;
            for (int i = 0; i < 100; i++)
            { _ = LaunchScreen.ReportOverview(c.Reports.Match); _ = LaunchScreen.ReportOverview(c.Reports.Week); c.Week.StartFixture(); }
            Assert.That(c.Readiness.Changes.Count, Is.EqualTo(changes)); Assert.That(c.Progression.Changes.Count, Is.EqualTo(xp)); Assert.That(c.Week.Match.DrawCount, Is.EqualTo(draws));
            c.SetPlayerCondition(player.Id, 0, 0, 1);
            Assert.That(c.Reports.Week, Is.SameAs(report)); Assert.That(report.Metrics.Select(m => m.Delta), Is.EqualTo(deltas));
            Assert.Throws<NotSupportedException>(() => ((IList<ReportMetric>)report.Metrics).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<ReportCause>)report.Causes).Clear());
        }
        [Test] public void NoReportIsInventedBeforeCompletionAndScreensReopenWithoutRewards()
        {
            var c = Create(); Assert.That(c.Reports.Match, Is.Null); Assert.That(c.Reports.Week, Is.Null);
            Assert.That(LaunchScreen.ReportOverview(null), Does.Contain("after"));
            ToFixture(c); Assert.That(c.Reports.Match, Is.Null);
            Finish(c);
            var screen = new GameObject("Reports Launch").AddComponent<LaunchScreen>();
            try { typeof(LaunchScreen).GetMethod("VerifyReports", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(screen, null); }
            finally { UnityEngine.Object.DestroyImmediate(screen.gameObject); }
        }
    }
}
