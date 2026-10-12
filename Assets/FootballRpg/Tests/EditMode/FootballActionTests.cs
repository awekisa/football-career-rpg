using System;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Simulation;
using NUnit.Framework;

namespace FootballRpg.Tests
{
    public sealed class FootballActionTests
    {
        private static bool Header(FootballActionKind kind) => kind == FootballActionKind.HeaderPass || kind == FootballActionKind.HeaderShot;
        private static bool Arrival(FootballActionKind kind) => Header(kind) || kind == FootballActionKind.Control || kind == FootballActionKind.FirstTimePass;
        private static bool Targeted(FootballActionKind kind) => kind == FootballActionKind.FirstTimePass || kind == FootballActionKind.Cross || kind == FootballActionKind.HeaderPass;
        private static FootballActionContext Context(FootballActionKind kind, CareerState career = null, double? speed = null, double? height = null)
        {
            career = career ?? PrototypeContent.CreateCareer();
            return new FootballActionContext(new PassActor(career.UserPlayer, new PitchPoint(20, 20), new PitchPoint(1, 0)),
                Arrival(kind) ? BallSituation.Arriving : BallSituation.Controlled, height ?? (Header(kind) ? 1.8 : Arrival(kind) ? .3 : 0), speed ?? (Arrival(kind) ? 12 : 0), 1,
                new[] { new PassActor(career.Players["meadow-player-02"], new PitchPoint(24, 21), new PitchPoint(-1, 0)) });
        }
        private static PassActor Receiver(CareerState career = null) => new PassActor((career ?? PrototypeContent.CreateCareer()).Players["harbour-player-11"], new PitchPoint(30, 0), new PitchPoint(1, 0));

        [TestCase(FootballActionKind.Control)] [TestCase(FootballActionKind.FirstTimePass)]
        [TestCase(FootballActionKind.Dribble)] [TestCase(FootballActionKind.Cross)] [TestCase(FootballActionKind.Shot)]
        [TestCase(FootballActionKind.HeaderPass)] [TestCase(FootballActionKind.HeaderShot)] [TestCase(FootballActionKind.Clearance)]
        public void EveryVariantUsesItsShownChanceAndEmitsAttributedOutcomes(FootballActionKind kind)
        {
            foreach (string variant in FootballActionResolver.Variants(kind))
            {
                var preview = FootballActionResolver.Preview(Context(kind), kind, variant, Targeted(kind) ? Receiver() : null);
                var session = new MatchSession(kind + variant, 42);
                int successes = 0;
                for (int i = 0; i < 1500; i++)
                {
                    var result = FootballActionResolver.Commit(session, "sample-" + i, preview, true);
                    Assert.That(result.Action.SuccessProbability, Is.EqualTo(preview.Percent / 100f));
                    Assert.That(result.Outcome, Is.EqualTo(result.Action.Succeeded ? preview.Success : preview.Failure));
                    Assert.That(result.Action.Succeeded, Is.EqualTo(result.Action.Roll < preview.Chance));
                    Assert.That(result.EventId, Is.EqualTo(result.Action.EventId));
                    Assert.That(result.SkillAttribution.Values.Sum(), Is.EqualTo(1).Within(.0001));
                    if (result.Action.Succeeded) successes++;
                }
                Assert.That(session.DrawCount, Is.EqualTo(1500));
                double tolerance = 6 * Math.Sqrt(preview.Chance * (1 - preview.Chance) / 1500) + 1.0 / 1500;
                Assert.That(successes / 1500.0, Is.EqualTo(preview.Chance).Within(tolerance));
                Assert.That(successes, Is.InRange(1, 1499));
            }
        }

        [TestCase(FootballActionKind.Control)] [TestCase(FootballActionKind.FirstTimePass)]
        [TestCase(FootballActionKind.Dribble)] [TestCase(FootballActionKind.Cross)] [TestCase(FootballActionKind.Shot)]
        [TestCase(FootballActionKind.HeaderPass)] [TestCase(FootballActionKind.HeaderShot)] [TestCase(FootballActionKind.Clearance)]
        public void InfeasibleContextsDoNotOfferOrResolveActions(FootballActionKind kind)
        {
            var career = PrototypeContent.CreateCareer();
            var context = new FootballActionContext(new PassActor(career.UserPlayer, new PitchPoint(0, 0), new PitchPoint(1, 0)),
                Arrival(kind) ? BallSituation.Controlled : BallSituation.Arriving, 6, 12, 1, Array.Empty<PassActor>());
            string variant = FootballActionResolver.Variants(kind)[0];
            Assert.That(FootballActionResolver.IsAvailable(context, kind, variant, Targeted(kind) ? Receiver() : null), Is.False);
            Assert.Throws<ArgumentException>(() => FootballActionResolver.Preview(context, kind, variant, Targeted(kind) ? Receiver() : null));
        }

