using System;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Presentation;
using FootballRpg.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace FootballRpg.Tests
{
    public sealed class OffsideIntegrationTests
    {
        private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(50);
        private static void Call(OpenPlayWorld w, string name, params object[] args) => GoalkeeperTests.Call(w, name, args);
        internal static FootballerMotion Arrange(OpenPlayWorld w, bool home = true)
        {
            typeof(OpenPlayWorld).GetProperty("Restart").SetValue(w, null); Call(w, "ClearPlay");
            int direction = w.Players.First(p => p.IsHome == home).Direction;
            foreach (var p in w.Players) DefenseTests.Place(p, new PitchPoint((p.IsHome == home ? -10 : 30) * direction, p.IsHome == home ? 24 : -24));
            var keeper = w.Players.Single(p => p.IsHome != home && p.Player.PrimaryPositionId == "gk"); DefenseTests.Place(keeper, new PitchPoint(50 * direction, 0));
            var passer = w.Players.Single(p => p.Player.Id == (home ? "harbour-player-07" : "meadow-player-07"));
            DefenseTests.Place(passer, new PitchPoint(20 * direction, 0));
            var forward = w.Players.Single(p => p.Player.Id == (home ? "harbour-player-08" : "meadow-player-08"));
            DefenseTests.Place(forward, new PitchPoint(40 * direction, 8));
            DefenseTests.Possess(w, passer.Player.Id); return forward;
        }
        private static void Touch(OpenPlayWorld w, FootballerMotion player, OffsideTouch touch) => Call(w, "RecordOffsideTouch", player, touch, null);
        [TestCase(true, false)] [TestCase(false, false)] [TestCase(true, true)] [TestCase(false, true)]
        public void LivePassPenalizesAtReceptionUsingFrozenPositions(bool home, bool secondHalf)
        {
            bool tested = false;
            for (ulong seed = 1; seed < 30 && !tested; seed++)
            {
                var m = new MatchSession("offside-flight", seed); var c = PrototypeContent.CreateCareer();
                string user = home ? "harbour-player-08" : "meadow-player-08";
                using var w = new OpenPlayWorld(c, m, user); w.StartFixture();
                if (secondHalf) { typeof(OpenPlayWorld).GetProperty("FixturePhase").SetValue(w, FixturePhase.HalfTime); w.ContinueSecondHalf(); }
                var forward = Arrange(w, home); var passer = w.Players.Single(p => p.Player.Id == w.OwnerId);
                var preview = w.PreviewPass(forward.Player.Id, PassKind.ToFeet);
                var result = PassResolver.Commit(m, "offside-pass", preview, true);
                Call(w, "BeginFlight", result, passer);
                if (!result.Action.Succeeded) continue;
                tested = true; Assert.That(w.LastOffside, Is.Null); Assert.That(w.Offside.Phase.Candidates.ContainsKey(forward.Player.Id), Is.True);
                long touchTick = w.Offside.Phase.TouchTick;
                for (int i = 0; i < 2000 && w.LastOffside == null; i++) m.Advance(Step);
                Assert.That(w.LastOffside, Is.Not.Null); Assert.That(w.LastOffside.Phase.TouchTick, Is.EqualTo(touchTick));
                Assert.That(w.Restart.Kind, Is.EqualTo(RestartKind.IndirectFreeKick)); Assert.That(w.Restart.IsHome, Is.EqualTo(!home));
                Assert.That(w.PendingDecision, Is.Null); Assert.That(w.CompletedPasses, Is.Zero); Assert.That(m.DrawCount, Is.EqualTo(1));
                Assert.That(w.OffsideNoticePending, Is.True); var clock = w.MatchSeconds; var ball = w.BallPosition;
                w.TogglePause(); m.Resume(); m.Advance(TimeSpan.FromSeconds(5)); Assert.That(w.MatchSeconds, Is.EqualTo(clock)); Assert.That(w.BallPosition, Is.EqualTo(ball));
                w.AcknowledgeOffside(); w.AcknowledgeOffside(); Assert.That(w.OffsideNoticePending, Is.False);
                Assert.That(w.FixtureEvents.Count(e => e.Kind == "Offside"), Is.EqualTo(1));
                Assert.That(w.Offside.Phase, Is.Null); AssertLegalFreeKick(w);
            }
            Assert.That(tested, Is.True);
        }
        [Test]
        public void ComingBackOnsideDoesNotEraseOriginalPositionAndMovingBeyondLaterIsLegal()
        {
            var m = new MatchSession("timing", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture();
            var forward = Arrange(w); DefenseTests.Place(forward, new PitchPoint(-5, 7)); Call(w, "GiveBall", forward);
            Assert.That(w.LastOffside.Location, Is.EqualTo(new PitchPoint(-5, 7))); Assert.That(w.Restart.Spot, Is.EqualTo(new PitchPoint(-5, 7)));
            forward = Arrange(w); DefenseTests.Place(forward, new PitchPoint(25, 8));
            Touch(w, w.Players.Single(p => p.Player.Id == w.OwnerId), OffsideTouch.DeliberatePlay);
            DefenseTests.Place(forward, new PitchPoint(45, 8)); Call(w, "GiveBall", forward);
            Assert.That(w.OwnerId, Is.EqualTo(forward.Player.Id)); Assert.That(w.Restart, Is.Null);
        }
        [TestCase(OffsideTouch.DeliberatePlay, false)] [TestCase(OffsideTouch.Deflection, true)] [TestCase(OffsideTouch.Save, true)]
        public void OpponentContactClassificationControlsTheLiveRestart(OffsideTouch touch, bool penalized)
        {
            var m = new MatchSession("rebound", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture(); var forward = Arrange(w);
            var keeper = w.Players.Single(p => !p.IsHome && p.Player.PrimaryPositionId == "gk");
            Touch(w, keeper, touch); Call(w, "GiveBall", forward);
            Assert.That(w.LastOffside != null, Is.EqualTo(penalized));
            if (penalized) Assert.That(w.LastOffside.Explanation, Does.Contain("does not reset"));
            else Assert.That(w.OwnerId, Is.EqualTo(forward.Player.Id));
        }
        [Test]
        public void ActualKeeperParryPreservesOffsideForTheRebound()
        {
            bool tested = false;
            for (ulong seed = 1; seed < 30 && !tested; seed++)
            {
                var m = new MatchSession("save-rebound", seed); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture();
                var forward = Arrange(w, false); var original = w.Offside.Phase;
                var keeper = w.Players.Single(p => p.IsHome && p.Player.PrimaryPositionId == "gk");
                var context = new KeeperContext(new PassActor(keeper.Player, keeper.Position, keeper.Facing), KeeperSituation.Shot,
                    new PitchPoint(-49, 0), .5, 21, keeper.Direction, w.Handling, false,
                    w.Players.Where(p => !p.IsHome).Select(p => new PassActor(p.Player, p.Position, p.Facing)));
                var preview = GoalkeeperResolver.Preview(context, KeeperAction.Parry);
                var fact = GoalkeeperResolver.Commit(m, "save", preview, true); if (!fact.Action.Succeeded) continue;
                tested = true; typeof(OpenPlayWorld).GetProperty("BallPosition").SetValue(w, new PitchPoint(-49, 0));
                Call(w, "ApplyKeeperAction", fact); Assert.That(w.Offside.Phase, Is.SameAs(original));
                Assert.That(w.BallState, Is.EqualTo(BallPhase.Loose)); Call(w, "GiveBall", forward);
                Assert.That(w.LastOffside.Rebound, Is.EqualTo(OffsideRebound.DeliberateSave));
                Assert.That(w.Restart.Kind, Is.EqualTo(RestartKind.IndirectFreeKick));
            }
            Assert.That(tested, Is.True);
        }
        [TestCase(RestartKind.ThrowIn)] [TestCase(RestartKind.GoalKick)] [TestCase(RestartKind.Corner)]
        public void ActualRestartReleaseExemptsAnAdvancedReceiver(RestartKind kind)
        {
            var m = new MatchSession("exempt", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture(); var forward = Arrange(w);
            var spot = kind == RestartKind.ThrowIn ? new PitchPoint(0, 34) : kind == RestartKind.GoalKick ? new PitchPoint(-49, 5) : new PitchPoint(52, 33.5);
            if (kind == RestartKind.Corner) DefenseTests.Place(w.Players.Single(p => p.Player.Id == "harbour-player-07"), new PitchPoint(51, 32));
            Call(w, "AwardRestart", kind, true, spot);
            // Corner receivers cannot be beyond the ball at release, but the exemption still persists explicitly.
            w.ReleaseRestart(w.Restart.Id, forward.Player.Id); Assert.That(w.Offside.Phase.Exempt, Is.True);
            Call(w, "GiveBall", forward); Assert.That(w.LastOffside, Is.Null); Assert.That(w.OwnerId, Is.EqualTo(forward.Player.Id));
            Assert.That(w.Offside.Phase.Exempt, Is.False);
        }
        [TestCase(true)] [TestCase(false)]
        public void ARelevantScreenStopsTheShotButPassivePositionDoesNot(bool screen)
        {
            bool tested = false;
            for (ulong seed = 1; seed < 30 && !tested; seed++)
            {
                var m = new MatchSession("screen", seed); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture(); var forward = Arrange(w);
                DefenseTests.Place(forward, new PitchPoint(46, screen ? 0 : 10));
                var shooter = w.Players.Single(p => p.Player.Id == w.OwnerId); DefenseTests.Place(shooter, new PitchPoint(35, 0)); DefenseTests.Possess(w, shooter.Player.Id);
                var shot = w.AvailableActions(shooter).First(p => p.Kind == FootballActionKind.Shot && p.Variant == "power");
                Call(w, "StartAutomaticFootballAction", shooter, shot); if (!w.LastFootballAction.Action.Succeeded) continue;
                tested = true; m.Advance(Step);
                Assert.That(w.LastOffside != null, Is.EqualTo(screen));
                if (screen) { Assert.That(w.LastOffside.Involvement, Is.EqualTo(OffsideInvolvement.ObstructingVision)); Assert.That(w.HomeGoals, Is.Zero); }
                else Assert.That(w.BallState, Is.EqualTo(BallPhase.Travelling));
            }
            Assert.That(tested, Is.True);
        }
        [Test]
        public void AnOffsideChaserChallengingForALooseBallIsPenalized()
        {
            var m = new MatchSession("challenge", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture(); var forward = Arrange(w);
            var defender = w.Players.Single(p => p.Player.Id == "meadow-player-02");
            DefenseTests.Place(defender, new PitchPoint(40.5, 8));
            typeof(OpenPlayWorld).GetProperty("BallPosition").SetValue(w, new PitchPoint(40, 8)); Call(w, "MakeLoose");
            m.Advance(Step); Assert.That(w.LastOffside.Involvement, Is.EqualTo(OffsideInvolvement.ChallengingOpponent));
            Assert.That(w.Restart.Kind, Is.EqualTo(RestartKind.IndirectFreeKick));
        }
        [TestCase(true)] [TestCase(false)]
        public void IndirectFreeKickCannotScoreDirectly(bool ownGoal)
        {
            var m = new MatchSession("indirect", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture();
            Call(w, "AwardRestart", RestartKind.IndirectFreeKick, true, new PitchPoint(15, 30)); AssertLegalFreeKick(w);
            w.ReleaseRestart(w.Restart.Id, w.RestartRecipients()[0].Receiver.Id);
            int end = ownGoal ? -1 : 1;
            Call(w, "ApplyBoundary", PitchBoundary.Cross(new PitchPoint(end * 50, 0), .3, new PitchPoint(end * 54, 0), .3).Value);
            Assert.That(w.HomeGoals + w.AwayGoals, Is.Zero); Assert.That(w.Restart.Kind, Is.EqualTo(ownGoal ? RestartKind.Corner : RestartKind.GoalKick));
        }
        [Test]
        public void GoalAreaLocationAndCrowdedTouchlineFreeKickAreLegal()
        {
            var m = new MatchSession("ifk-positions", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture(); var forward = Arrange(w);
            DefenseTests.Place(forward, new PitchPoint(-50, 5)); Call(w, "GiveBall", forward);
            Assert.That(w.Restart.Spot, Is.EqualTo(new PitchPoint(-47, 5))); AssertLegalFreeKick(w);
            foreach (var p in w.Players.Where(p => p.IsHome)) DefenseTests.Place(p, new PitchPoint(50, 32));
            Call(w, "AwardRestart", RestartKind.IndirectFreeKick, false, new PitchPoint(51, 30)); AssertLegalFreeKick(w);
        }
        private static void AssertLegalFreeKick(OpenPlayWorld w)
        {
            var r = w.Restart; var taker = w.Players.Single(p => p.Player.Id == r.TakerId);
            foreach (var opponent in w.Players.Where(p => p.IsHome != r.IsHome))
            {
                Assert.That(opponent.Position.Distance(r.Spot), Is.GreaterThanOrEqualTo(9.15 - 1e-8));
                if (HandlingRules.InOwnArea(r.Spot, taker.Direction)) Assert.That(HandlingRules.InOwnArea(opponent.Position, taker.Direction), Is.False);
            }
        }
        [Test]
        public void ForwardRunStrategyReachesAnOffsideCallDuringNormalSimulation()
        {
            var root = new GameObject("Live Offside");
            try
            {
                var data = PrototypeContent.Load(); data.userPlayerId = "harbour-player-11";
                var view = root.AddComponent<PitchPreview>(); view.Initialize(CareerState.Create(data)); view.World.StartFixture(new CompetitionRules(600));
                for (int i = 0; i < 24000 && view.World.LastOffside == null; i++)
                {
                    if (view.World.RefereeNoticePending || view.World.FixturePhase == FixturePhase.HalfTime || view.World.HasPendingDecision || view.UserRestart) view.CommitChosenPass();
                    var intent = view.World.CanSetIntent(OffBallIntent.ForwardRun) ? OffBallIntent.ForwardRun : OffBallIntent.Counterattack;
                    if (view.World.CanSetIntent(intent) && view.World.ActiveIntent?.Kind != intent) view.ChooseIntent(intent);
                    view.Match.Advance(Step);
                }
                Assert.That(view.World.LastOffside, Is.Not.Null); Assert.That(view.World.OffsideNoticePending, Is.True);
                Assert.That(view.World.Restart.Kind, Is.EqualTo(RestartKind.IndirectFreeKick));
                view.CommitChosenPass(); Assert.That(view.World.OffsideNoticePending, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test]
        public void NoticeCanBeInspectedAndAcknowledgedThroughPresentation()
        {
            var root = new GameObject("Offside View");
            try
            {
                var view = root.AddComponent<PitchPreview>(); view.Initialize(PrototypeContent.CreateCareer()); view.World.StartFixture();
                var forward = Arrange(view.World); Call(view.World, "GiveBall", forward); var decision = view.World.LastOffside;
                view.SelectPlayer("meadow-player-11"); view.SyncWorldView(); Assert.That(view.World.LastOffside, Is.SameAs(decision));
                view.TogglePause(); Assert.That(view.World.OffsideNoticePending, Is.True);
                Assert.That(view.CommitChosenPass(), Is.True); Assert.That(view.World.OffsideNoticePending, Is.False);
                Assert.That(view.World.Restart.Kind, Is.EqualTo(RestartKind.IndirectFreeKick));
                Assert.That(view.Match.DrawCount, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
