using System;
using System.Linq;
using System.Reflection;
using FootballRpg.Content;
using FootballRpg.Simulation;
using NUnit.Framework;

namespace FootballRpg.Tests
{
    public sealed class DefenseTests
    {
        // Construct identical spatial scenarios without adding a production teleport API.
        internal static void Place(FootballerMotion player, PitchPoint point) => typeof(FootballerMotion).GetProperty("Position").SetValue(player, point);
        internal static void Possess(OpenPlayWorld world, string id) => typeof(OpenPlayWorld).GetMethod("GiveBall", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(world, new object[] { world.Players.Single(p => p.Player.Id == id) });
        internal static void Attack(OpenPlayWorld world)
        {
            foreach (var p in world.Players) Place(p, new PitchPoint(p.IsHome ? 35 : 40, 25));
            Place(world.Players.Single(p => p.Player.Id == "harbour-player-08"), new PitchPoint(-31.5, 0));
            Place(world.Players.Single(p => p.Player.Id == "meadow-player-11"), new PitchPoint(-30, 0));
            Possess(world, "meadow-player-11");
        }
        private static DefensePreview Preview(DefensiveAction kind)
        {
            var c = PrototypeContent.CreateCareer();
            return DefenseResolver.Preview(new PassActor(c.UserPlayer, new PitchPoint(-31.5, 0), new PitchPoint(1, 0)),
                new PassActor(c.Players["meadow-player-11"], new PitchPoint(-30, 0), new PitchPoint(-1, 0)), 1, kind);
        }
        [TestCase(DefensiveAction.Tackle)] [TestCase(DefensiveAction.Contain)] [TestCase(DefensiveAction.Block)]
        public void EveryDefensiveActionUsesDisplayedChanceWithOneAttributedDraw(DefensiveAction kind)
        {
            var preview = Preview(kind); var match = new MatchSession("defense-chances", 42); int successes = 0;
            for (int i = 0; i < 2000; i++)
            {
                var fact = DefenseResolver.Commit(match, "defense-" + i, preview, true);
                Assert.That(fact.Action.SuccessProbability, Is.EqualTo(preview.Percent / 100f));
                Assert.That(fact.Action.Succeeded, Is.EqualTo(fact.Action.Roll < preview.Chance));
                Assert.That(fact.SkillAttribution.Values.Sum(), Is.EqualTo(1).Within(.0001));
                if (fact.Action.Succeeded) successes++;
            }
            Assert.That(match.DrawCount, Is.EqualTo(2000));
            Assert.That(successes / 2000.0, Is.EqualTo(preview.Chance).Within(6 * Math.Sqrt(preview.Chance * (1-preview.Chance) / 2000)));
        }
        [Test]
        public void ReachLaneAndTeamConstraintsDoNotDependOnAttributeThresholds()
        {
            var p = Preview(DefensiveAction.Block);
            Assert.That(DefenseResolver.IsAvailable(p.Defender, p.Attacker, 1, DefensiveAction.Block), Is.True);
            Assert.That(DefenseResolver.IsAvailable(p.Attacker, p.Defender, 1, DefensiveAction.Block), Is.False);
            Assert.That(DefenseResolver.IsAvailable(p.Defender, p.Defender, 1, DefensiveAction.Tackle), Is.False);
            var c = PrototypeContent.CreateCareer();
            var far = new PassActor(c.UserPlayer, new PitchPoint(0, 20), new PitchPoint(1, 0));
            foreach (DefensiveAction kind in Enum.GetValues(typeof(DefensiveAction)))
                Assert.Throws<ArgumentException>(() => DefenseResolver.Preview(far, p.Attacker, 1, kind));
            var mid = new PassActor(c.UserPlayer, new PitchPoint(-34, 0), new PitchPoint(1, 0));
            Assert.That(DefenseResolver.IsAvailable(mid, p.Attacker, 1, DefensiveAction.Contain), Is.True);
            Assert.That(DefenseResolver.IsAvailable(mid, p.Attacker, 1, DefensiveAction.Tackle), Is.False);
        }
        [TestCase(DefensiveAction.Tackle)] [TestCase(DefensiveAction.Contain)] [TestCase(DefensiveAction.Block)]
        public void SameAttackHasDistinctSuccessAndFailureConsequencesAndRetriesDoNotReplay(DefensiveAction kind)
        {
            var c = PrototypeContent.CreateCareer(); bool success = false, failure = false;
            for (ulong seed = 1; seed < 100 && !(success && failure); seed++)
            {
                var match = new MatchSession("defense-world", seed);
                using (var w = new OpenPlayWorld(c, match, c.UserPlayerId))
                {
                    Attack(w); match.Advance(TimeSpan.FromMilliseconds(50));
                    Assert.That(w.PendingDefense.Options.Count, Is.EqualTo(3));
                    var d = w.PendingDefense; var positions = w.Players.Select(p => p.Position).ToArray();
                    w.TogglePause(); match.Advance(TimeSpan.FromHours(1));
                    Assert.That(match.Tick, Is.EqualTo(d.Tick));
                    CollectionAssert.AreEqual(positions, w.Players.Select(p => p.Position));
                    Assert.Throws<InvalidOperationException>(() => w.CommitDefense("stale", kind));
                    Assert.Throws<ArgumentException>(() => w.CommitDefense(d.Id, (DefensiveAction)99));
                    Assert.That(match.DrawCount, Is.Zero);
                    var result = w.CommitDefense(d.Id, kind); var ball = w.BallPosition; int decisions = w.DecisionCount;
                    Assert.That(w.CommitDefense(d.Id, kind), Is.SameAs(result));
                    Assert.That(w.BallPosition, Is.EqualTo(ball)); Assert.That(w.DecisionCount, Is.EqualTo(decisions));
                    Assert.That(match.DrawCount, Is.EqualTo(1));
                    Assert.Throws<InvalidOperationException>(() => w.CommitDefense(d.Id, kind == DefensiveAction.Tackle ? DefensiveAction.Block : DefensiveAction.Tackle));
                    if (result.Action.Succeeded)
                    {
                        success = true;
                        if (kind == DefensiveAction.Tackle)
                        { Assert.That(w.OwnerId, Is.EqualTo(c.UserPlayerId)); Assert.That(w.PendingDecision, Is.Not.Null); }
                        else if (kind == DefensiveAction.Block)
                        { Assert.That(w.BallState, Is.EqualTo(BallPhase.Loose)); Assert.That(w.BallPosition.Z, Is.EqualTo(4)); }
                        else
                        {
                            Assert.That(w.OwnerId, Is.EqualTo("meadow-player-11"));
                            match.Advance(TimeSpan.FromSeconds(1));
                            Assert.That(w.OwnerId, Is.EqualTo("meadow-player-11"));
                            Assert.That(w.BallPosition.Distance(ball), Is.LessThan(2));
                            Assert.That(w.PendingDefense, Is.Null);
                        }
                    }
                    else
                    {
                        failure = true;
                        Assert.That(w.OwnerId, Is.EqualTo("meadow-player-11"));
                        match.Advance(TimeSpan.FromSeconds(.5));
                        Assert.That(w.PendingDefense, Is.Null);
                        Assert.That(w.Players.Single(p => p.Player.Id == c.UserPlayerId).Position.Distance(positions[7]), Is.LessThan(1));
                    }
                }
            }
            Assert.That(success && failure, Is.True);
        }
        [TestCase(OffBallIntent.Press)] [TestCase(OffBallIntent.PersonalMark)] [TestCase(OffBallIntent.HoldZone)] [TestCase(OffBallIntent.Track)]
        [TestCase(OffBallIntent.FindSpace)] [TestCase(OffBallIntent.Support)] [TestCase(OffBallIntent.ForwardRun)] [TestCase(OffBallIntent.Counterattack)]
        public void IntentChangesOnlyTheUserTargetAndPausePreservesItsDuration(OffBallIntent kind)
        {
            var c = PrototypeContent.CreateCareer(); var match = new MatchSession("intent", 42);
            using (var w = new OpenPlayWorld(c, match, c.UserPlayerId))
            {
                bool attack = kind == OffBallIntent.FindSpace || kind == OffBallIntent.Support || kind == OffBallIntent.ForwardRun;
                Possess(w, attack ? "harbour-player-02" : "meadow-player-11");
                var before = w.Players.Select(p => p.Target).ToArray();
                match.Pause(); w.SetIntent(kind, "meadow-player-09");
                Assert.That(w.ActiveIntent.Kind, Is.EqualTo(kind)); Assert.That(w.IntentSecondsRemaining, Is.EqualTo(20).Within(.00001));
                var target = w.Players[7].Target;
                if (kind != OffBallIntent.HoldZone) Assert.That(target, Is.Not.EqualTo(before[7]));
                for (int i = 0; i < 22; i++) if (i != 7) Assert.That(w.Players[i].Target, Is.EqualTo(before[i]));
                match.Advance(TimeSpan.FromHours(1));
                Assert.That(w.IntentSecondsRemaining, Is.EqualTo(20).Within(.00001));
                match.Resume(); match.Advance(TimeSpan.FromMilliseconds(50));
                Assert.That(w.IntentSecondsRemaining, Is.LessThan(20));
            }
        }
        [Test]
        public void MarkingTracksItsSelectedOpponentAndDiffersFromTrackingAndZoneHolding()
        {
            var c = PrototypeContent.CreateCareer(); var match = new MatchSession("mark", 42);
            using (var w = new OpenPlayWorld(c, match, c.UserPlayerId))
            {
                Possess(w, "meadow-player-11"); w.SetIntent(OffBallIntent.PersonalMark, "meadow-player-09");
                var mark = w.Players[7].Target;
                w.SetIntent(OffBallIntent.Track, "meadow-player-09"); Assert.That(w.Players[7].Target, Is.Not.EqualTo(mark));
                w.SetIntent(OffBallIntent.PersonalMark, "meadow-player-10"); Assert.That(w.Players[7].Target, Is.Not.EqualTo(mark));
                w.SetIntent(OffBallIntent.HoldZone); var zone = w.Players[7].Position;
                match.Advance(TimeSpan.FromMilliseconds(50)); Assert.That(w.Players[7].Position, Is.EqualTo(zone));
                foreach (string invalid in new[] { c.UserPlayerId, "harbour-player-02", "meadow-player-18", "missing" })
                    Assert.Throws<ArgumentException>(() => w.SetIntent(OffBallIntent.PersonalMark, invalid));
                Assert.Throws<ArgumentException>(() => w.SetIntent(OffBallIntent.Support));
            }
        }
        [Test]
        public void IncomingReceptionInvalidatesIntentBeforeItsDecisionPauses()
        {
            var c = PrototypeContent.CreateCareer(); var match = new MatchSession("arrival-intent", 42);
            using (var w = new OpenPlayWorld(c, match, c.UserPlayerId))
            {
                w.SetIntent(OffBallIntent.Support);
                typeof(OpenPlayWorld).GetMethod("BeginArrival", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(w, new object[] { w.Players.Single(p => p.Player.Id == c.UserPlayerId), 1.8, 12.0 });
                Assert.That(w.PendingDecision, Is.Not.Null);
                Assert.That(w.ActiveIntent, Is.Null);
                Assert.That(match.State, Is.EqualTo(MatchRunState.Paused));
            }
        }
        [Test]
        public void DistantAndFriendlyPossessionDoNotPauseAndCommittedFlightsAreNeverIntercepted()
        {
            var c = PrototypeContent.CreateCareer(); var match = new MatchSession("threat-phase", 42);
            using (var w = new OpenPlayWorld(c, match, c.UserPlayerId))
            {
                match.Advance(TimeSpan.FromMilliseconds(50)); Assert.That(w.PendingDefense, Is.Null);
                Possess(w, "meadow-player-02");
                match.Advance(TimeSpan.FromMilliseconds(50)); Assert.That(w.PendingDefense, Is.Null);
            }
            match = new MatchSession("frozen-flight", 42);
            using (var w = new OpenPlayWorld(c, match, c.UserPlayerId, true))
            {
                var d = w.PendingDecision; var pass = d.Options.First();
                w.CommitDecision(d.Id, pass.Receiver.Id, pass.Kind);
                for (int i = 0; i < 500 && w.BallState == BallPhase.Travelling; i++)
                { match.Advance(TimeSpan.FromMilliseconds(50)); Assert.That(w.PendingDefense, Is.Null); }
                Assert.That(match.DrawCount, Is.EqualTo(1));
            }
        }
        [Test]
        public void MinimumAttributesKeepReachableDefenseChoicesAvailable()
        {
            var definitions = PrototypeContent.Load();
            var user = definitions.players.Single(p => p.id == definitions.userPlayerId);
            user.attributes = definitions.footballAttributes.Select(a => new ValueOverride { id = a.id, value = a.minimum }).ToArray();
            var c = CareerState.Create(definitions); var match = new MatchSession("low-defense", 42);
            using (var w = new OpenPlayWorld(c, match, c.UserPlayerId))
            { Attack(w); match.Advance(TimeSpan.FromMilliseconds(50)); Assert.That(w.PendingDefense.Options.Count, Is.EqualTo(3)); }
        }

        [Test]
        public void ExpiryAndPossessionChangesClearIntentWithVisibleReason()
        {
            var c = PrototypeContent.CreateCareer(); var match = new MatchSession("expiry", 42);
            using (var w = new OpenPlayWorld(c, match, c.UserPlayerId))
            {
                w.SetIntent(OffBallIntent.Support); Possess(w, "meadow-player-11");
                Assert.That(w.ActiveIntent, Is.Null); StringAssert.Contains("phase changed", w.IntentStatus);
                w.SetIntent(OffBallIntent.Press); Possess(w, c.UserPlayerId);
                Assert.That(w.ActiveIntent, Is.Null);
                Assert.Throws<ArgumentException>(() => w.SetIntent(OffBallIntent.HoldZone));
            }
            match = new MatchSession("expiry", 42);
            using (var w = new OpenPlayWorld(c, match, c.UserPlayerId))
            {
                w.SetIntent(OffBallIntent.HoldZone);
                // Keep the ball loose and remote to isolate simulated-time expiry from possession invalidation.
                typeof(OpenPlayWorld).GetProperty("BallState").SetValue(w, BallPhase.Loose);
                typeof(OpenPlayWorld).GetProperty("OwnerId").SetValue(w, null);
                typeof(OpenPlayWorld).GetProperty("BallPosition").SetValue(w, new PitchPoint(1000, 1000));
                for (int i = 0; i < 400; i++) match.Advance(TimeSpan.FromMilliseconds(50));
                Assert.That(w.ActiveIntent, Is.Null); StringAssert.Contains("expired", w.IntentStatus);
            }
        }
    }
}
