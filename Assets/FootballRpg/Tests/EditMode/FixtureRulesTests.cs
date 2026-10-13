using System;
using System.Linq;
using System.Reflection;
using FootballRpg.Content;
using FootballRpg.Simulation;
using FootballRpg.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace FootballRpg.Tests
{
    [Category("FootballVerification")]
    public sealed class FixtureRulesTests
    {
        private static void Call(OpenPlayWorld w, string name, params object[] args) => GoalkeeperTests.Call(w, name, args);
        private static void Advance(MatchSession m, double seconds)
        { for (int i = 0; i < seconds * 20; i++) m.Advance(TimeSpan.FromMilliseconds(50)); }
        private static void Boundary(OpenPlayWorld w, int end, double z, bool lastHome, double height = .3)
        {
            var last = w.Players.First(p => p.IsHome == lastHome);
            w.Handling.RecordTouch(last.Player.Id, last.Player.ClubId, BallTouchKind.Other);
            var cross = PitchBoundary.Cross(new PitchPoint(50 * end, z), height, new PitchPoint(54 * end, z), height);
            Call(w, "ApplyBoundary", cross.Value);
        }
        [TestCase(1)] [TestCase(-1)]
        public void WholeBallMustCrossAndFitBetweenPostsAndUnderBar(int end)
        {
            Assert.That(PitchBoundary.Cross(new PitchPoint(50 * end, 0), .3, new PitchPoint(52.6 * end, 0), .3), Is.Null);
            Assert.That(PitchBoundary.Cross(new PitchPoint(50 * end, 0), .3, new PitchPoint(52.61 * end, 0), .3), Is.Null);
            var goal = PitchBoundary.Cross(new PitchPoint(50 * end, 0), .3, new PitchPoint(53 * end, 0), .3);
            Assert.That(goal.Value.IsGoal, Is.True);
            Assert.That(PitchBoundary.Cross(new PitchPoint(50 * end, 3.56), .3, new PitchPoint(53 * end, 3.56), .3).Value.IsGoal, Is.False);
            Assert.That(PitchBoundary.Cross(new PitchPoint(50 * end, 0), 2.34, new PitchPoint(53 * end, 0), 2.34).Value.IsGoal, Is.False);
        }
        [Test]
        public void SweptCrossingUsesFirstLineAndInterpolatedHeight()
        {
            var crossing = PitchBoundary.Cross(new PitchPoint(49, 33), 1, new PitchPoint(60, 40), 4).Value;
            Assert.That(crossing.GoalLine, Is.False);
            Assert.That(crossing.Point.Z, Is.EqualTo(34.11).Within(1e-9));
            Assert.That(crossing.Height, Is.InRange(1.4, 1.6));
        }
        [TestCase(true, 1)] [TestCase(false, 1)] [TestCase(true, -1)] [TestCase(false, -1)]
        public void BoundaryRestartsUseLastTouchAndGoalEnd(bool lastHome, int end)
        {
            var m = new MatchSession("boundary", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m);
            w.StartFixture(); Boundary(w, end, 10, lastHome);
            bool defendingHome = end < 0;
            Assert.That(w.Restart.Kind, Is.EqualTo(lastHome == defendingHome ? RestartKind.Corner : RestartKind.GoalKick));
            Assert.That(w.Restart.IsHome, Is.EqualTo(!lastHome));
            Assert.That(w.HomeGoals + w.AwayGoals, Is.Zero);
            AssertLegal(w);
        }
        [TestCase(true, 1)] [TestCase(false, 1)] [TestCase(true, -1)] [TestCase(false, -1)]
        public void ThrowInUsesCrossingSpotAndOppositeTeam(bool lastHome, int side)
        {
            var m = new MatchSession("throw", 1); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture();
            var last = w.Players.First(p => p.IsHome == lastHome); w.Handling.RecordTouch(last.Player.Id, last.Player.ClubId, BallTouchKind.Other);
            Call(w, "ApplyBoundary", PitchBoundary.Cross(new PitchPoint(0, side * 33), .3, new PitchPoint(0, side * 35), .3).Value);
            Assert.That(w.Restart.Kind, Is.EqualTo(RestartKind.ThrowIn)); Assert.That(w.Restart.IsHome, Is.EqualTo(!lastHome));
            Assert.That(w.Restart.Spot, Is.EqualTo(new PitchPoint(0, side * 34))); AssertLegal(w);
        }
        [TestCase(1)] [TestCase(-1)]
        public void GoalsRestartForConcedingSideAndSecondHalfReversesOwnership(int end)
        {
            var m = new MatchSession("goals", 1); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture(new CompetitionRules(.1, 0, .05));
            Boundary(w, end, 0, end > 0);
            Assert.That(w.HomeGoals, Is.EqualTo(end > 0 ? 1 : 0)); Assert.That(w.AwayGoals, Is.EqualTo(end < 0 ? 1 : 0));
            Assert.That(w.Restart.Kind, Is.EqualTo(RestartKind.Kickoff)); Assert.That(w.Restart.IsHome, Is.EqualTo(end < 0)); AssertLegal(w);
            Advance(m, 1); Assert.That(w.FixturePhase, Is.EqualTo(FixturePhase.HalfTime)); w.ContinueSecondHalf();
            Boundary(w, end, 0, end < 0);
            Assert.That(w.HomeGoals, Is.EqualTo(1)); Assert.That(w.AwayGoals, Is.EqualTo(1));
            Assert.That(w.Restart.IsHome, Is.EqualTo(end > 0));
        }
        [TestCase(true)] [TestCase(false)]
        public void TwoHalvesAddedTimePauseAndFullTimeAreExactlyOnce(bool homeStarts)
        {
            var m = new MatchSession("clock", 1); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m);
            w.StartFixture(new CompetitionRules(1, .5, .2, homeStarts));
            Assert.That(w.Restart.IsHome, Is.EqualTo(homeStarts));
            w.TogglePause(); Advance(m, 10); Assert.That(w.HalfElapsedSeconds, Is.Zero); w.TogglePause();
            Advance(m, 1.5); Assert.That(w.FixturePhase, Is.EqualTo(FixturePhase.FirstHalf)); Assert.That(w.AnnouncedAddedSeconds, Is.GreaterThanOrEqualTo(.5 - 1e-8));
            Advance(m, 1); Assert.That(w.FixturePhase, Is.EqualTo(FixturePhase.HalfTime));
            var home = w.Players.First(); var direction = home.Direction; var anchor = home.Anchor;
            w.ContinueSecondHalf(); w.ContinueSecondHalf();
            Assert.That(home.Direction, Is.EqualTo(-direction)); Assert.That(home.Anchor.X, Is.EqualTo(-anchor.X));
            Assert.That(w.Restart.IsHome, Is.EqualTo(!homeStarts)); Assert.That(w.HalfElapsedSeconds, Is.Zero);
            Advance(m, 3); Assert.That(w.FixturePhase, Is.EqualTo(FixturePhase.FullTime));
            var facts = w.FixtureEvents.Count; double time = w.MatchSeconds; w.TogglePause(); m.Resume(); Advance(m, 10);
            Assert.That(w.MatchSeconds, Is.EqualTo(time)); Assert.That(w.FixtureEvents.Count, Is.EqualTo(facts));
            Assert.That(w.FixtureEvents.Count(e => e.Kind == "FullTime"), Is.EqualTo(1)); Assert.That(w.Rules.AllowsDraw, Is.True);
            Assert.That(w.HomeGoals, Is.EqualTo(w.AwayGoals));
        }
        [TestCase(RestartKind.Kickoff, true)] [TestCase(RestartKind.Kickoff, false)]
        [TestCase(RestartKind.ThrowIn, true)] [TestCase(RestartKind.ThrowIn, false)]
        [TestCase(RestartKind.GoalKick, true)] [TestCase(RestartKind.GoalKick, false)]
        [TestCase(RestartKind.Corner, true)] [TestCase(RestartKind.Corner, false)]
        public void EveryRestartReleasesOnceToLivePlayWithLegalPlayers(RestartKind kind, bool home)
        {
            var m = new MatchSession("restart", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture();
            var spot = kind == RestartKind.Kickoff ? new PitchPoint(0, 0) : kind == RestartKind.ThrowIn ? new PitchPoint(0, 34)
                : kind == RestartKind.GoalKick ? new PitchPoint(home ? -49 : 49, 5) : new PitchPoint(home ? 52 : -52, 33.5);
            Call(w, "AwardRestart", kind, home, spot); AssertLegal(w);
            Assert.Throws<ArgumentException>(() => w.ReleaseRestart(null, null));
            var restart = w.Restart; var option = w.RestartRecipients().OrderByDescending(p => p.Chance).First();
            var result = w.ReleaseRestart(restart.Id, option.Receiver.Id);
            Assert.That(w.ReleaseRestart(restart.Id, option.Receiver.Id), Is.SameAs(result)); Assert.That(m.DrawCount, Is.EqualTo(1));
            Assert.That(w.FixtureEvents.Count(e => e.Kind == "RestartReleased"), Is.EqualTo(1)); Assert.That(w.BallState, Is.EqualTo(BallPhase.Travelling));
            Assert.That(w.Restart, Is.Null); Assert.That(w.BallPosition.Distance(spot), Is.GreaterThan(0));
            Assert.That(w.Handling.LastKind, Is.EqualTo(kind == RestartKind.ThrowIn ? BallTouchKind.ThrowIn : BallTouchKind.DeliberateKick));
            Assert.That(result.Action.SuccessProbability, Is.EqualTo(option.Chance));
            Assert.Throws<InvalidOperationException>(() => w.ReleaseRestart(restart.Id, "other"));
            for (int i = 0; i < 1000 && w.BallState == BallPhase.Travelling; i++) m.Advance(TimeSpan.FromMilliseconds(50));
            Assert.That(w.BallState, Is.Not.EqualTo(BallPhase.Travelling));
        }
        [TestCase(true)] [TestCase(false)]
        public void CommittedShotFlightProducesOneGoalOrGoalKick(bool home)
        {
            bool scored = false, missed = false;
            for (ulong seed = 1; seed < 40 && !(scored && missed); seed++)
            {
                var m = new MatchSession("shot-fixture", seed); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture();
                var taker = w.Restart; w.ReleaseRestart(taker.Id, w.RestartRecipients()[0].Receiver.Id);
                foreach (var p in w.Players) DefenseTests.Place(p, new PitchPoint(0, p.IsHome ? 30 : -30));
                var shooter = w.Players.First(p => p.IsHome == home && p.Player.PrimaryPositionId != "gk");
                DefenseTests.Place(shooter, new PitchPoint(45 * shooter.Direction, 0)); DefenseTests.Possess(w, shooter.Player.Id);
                var preview = w.AvailableActions(shooter).First(p => p.Kind == FootballActionKind.Shot);
                Call(w, "StartAutomaticFootballAction", shooter, preview);
                var fact = w.LastFootballAction;
                for (int i = 0; i < 100 && w.Restart == null; i++) m.Advance(TimeSpan.FromMilliseconds(50));
                Assert.That(w.Restart, Is.Not.Null);
                if (fact.Action.Succeeded)
                {
                    scored = true; Assert.That(w.Restart.Kind, Is.EqualTo(RestartKind.Kickoff));
                    Assert.That(w.HomeGoals + w.AwayGoals, Is.EqualTo(1)); Assert.That(w.FixtureEvents.Count(e => e.Kind == "Goal"), Is.EqualTo(1));
                }
                else { missed = true; Assert.That(w.Restart.Kind, Is.EqualTo(RestartKind.GoalKick)); Assert.That(w.HomeGoals + w.AwayGoals, Is.Zero); }
                Assert.That(w.Restart.IsHome, Is.EqualTo(!home));
            }
            Assert.That(scored && missed, Is.True);
        }
        [TestCase(true)] [TestCase(false)]
        public void DirectThrowCannotScoreInEitherGoal(bool ownGoal)
        {
            var m = new MatchSession("direct-throw", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture();
            Call(w, "AwardRestart", RestartKind.ThrowIn, true, new PitchPoint(0, 34));
            w.ReleaseRestart(w.Restart.Id, w.RestartRecipients()[0].Receiver.Id);
            Boundary(w, ownGoal ? -1 : 1, 0, true);
            Assert.That(w.HomeGoals + w.AwayGoals, Is.Zero);
            Assert.That(w.Restart.Kind, Is.EqualTo(ownGoal ? RestartKind.Corner : RestartKind.GoalKick));
            Assert.That(w.Restart.IsHome, Is.False);
        }
        [Test]
        public void FixtureFlightDetectsTouchlineBeforeEndpointAndCannotRepeatRestart()
        {
            var m = new MatchSession("flight-line", 1); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture();
            var option = w.RestartRecipients()[0]; w.ReleaseRestart(w.Restart.Id, option.Receiver.Id);
            var type = typeof(OpenPlayWorld); var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            type.GetField("flightOrigin", flags).SetValue(w, new PitchPoint(0, 33));
            type.GetField("destination", flags).SetValue(w, new PitchPoint(0, 45));
            type.GetField("flightDuration", flags).SetValue(w, 1d);
            type.GetProperty("BallPosition").SetValue(w, new PitchPoint(0, 33));
            Advance(m, .15);
            Assert.That(w.Restart.Kind, Is.EqualTo(RestartKind.ThrowIn)); Assert.That(w.Restart.Spot.Z, Is.EqualTo(34));
            var count = w.FixtureEvents.Count; Advance(m, .5); Assert.That(w.FixtureEvents.Count, Is.EqualTo(count));
        }
        private static void AssertLegal(OpenPlayWorld w)
        {
            var r = w.Restart; var taker = w.Players.Single(p => p.Player.Id == r.TakerId);
            Assert.That(taker.Position, Is.EqualTo(r.Spot));
            foreach (var p in w.Players.Where(p => p != taker))
            {
                if (r.Kind == RestartKind.Kickoff) Assert.That(p.Position.X * p.Direction, Is.LessThanOrEqualTo(0));
                if (p.IsHome == r.IsHome) continue;
                if (r.Kind == RestartKind.Kickoff) Assert.That(p.Position.Distance(r.Spot), Is.GreaterThanOrEqualTo(9.15 - 1e-8));
                if (r.Kind == RestartKind.ThrowIn) Assert.That(p.Position.Distance(r.Spot), Is.GreaterThanOrEqualTo(2 - 1e-8));
                if (r.Kind == RestartKind.Corner) Assert.That(p.Position.Distance(r.Spot), Is.GreaterThanOrEqualTo(10.15 - 1e-8));
                if (r.Kind == RestartKind.GoalKick) Assert.That(HandlingRules.InOwnArea(p.Position, taker.Direction), Is.False);
            }
        }
        [Test]
        public void KeeperRestartCanBeInspectedCycledAndCommittedInView()
        {
            var root = new GameObject("Restart View");
            try
            {
                var data = PrototypeContent.Load(); data.userPlayerId = "harbour-player-01";
                var view = root.AddComponent<PitchPreview>(); view.Initialize(CareerState.Create(data)); view.World.StartFixture();
                Boundary(view.World, -1, 10, false); Assert.That(view.UserRestart, Is.True);
                view.TogglePause(); Assert.That(view.Match.State, Is.EqualTo(MatchRunState.Paused));
                var initial = view.ChosenRestart.Receiver.Id; view.CycleRestartTarget(); Assert.That(view.ChosenRestart.Receiver.Id, Is.Not.EqualTo(initial));
                var choice = view.ChosenRestart; view.SelectPlayer("meadow-player-11"); Assert.That(view.ChosenRestart.Receiver.Id, Is.EqualTo(choice.Receiver.Id));
                view.SyncWorldView(); Assert.That(view.CommitChosenPass(), Is.True); Assert.That(view.CommitChosenPass(), Is.False);
                Assert.That(view.World.LastPass.Action.SuccessProbability, Is.EqualTo(choice.Chance)); Assert.That(view.World.PendingKeeper, Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test]
        public void DisplayClockDoesNotWrapAtSixtyMinutes()
        {
            var root = new GameObject("Clock View");
            try
            {
                var view = root.AddComponent<PitchPreview>(); view.Initialize(PrototypeContent.CreateCareer()); view.World.StartFixture();
                typeof(OpenPlayWorld).GetProperty("FixturePhase").SetValue(view.World, FixturePhase.HalfTime); view.World.ContinueSecondHalf();
                typeof(OpenPlayWorld).GetProperty("HalfElapsedSeconds").SetValue(view.World, 2705d);
                Assert.That(view.MatchClockText, Is.EqualTo("90:05"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test]
        public void OpeningSetupDoesNotRunClockButLaterDelaysAddTime()
        {
            var m = new MatchSession("delays", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m);
            w.StartFixture(new CompetitionRules(10, 0, 1));
            Advance(m, .5); Assert.That(w.HalfElapsedSeconds, Is.Zero); Assert.That(w.AddedSeconds, Is.Zero);
            w.ReleaseRestart(w.Restart.Id, w.RestartRecipients()[0].Receiver.Id);
            Advance(m, .5); Assert.That(w.HalfElapsedSeconds, Is.EqualTo(.5).Within(1e-8));
            Boundary(w, 1, 10, true); Advance(m, .5);
            Assert.That(w.AddedSeconds, Is.EqualTo(.5).Within(1e-8));
            Assert.That(w.HalfElapsedSeconds, Is.EqualTo(1).Within(1e-8));
        }
        [Test]
        public void UserRestartCannotAdvanceMatchClockThroughRawResume()
        {
            var c = PrototypeContent.CreateCareer(); var m = new MatchSession("restart-pause", 42);
            using var w = new OpenPlayWorld(c, m, "harbour-player-01"); w.StartFixture(); Boundary(w, -1, 10, false);
            m.Resume(); Advance(m, 10);
            Assert.That(w.HalfElapsedSeconds, Is.Zero); Assert.That(w.AddedSeconds, Is.Zero);
            Assert.That(m.State, Is.EqualTo(MatchRunState.Paused));
        }
        [Test]
        public void TwoFootedTakerHasValidRestartOptions()
        {
            var data = PrototypeContent.Load(); foreach (var player in data.players) player.preferredFoot = "both";
            var m = new MatchSession("two-footed", 42); using var w = new OpenPlayWorld(CareerState.Create(data), m); w.StartFixture();
            var option = w.RestartRecipients().First(); Assert.That(w.ReleaseRestart(w.Restart.Id, option.Receiver.Id), Is.Not.Null);
        }
        [TestCase(0)] [TestCase(-1)] [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)]
        public void InvalidDurationsAreRejected(double duration) => Assert.Throws<ArgumentOutOfRangeException>(() => new CompetitionRules(duration));
    }
}
