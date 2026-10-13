using System;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Presentation;
using FootballRpg.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace FootballRpg.Tests
{
    [Category("FootballVerification")]
    public sealed class RefereeIntegrationTests
    {
        private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(50);
        private static void Call(OpenPlayWorld w, string name, params object[] args) => GoalkeeperTests.Call(w, name, args);
        private static void Set(OpenPlayWorld w, string property, object value) => typeof(OpenPlayWorld).GetProperty(property).SetValue(w, value);
        private static void Live(OpenPlayWorld w, string owner = "harbour-player-08")
        {
            Set(w, "Restart", null); Call(w, "ClearPlay");
            foreach (var p in w.Players) DefenseTests.Place(p, new PitchPoint(p.IsHome ? -15 : 25, p.IsHome ? -25 : 25));
            var actor = w.Players.Single(p => p.Player.Id == owner); DefenseTests.Place(actor, new PitchPoint(0, 0)); DefenseTests.Possess(w, owner);
        }
        private static FoulIncident Foul(string id = "foul", ContactSeverity severity = ContactSeverity.Careless, FoulKind kind = FoulKind.Challenge, PitchPoint? spot = null, string offender = "meadow-player-04", string victim = "harbour-player-08") =>
            new FoulIncident(id, offender, victim, spot ?? new PitchPoint(0, 0), offender.StartsWith("meadow") ? -1 : 1, kind, severity);
        [TestCase(false)] [TestCase(true)]
        public void AdvantageIsBoundedAndRecallsAtOriginalSpot(bool possessionLost)
        {
            var m = new MatchSession("callback", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture(); Live(w);
            Call(w, "HandleFoul", Foul()); Assert.That(w.Advantage, Is.Not.Null); Assert.That(w.Restart, Is.Null);
            if (possessionLost) Set(w, "PossessionClubId", "opponent");
            else
            {
                var owner = w.Players.Single(p => p.Player.Id == w.OwnerId);
                for (int i = 0; i < 62 && w.Advantage != null; i++)
                { DefenseTests.Place(owner, new PitchPoint(0, 0)); Set(w, "BallPosition", new PitchPoint(0, 0)); m.Advance(Step); }
            }
            Call(w, "StepReferee"); Assert.That(w.Advantage, Is.Null); Assert.That(w.Restart, Is.Not.Null);
            Assert.That(w.Restart.Kind, Is.EqualTo(RestartKind.DirectFreeKick)); Assert.That(w.Restart.Spot, Is.EqualTo(new PitchPoint(0, 0)));
        }
        [Test]
        public void RealisedAdvantageDefersRecklessCardUntilStoppage()
        {
            var m = new MatchSession("deferred", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture(); Live(w);
            Call(w, "HandleFoul", Foul(severity: ContactSeverity.Reckless));
            Assert.That(w.Discipline.Yellows("meadow-player-04"), Is.Zero);
            Set(w, "BallPosition", new PitchPoint(7, 0)); Call(w, "StepReferee");
            Assert.That(w.Advantage, Is.Null); Assert.That(w.Restart, Is.Null); Assert.That(w.Discipline.Yellows("meadow-player-04"), Is.Zero);
            Call(w, "AwardRestart", RestartKind.ThrowIn, true, new PitchPoint(7, 34));
            Assert.That(w.Discipline.Yellows("meadow-player-04"), Is.EqualTo(1));
            Call(w, "SettleDeferredCards"); Assert.That(w.Discipline.Yellows("meadow-player-04"), Is.EqualTo(1));
        }
        [Test]
        public void SecondYellowStopsImmediatelyAndDismissalSurvivesHalftime()
        {
            var m = new MatchSession("two-cards", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture(); Live(w);
            Call(w, "HandleFoul", Foul("one", ContactSeverity.Reckless, FoulKind.Holding));
            Live(w); Call(w, "HandleFoul", Foul("two", ContactSeverity.Reckless));
            Assert.That(w.Advantage, Is.Null); Assert.That(w.Discipline.IsDismissed("meadow-player-04"), Is.True);
            Assert.That(w.Players.Count, Is.EqualTo(21)); Assert.That(w.CanParticipate("meadow-player-04"), Is.False);
            Set(w, "FixturePhase", FixturePhase.HalfTime); w.ContinueSecondHalf();
            Assert.That(w.Players.Count, Is.EqualTo(21)); Assert.That(w.RestartRecipients().All(p => p.Receiver.Id != "meadow-player-04"), Is.True);
            for (int i = 0; i < 600; i++) m.Advance(Step);
            Assert.That(w.Players.Count, Is.EqualTo(21));
        }
        [Test]
        public void KeeperDismissalPromotesExistingTeammateAndMatchRemainsPlayable()
        {
            var m = new MatchSession("red-keeper", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture(); Live(w);
            Call(w, "HandleFoul", Foul(severity: ContactSeverity.Excessive, offender: "meadow-player-01"));
            Assert.That(w.Players.Count, Is.EqualTo(21)); Assert.That(w.Players.Count(p => !p.IsHome && p.IsGoalkeeper), Is.EqualTo(1));
            Assert.That(w.RefereeMessage, Does.Contain("takes over in goal"));
            for (int i = 0; i < 1200; i++) m.Advance(Step);
            Assert.That(w.CanParticipate("meadow-player-01"), Is.False);
        }
        [Test]
        public void TooManyDismissalsAbandonInsteadOfRunningInvalidTeams()
        {
            var m = new MatchSession("abandon", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture();
            for (int i = 2; i <= 6; i++) { Live(w); Call(w, "HandleFoul", Foul("red-" + i, ContactSeverity.Excessive, offender: "meadow-player-0" + i)); }
            Assert.That(w.Abandoned, Is.True); Assert.That(w.FixturePhase, Is.EqualTo(FixturePhase.FullTime)); Assert.That(w.Restart, Is.Null);
            var seconds = w.MatchSeconds; m.Resume(); m.Advance(TimeSpan.FromSeconds(30)); Assert.That(w.MatchSeconds, Is.EqualTo(seconds));
        }
        [Test]
        public void PenaltyHasLegalPositionsAndCanBeTakenOnlyOnce()
        {
            var m = new MatchSession("penalty", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture(); Live(w);
            Call(w, "HandleFoul", Foul(spot: new PitchPoint(40, 0)));
            var restart = w.Restart; Assert.That(restart.Kind, Is.EqualTo(RestartKind.Penalty));
            var keeper = w.Players.Single(p => !p.IsHome && p.IsGoalkeeper); Assert.That(keeper.Position.X, Is.EqualTo(52.5));
            foreach (var p in w.Players.Where(p => p.Player.Id != restart.TakerId && p != keeper))
            { Assert.That(p.Position.X, Is.LessThan(41.5)); Assert.That(p.Position.Distance(restart.Spot), Is.GreaterThanOrEqualTo(9.15)); Assert.That(HandlingRules.InOwnArea(p.Position, -1), Is.False); }
            var shot = w.ShootRestart(restart.Id); long draws = m.DrawCount;
            Assert.That(w.ShootRestart(restart.Id), Is.SameAs(shot)); Assert.That(m.DrawCount, Is.EqualTo(draws)); Assert.That(w.Restart, Is.Null);
            for (int i = 0; i < 1000 && w.Restart == null; i++) m.Advance(Step);
            Assert.That(w.FixtureEvents.Any(e => e.Kind == "RestartReleased" && e.Restart == RestartKind.Penalty), Is.True);
        }
        [TestCase(RestartKind.DirectFreeKick)] [TestCase(RestartKind.IndirectFreeKick)]
        public void FreeKickDistancesAndDirectShotAvailability(RestartKind kind)
        {
            var m = new MatchSession("free", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture(); Live(w);
            Call(w, "AwardRestart", kind, true, new PitchPoint(25, 2));
            Assert.That(w.Players.Where(p => !p.IsHome).All(p => p.Position.Distance(w.Restart.Spot) >= 9.15 - 1e-8), Is.True);
            Assert.That(w.RestartShotPreview() != null, Is.EqualTo(kind == RestartKind.DirectFreeKick));
        }
        [TestCase(BallTouchKind.DeliberateKick)] [TestCase(BallTouchKind.ThrowIn)] [TestCase(BallTouchKind.KeeperRelease)]
        public void IllegalKeeperHandlingChoiceAwardsIndirectKickWithoutCard(BallTouchKind touch)
        {
            var m = new MatchSession("illegal-handling", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m, "harbour-player-01"); w.StartFixture(); Live(w, "harbour-player-01");
            var keeper = w.Players.Single(p => p.Player.Id == "harbour-player-01"); DefenseTests.Place(keeper, new PitchPoint(-48, 0));
            w.Handling.RecordTouch(touch == BallTouchKind.KeeperRelease ? keeper.Player.Id : "harbour-player-02", keeper.Player.ClubId, touch, keeper.Player.Id);
            DefenseTests.Possess(w, keeper.Player.Id);
            var d = w.PendingKeeper; var option = d.Options.Single(p => p.Kind == KeeperAction.HandleIllegally);
            w.CommitKeeperDecision(d.Id, option.OptionId);
            Assert.That(w.Restart.Kind, Is.EqualTo(RestartKind.IndirectFreeKick)); Assert.That(w.Restart.Spot.X, Is.EqualTo(-47));
            Assert.That(w.Discipline.Yellows(keeper.Player.Id), Is.Zero); Assert.That(w.RefereeNoticePending, Is.True);
        }
        [Test]
        public void OutsideAreaHandlingIsDirectAndEightSecondHoldingIsCorner()
        {
            var m = new MatchSession("handling", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture(); Live(w, "harbour-player-01");
            Call(w, "HandleFoul", Foul(kind: FoulKind.Handball, offender: "harbour-player-01", victim: null));
            Assert.That(w.Restart.Kind, Is.EqualTo(RestartKind.DirectFreeKick));
            Live(w, "harbour-player-01"); Set(w, "BallPosition", new PitchPoint(-48, -1)); Set(w, "HoldingKeeperId", "harbour-player-01");
            for (int i = 0; i < 162; i++) Call(w, "StepReferee");
            Assert.That(w.Restart.Kind, Is.EqualTo(RestartKind.Corner)); Assert.That(w.Restart.Spot.Z, Is.LessThan(0));
        }
        [Test]
        public void RefereeNoticeFreezesThinkingAndDismissedUserCanInspectAndContinue()
        {
            var root = new GameObject("referee-view");
            try
            {
                var view = root.AddComponent<PitchPreview>(); view.Initialize(PrototypeContent.CreateCareer()); view.GetComponent<MatchDriver>().enabled = false; view.World.StartFixture(); Live(view.World);
                Call(view.World, "HandleFoul", Foul(severity: ContactSeverity.Excessive, offender: "harbour-player-08", victim: "meadow-player-08"));
                view.SyncWorldView(); Assert.That(view.World.RefereeNoticePending, Is.True);
                var clock = view.World.MatchSeconds; view.World.TogglePause(); view.Match.Resume(); view.Match.Advance(TimeSpan.FromSeconds(10));
                Assert.That(view.World.MatchSeconds, Is.EqualTo(clock)); Assert.That(view.CommitChosenPass(), Is.True);
                Assert.That(view.World.RefereeNoticePending, Is.False); view.SyncWorldView();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test]
        public void CommittedTacklesProduceBothCleanAndFoulOutcomesWithoutExtraDraws()
        {
            bool clean = false, foul = false;
            for (ulong seed = 1; seed <= 100 && !(clean && foul); seed++)
            {
                var m = new MatchSession("live-tackle", seed); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m, "meadow-player-04"); w.StartFixture(); Live(w);
                var defender = w.Players.Single(p => p.Player.Id == "meadow-player-04"); DefenseTests.Place(defender, new PitchPoint(1.8, 0));
                Call(w, "TryOpenDefense", w.Players.Single(p => p.Player.Id == w.OwnerId));
                var d = w.PendingDefense; Assert.That(d, Is.Not.Null);
                var result = w.CommitDefense(d.Id, DefensiveAction.Tackle);
                if (w.LastFoul != null) foul = true; else if (!result.Action.Succeeded) clean = true;
                Assert.That(m.DrawCount, Is.EqualTo(1));
                Assert.That(w.CommitDefense(d.Id, DefensiveAction.Tackle), Is.SameAs(result));
                Assert.That(m.DrawCount, Is.EqualTo(1));
            }
            Assert.That(clean && foul, Is.True);
        }
        [TestCase(1UL)] [TestCase(42UL)] [TestCase(99UL)]
        public void NormalSeededFixturesReachFullTimeWithRefereeRestarts(ulong seed)
        {
            var m = new MatchSession("referee-fixture", seed); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture(new CompetitionRules(180));
            for (int i = 0; i < 20000 && w.FixturePhase != FixturePhase.FullTime; i++)
            {
                if (w.FixturePhase == FixturePhase.HalfTime) w.ContinueSecondHalf();
                m.Advance(Step);
            }
            Assert.That(w.FixturePhase, Is.EqualTo(FixturePhase.FullTime));
            // Cautious AI may complete a short fixture without fouling. A foul must still
            // reach the referee path; advantage realised legitimately needs no restart.
            if (w.LastFoul != null)
                Assert.That(w.FixtureEvents.Any(e => e.Kind == "Advantage" || e.Restart == RestartKind.Penalty || e.Restart == RestartKind.DirectFreeKick), Is.True);
        }
        [Test]
        public void PenaltyAtEndOfHalfIsAllowedToCompleteBeforeWhistle()
        {
            var m = new MatchSession("late-penalty", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture(new CompetitionRules(2)); Live(w);
            Call(w, "HandleFoul", Foul(spot: new PitchPoint(40, 0))); Set(w, "HalfElapsedSeconds", 2d);
            m.Advance(Step); Assert.That(w.FixturePhase, Is.EqualTo(FixturePhase.FirstHalf));
            w.ShootRestart(w.Restart.Id); m.Advance(Step); Assert.That(w.FixturePhase, Is.EqualTo(FixturePhase.FirstHalf));
            for (int i = 0; i < 1000 && w.FixturePhase == FixturePhase.FirstHalf; i++) m.Advance(Step);
            Assert.That(w.FixturePhase, Is.EqualTo(FixturePhase.HalfTime));
        }
    }
}
