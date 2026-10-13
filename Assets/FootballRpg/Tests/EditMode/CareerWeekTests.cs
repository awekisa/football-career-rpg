using System;
using System.Linq;
using System.Reflection;
using FootballRpg.Content;
using FootballRpg.Presentation;
using FootballRpg.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace FootballRpg.Tests
{
    [Category("FootballVerification")]
    public sealed class CareerWeekTests
    {
        private static CareerState Create(string role = "cm")
        {
            var content = PrototypeContent.Load(); var draft = CharacterCreation.NewDraft(content); draft.positionId = role; draft.name = "Career Prospect";
            var rules = JsonUtility.FromJson<CreationRules>(Resources.Load<TextAsset>("CharacterCreation").text);
            Spend(draft.attributes, rules.footballBudget); Spend(draft.traits, rules.personalityBudget);
            var c = CharacterCreation.CreateCareer(content, draft, rules); c.Week.Begin(); return c;
        }
        private static void Spend(ValueOverride[] values, int budget)
        { while (budget > 0) foreach (var v in values) if (budget > 0 && v.value < 100) { v.value++; budget--; } }
        private static void Commit(CareerState c, CalendarChoiceKind kind, string id = null)
        {
            c.Calendar.Commit(c.Calendar.Preview(kind, id));
            if (c.Meetings.Pending != null) c.Meetings.Commit(c.Meetings.Preview(c.Meetings.Pending.Options[0].Id));
        }
        private static void Until(CareerState c, int slot, bool skipTraining = false)
        {
            while (c.Calendar.Minute < slot * c.Calendar.MinutesPerSlot)
            {
                var a = c.Calendar.CurrentCommitments.FirstOrDefault();
                Commit(c, a == null ? CalendarChoiceKind.Replace : skipTraining && a.Activity.Training ? CalendarChoiceKind.Skip : CalendarChoiceKind.Attend, a?.Id ?? "rest");
            }
        }
        private static void Finish(CareerState c)
        {
            var view = new GameObject("Week Test").AddComponent<PitchPreview>();
            try
            {
                view.Initialize(c, careerFixture: true, careerRules: new CompetitionRules(3, 0, .1));
                view.GetComponent<MatchDriver>().enabled = false; view.Match.Resume();
                for (int i = 0; i < 20000 && c.Week.Phase != CareerWeekPhase.Complete; i++)
                {
                    if (view.World.HasPendingDecision || view.UserRestart || view.World.RefereeNoticePending || view.World.OffsideNoticePending || view.World.FixturePhase == FixturePhase.HalfTime) view.CommitChosenPass();
                    view.Match.Advance(TimeSpan.FromMilliseconds(50));
                }
                Assert.That(c.Week.Phase, Is.EqualTo(CareerWeekPhase.Complete));
                Assert.That(c.Week.World.FixturePhase, Is.EqualTo(FixturePhase.FullTime));
                Assert.That(c.Coach.MatchActive, Is.False);
            }
            finally { view.Close(); c.Week.World?.Dispose(); }
        }
        [TestCase("cm")] [TestCase("gk")] [TestCase("st")] [TestCase("cb")]
        public void CreatedPlayerFollowsDisciplinedWeekThroughFullTime(string role)
        {
            var c = Create(role); var p = c.UserPlayer;
            Assert.That(c.Meetings.Meetings.Count, Is.EqualTo(3));
            Until(c, 25); Assert.That(c.Training.Results.Count, Is.EqualTo(5)); Assert.That(c.Meetings.Results.Count, Is.EqualTo(3));
            Assert.That(c.Week.SelectionAnnouncement.Outcome, Is.EqualTo(SelectionOutcome.Starter));
            Commit(c, CalendarChoiceKind.Attend, "sun-match");
            int minute = c.Calendar.Minute; float fatigue = p.Fatigue; var levels = p.FootballAttributes.ToArray();
            var w = c.Week.StartFixture(new CompetitionRules(3, 0, .1), new MatchFitnessRules(injuryRatePerSecond: 0));
            Assert.That(w.UserParticipation, Is.EqualTo(ParticipationState.Playing)); Assert.That(w.Players.Count, Is.EqualTo(22));
            var motion = w.Players.Single(m => m.Player.Id == p.Id);
            Assert.That(motion.Player, Is.SameAs(p)); Assert.That(motion.PositionRoleId, Is.EqualTo(role));
            Assert.That(p.Fatigue, Is.EqualTo(fatigue)); Assert.That(p.FootballAttributes.ToArray(), Is.EqualTo(levels));
            Assert.That(w.CoachEnabled, Is.True); Assert.That(w.CoachMessage, Does.Contain(role.ToUpperInvariant()));
            Finish(c); Assert.That(c.Calendar.Minute, Is.EqualTo(minute));
            Assert.Throws<InvalidOperationException>(() => c.Calendar.Preview(CalendarChoiceKind.Wait));
            Assert.That(c.Week.StartFixture(), Is.SameAs(w));
        }
        [Test] public void ExtraPracticeAndSkippedTrainingRouteHasDifferentConsequencesAndStillCompletes()
        {
            var good = Create(); var risk = Create(); Until(good, 25);
            Commit(risk, CalendarChoiceKind.Attend, "mon-training"); Commit(risk, CalendarChoiceKind.ExtraPractice, "extra-practice");
            Until(risk, 25, skipTraining: true);
            Assert.That(risk.Training.Results.Count, Is.EqualTo(2)); Assert.That(good.Training.Results.Count, Is.EqualTo(5));
            Assert.That(risk.Calendar.Attendance.Count(a => a.Fact.Attendance == AttendanceOutcome.Skipped), Is.GreaterThan(0));
            Assert.That(risk.Coach.Relationship.ProfessionalTrust, Is.LessThan(good.Coach.Relationship.ProfessionalTrust));
            Assert.That(risk.Progression.ForPlayer(risk.UserPlayerId).Values.Sum(x => x.TotalXp), Is.Not.EqualTo(good.Progression.ForPlayer(good.UserPlayerId).Values.Sum(x => x.TotalXp)));
            Commit(risk, CalendarChoiceKind.Attend, "sun-match"); Commit(good, CalendarChoiceKind.Attend, "sun-match");
            risk.Week.StartFixture(new CompetitionRules(120, 0, .1), new MatchFitnessRules(injuryRatePerSecond: 0));
            good.Week.StartFixture(new CompetitionRules(120, 0, .1), new MatchFitnessRules(injuryRatePerSecond: 0));
            Finish(risk); Finish(good);
        }
        [TestCase(false)] [TestCase(true)]
        public void MissingFixtureDirectlyOrByAnotherActivityWatchesWithoutEntering(bool replace)
        {
            var c = Create(); Until(c, 25);
            if (replace) { Commit(c, CalendarChoiceKind.Replace, "rest"); Commit(c, CalendarChoiceKind.Replace, "rest"); }
            else Commit(c, CalendarChoiceKind.Skip, "sun-match");
            Assert.That(c.Week.FixtureAttendance, Is.EqualTo(AttendanceOutcome.Skipped));
            var w = c.Week.StartFixture(new CompetitionRules(3, 0, .1));
            Assert.That(w.CoachSelection.Outcome, Is.EqualTo(SelectionOutcome.NotSelected)); Assert.That(w.UserParticipation, Is.EqualTo(ParticipationState.NotSelected));
            Assert.That(w.Players.Count, Is.EqualTo(22)); Assert.That(w.Players.Any(p => p.Player.Id == c.UserPlayerId), Is.False);
            Finish(c); Assert.That(w.UserParticipation, Is.EqualTo(ParticipationState.NotSelected)); Assert.That(w.DecisionCount, Is.Zero);
        }
        [Test] public void PartyDelayShowsConflictAndStartsLatePlayerOnBench()
        {
            var c = Create(); Until(c, 24);
            var party = c.Calendar.Preview(CalendarChoiceKind.Replace, "party");
            Assert.That(party.Impacts.Any(i => i.Commitment.Id == "sun-match" && i.Outcome == AttendanceOutcome.Late), Is.True);
            c.Calendar.Commit(party); Commit(c, CalendarChoiceKind.Attend, "sun-match");
            Assert.That(c.Calendar.ResultFor("sun-match").LateMinutes, Is.EqualTo(60));
            var w = c.Week.StartFixture(new CompetitionRules(3, 0, .1)); Assert.That(w.CoachSelection.Outcome, Is.EqualTo(SelectionOutcome.Substitute));
            Assert.That(w.UserParticipation, Is.EqualTo(ParticipationState.Bench)); Finish(c);
        }
        [TestCase(false)] [TestCase(true)]
        public void ReadinessAtKickoffOverridesEarlierStartingAnnouncement(bool injured)
        {
            var c = Create(); Until(c, 25); Assert.That(c.Week.SelectionAnnouncement.Outcome, Is.EqualTo(SelectionOutcome.Starter));
            if (injured) typeof(PlayerState).GetProperty("InjuryDaysRemaining").SetValue(c.UserPlayer, 20);
            else c.SetPlayerCondition(c.UserPlayerId, .3f, .4f, .95f);
            Commit(c, CalendarChoiceKind.Attend, "sun-match");
            var w = c.Week.StartFixture(new CompetitionRules(3, 0, .1));
            Assert.That(w.CoachSelection.Outcome, Is.EqualTo(injured ? SelectionOutcome.NotSelected : SelectionOutcome.Substitute));
            Assert.That(w.UserParticipation, Is.EqualTo(injured ? ParticipationState.InjuredOut : ParticipationState.Bench)); Finish(c);
        }
        [Test] public void PreviewQueriesAndRepeatedStartDoNotConsumeTimeOrReplayFixture()
        {
            var c = Create(); Assert.Throws<InvalidOperationException>(() => c.Week.StartFixture());
            for (int i = 0; i < 10; i++) { _ = c.Week.Preparation; c.Week.Begin(); }
            Assert.That(c.Calendar.Minute, Is.Zero); Until(c, 25);
            var choice = c.Calendar.Preview(CalendarChoiceKind.Attend, "sun-match");
            Assert.That(c.Week.Phase, Is.EqualTo(CareerWeekPhase.ClubWeek)); c.Calendar.Commit(choice);
            int count = c.Calendar.Attendance.Count; c.Calendar.Commit(choice); Assert.That(c.Calendar.Attendance.Count, Is.EqualTo(count));
            Assert.Throws<InvalidOperationException>(() => c.Calendar.Preview(CalendarChoiceKind.Replace, "rest"));
            Assert.That(c.Coach.RequestUnavailable, Is.Not.Null); Assert.That(c.Social.UnavailableReason(c.Coach.Profile.Id), Is.Not.Null);
            var w = c.Week.StartFixture(new CompetitionRules(3, 0, .1)); c.Week.Match.Advance(TimeSpan.FromMilliseconds(50));
            long tick = c.Week.Match.Tick, draws = c.Week.Match.DrawCount;
            Assert.That(c.Week.StartFixture(), Is.SameAs(w)); Assert.That(c.Week.Match.Tick, Is.EqualTo(tick)); Assert.That(c.Week.Match.DrawCount, Is.EqualTo(draws));
            Finish(c);
        }
        [Test] public void ReturningToPitchKeepsWorldClockAndDecision()
        {
            var c = Create(); Until(c, 25); Commit(c, CalendarChoiceKind.Attend, "sun-match");
            var first = new GameObject("First View").AddComponent<PitchPreview>(); first.Initialize(c, careerFixture: true, careerRules: new CompetitionRules(3, 0, .1));
            first.GetComponent<MatchDriver>().enabled = false; var w = first.World; var match = first.Match;
            match.Advance(TimeSpan.FromMilliseconds(50)); long tick = match.Tick; first.Close();
            Assert.That(match.State, Is.EqualTo(MatchRunState.Paused));
            var second = new GameObject("Second View").AddComponent<PitchPreview>(); second.Initialize(c, careerFixture: true); second.GetComponent<MatchDriver>().enabled = false;
            Assert.That(second.World, Is.SameAs(w)); Assert.That(second.Match, Is.SameAs(match)); Assert.That(match.Tick, Is.EqualTo(tick));
            second.Close(); Finish(c);
        }
        [Test] public void EndingGuidedPracticeAndLeavingMeetingStillReachesFixture()
        {
            var c = Create();
            c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training", practice: PracticeFamily.Passing));
            Assert.Throws<InvalidOperationException>(() => c.Calendar.Preview(CalendarChoiceKind.Wait));
            var d = c.Training.ActivePractice.Decision; c.Training.ChoosePractice(d.Id, d.Options[0].Id); c.Training.FinishPractice();
            Until(c, 6); c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Attend, "tue-coach")); c.Meetings.Leave();
            Until(c, 25); Commit(c, CalendarChoiceKind.Attend, "sun-match"); Finish(c);
        }
        [Test] public void PresentationRouteJoinsClubHandsOffMatchAndCompletes()
        {
            var screen = new GameObject("Week Launch").AddComponent<LaunchScreen>();
            try { typeof(LaunchScreen).GetMethod("VerifyCareerWeek", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(screen, null); }
            finally { UnityEngine.Object.DestroyImmediate(screen.gameObject); }
        }
    }
}
