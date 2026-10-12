using System;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Simulation;
using NUnit.Framework;

namespace FootballRpg.Tests
{
    public sealed class PassResolverTests
    {
        private static CareerState Career(string skill = null, int value = 50)
        {
            var definitions = PrototypeContent.Load();
            foreach (var player in definitions.players)
            {
                player.preferredFoot = "right";
                player.confidence = player.morale = .5f; player.fatigue = 0;
                player.attributes = definitions.footballAttributes.Select(a => new ValueOverride { id = a.id, value = 50 }).ToArray();
            }
            if (skill != null)
                definitions.players.Single(p => p.id == "harbour-player-02").attributes.Single(a => a.id == skill).value = value;
            return CareerState.Create(definitions);
        }
        private static PassActor Actor(CareerState career, string id, double x, double z = 0, double facing = 1) =>
            new PassActor(career.Players[id], new PitchPoint(x, z), new PitchPoint(facing, 0));
        private static PassPreview Preview(CareerState career = null, PassKind kind = PassKind.ToFeet,
            double distance = 15, double z = 0, string foot = "right", double facing = 1, PassActor[] opponents = null)
        {
            career = career ?? Career();
            var passer = Actor(career, "harbour-player-02", 0, 0, facing);
            var receiver = Actor(career, "harbour-player-08", distance, z);
            return PassResolver.Preview(passer, receiver, kind, new PitchPoint(distance + (kind == PassKind.ThroughBall ? 8 : 0), z), foot, opponents ?? Array.Empty<PassActor>());
        }

        [Test]
        public void ControlledModifiersHaveExpectedDirection()
        {
            var baseline = Preview();
            Assert.That(Preview(distance: 30).Percent, Is.LessThan(baseline.Percent));
            Assert.That(Preview(facing: -1).Percent, Is.LessThan(baseline.Percent));
            Assert.That(Preview(foot: "left").Percent, Is.LessThan(baseline.Percent));
            Assert.That(Preview(Career("short-passing", 85)).Percent, Is.GreaterThan(baseline.Percent));
            Assert.That(Preview(kind: PassKind.ThroughBall).Percent, Is.LessThan(baseline.Percent));
            var career = Career();
            var pressure = Preview(career, opponents: new[] { Actor(career, "meadow-player-02", -1) });
            Assert.That(pressure.Modifiers["pressure"], Is.LessThan(baseline.Modifiers["pressure"]));
            Assert.That(pressure.Percent, Is.LessThan(baseline.Percent));
            var covered = Preview(career, opponents: new[] { Actor(career, "meadow-player-02", 15, 1) });
            Assert.That(covered.Modifiers["receiver-coverage"], Is.LessThan(baseline.Modifiers["receiver-coverage"]));
            Assert.That(covered.Percent, Is.LessThan(baseline.Percent));
            career.SetPlayerCondition("harbour-player-02", .1f, .1f, .9f);
            Assert.That(Preview(career).Percent, Is.LessThan(baseline.Percent));
            career.SetPlayerCondition("harbour-player-02", .9f, .9f, 0);
            Assert.That(Preview(career).Percent, Is.GreaterThan(baseline.Percent));
        }

        [Test]
        public void ReceiverAndDefenderSkillsInfluenceTheEstimate()
        {
            var definitions = PrototypeContent.Load();
            var receiver = definitions.players.Single(p => p.id == "harbour-player-08");
            receiver.attributes = new[] { new ValueOverride { id = "ball-control", value = 10 } };
            var lowReceiver = CareerState.Create(definitions);
            receiver.attributes[0].value = 90;
            var highReceiver = CareerState.Create(definitions);
            Assert.That(Preview(highReceiver).Percent, Is.GreaterThan(Preview(lowReceiver).Percent));
            var defender = definitions.players.Single(p => p.id == "meadow-player-02");
            defender.attributes = new[] { new ValueOverride { id = "anticipation", value = 10 }, new ValueOverride { id = "positioning", value = 10 } };
            var weak = CareerState.Create(definitions);
            foreach (var attribute in defender.attributes) attribute.value = 90;
            var strong = CareerState.Create(definitions);
            Assert.That(Preview(strong, opponents: new[] { Actor(strong, defender.id, 7, 1) }).Percent,
                Is.LessThan(Preview(weak, opponents: new[] { Actor(weak, defender.id, 7, 1) }).Percent));
        }

        [TestCase(PassKind.ToFeet)] [TestCase(PassKind.ThroughBall)]
        public void DisplayedProbabilityMatchesSeededSuccessFrequency(PassKind kind)
        {
            var preview = Preview(kind: kind);
            var session = new MatchSession("sampling", 123456);
            int successes = 0; const int count = 8000;
            for (int i = 0; i < count; i++)
            {
                var result = PassResolver.Commit(session, "sample-" + i, preview, true);
                if (result.Action.Succeeded) successes++;
                Assert.That(result.Action.SuccessProbability, Is.EqualTo(preview.Percent / 100f));
                Assert.That(result.Outcome == PassOutcome.Received, Is.EqualTo(result.Action.Roll < preview.Chance));
            }
            double tolerance = 6 * Math.Sqrt(preview.Chance * (1 - preview.Chance) / count) + 1.0 / count;
            Assert.That(successes / (double)count, Is.EqualTo(preview.Chance).Within(tolerance));
            Assert.That(session.DrawCount, Is.EqualTo(count));
            TestContext.WriteLine(kind + ": displayed " + preview.Percent + "%, observed " + (successes * 100.0 / count).ToString("F2") + "% over " + count + " seeded actions.");
        }

