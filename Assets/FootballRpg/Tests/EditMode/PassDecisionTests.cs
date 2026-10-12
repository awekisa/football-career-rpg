using System;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Simulation;
using NUnit.Framework;

namespace FootballRpg.Tests
{
    public sealed class PassDecisionTests
    {
        [Test]
        public void OpeningPossessionFreezesTwentyOptionsAndCannotBeSkippedByPauseButton()
        {
            var career = PrototypeContent.CreateCareer();
            var session = new MatchSession("choices", 42);
            using (var world = new OpenPlayWorld(career, session, career.UserPlayerId, true))
            {
                var decision = world.PendingDecision;
                Assert.That(session.State, Is.EqualTo(MatchRunState.Paused));
                Assert.That(decision.Options.Count, Is.EqualTo(20));
                var positions = world.Players.Select(p => p.Position).ToArray();
                var ball = world.BallPosition;
                foreach (var choice in decision.Options)
                {
                    Assert.That(choice.Passer.Id, Is.EqualTo(career.UserPlayerId));
                    Assert.That(choice.Receiver.ClubId, Is.EqualTo(career.UserPlayer.ClubId));
                    Assert.That(choice.Receiver.Id, Is.Not.EqualTo(career.UserPlayerId));
                }
                world.TogglePause(); session.Advance(TimeSpan.FromDays(1));
                CollectionAssert.AreEqual(positions, world.Players.Select(p => p.Position));
                Assert.That(world.BallPosition, Is.EqualTo(ball));
                Assert.That(world.PendingDecision, Is.SameAs(decision));
                Assert.That(world.DecisionCount, Is.EqualTo(1));
                Assert.That(session.DrawCount, Is.Zero);
                Assert.That(session.Tick, Is.Zero);
            }
        }

        [Test]
        public void LowSkillsDoNotHideEitherPassTypeOrTeammates()
        {
            var definitions = PrototypeContent.Load();
            var user = definitions.players.Single(p => p.id == definitions.userPlayerId);
            user.attributes = definitions.footballAttributes.Select(a => new ValueOverride { id = a.id, value = a.minimum }).ToArray();
            var career = CareerState.Create(definitions);
            using (var world = new OpenPlayWorld(career, new MatchSession("low", 1), career.UserPlayerId, true))
            {
                Assert.That(world.PendingDecision.Options.Count, Is.EqualTo(20));
                Assert.That(world.PendingDecision.Options.Count(p => p.Kind == PassKind.ThroughBall), Is.EqualTo(10));
            }
        }

        [Test]
        public void CommitRetriesAreIdempotentAndInvalidChoicesDoNotConsumeTheDecision()
        {
            var career = PrototypeContent.CreateCareer();
            var session = new MatchSession("retry", 42);
            using (var world = new OpenPlayWorld(career, session, career.UserPlayerId, true))
            {
                var decision = world.PendingDecision;
                var choice = decision.Options.First();
                Assert.Throws<ArgumentException>(() => world.CommitDecision(decision.Id, "meadow-player-01", PassKind.ToFeet));
                Assert.Throws<InvalidOperationException>(() => world.CommitDecision("stale", choice.Receiver.Id, choice.Kind));
                Assert.That(session.DrawCount, Is.Zero);
                int events = 0; world.PassCommitted += pass => events++;
                var committed = world.CommitDecision(decision.Id, choice.Receiver.Id, choice.Kind);
                var repeated = world.CommitDecision(decision.Id, choice.Receiver.Id, choice.Kind);
                Assert.That(repeated, Is.SameAs(committed));
                Assert.That(committed.Preview, Is.SameAs(choice));
                Assert.That(committed.Action.SuccessProbability, Is.EqualTo(choice.Percent / 100f));
                Assert.That(session.DrawCount, Is.EqualTo(1));
                Assert.That(events, Is.EqualTo(1));
                Assert.That(world.PendingDecision, Is.Null);
                Assert.That(world.BallState, Is.EqualTo(BallPhase.Travelling));
                Assert.That(session.State, Is.EqualTo(MatchRunState.Running));
                Assert.Throws<InvalidOperationException>(() => world.CommitDecision(decision.Id, choice.Receiver.Id, PassKind.ThroughBall));
                session.Advance(TimeSpan.FromMilliseconds(100));
                var ball = world.BallPosition;
                world.TogglePause(); session.Advance(TimeSpan.FromHours(1));
                Assert.That(world.BallPosition, Is.EqualTo(ball));
                world.TogglePause(); session.Advance(TimeSpan.FromMilliseconds(100));
                Assert.That(world.BallPosition, Is.Not.EqualTo(ball));
                Assert.That(world.DecisionCount, Is.EqualTo(1));
            }
        }

