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
    public sealed class ParticipationTests
    {
        private static void Call(OpenPlayWorld w, string method, params object[] args) => GoalkeeperTests.Call(w, method, args);
        private static void Stop(OpenPlayWorld w) => Call(w, "AwardRestart", RestartKind.ThrowIn, true, new PitchPoint(0, 34));
        private static MatchFitnessRules Quiet(int windows = 3) => new MatchFitnessRules(injuryRatePerSecond: 0, substitutionWindows: windows, coachChanges: false);
        private static void Expose(OpenPlayWorld w, string id, double amount)
        {
            var exposure = (Dictionary<string, double>)typeof(OpenPlayWorld).GetField("injuryExposure", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(w);
            exposure[id] = amount;
        }
        private static void HalfTime(OpenPlayWorld w)
        {
            typeof(OpenPlayWorld).GetProperty("FixturePhase").SetValue(w, FixturePhase.HalfTime);
            Call(w, "ClearPlay"); typeof(OpenPlayWorld).GetProperty("Restart").SetValue(w, null);
        }
        [Test]
        public void NamedBenchCanBeControlledAndDoesNotReceiveOnPitchDecisions()
        {
            var c = PrototypeContent.CreateCareer(); var m = new MatchSession("bench", 42);
            using var w = new OpenPlayWorld(c, m, "harbour-player-17", true);
            w.StartFixture(fitnessRules: Quiet()); Stop(w);
            Assert.That(w.UserParticipation, Is.EqualTo(ParticipationState.Bench));
            Assert.That(w.Bench(true).Count, Is.EqualTo(7)); Assert.That(w.Players.Count, Is.EqualTo(22));
            Assert.That(w.HasPendingDecision, Is.False); Assert.That(w.CanSetIntent(OffBallIntent.Press), Is.False);
        }
        [Test]
        public void LiveRequestWaitsThenEntersAndGetsContextualChoices()
        {
            var m = new MatchSession("entry", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m, "harbour-player-17");
            w.StartFixture(fitnessRules: Quiet()); w.ReleaseRestart(w.Restart.Id, w.RestartRecipients()[0].Receiver.Id);
            w.RequestSubstitution("harbour-player-07", "harbour-player-17");
            Assert.That(w.UserParticipation, Is.EqualTo(ParticipationState.Bench)); Stop(w);
            Assert.That(w.UserParticipation, Is.EqualTo(ParticipationState.Playing)); Assert.That(w.PendingSetPiece, Is.Not.Null);
            Assert.That(w.CanParticipate("harbour-player-07"), Is.False);
            Assert.That(w.Participation("harbour-player-07"), Is.EqualTo(ParticipationState.Replaced));
            Assert.That(w.Players.Count, Is.EqualTo(22)); Assert.That(w.SubstitutionsUsed(true), Is.EqualTo(1));
            Assert.That(w.Players.Single(p => p.Player.Id == "harbour-player-17").PositionRoleId, Is.EqualTo("cm"));
        }
        [Test]
        public void FiveChangesAtOneStoppageUseOneWindowAndCannotExceedLimit()
        {
            var m = new MatchSession("limit", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture(fitnessRules: Quiet());
            for (int i = 0; i < 5; i++) w.RequestSubstitution("harbour-player-" + (2+i).ToString("00"), "harbour-player-" + (13+i).ToString("00"));
            Assert.That(w.SubstitutionsUsed(true), Is.EqualTo(5)); Assert.That(w.WindowsUsed(true), Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => w.RequestSubstitution("harbour-player-11", "harbour-player-18"));
            Assert.That(w.Players.Count, Is.EqualTo(22));
        }
        [Test]
        public void ThreeDistinctWindowsThenHalftimeStillAllowsRemainingChanges()
        {
            var m = new MatchSession("windows", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture(fitnessRules: Quiet());
            for (int i = 0; i < 3; i++) { Stop(w); w.RequestSubstitution("harbour-player-"+(2+i).ToString("00"), "harbour-player-"+(13+i).ToString("00")); }
            Stop(w); Assert.Throws<InvalidOperationException>(() => w.RequestSubstitution("harbour-player-05", "harbour-player-16"));
            HalfTime(w); w.RequestSubstitution("harbour-player-05", "harbour-player-16");
            Assert.That(w.SubstitutionsUsed(true), Is.EqualTo(4)); Assert.That(w.WindowsUsed(true), Is.EqualTo(3));
            w.ContinueSecondHalf(); Assert.That(w.Players.Count, Is.EqualTo(22));
        }
        [Test]
        public void TeamsHaveIndependentWindowsAndCannotUseEachOthersBench()
        {
            var m = new MatchSession("teams", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture(fitnessRules: Quiet(1));
            w.RequestSubstitution("harbour-player-07", "harbour-player-17"); Stop(w);
            w.RequestSubstitution("meadow-player-07", "meadow-player-17");
            Assert.That(w.WindowsUsed(true), Is.EqualTo(1)); Assert.That(w.WindowsUsed(false), Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => w.RequestSubstitution("harbour-player-08", "meadow-player-18"));
            Assert.Throws<ArgumentException>(() => w.RequestSubstitution("harbour-player-08", "harbour-player-07"));
        }
        [TestCase(true)] [TestCase(false)]
        public void InjuredKeeperIsReplacedOrTeamPlaysShortWithEmergencyKeeper(bool available)
        {
            var c = PrototypeContent.CreateCareer(); var m = new MatchSession("injury", 42); using var w = new OpenPlayWorld(c, m, "harbour-player-01");
            w.StartFixture(fitnessRules: new MatchFitnessRules(injuryRatePerSecond: 1, matchEndingChance: 1, substitutionWindows: available ? 3 : 0, coachChanges: false));
            Expose(w, "harbour-player-01", 100); Stop(w);
            Assert.That(w.UserParticipation, Is.EqualTo(ParticipationState.InjuredOut)); Assert.That(w.CanParticipate("harbour-player-01"), Is.False);
            Assert.That(c.Players["harbour-player-01"].InjuryDaysRemaining, Is.EqualTo(7));
            Assert.That(w.Players.Count, Is.EqualTo(available ? 22 : 21)); Assert.That(w.HasPendingDecision, Is.False);
            Assert.That(w.Players.Count(p => p.IsHome && p.IsGoalkeeper), Is.EqualTo(1));
            if (available) Assert.That(w.CanParticipate("harbour-player-12"), Is.True);
            var id = w.Restart.Id; w.ReleaseRestart(id, w.RestartRecipients()[0].Receiver.Id);
            for (int i = 0; i < 10; i++) m.Advance(TimeSpan.FromMilliseconds(50));
            Assert.That(m.Tick, Is.GreaterThan(0)); Assert.That(w.PendingKeeper, Is.Null);
        }
        [Test]
        public void KnockAddsFatigueButDoesNotRemovePlayerOrChangePersonality()
        {
            var c = PrototypeContent.CreateCareer(); var m = new MatchSession("knock", 42); using var w = new OpenPlayWorld(c, m);
            w.StartFixture(fitnessRules: new MatchFitnessRules(injuryRatePerSecond: 1, matchEndingChance: 0, coachChanges: false));
            var old = c.UserPlayer.Fatigue; Expose(w, c.UserPlayerId, 100); Stop(w);
            Assert.That(c.UserPlayer.Fatigue, Is.EqualTo(old + .1f).Within(.00001)); Assert.That(w.CanParticipate(c.UserPlayerId), Is.True);
            Assert.That(c.UserPlayer.InjuryDaysRemaining, Is.Zero); Assert.That(w.ParticipationEvents.Single(e => e.Kind == "Injury").Severity, Is.EqualTo(InjurySeverity.Knock));
        }
        [Test]
        public void InjuryWithEmptyBenchCannotRestoreMissingPlayer()
        {
            var d = PrototypeContent.Load(); d.fixture.benchSize = 1; d.fixture.substitutionLimit = 1;
            var m = new MatchSession("empty", 42); using var w = new OpenPlayWorld(CareerState.Create(d), m);
            w.StartFixture(fitnessRules: new MatchFitnessRules(injuryRatePerSecond: 1, matchEndingChance: 1));
            w.RequestSubstitution("harbour-player-01", "harbour-player-12");
            Assert.That(w.Bench(true).Count, Is.Zero); Expose(w, "harbour-player-08", 100); Stop(w);
            Assert.That(w.Players.Count, Is.EqualTo(21)); Assert.That(w.SubstitutionsUsed(true), Is.EqualTo(1));
        }
        [Test]
        public void RedCardsCannotBeSubstitutedAndPersistThroughHalves()
        {
            var m = new MatchSession("red", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m, "harbour-player-08"); w.StartFixture(fitnessRules: Quiet());
            var p = w.Players.Single(x => x.Player.Id == "harbour-player-08");
            var foul = FoulRules.Evaluate(new FoulIncident("red", p.Player.Id, null, p.Position, 1, FoulKind.Challenge, ContactSeverity.Excessive));
            Call(w, "ApplyCard", foul, CardKind.Red); Stop(w);
            Assert.That(w.UserParticipation, Is.EqualTo(ParticipationState.Dismissed));
            Assert.Throws<ArgumentException>(() => w.RequestSubstitution(p.Player.Id, "harbour-player-17"));
            w.RequestSubstitution("harbour-player-07", "harbour-player-17");
            Assert.That(w.Players.Count, Is.EqualTo(21)); HalfTime(w); w.ContinueSecondHalf(); Assert.That(w.CanParticipate(p.Player.Id), Is.False);
        }
        [Test]
        public void RemovingControlledTakerClearsOldDecisionAndAllowsSpectating()
        {
            var m = new MatchSession("watch", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m, "harbour-player-11"); w.StartFixture(fitnessRules: Quiet());
            string old = w.Restart.Id; w.RequestSubstitution("harbour-player-11", "harbour-player-18");
            Assert.That(w.UserParticipation, Is.EqualTo(ParticipationState.Replaced)); Assert.That(w.HasPendingDecision, Is.False);
            Assert.That(w.Restart.TakerId, Is.EqualTo("harbour-player-18")); Assert.That(w.Restart.Id, Is.Not.EqualTo(old));
            Assert.Throws<InvalidOperationException>(() => w.ReleaseRestart(old, w.RestartRecipients()[0].Receiver.Id));
            Assert.That(m.State, Is.EqualTo(MatchRunState.Running));
        }
        [Test]
        public void CoachUsesFatigueAndBringsBenchUserOnAtHalftime()
        {
            var c = PrototypeContent.CreateCareer(); var m = new MatchSession("coach", 42); using var w = new OpenPlayWorld(c, m, "harbour-player-17");
            w.StartFixture(fitnessRules: new MatchFitnessRules(injuryRatePerSecond: 0));
            c.SetPlayerCondition("harbour-player-02", .5f, .5f, .9f); Stop(w);
            Assert.That(w.Participation("harbour-player-02"), Is.EqualTo(ParticipationState.Replaced));
            HalfTime(w); Call(w, "ProcessRoster", true);
            Assert.That(w.UserParticipation, Is.EqualTo(ParticipationState.Playing));
            Assert.That(w.WindowsUsed(true), Is.EqualTo(1)); w.ContinueSecondHalf();
            Assert.That(w.Players.Single(p => p.Player.Id == "harbour-player-17").Direction, Is.EqualTo(-1));
        }
        [Test]
        public void FatigueOnlyAccumulatesOnPitchDuringLiveMatchTime()
        {
            var c = PrototypeContent.CreateCareer(); var m = new MatchSession("fatigue", 42); using var w = new OpenPlayWorld(c, m); w.StartFixture(fitnessRules: Quiet());
            float bench = c.Players["harbour-player-17"].Fatigue, original = c.UserPlayer.Fatigue;
            m.Pause(); m.Advance(TimeSpan.FromHours(1)); Assert.That(c.UserPlayer.Fatigue, Is.EqualTo(original)); m.Resume();
            w.ReleaseRestart(w.Restart.Id, w.RestartRecipients()[0].Receiver.Id);
            for (int i = 0; i < 20; i++) m.Advance(TimeSpan.FromMilliseconds(50));
            Assert.That(c.UserPlayer.Fatigue, Is.GreaterThan(original)); Assert.That(c.Players["harbour-player-17"].Fatigue, Is.EqualTo(bench));
        }
        [Test]
        public void FewerThanSevenAfterUnreplaceableInjuriesAbandonsMatch()
        {
            var m = new MatchSession("short", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m);
            w.StartFixture(fitnessRules: new MatchFitnessRules(injuryRatePerSecond: 1, matchEndingChance: 1, substitutionWindows: 0));
            foreach (var p in w.Players.Where(p => p.IsHome).Take(5)) Expose(w, p.Player.Id, 100);
            Stop(w); Assert.That(w.Abandoned, Is.True); Assert.That(w.FixturePhase, Is.EqualTo(FixturePhase.FullTime)); Assert.That(m.State, Is.EqualTo(MatchRunState.Paused));
        }
        [Test]
        public void CareerReadinessAndInjuryExclusionCarryIntoNextFixture()
        {
            var c = PrototypeContent.CreateCareer(); var m = new MatchSession("first", 42); using var w = new OpenPlayWorld(c, m);
            w.StartFixture(fitnessRules: new MatchFitnessRules(injuryRatePerSecond: 1, matchEndingChance: 1)); Expose(w, c.UserPlayerId, 100); Stop(w);
            using var next = new OpenPlayWorld(c, new MatchSession("next", 43), c.UserPlayerId);
            Assert.That(next.UserParticipation, Is.EqualTo(ParticipationState.InjuredOut)); Assert.That(next.Players.Count, Is.EqualTo(22));
            Assert.That(next.CanParticipate(c.UserPlayerId), Is.False);
        }
        [Test]
        public void NewSubstituteViewAppearsAndOutgoingViewDisappears()
        {
            var d = PrototypeContent.Load(); d.userPlayerId = "harbour-player-17";
            var root = new GameObject("Bench test");
            try {
                var v = root.AddComponent<PitchPreview>(); v.Initialize(CareerState.Create(d)); v.GetComponent<MatchDriver>().enabled = false; v.World.StartFixture(fitnessRules: Quiet());
                Assert.That(v.Players.Any(p => p.IsUser), Is.False); Assert.That(v.SquadViews.Count, Is.EqualTo(36));
                v.World.RequestSubstitution("harbour-player-07", "harbour-player-17"); v.SyncWorldView();
                Assert.That(v.Players.Count, Is.EqualTo(22)); Assert.That(v.Players.Single(p => p.IsUser).PlayerId, Is.EqualTo(d.userPlayerId));
                Assert.That(v.Players.Any(p => p.PlayerId == "harbour-player-07"), Is.False);
                v.SelectPlayer(d.userPlayerId); Assert.That(v.SelectedPlayer.Id, Is.EqualTo(d.userPlayerId));
            } finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test]
        public void FitnessReplayIsIndependentOfFrameChunkingAndDoesNotDrawActionRandomness()
        {
            var c1 = PrototypeContent.CreateCareer(); var c2 = PrototypeContent.CreateCareer();
            var m1 = new MatchSession("same", 19); var m2 = new MatchSession("same", 19);
            using var a = new OpenPlayWorld(c1, m1); using var b = new OpenPlayWorld(c2, m2);
            var rules = new MatchFitnessRules(injuryRatePerSecond: .2, matchEndingChance: .5, coachChanges: false);
            a.StartFixture(fitnessRules: rules); b.StartFixture(fitnessRules: rules);
            a.ReleaseRestart(a.Restart.Id, a.RestartRecipients()[0].Receiver.Id); b.ReleaseRestart(b.Restart.Id, b.RestartRecipients()[0].Receiver.Id);
            for (int i = 0; i < 100; i++) m1.Advance(TimeSpan.FromMilliseconds(50));
            for (int i = 0; i < 10; i++) m2.Advance(TimeSpan.FromMilliseconds(500));
            var draws = m1.DrawCount; Stop(a); Stop(b);
            Assert.That(m1.DrawCount, Is.EqualTo(draws)); Assert.That(m2.RandomState, Is.EqualTo(m1.RandomState));
            CollectionAssert.AreEqual(c1.Players.Values.Select(p => p.Fatigue), c2.Players.Values.Select(p => p.Fatigue));
            CollectionAssert.AreEqual(a.ParticipationEvents.Select(e => e.Kind + e.PlayerId + e.Severity), b.ParticipationEvents.Select(e => e.Kind + e.PlayerId + e.Severity));
        }
        [Test]
        public void TiredPlayerAccumulatesMoreInjuryRiskAndGoalkeeperLessExposure()
        {
            var c = PrototypeContent.CreateCareer(); var m = new MatchSession("risk", 42); using var w = new OpenPlayWorld(c, m);
            w.StartFixture(fitnessRules: new MatchFitnessRules(fatiguePerSecond: 0, injuryRatePerSecond: 0, coachChanges: false));
            c.SetPlayerCondition("harbour-player-04", .5f, .5f, 0); c.SetPlayerCondition("harbour-player-05", .5f, .5f, 1);
            c.SetPlayerCondition("harbour-player-01", .5f, .5f, 0); Call(w, "StepFitness", 60.0);
            var exposure = (Dictionary<string, double>)typeof(OpenPlayWorld).GetField("injuryExposure", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(w);
            Assert.That(exposure["harbour-player-05"], Is.EqualTo(exposure["harbour-player-04"] * 5));
            Assert.That(exposure["harbour-player-01"], Is.LessThan(exposure["harbour-player-04"]));
        }
        [Test]
        public void QueuedRequestsCannotReserveSameBenchPlayerTwice()
        {
            var m = new MatchSession("queue", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture(fitnessRules: Quiet());
            w.ReleaseRestart(w.Restart.Id, w.RestartRecipients()[0].Receiver.Id); w.RequestSubstitution("harbour-player-07", "harbour-player-17");
            Assert.Throws<ArgumentException>(() => w.RequestSubstitution("harbour-player-08", "harbour-player-17")); Stop(w);
            Assert.That(w.Players.Select(p => p.Player.Id).Distinct().Count(), Is.EqualTo(22)); Assert.That(w.SubstitutionsUsed(true), Is.EqualTo(1));
            typeof(OpenPlayWorld).GetProperty("FixturePhase").SetValue(w, FixturePhase.FullTime);
            Assert.Throws<InvalidOperationException>(() => w.RequestSubstitution("harbour-player-11", "harbour-player-18"));
        }
        [TestCase(double.NaN)] [TestCase(-1)] [TestCase(double.PositiveInfinity)]
        public void InvalidFitnessTuningIsRejected(double value) => Assert.Throws<ArgumentOutOfRangeException>(() => new MatchFitnessRules(injuryRatePerSecond: value));
    }
}