        [Test]
        public void TrajectoryPhysicalProfileAndVariantsMatter()
        {
            var slow = FootballActionResolver.Preview(Context(FootballActionKind.Control, speed: 2), FootballActionKind.Control, "cushion");
            var fast = FootballActionResolver.Preview(Context(FootballActionKind.Control, speed: 24), FootballActionKind.Control, "cushion");
            Assert.That(fast.Percent, Is.LessThan(slow.Percent));
            var definitions = PrototypeContent.Load();
            var user = definitions.players.Single(p => p.id == definitions.userPlayerId);
            user.heightCm = 165; var shortPlayer = CareerState.Create(definitions);
            user.heightCm = 205; var tallPlayer = CareerState.Create(definitions);
            Assert.That(FootballActionResolver.Preview(Context(FootballActionKind.HeaderPass, tallPlayer), FootballActionKind.HeaderPass, "direct", Receiver(tallPlayer)).Percent,
                Is.GreaterThan(FootballActionResolver.Preview(Context(FootballActionKind.HeaderPass, shortPlayer), FootballActionKind.HeaderPass, "direct", Receiver(shortPlayer)).Percent));
            Assert.That(FootballActionResolver.IsAvailable(Context(FootballActionKind.HeaderPass, height: 4), FootballActionKind.HeaderPass, "direct", Receiver()), Is.False);
            var low = FootballActionResolver.Preview(Context(FootballActionKind.Cross), FootballActionKind.Cross, "low", Receiver());
            var lofted = FootballActionResolver.Preview(Context(FootballActionKind.Cross), FootballActionKind.Cross, "lofted", Receiver());
            Assert.That(lofted.DeliveryHeight, Is.GreaterThan(low.DeliveryHeight));
            Assert.That(lofted.Percent, Is.Not.EqualTo(low.Percent));
            Assert.That(FootballActionResolver.Preview(Context(FootballActionKind.Shot), FootballActionKind.Shot, "power").Target,
                Is.Not.EqualTo(FootballActionResolver.Preview(Context(FootballActionKind.Shot), FootballActionKind.Shot, "placed").Target));
        }

        [TestCase(FootballActionKind.Control, .3)] [TestCase(FootballActionKind.FirstTimePass, .3)]
        [TestCase(FootballActionKind.HeaderPass, 1.8)] [TestCase(FootballActionKind.HeaderShot, 1.8)]
        public void IncomingUserCanCommitAndAnimateEachReceivingAction(FootballActionKind kind, double height)
        {
            var career = PrototypeContent.CreateCareer();
            var session = new MatchSession("arrival", 42);
            using (var world = new OpenPlayWorld(career, session, career.UserPlayerId, true, height))
            {
                Assert.That(world.BallState, Is.EqualTo(BallPhase.Incoming));
                Assert.That(world.OwnerId, Is.Null);
                Assert.That(world.PendingDecision.Options, Is.Empty);
                var decision = world.PendingDecision;
                var option = decision.Actions.First(p => p.Kind == kind);
                var result = world.CommitFootballDecision(decision.Id, option.OptionId);
                Assert.That(world.CommitFootballDecision(decision.Id, option.OptionId), Is.SameAs(result));
                Assert.Throws<InvalidOperationException>(() => world.CommitDecision(decision.Id, "harbour-player-11", PassKind.ToFeet));
                for (int i = 0; i < 500 && world.BallState == BallPhase.Travelling; i++) session.Advance(TimeSpan.FromMilliseconds(50));
                Assert.That(world.BallState, Is.Not.EqualTo(BallPhase.Travelling));
                Assert.That(session.DrawCount, Is.EqualTo(1));
                if (result.ReceivingPlayerId != null) Assert.That(world.OwnerId, Is.EqualTo(result.ReceivingPlayerId));
                if (result.Outcome == FootballOutcome.Goal) Assert.That(world.HomeGoals, Is.EqualTo(1));
            }
        }

        [Test]
        public void ScoredGoalIsCountedOnceEvenWhenTheCommitIsRetried()
        {
            var career = PrototypeContent.CreateCareer(); bool scored = false;
            for (ulong seed = 1; seed <= 200 && !scored; seed++)
            {
                var session = new MatchSession("goal", seed);
                using (var world = new OpenPlayWorld(career, session, career.UserPlayerId, true))
                {
                    var decision = world.PendingDecision;
                    var option = decision.Actions.First(p => p.Kind == FootballActionKind.Shot);
                    var result = world.CommitFootballDecision(decision.Id, option.OptionId);
                    if (!result.Action.Succeeded) continue;
                    for (int i = 0; i < 500 && world.BallState == BallPhase.Travelling; i++) session.Advance(TimeSpan.FromMilliseconds(50));
                    Assert.That(world.HomeGoals, Is.EqualTo(1));
                    Assert.That(world.BallState, Is.EqualTo(BallPhase.OutOfPlay));
                    world.CommitFootballDecision(decision.Id, option.OptionId);
                    session.Advance(TimeSpan.FromMilliseconds(50));
                    Assert.That(world.HomeGoals, Is.EqualTo(1));
                    Assert.That(session.DrawCount, Is.EqualTo(1));
                    scored = true;
                }
            }
            Assert.That(scored, Is.True);
        }

