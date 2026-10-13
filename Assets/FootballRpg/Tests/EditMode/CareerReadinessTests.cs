using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FootballRpg.Content;
using FootballRpg.Simulation;
using NUnit.Framework;

namespace FootballRpg.Tests
{
    public sealed class CareerReadinessTests
    {
        private CareerState career;
        [SetUp] public void Setup() { career = PrototypeContent.CreateCareer(); }
        private static void Apply(CareerReadiness r, string method, params object[] args) =>
            typeof(CareerReadiness).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(r, args);
        private static CalendarDecision Choose(CareerState c, CalendarChoiceKind kind, string id = null) => c.Calendar.Commit(c.Calendar.Preview(kind, id));
        private void Injury(string player = null, int days = 2) => Apply(career.Readiness, "Injury", "injury-match", player ?? career.UserPlayerId, "injury-1", days);
        private static double PassChance(CareerState c)
        {
            using var w = new OpenPlayWorld(c, new MatchSession("readiness-preview", 42), c.UserPlayerId, true);
            return w.PreviewPass("harbour-player-07", PassKind.ThroughBall).Chance;
        }
        [Test] public void RestedAndOverworkedSchedulesAffectSharedStateTrainingYieldAndMatchChances()
        {
            var rested = PrototypeContent.CreateCareer(); var tired = career;
            foreach (var c in new[] { rested, tired }) Choose(c, CalendarChoiceKind.Attend, "mon-training");
            Choose(rested, CalendarChoiceKind.Replace, "rest"); Choose(tired, CalendarChoiceKind.ExtraPractice, "extra-practice");
            Assert.That(tired.UserPlayer.Fatigue, Is.GreaterThan(rested.UserPlayer.Fatigue));
            Assert.That(tired.Readiness.TrainingMultiplier(tired.UserPlayerId), Is.LessThan(rested.Readiness.TrainingMultiplier(rested.UserPlayerId)));
            Assert.That(PassChance(tired), Is.LessThan(PassChance(rested)));
            Assert.That(tired.Readiness.Training[1].Multiplier, Is.LessThan(tired.Readiness.Training[0].Multiplier));
            Assert.That(tired.Readiness.Training[1].EffectiveSlots, Is.EqualTo(tired.Readiness.Training[1].Multiplier));
        }
        [Test] public void DuplicateConfirmationAndRepeatedPreviewCannotChangeReadiness()
        {
            var p = career.UserPlayer; var before = p.Fatigue;
            var choice = career.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training");
            for (int i = 0; i < 20; i++) { career.Calendar.Preview(CalendarChoiceKind.Replace, "rest"); career.Readiness.TrainingMultiplier(p.Id); }
            Assert.That(p.Fatigue, Is.EqualTo(before)); Assert.That(career.Readiness.Changes, Is.Empty);
            career.Calendar.Commit(choice); var fatigue = p.Fatigue; var count = career.Readiness.Changes.Count;
            career.Calendar.Commit(choice);
            Assert.That(p.Fatigue, Is.EqualTo(fatigue)); Assert.That(career.Readiness.Changes.Count, Is.EqualTo(count)); Assert.That(career.Readiness.Training.Count, Is.EqualTo(1));
        }
        [Test] public void LateTrainingScalesWorkloadAndEffectiveYieldToActualParticipation()
        {
            Choose(career, CalendarChoiceKind.Attend, "mon-training"); Choose(career, CalendarChoiceKind.Wait);
            Choose(career, CalendarChoiceKind.Skip, "mon-media"); Choose(career, CalendarChoiceKind.Replace, "party");
            var p = career.UserPlayer; double fatigue = p.Fatigue, multiplier = career.Readiness.TrainingMultiplier(p.Id), morale = p.Morale;
            var decision = Choose(career, CalendarChoiceKind.Attend, "tue-training");
            Assert.That(decision.Attendance.Single().LateMinutes, Is.EqualTo(60));
            var yield = career.Readiness.Training.Last();
            Assert.That(yield.Multiplier, Is.EqualTo(multiplier)); Assert.That(yield.ParticipatedSlots, Is.EqualTo(5d / 6));
            Assert.That(yield.EffectiveSlots, Is.EqualTo(multiplier * 5d / 6).Within(1e-7));
            Assert.That(p.Fatigue - fatigue, Is.EqualTo(.14 * 5 / 6).Within(1e-6));
            Assert.That(p.Morale, Is.LessThan(morale));
        }
        [Test] public void ReplacingRequiredTrainingWithRestDoesNotAwardTrainingAndKeepsBothReasons()
        {
            career.SetPlayerCondition(career.UserPlayerId, .5f, .5f, .8f);
            Choose(career, CalendarChoiceKind.Replace, "rest");
            Assert.That(career.Readiness.Training, Is.Empty);
            Assert.That(career.UserPlayer.Fatigue, Is.EqualTo(.58).Within(1e-6));
            Assert.That(career.Readiness.Changes.Count, Is.EqualTo(2));
            Assert.That(career.Readiness.Changes[1].Reason, Does.Contain("Skipped"));
        }
        [Test] public void TrainingWhileInjuredHasZeroYieldAndRecoveryRequiresAWholeElapsedDay()
        {
            Injury(); var before = career.UserPlayer.Fatigue;
            Choose(career, CalendarChoiceKind.Attend, "mon-training");
            Assert.That(career.Readiness.Training.Single().EffectiveSlots, Is.Zero); Assert.That(career.UserPlayer.Fatigue, Is.EqualTo(before));
            for (int i = 0; i < 2; i++) Choose(career, CalendarChoiceKind.Wait);
            Assert.That(career.UserPlayer.InjuryDaysRemaining, Is.EqualTo(2));
            Choose(career, CalendarChoiceKind.Wait);
            Assert.That(career.UserPlayer.InjuryDaysRemaining, Is.EqualTo(1));
            for (int i = 0; i < 4; i++) Choose(career, CalendarChoiceKind.Wait);
            Assert.That(career.UserPlayer.InjuryDaysRemaining, Is.Zero);
            Assert.That(career.Readiness.TrainingMultiplier(career.UserPlayerId), Is.GreaterThan(0));
            Assert.That(career.Readiness.Changes.Count(c => c.InjuryDaysDelta == -1), Is.EqualTo(2));
        }
        [Test] public void InjuriesPersistAcrossFixturesAndAllPlayersRecoverOnlyThroughCareerTime()
        {
            string id = "meadow-player-08"; Injury(id, 1);
            using (var w = new OpenPlayWorld(career, new MatchSession("injured", 1)))
                Assert.That(w.Participation(id), Is.EqualTo(ParticipationState.InjuredOut));
            for (int i = 0; i < 4; i++) Choose(career, CalendarChoiceKind.Wait);
            using var next = new OpenPlayWorld(career, new MatchSession("fit", 1));
            Assert.That(next.Participation(id), Is.EqualTo(ParticipationState.Playing));
        }
        [Test] public void InjuryAtLateDayStartsItsOwnFullDayRecoveryClock()
        {
            for (int i = 0; i < 3; i++) Choose(career, CalendarChoiceKind.Wait);
            Injury(days: 1); Choose(career, CalendarChoiceKind.Wait);
            Assert.That(career.UserPlayer.InjuryDaysRemaining, Is.EqualTo(1));
            for (int i = 0; i < 3; i++) Choose(career, CalendarChoiceKind.Wait);
            Assert.That(career.UserPlayer.InjuryDaysRemaining, Is.Zero);
        }
        [Test] public void MeaningfulMatchActionsChangeConfidenceOnceButNeverStableTraitsOrFrozenSnapshots()
        {
            var p = career.UserPlayer; var traits = p.PersonalityTraits.ToArray(); var snap = new PassActor(p, new PitchPoint(0, 0), new PitchPoint(1, 0));
            using var w = new OpenPlayWorld(career, new MatchSession("confidence", 42));
            var fact = new ActionResolved("shot-1", p.Id, 1, "shot", "goal", .5f, .1f, "on-target");
            var skills = new Dictionary<string, float> { ["finishing"] = 1 };
            GoalkeeperTests.Call(w, "Progress", fact, skills, 2d, (RestartKind?)null);
            Assert.That(p.Confidence, Is.EqualTo(snap.Confidence), "Practice does not apply match confidence.");
            w.StartFixture(); GoalkeeperTests.Call(w, "Progress", fact, skills, 2d, (RestartKind?)null);
            float after = p.Confidence; Assert.That(after, Is.GreaterThan(snap.Confidence));
            GoalkeeperTests.Call(w, "Progress", fact, skills, 2d, (RestartKind?)null);
            Assert.That(p.Confidence, Is.EqualTo(after)); Assert.That(p.PersonalityTraits.ToArray(), Is.EqualTo(traits));
            Assert.That(snap.Confidence, Is.LessThan(new PassActor(p, new PitchPoint(0, 0), new PitchPoint(1, 0)).Confidence));
        }
        [Test] public void ConfidenceHasPerMatchBudgetAndLowImpactActionsDoNotFarmIt()
        {
            float before = career.UserPlayer.Confidence;
            for (int i = 0; i < 100; i++)
            {
                var fact = new ActionResolved("shot" + i, career.UserPlayerId, i, "shot", "goal", .8f, .9f, "miss");
                Apply(career.Readiness, "ApplyPerformance", "match", fact, .5d);
            }
            Assert.That(career.UserPlayer.Confidence, Is.EqualTo(before));
            for (int i = 0; i < 100; i++)
            {
                var fact = new ActionResolved("shot" + i, career.UserPlayerId, i, "shot", "goal", .8f, .9f, "miss");
                Apply(career.Readiness, "ApplyPerformance", "match", fact, 2d);
            }
            Assert.That(before - career.UserPlayer.Confidence, Is.EqualTo(career.Readiness.Rules.MatchConfidenceBudget).Within(1e-6));
            Apply(career.Readiness, "ApplyPerformance", "another-match", new ActionResolved("shot0", career.UserPlayerId, 0, "shot", "goal", .8f, .9f, "miss"), 2d);
            Assert.That(before - career.UserPlayer.Confidence, Is.GreaterThan(career.Readiness.Rules.MatchConfidenceBudget));
        }
        [Test] public void MatchExertionIsSharedAndJournaledWithoutFatiguingTheBenchOrDuringPause()
        {
            var session = new MatchSession("effort", 42); using var w = new OpenPlayWorld(career, session, career.UserPlayerId);
            w.StartFixture(fitnessRules: new MatchFitnessRules(injuryRatePerSecond: 0, coachChanges: false));
            float before = career.UserPlayer.Fatigue, bench = career.Players["harbour-player-17"].Fatigue;
            GoalkeeperTests.Call(w, "StepFitness", 60d);
            Assert.That(career.UserPlayer.Fatigue, Is.GreaterThan(before)); Assert.That(career.Players["harbour-player-17"].Fatigue, Is.EqualTo(bench));
            Assert.That(career.Readiness.Changes.Any(c => c.PlayerId == career.UserPlayerId && c.Reason == "Match exertion"), Is.True);
            session.Pause(); float paused = career.UserPlayer.Fatigue; session.Advance(TimeSpan.FromMinutes(10));
            Assert.That(career.UserPlayer.Fatigue, Is.EqualTo(paused)); Assert.That(career.Calendar.Minute, Is.Zero);
        }
        [TestCase(1)] [TestCase(-1)] [TestCase(0)] public void CompletedMatchChangesParticipantMoraleOnce(int result)
        {
            using var w = new OpenPlayWorld(career, new MatchSession("result", 42)); w.StartFixture();
            GoalkeeperTests.Call(w, "StepFitness", 1d);
            typeof(OpenPlayWorld).GetProperty(result < 0 ? "AwayGoals" : "HomeGoals").SetValue(w, Math.Abs(result));
            float before = career.UserPlayer.Morale, bench = career.Players["harbour-player-17"].Morale;
            GoalkeeperTests.Call(w, "RecordFixture", "FullTime", (bool?)null, (RestartKind?)null);
            Assert.That(career.UserPlayer.Morale - before, Is.EqualTo(result * career.Readiness.Rules.ResultMorale).Within(1e-6));
            float after = career.UserPlayer.Morale;
            GoalkeeperTests.Call(w, "RecordFixture", "FullTime", (bool?)null, (RestartKind?)null);
            Assert.That(career.UserPlayer.Morale, Is.EqualTo(after)); Assert.That(career.Players["harbour-player-17"].Morale, Is.EqualTo(bench));
        }
        [Test] public void InjuriesUseExistingFitnessAssessmentAndDoNotMutatePersonality()
        {
            var traits = career.UserPlayer.PersonalityTraits.ToArray();
            using var w = new OpenPlayWorld(career, new MatchSession("injury-integration", 42));
            w.StartFixture(fitnessRules: new MatchFitnessRules(injuryRatePerSecond: 100, matchEndingChance: 1, coachChanges: false));
            GoalkeeperTests.Call(w, "StepFitness", 60d); GoalkeeperTests.Call(w, "ProcessRoster", true);
            Assert.That(career.UserPlayer.InjuryDaysRemaining, Is.GreaterThan(0));
            Assert.That(career.Readiness.Changes.Any(c => c.PlayerId == career.UserPlayerId && c.InjuryDaysDelta > 0), Is.True);
            Assert.That(career.UserPlayer.PersonalityTraits.ToArray(), Is.EqualTo(traits));
        }
        [Test] public void ActivityTuningIsCopiedAndRuleTuningChangesCalculations()
        {
            var d = PrototypeContent.Load(); d.calendar.activities.Single(a => a.id == "training").fatiguePerSlot = .5f;
            var c = CareerState.Create(d, readinessRules: new ReadinessRules(trainingFatiguePenalty: 1, trainingConfidenceWeight: 0, trainingMoraleWeight: 0));
            d.calendar.activities.Single(a => a.id == "training").fatiguePerSlot = 0;
            float before = c.UserPlayer.Fatigue; Choose(c, CalendarChoiceKind.Attend, "mon-training");
            Assert.That(c.UserPlayer.Fatigue - before, Is.EqualTo(.5).Within(1e-6));
            Assert.That(c.Readiness.TrainingMultiplier(c.UserPlayerId), Is.EqualTo(1 - c.UserPlayer.Fatigue).Within(1e-6));
        }
        [Test] public void ClosingAnEarlyMatchExplainsExertionOnceWithoutAwardingResultMorale()
        {
            var w = new OpenPlayWorld(career, new MatchSession("early-exit", 42)); w.StartFixture();
            GoalkeeperTests.Call(w, "StepFitness", 3d); float fatigue = career.UserPlayer.Fatigue, morale = career.UserPlayer.Morale;
            w.Dispose(); int count = career.Readiness.Changes.Count; w.Dispose();
            Assert.That(career.Readiness.Changes.Count, Is.EqualTo(count));
            Assert.That(career.Readiness.Changes.Single(c => c.PlayerId == career.UserPlayerId).Reason, Is.EqualTo("Match exertion"));
            Assert.That(career.UserPlayer.Fatigue, Is.EqualTo(fatigue)); Assert.That(career.UserPlayer.Morale, Is.EqualTo(morale));
        }
        [Test] public void BoundsClampAndReasonDeltasReflectActualChangeWithoutTouchingTraits()
        {
            var traits = career.UserPlayer.PersonalityTraits.ToArray();
            career.SetPlayerCondition(career.UserPlayerId, 1, 1, .02f);
            Choose(career, CalendarChoiceKind.Replace, "rest");
            Assert.That(career.UserPlayer.Fatigue, Is.Zero);
            Assert.That(career.Readiness.Changes[0].FatigueDelta, Is.EqualTo(-.02).Within(1e-6));
            Assert.That(career.Readiness.Changes[0].MoraleDelta, Is.Zero);
            Assert.That(career.UserPlayer.PersonalityTraits.ToArray(), Is.EqualTo(traits));
        }
        [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(-.1)] [TestCase(1.1)]
        public void InvalidRulesAreRejected(double value) => Assert.Throws<ArgumentOutOfRangeException>(() => new ReadinessRules(trainingFatiguePenalty: value));
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1.1f)] [TestCase(1.1f)]
        public void InvalidActivityRatesAreRejected(float value)
        {
            var d = PrototypeContent.Load(); d.calendar.activities[0].fatiguePerSlot = value;
            Assert.Throws<ArgumentException>(() => CareerState.Create(d));
        }
    }
}