        [TestCase(PassKind.ToFeet)] [TestCase(PassKind.ThroughBall)]
        public void SuccessfulAndFailedUserPassesAnimateWithoutRerolls(PassKind kind)
        {
            bool success = false, failure = false;
            var career = PrototypeContent.CreateCareer();
            for (ulong seed = 1; seed <= 20 && !(success && failure); seed++)
            {
                var session = new MatchSession("outcomes", seed);
                using (var world = new OpenPlayWorld(career, session, career.UserPlayerId, true))
                {
                    var decision = world.PendingDecision;
                    var choice = decision.Options.First(p => p.Kind == kind);
                    var committed = world.CommitDecision(decision.Id, choice.Receiver.Id, kind);
                    for (int i = 0; i < 500 && world.BallState == BallPhase.Travelling; i++)
                        session.Advance(TimeSpan.FromMilliseconds(50));
                    Assert.That(world.BallState, Is.Not.EqualTo(BallPhase.Travelling));
                    Assert.That(session.DrawCount, Is.EqualTo(1));
                    Assert.That(world.DecisionCount, Is.EqualTo(1));
                    if (committed.Action.Succeeded)
                    { success = true; Assert.That(world.OwnerId, Is.EqualTo(choice.Receiver.Id)); }
                    else
                    {
                        failure = true;
                        Assert.That(committed.Outcome, Is.Not.EqualTo(PassOutcome.Received));
                        Assert.That(world.OwnerId, Is.Not.EqualTo(choice.Receiver.Id));
                    }
                    Assert.That(session.State, Is.EqualTo(MatchRunState.Running));
                }
            }
            Assert.That(success && failure, Is.True, "Both outcome paths must be exercised.");
        }

        [Test]
        public void ReceivingTheBallOpensOneNewDecisionAndOldRetriesCannotReplayFlight()
        {
            var career = PrototypeContent.CreateCareer();
            var session = new MatchSession("receive", 42);
            using (var world = new OpenPlayWorld(career, session, career.UserPlayerId, true))
            {
                var first = world.PendingDecision;
                var choice = first.Options.OrderByDescending(p => p.Chance).First();
                world.CommitDecision(first.Id, choice.Receiver.Id, choice.Kind);
                for (int i = 0; i < 24000 && world.PendingDecision == null; i++) session.Advance(TimeSpan.FromMilliseconds(50));
                Assert.That(world.PendingDecision, Is.Not.Null, "User should receive or recover the ball again.");
                var next = world.PendingDecision;
                var ball = world.BallPosition;
                long draws = session.DrawCount;
                Assert.That(next.Id, Is.Not.EqualTo(first.Id));
                Assert.That(world.OwnerId, Is.EqualTo(career.UserPlayerId));
                Assert.That(session.State, Is.EqualTo(MatchRunState.Paused));
                Assert.That(world.DecisionCount, Is.EqualTo(2));
                world.CommitDecision(first.Id, choice.Receiver.Id, choice.Kind);
                session.Advance(TimeSpan.FromHours(1));
                Assert.That(world.PendingDecision, Is.SameAs(next));
                Assert.That(world.BallPosition, Is.EqualTo(ball));
                Assert.That(session.DrawCount, Is.EqualTo(draws));
            }
        }
    }
}