        [TestCase(PassOutcome.Intercepted)] [TestCase(PassOutcome.LooseBall)] [TestCase(PassOutcome.OutOfPlay)]
        public void FailureContextIsCoherentAndUsesNoExtraRandomDraw(PassOutcome expected)
        {
            var career = Career();
            var preview = expected == PassOutcome.Intercepted
                ? Preview(career, opponents: new[] { Actor(career, "meadow-player-02", 7, 1) })
                : expected == PassOutcome.OutOfPlay ? Preview(career, z: 30) : Preview(career);
            Assert.That(preview.Failure, Is.EqualTo(expected));
            var session = new MatchSession("failures", 42);
            PassResolved failure = null;
            for (int i = 0; i < 100 && failure == null; i++)
            {
                long before = session.DrawCount;
                var result = PassResolver.Commit(session, "failure-" + i, preview, true);
                Assert.That(session.DrawCount, Is.EqualTo(before + 1));
                if (!result.Action.Succeeded) failure = result;
            }
            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.Outcome, Is.EqualTo(expected));
            Assert.That(failure.Destination, Is.EqualTo(preview.FailureTarget));
            Assert.That(failure.ReceivingPlayerId, Is.EqualTo(expected == PassOutcome.Intercepted ? "meadow-player-02" : null));
            if (expected == PassOutcome.OutOfPlay) Assert.That(Math.Abs(failure.Destination.Z), Is.GreaterThan(34));
        }

        [Test]
        public void PreviewIsFrozenAndRetriesDoNotRerollOrChangeAttribution()
        {
            var career = Career();
            var preview = Preview(career);
            int percent = preview.Percent;
            career.SetPlayerCondition("harbour-player-02", 0, 0, 1);
            Assert.That(preview.Passer.Fatigue, Is.Zero);
            var session = new MatchSession("frozen", 42); session.Pause();
            var first = PassResolver.Commit(session, "one", preview, false);
            var retry = PassResolver.Commit(session, "one", preview, false);
            Assert.That(first.Action, Is.SameAs(retry.Action));
            Assert.That(session.DrawCount, Is.EqualTo(1));
            Assert.That(first.Action.SuccessProbability, Is.EqualTo(percent / 100f));
            Assert.That(first.SkillAttribution.Values.Sum(), Is.EqualTo(1).Within(.0001));
            Assert.That(first.Context["success-definition"], Is.EqualTo("receiver-controls-at-target"));
            Assert.That(first.EventId, Is.EqualTo(first.Action.EventId));
            Assert.That(first.PlayerId, Is.EqualTo(preview.Passer.Id));
            Assert.Throws<InvalidOperationException>(() => PassResolver.Commit(session, "one", Preview(career), false));
        }

        [Test]
        public void TargetsMustBeFiniteValidAndOnTheSameTeam()
        {
            var career = Career(); var passer = Actor(career, "harbour-player-02", 0);
            var receiver = Actor(career, "harbour-player-08", 15);
            Assert.Throws<ArgumentException>(() => PassResolver.Preview(passer, passer, PassKind.ToFeet, new PitchPoint(15, 0), "right", Array.Empty<PassActor>()));
            Assert.Throws<ArgumentException>(() => PassResolver.Preview(passer, Actor(career, "meadow-player-02", 15), PassKind.ToFeet, new PitchPoint(15, 0), "right", Array.Empty<PassActor>()));
            Assert.Throws<ArgumentException>(() => PassResolver.Preview(passer, receiver, PassKind.ThroughBall, new PitchPoint(double.NaN, 0), "right", Array.Empty<PassActor>()));
            Assert.Throws<ArgumentException>(() => PassResolver.Preview(passer, receiver, PassKind.ToFeet, new PitchPoint(23, 0), "right", Array.Empty<PassActor>()));
        }

        [Test]
        public void WorldFlightAlwaysAppliesTheCommittedOutcomeWithoutRerolling()
        {
            var session = new MatchSession("flight", 987);
            using (var world = new OpenPlayWorld(Career(), session))
            {
                PassResolved active = null; long draws = 0; int applied = 0;
                world.PassCommitted += result => { active = result; draws = session.DrawCount; };
                for (int i = 0; i < 12000; i++)
                {
                    session.Advance(TimeSpan.FromMilliseconds(50));
                    if (active == null) continue;
                    Assert.That(session.DrawCount, Is.EqualTo(draws), "No hidden success or interception rolls during flight");
                    if (world.BallState == BallPhase.Travelling) continue;
                    switch (active.Outcome)
                    {
                        case PassOutcome.Received:
                        case PassOutcome.Intercepted:
                            Assert.That(world.OwnerId, Is.EqualTo(active.ReceivingPlayerId));
                            Assert.That(world.BallPosition.Distance(active.Destination), Is.LessThan(.001)); break;
                        case PassOutcome.LooseBall: Assert.That(world.BallState, Is.EqualTo(BallPhase.Loose)); break;
                        case PassOutcome.OutOfPlay: Assert.That(world.BallState, Is.EqualTo(BallPhase.OutOfPlay)); break;
                    }
                    active = null; applied++;
                }
                Assert.That(applied, Is.GreaterThan(10));
            }
        }
    }
}
