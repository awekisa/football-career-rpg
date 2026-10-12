using System;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Simulation;
using NUnit.Framework;

namespace FootballRpg.Tests
{
    public sealed class OpenPlayWorldTests
    {
        [TestCase(1UL)] [TestCase(42UL)] [TestCase(987UL)]
        public void OpenPlaySustainsMovementPassesAndTurnovers(ulong seed)
        {
            var career = PrototypeContent.CreateCareer();
            var match = new MatchSession("open-test", seed);
            using (var world = new OpenPlayWorld(career, match))
            {
                var moved = new bool[22];
                var seen = new System.Collections.Generic.HashSet<BallPhase>();
                double looseTime = 0;
                for (int tick = 0; tick < 6000; tick++)
                {
                    match.Advance(TimeSpan.FromMilliseconds(50));
                    seen.Add(world.BallState);
                    Assert.That(world.OwnerId == null, Is.EqualTo(world.BallState != BallPhase.Owned));
                    if (world.OwnerId != null)
                    {
                        var owner = world.Players.Single(p => p.Player.Id == world.OwnerId);
                        Assert.That(owner.Player.ClubId, Is.EqualTo(world.PossessionClubId));
                        Assert.That(owner.Position.Distance(world.BallPosition), Is.LessThan(.001));
                    }
                    looseTime = world.BallState == BallPhase.Loose ? looseTime + .05 : 0;
                    Assert.That(looseTime, Is.LessThan(25), "Loose-ball recovery stalled");
                    for (int i = 0; i < 22; i++)
                    {
                        var p = world.Players[i];
                        moved[i] |= p.Position.Distance(p.Anchor) > .5;
                        Assert.That(Math.Abs(p.Position.X), Is.LessThanOrEqualTo(51));
                        Assert.That(Math.Abs(p.Position.Z), Is.LessThanOrEqualTo(32));
                    }
                }
                Assert.That(moved.All(x => x), Is.True, "All roles, including keepers, should move");
                Assert.That(seen.Count, Is.EqualTo(3));
                Assert.That(world.CompletedPasses, Is.GreaterThan(10));
                Assert.That(world.Recoveries, Is.GreaterThan(2));
                Assert.That(world.PossessionChanges, Is.GreaterThan(2));
                Assert.That(match.Diagnostics.Any(d => d.Request.ActionId == "tackle"), Is.True);
                Assert.That(career.UserPlayer.Confidence, Is.EqualTo(.65f));
            }
        }

        [Test]
        public void ReplayIsIndependentOfFrameBatchingSpeedAndPauses()
        {
            string Run(int milliseconds, int speed, bool pause)
            {
                var match = new MatchSession("replay", 42);
                using (var world = new OpenPlayWorld(PrototypeContent.CreateCareer(), match))
                {
                    for (int elapsed = 0; elapsed < 60000; elapsed += milliseconds * speed)
                    {
                        if (pause) { match.Pause(); match.Advance(TimeSpan.FromHours(2)); match.Resume(); }
                        match.Advance(TimeSpan.FromMilliseconds(milliseconds), speed);
                    }
                    return match.Tick + ":" + world.OwnerId + ":" + world.BallPosition.X.ToString("R") + ":" +
                        string.Join("|", world.Players.Select(p => p.Position.X.ToString("R") + "," + p.Position.Z.ToString("R"))) +
                        string.Join("|", match.Diagnostics.Select(d => d.ToJsonLine()));
                }
            }
            Assert.That(Run(200, 4, false), Is.EqualTo(Run(50, 1, true)));
            Assert.That(Run(10, 1, false), Is.EqualTo(Run(200, 4, false)));
        }

        [Test]
        public void PauseAndDisposeFreezeWorldAndRandomness()
        {
            var match = new MatchSession("pause", 42);
            var world = new OpenPlayWorld(PrototypeContent.CreateCareer(), match);
            match.Advance(TimeSpan.FromSeconds(10));
            match.Pause();
            var position = world.Players[7].Position;
            var ball = world.BallPosition;
            var random = match.RandomState;
            match.Advance(TimeSpan.FromHours(1));
            Assert.That(world.Players[7].Position, Is.EqualTo(position));
            Assert.That(world.BallPosition, Is.EqualTo(ball));
            Assert.That(match.RandomState, Is.EqualTo(random));
            world.Dispose(); match.Resume(); match.Advance(TimeSpan.FromSeconds(10));
            Assert.That(world.BallPosition, Is.EqualTo(ball));
        }

        [Test]
        public void EveryDefinedPositionHasAnAnchorAndRespondsToPlay()
        {
            var definitions = PrototypeContent.Load();
            foreach (var role in definitions.positions)
            {
                var anchor = OpenPlayWorld.RoleAnchor(role.id);
                Assert.That(Math.Abs(anchor.X), Is.LessThan(52.5));
                Assert.That(Math.Abs(anchor.Z), Is.LessThan(34));
            }
            var match = new MatchSession("target", 42);
            using (var world = new OpenPlayWorld(PrototypeContent.CreateCareer(), match))
            {
                match.Advance(TimeSpan.FromMilliseconds(50));
                var initial = world.Players.Select(p => p.Target).ToArray();
                for (int i = 0; i < 100; i++) match.Advance(TimeSpan.FromMilliseconds(200));
                Assert.That(world.Players.Where((p, i) => p.Target.Distance(initial[i]) > .1).Count(), Is.GreaterThan(18));
            }
        }

        [Test]
        public void AutomaticResolutionRetainsTimeAndRejectsPausedCalls()
        {
            var match = new MatchSession("automatic", 1);
            var request = new ActionRequest("one", "player", "pass", "other", .9f, "yes", "no",
                new System.Collections.Generic.Dictionary<string, string>());
            match.TickAdvanced += tick => { if (tick == 1) { match.ResolveAutomatic(request); match.ResolveAutomatic(request); } };
            match.Advance(TimeSpan.FromMilliseconds(200));
            Assert.That(match.Tick, Is.EqualTo(4));
            Assert.That(match.DrawCount, Is.EqualTo(1));
            match.Pause();
            Assert.Throws<InvalidOperationException>(() => match.ResolveAutomatic(request));
        }
    }
}