        [Test]
        public void TakeOnRequiresAnActualNearbyOpponent()
        {
            var original = Context(FootballActionKind.Dribble);
            var context = new FootballActionContext(original.Actor, BallSituation.Controlled, 0, 0, 1, Array.Empty<PassActor>());
            Assert.That(FootballActionResolver.IsAvailable(context, FootballActionKind.Dribble, "take-on", null), Is.False);
            Assert.That(FootballActionResolver.IsAvailable(context, FootballActionKind.Dribble, "carry", null), Is.True);
        }

        [Test]
        public void LowSkillsKeepFeasibleAerialActionsAvailable()
        {
            var definitions = PrototypeContent.Load();
            var user = definitions.players.Single(p => p.id == definitions.userPlayerId);
            user.attributes = definitions.footballAttributes.Select(a => new ValueOverride { id = a.id, value = a.minimum }).ToArray();
            var career = CareerState.Create(definitions);
            using (var world = new OpenPlayWorld(career, new MatchSession("low-header", 1), career.UserPlayerId, true, 2.6))
            {
                Assert.That(world.PendingDecision.Actions.Any(p => p.Kind == FootballActionKind.HeaderPass), Is.True);
                Assert.That(world.PendingDecision.Actions.Any(p => p.Kind == FootballActionKind.HeaderShot), Is.True);
                Assert.That(world.PendingDecision.Actions.Any(p => p.Kind == FootballActionKind.Clearance), Is.True);
            }
        }

        [Test]
        public void CrossCreatesAnExplicitArrivalAndAiResolvesItsOwnNextAction()
        {
            var career = PrototypeContent.CreateCareer(); bool arrived = false;
            for (ulong seed = 1; seed < 30 && !arrived; seed++)
            {
                var session = new MatchSession("cross", seed);
                using (var world = new OpenPlayWorld(career, session, "harbour-player-09", true))
                {
                    var decision = world.PendingDecision;
                    var option = decision.Actions.First(p => p.Kind == FootballActionKind.Cross && p.Variant == "lofted");
                    var result = world.CommitFootballDecision(decision.Id, option.OptionId);
                    for (int i = 0; i < 500 && world.BallState == BallPhase.Travelling; i++) session.Advance(TimeSpan.FromMilliseconds(50));
                    if (!result.Action.Succeeded) continue;
                    arrived = true;
                    Assert.That(world.BallState, Is.EqualTo(BallPhase.Incoming));
                    Assert.That(world.IncomingPlayerId, Is.EqualTo(option.Receiver.Id));
                    Assert.That(world.BallHeight, Is.EqualTo(1.8));
                    Assert.That(session.DrawCount, Is.EqualTo(1));
                    session.Advance(TimeSpan.FromMilliseconds(50));
                    Assert.That(session.DrawCount, Is.EqualTo(2));
                    Assert.That(world.LastFootballAction.PlayerId, Is.EqualTo(option.Receiver.Id));
                }
            }
            Assert.That(arrived, Is.True);
        }

        [TestCase(FootballActionKind.Dribble)] [TestCase(FootballActionKind.Shot)] [TestCase(FootballActionKind.Clearance)]
        public void ControlledActionsFinishOnceAndPauseFreezesTheirFlight(FootballActionKind kind)
        {
            var career = PrototypeContent.CreateCareer(); var session = new MatchSession("controlled", 42);
            using (var world = new OpenPlayWorld(career, session, career.UserPlayerId, true))
            {
                var decision = world.PendingDecision; var option = decision.Actions.First(p => p.Kind == kind);
                var result = world.CommitFootballDecision(decision.Id, option.OptionId);
                session.Advance(TimeSpan.FromMilliseconds(100));
                world.TogglePause(); var ball = world.BallPosition; double height = world.BallHeight;
                session.Advance(TimeSpan.FromHours(1));
                Assert.That(world.BallPosition, Is.EqualTo(ball)); Assert.That(world.BallHeight, Is.EqualTo(height));
                world.TogglePause();
                for (int i = 0; i < 500 && world.BallState == BallPhase.Travelling; i++) session.Advance(TimeSpan.FromMilliseconds(50));
                Assert.That(world.BallState, Is.Not.EqualTo(BallPhase.Travelling));
                Assert.That(session.DrawCount, Is.EqualTo(1));
                Assert.That(world.LastFootballAction, Is.SameAs(result));
                if (result.ReceivingPlayerId != null) Assert.That(world.OwnerId, Is.EqualTo(result.ReceivingPlayerId));
            }
        }
    }
}
