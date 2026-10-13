using System;
using System.Linq;
using System.Reflection;
using FootballRpg.Content;
using FootballRpg.Simulation;
using NUnit.Framework;

namespace FootballRpg.Tests
{
    [Category("FootballVerification")]
    public sealed class GoalkeeperTests
    {
        internal static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        internal static FootballerMotion Player(OpenPlayWorld w, string id) => w.Players.Single(p => p.Player.Id == id);
        internal static void Call(OpenPlayWorld w, string method, params object[] args) => typeof(OpenPlayWorld).GetMethod(method, Hidden).Invoke(w, args);
        internal static KeeperContext Context(KeeperSituation situation, HandlingRules rules = null, bool holding = false, double height = .5, double speed = 15, CareerState career = null)
        {
            var c = career ?? PrototypeContent.CreateCareer();
            var keeper = new PassActor(c.Players["harbour-player-01"], new PitchPoint(-49, 0), new PitchPoint(1, 0));
            return new KeeperContext(keeper, situation, new PitchPoint(situation == KeeperSituation.Distribution ? -49 : -47, 0), height, speed, 1,
                rules ?? new HandlingRules(), holding, new[] { new PassActor(c.Players["meadow-player-11"], new PitchPoint(-43, 0), new PitchPoint(-1, 0)) });
        }
        private static KeeperSituation Situation(KeeperAction k) => GoalkeeperResolver.IsDistribution(k) || k == KeeperAction.Gather || k == KeeperAction.ReleaseToFeet ? KeeperSituation.Distribution
            : k == KeeperAction.Claim || k == KeeperAction.Punch ? KeeperSituation.Cross : k == KeeperAction.HoldSave || k == KeeperAction.Parry ? KeeperSituation.Shot : KeeperSituation.OneOnOne;
        private static PassActor Recipient(CareerState c = null) => new PassActor((c ?? PrototypeContent.CreateCareer()).Players["harbour-player-02"], new PitchPoint(-32, -4), new PitchPoint(1, 0));

        [TestCase(KeeperAction.SetPosition)] [TestCase(KeeperAction.RushOut)] [TestCase(KeeperAction.HoldSave)] [TestCase(KeeperAction.Parry)]
        [TestCase(KeeperAction.Claim)] [TestCase(KeeperAction.Punch)] [TestCase(KeeperAction.Gather)] [TestCase(KeeperAction.Roll)]
        [TestCase(KeeperAction.Throw)] [TestCase(KeeperAction.ShortKick)] [TestCase(KeeperAction.LongKick)] [TestCase(KeeperAction.ReleaseToFeet)]
        public void EveryKeeperActionUsesExactChanceAndSkillAttribution(KeeperAction kind)
        {
            var c = Context(Situation(kind), holding: kind == KeeperAction.Roll || kind == KeeperAction.Throw || kind == KeeperAction.ReleaseToFeet, height: kind == KeeperAction.Claim || kind == KeeperAction.Punch ? 1.8 : .3);
            var p = GoalkeeperResolver.Preview(c, kind, GoalkeeperResolver.IsDistribution(kind) ? Recipient() : null);
            var match = new MatchSession("keeper-chance", 42); int success = 0;
            for (int i = 0; i < 1500; i++)
            {
                var fact = GoalkeeperResolver.Commit(match, "sample-" + i, p, true);
                Assert.That(fact.Action.SuccessProbability, Is.EqualTo(p.Percent / 100f));
                Assert.That(fact.Action.Succeeded, Is.EqualTo(fact.Action.Roll < p.Chance));
                Assert.That(fact.SkillAttribution.Values.Sum(), Is.EqualTo(1).Within(.0001));
                if (fact.Action.Succeeded) success++;
            }
            Assert.That(match.DrawCount, Is.EqualTo(1500));
            Assert.That(success / 1500.0, Is.EqualTo(p.Chance).Within(6 * Math.Sqrt(p.Chance * (1-p.Chance) / 1500)));
        }
        [TestCase(1)] [TestCase(-1)]
        public void HandlingAreaAndTouchHistoryEnforceRestrictions(int direction)
        {
            var c = PrototypeContent.CreateCareer();
            var keeper = new PassActor(c.Players["harbour-player-01"], new PitchPoint(-49 * direction, 0), new PitchPoint(direction, 0));
            var rules = new HandlingRules(); var ball = keeper.Position;
            Assert.That(rules.Restriction(keeper, ball, direction), Is.Null);
            Assert.That(HandlingRules.InOwnArea(new PitchPoint(-36 * direction, 20.16), direction), Is.True);
            Assert.That(HandlingRules.InOwnArea(new PitchPoint(-35.99 * direction, 0), direction), Is.False);
            Assert.That(HandlingRules.InOwnArea(new PitchPoint(-45 * direction, 20.17), direction), Is.False);
            Assert.That(HandlingRules.InOwnArea(new PitchPoint(49 * direction, 0), direction), Is.False);
            rules.RecordTouch("harbour-player-02", keeper.ClubId, BallTouchKind.DeliberateKick, keeper.Id);
            Assert.That(rules.Restriction(keeper, ball, direction), Is.Not.Null);
            rules.RecordTouch("harbour-player-02", keeper.ClubId, BallTouchKind.ThrowIn);
            Assert.That(rules.Restriction(keeper, ball, direction), Is.Not.Null);
            rules.RecordTouch("harbour-player-02", keeper.ClubId, BallTouchKind.Header, keeper.Id);
            Assert.That(rules.Restriction(keeper, ball, direction), Is.Null);
            rules.RecordTouch(keeper.Id, keeper.ClubId, BallTouchKind.KeeperRelease);
            Assert.That(rules.Restriction(keeper, ball, direction), Is.Not.Null);
            rules.RecordTouch("meadow-player-11", c.Players["meadow-player-11"].ClubId, BallTouchKind.Other);
            Assert.That(rules.Restriction(keeper, ball, direction), Is.Null);
        }
        [Test]
        public void UnlawfulOrUnreachableActionsAreUnavailableButFeetDistributionRemains()
        {
            var rules = new HandlingRules(); var c = Context(KeeperSituation.Distribution);
            rules.RecordTouch("harbour-player-02", c.Keeper.ClubId, BallTouchKind.DeliberateKick, c.Keeper.Id);
            var backpass = Context(KeeperSituation.Distribution, rules);
            Assert.That(GoalkeeperResolver.IsAvailable(backpass, KeeperAction.Gather), Is.False);
            Assert.That(GoalkeeperResolver.IsAvailable(backpass, KeeperAction.ShortKick, Recipient()), Is.True);
            Assert.Throws<ArgumentException>(() => GoalkeeperResolver.Preview(backpass, KeeperAction.Gather));
            Assert.That(GoalkeeperResolver.IsAvailable(Context(KeeperSituation.Shot, height: 5), KeeperAction.HoldSave), Is.False);
            var distant = new KeeperContext(c.Keeper, KeeperSituation.Shot, new PitchPoint(-36, 18), 1, 20, 1, new HandlingRules(), false, c.Opponents);
            Assert.That(GoalkeeperResolver.IsAvailable(distant, KeeperAction.Parry), Is.False);
            Assert.That(GoalkeeperResolver.IsAvailable(Context(KeeperSituation.Cross, height: .3), KeeperAction.Punch), Is.False);
        }
        [Test]
        public void AttributesTrajectoryPressureAndPhysicalProfileChangeTheProbability()
        {
            var normal = GoalkeeperResolver.Preview(Context(KeeperSituation.Shot), KeeperAction.HoldSave);
            var fast = GoalkeeperResolver.Preview(Context(KeeperSituation.Shot, speed: 35), KeeperAction.HoldSave);
            Assert.That(fast.Percent, Is.LessThan(normal.Percent));
            var definitions = PrototypeContent.Load(); var keeper = definitions.players.Single(p => p.id == "harbour-player-01");
            keeper.heightCm = 205; keeper.attributes = definitions.footballAttributes.Select(a => new ValueOverride { id = a.id, value = a.maximum }).ToArray();
            var strong = GoalkeeperResolver.Preview(Context(KeeperSituation.Shot, career: CareerState.Create(definitions)), KeeperAction.HoldSave);
            Assert.That(strong.Percent, Is.GreaterThan(normal.Percent));
            keeper.heightCm = 165; keeper.attributes = definitions.footballAttributes.Select(a => new ValueOverride { id = a.id, value = a.minimum }).ToArray();
            var weak = GoalkeeperResolver.Preview(Context(KeeperSituation.Shot, career: CareerState.Create(definitions)), KeeperAction.HoldSave);
            Assert.That(weak.Percent, Is.LessThan(normal.Percent));
            Assert.That(GoalkeeperResolver.IsAvailable(Context(KeeperSituation.Shot, career: CareerState.Create(definitions)), KeeperAction.Parry), Is.True);
        }
        internal static void ArrangeAttack(OpenPlayWorld w, bool cross = false, bool distantKeeper = false)
        {
            foreach (var p in w.Players) DefenseTests.Place(p, new PitchPoint(p.IsHome ? 25 : 30, 25));
            DefenseTests.Place(Player(w, "harbour-player-01"), new PitchPoint(distantKeeper ? -20 : -49, 0));
            DefenseTests.Place(Player(w, "meadow-player-11"), new PitchPoint(cross ? -44 : -30, cross ? 20 : 0));
            DefenseTests.Place(Player(w, "meadow-player-10"), new PitchPoint(-46, 0));
            DefenseTests.Possess(w, "meadow-player-11");
        }
        internal static FootballActionResolved StartAttack(OpenPlayWorld w, bool cross = false)
        {
            var actor = Player(w, "meadow-player-11");
            var context = new FootballActionContext(new PassActor(actor.Player, actor.Position, actor.Facing), BallSituation.Controlled, 0, 0, -1,
                w.Players.Where(p => p.IsHome).Select(p => new PassActor(p.Player, p.Position, p.Facing)));
            var receiver = Player(w, "meadow-player-10");
            var preview = FootballActionResolver.Preview(context, cross ? FootballActionKind.Cross : FootballActionKind.Shot, cross ? "lofted" : "placed",
                cross ? new PassActor(receiver.Player, receiver.Position, receiver.Facing) : null);
            Call(w, "StartAutomaticFootballAction", actor, preview);
            return w.LastFootballAction;
        }
        [TestCase(KeeperAction.HoldSave)] [TestCase(KeeperAction.Parry)] [TestCase(KeeperAction.Claim)] [TestCase(KeeperAction.Punch)]
        public void SaveAndAerialDecisionsPauseAndApplySuccessAndFailureOnce(KeeperAction kind)
        {
            bool cross = kind == KeeperAction.Claim || kind == KeeperAction.Punch;
            bool success = false, failure = false; var c = PrototypeContent.CreateCareer();
            for (ulong seed = 1; seed < 300 && !(success && failure); seed++)
            {
                var match = new MatchSession("keeper-world", seed);
                using (var w = new OpenPlayWorld(c, match, "harbour-player-01"))
                {
                    ArrangeAttack(w, cross); var attack = StartAttack(w, cross);
                    if (!attack.Action.Succeeded) continue;
                    for (int i = 0; i < 500 && w.BallState == BallPhase.Travelling; i++) match.Advance(TimeSpan.FromMilliseconds(50));
                    Assert.That(w.PendingKeeper, Is.Not.Null); Assert.That(w.AwayGoals, Is.Zero);
                    var d = w.PendingKeeper; var p = d.Options.Single(x => x.Kind == kind); long draws = match.DrawCount;
                    w.TogglePause(); match.Advance(TimeSpan.FromHours(1)); Assert.That(match.Tick, Is.EqualTo(d.Tick));
                    Assert.Throws<InvalidOperationException>(() => w.CommitKeeperDecision("stale", p.OptionId));
                    Assert.Throws<ArgumentException>(() => w.CommitKeeperDecision(d.Id, "invalid"));
                    var fact = w.CommitKeeperDecision(d.Id, p.OptionId); var ball = w.BallPosition; int goals = w.AwayGoals;
                    Assert.That(w.CommitKeeperDecision(d.Id, p.OptionId), Is.SameAs(fact));
                    Assert.That(w.BallPosition, Is.EqualTo(ball)); Assert.That(w.AwayGoals, Is.EqualTo(goals));
                    Assert.That(match.DrawCount, Is.EqualTo(draws + 1));
                    if (fact.Action.Succeeded)
                    {
                        success = true; Assert.That(w.AwayGoals, Is.Zero);
                        if (kind == KeeperAction.HoldSave || kind == KeeperAction.Claim)
                        { Assert.That(w.HoldingKeeperId, Is.EqualTo("harbour-player-01")); Assert.That(w.PendingKeeper.Context.Situation, Is.EqualTo(KeeperSituation.Distribution)); }
                        else Assert.That(w.BallState, Is.EqualTo(BallPhase.Loose));
                    }
                    else
                    {
                        failure = true;
                        if (cross) Assert.That(w.BallState, Is.EqualTo(BallPhase.Incoming));
                        else { Assert.That(w.AwayGoals, Is.EqualTo(1)); Assert.That(w.BallState, Is.EqualTo(BallPhase.OutOfPlay)); }
                    }
                }
            }
            Assert.That(success && failure, Is.True);
        }
        [TestCase(KeeperAction.Roll)] [TestCase(KeeperAction.Throw)] [TestCase(KeeperAction.ShortKick)] [TestCase(KeeperAction.LongKick)]
        public void DistributionAnimatesFrozenResultAndPauseDoesNotReroll(KeeperAction kind)
        {
            var c = PrototypeContent.CreateCareer(); bool success = false, failure = false;
            for (ulong seed = 1; seed < 200 && !(success && failure); seed++)
            {
                var match = new MatchSession("distribution", seed);
                using (var w = new OpenPlayWorld(c, match, "harbour-player-01"))
                {
                    typeof(OpenPlayWorld).GetProperty("HoldingKeeperId").SetValue(w, "harbour-player-01");
                    DefenseTests.Possess(w, "harbour-player-01");
                    var d = w.PendingKeeper; var p = d.Options.First(x => x.Kind == kind);
                    var fact = w.CommitKeeperDecision(d.Id, p.OptionId);
                    w.TogglePause(); var ball = w.BallPosition; match.Advance(TimeSpan.FromHours(1)); Assert.That(w.BallPosition, Is.EqualTo(ball));
                    w.TogglePause();
                    for (int i = 0; i < 500 && w.BallState == BallPhase.Travelling; i++) match.Advance(TimeSpan.FromMilliseconds(50));
                    Assert.That(match.DrawCount, Is.EqualTo(1)); Assert.That(w.HoldingKeeperId, Is.Null);
                    if (fact.Action.Succeeded) { success = true; Assert.That(w.OwnerId, Is.EqualTo(p.Receiver.Id)); }
                    else { failure = true; Assert.That(w.BallState, Is.EqualTo(BallPhase.Loose)); }
                }
            }
            Assert.That(success && failure, Is.True);
        }
        [TestCase(KeeperAction.RushOut)] [TestCase(KeeperAction.SetPosition)]
        public void OneOnOneHasOwnPromptInsteadOfAnOutfieldDefenseChoice(KeeperAction kind)
        {
            var c = PrototypeContent.CreateCareer(); var match = new MatchSession("one-on-one", 42);
            using (var w = new OpenPlayWorld(c, match, "harbour-player-01"))
            {
                ArrangeAttack(w); DefenseTests.Place(Player(w, "meadow-player-11"), new PitchPoint(-42, 0)); DefenseTests.Possess(w, "meadow-player-11");
                match.Advance(TimeSpan.FromMilliseconds(50));
                Assert.That(w.PendingKeeper.Context.Situation, Is.EqualTo(KeeperSituation.OneOnOne)); Assert.That(w.PendingDefense, Is.Null);
                var d = w.PendingKeeper; var p = d.Options.Single(x => x.Kind == kind);
                w.CommitKeeperDecision(d.Id, p.OptionId); Assert.That(match.DrawCount, Is.EqualTo(1));
            }
        }
        [Test]
        public void UnreachableShotScoresWithoutAskingKeeperOrTakingAnotherDraw()
        {
            var c = PrototypeContent.CreateCareer(); bool reached = false;
            for (ulong seed = 1; seed < 100 && !reached; seed++)
            {
                var match = new MatchSession("unreachable", seed);
                using (var w = new OpenPlayWorld(c, match, "harbour-player-01"))
                {
                    ArrangeAttack(w, distantKeeper: true); var shot = StartAttack(w);
                    if (!shot.Action.Succeeded) continue;
                    for (int i = 0; i < 500 && w.BallState == BallPhase.Travelling; i++) match.Advance(TimeSpan.FromMilliseconds(50));
                    Assert.That(w.PendingKeeper, Is.Null); Assert.That(w.AwayGoals, Is.EqualTo(1)); Assert.That(match.DrawCount, Is.EqualTo(1)); reached = true;
                }
            }
            Assert.That(reached, Is.True);
        }
        [Test]
        public void KeeperOutsideAreaCanDefendWithFeetButNeverGather()
        {
            var c = PrototypeContent.CreateCareer(); var match = new MatchSession("outside-area", 42);
            using (var w = new OpenPlayWorld(c, match, "harbour-player-01"))
            {
                ArrangeAttack(w); DefenseTests.Place(Player(w, "harbour-player-01"), new PitchPoint(-32, 0));
                DefenseTests.Place(Player(w, "meadow-player-11"), new PitchPoint(-31, 0)); DefenseTests.Possess(w, "meadow-player-11");
                match.Advance(TimeSpan.FromMilliseconds(50));
                Assert.That(w.PendingKeeper, Is.Null); Assert.That(w.PendingDefense.Options.Any(p => p.Kind == DefensiveAction.Tackle), Is.True);
            }
        }
        [Test]
        public void LooseBallRushOffersBothOutcomesAndNoRepeatHandlingAfterRelease()
        {
            var c = PrototypeContent.CreateCareer(); bool success = false, failure = false;
            for (ulong seed = 1; seed < 100 && !(success && failure); seed++)
            {
                var match = new MatchSession("loose-rush", seed);
                using (var w = new OpenPlayWorld(c, match, "harbour-player-01"))
                {
                    ArrangeAttack(w); Call(w, "MakeLoose");
                    typeof(OpenPlayWorld).GetProperty("BallPosition").SetValue(w, new PitchPoint(-44, 0));
                    match.Advance(TimeSpan.FromMilliseconds(50));
                    var d = w.PendingKeeper; Assert.That(d.Context.Situation, Is.EqualTo(KeeperSituation.LooseBall));
                    var choice = d.Options.Single(p => p.Kind == KeeperAction.RushOut);
                    var result = w.CommitKeeperDecision(d.Id, choice.OptionId);
                    if (result.Action.Succeeded)
                    {
                        success = true; Assert.That(w.HoldingKeeperId, Is.EqualTo("harbour-player-01"));
                        var distribution = w.PendingKeeper; var kick = distribution.Options.First(p => p.Kind == KeeperAction.ShortKick);
                        w.CommitKeeperDecision(distribution.Id, kick.OptionId);
                        Assert.That(w.Handling.Restriction(new PassActor(Player(w, "harbour-player-01").Player, new PitchPoint(-44, 0), new PitchPoint(1,0)), new PitchPoint(-44,0), 1), Is.Not.Null);
                    }
                    else { failure = true; Assert.That(w.HoldingKeeperId, Is.Null); Assert.That(w.PendingKeeper, Is.Null); }
                }
            }
            Assert.That(success && failure, Is.True);
        }

        [Test]
        public void HandControlIsProtectedFromTacklesAndDefensivePrompts()
        {
            var c = PrototypeContent.CreateCareer(); var match = new MatchSession("held-protection", 42);
            using (var w = new OpenPlayWorld(c, match, c.UserPlayerId))
            {
                DefenseTests.Place(Player(w, "meadow-player-01"), new PitchPoint(48, 0));
                DefenseTests.Place(Player(w, c.UserPlayerId), new PitchPoint(47, 0));
                typeof(OpenPlayWorld).GetProperty("HoldingKeeperId").SetValue(w, "meadow-player-01");
                DefenseTests.Possess(w, "meadow-player-01");
                match.Advance(TimeSpan.FromMilliseconds(250));
                Assert.That(w.OwnerId, Is.EqualTo("meadow-player-01")); Assert.That(w.HasPendingDecision, Is.False); Assert.That(match.DrawCount, Is.Zero);
            }
        }
        [Test]
        public void DroppingToFeetKeepsFootballActionsButPreventsPickingUpAgain()
        {
            var c = PrototypeContent.CreateCareer(); bool dropped = false;
            for (ulong seed = 1; seed < 100 && !dropped; seed++)
            {
                var match = new MatchSession("drop-to-feet", seed);
                using (var w = new OpenPlayWorld(c, match, "harbour-player-01"))
                {
                    typeof(OpenPlayWorld).GetProperty("HoldingKeeperId").SetValue(w, "harbour-player-01"); DefenseTests.Possess(w, "harbour-player-01");
                    var d = w.PendingKeeper; var p = d.Options.Single(x => x.Kind == KeeperAction.ReleaseToFeet);
                    if (!w.CommitKeeperDecision(d.Id, p.OptionId).Action.Succeeded) continue;
                    dropped = true; Assert.That(w.HoldingKeeperId, Is.Null); Assert.That(w.PendingDecision.Actions.Any(a => a.Kind == FootballActionKind.Dribble), Is.True);
                    Assert.That(w.PendingKeeper.Options.Any(x => x.Kind == KeeperAction.Gather), Is.False);
                }
            }
            Assert.That(dropped, Is.True);
        }
        [Test]
        public void DribblingABackpassDoesNotTurnItIntoLegalHandling()
        {
            var c = PrototypeContent.CreateCareer(); var match = new MatchSession("backpass-dribble", 42);
            using (var w = new OpenPlayWorld(c, match, "harbour-player-01"))
            {
                w.Handling.RecordTouch("harbour-player-02", c.Players["harbour-player-01"].ClubId, BallTouchKind.DeliberateKick, "harbour-player-01");
                DefenseTests.Possess(w, "harbour-player-01"); var d = w.PendingDecision;
                var dribble = d.Actions.First(a => a.Kind == FootballActionKind.Dribble);
                w.CommitFootballDecision(d.Id, dribble.OptionId);
                Assert.That(w.PendingKeeper, Is.Null);
                Assert.That(w.Handling.Restriction(new PassActor(Player(w, "harbour-player-01").Player, new PitchPoint(-49, 0), new PitchPoint(1, 0)), new PitchPoint(-49, 0), 1), Is.Not.Null);
            }
        }

        [Test]
        public void BackpassReceptionDoesNotLoseItsHandlingRestriction()
        {
            var c = PrototypeContent.CreateCareer(); bool received = false;
            for (ulong seed = 1; seed < 100 && !received; seed++)
            {
                var match = new MatchSession("backpass", seed);
                using (var w = new OpenPlayWorld(c, match, "harbour-player-01"))
                {
                    var passer = Player(w, w.OwnerId); var preview = w.PreviewPass("harbour-player-01", PassKind.ToFeet);
                    var fact = PassResolver.Commit(match, "backpass", preview, true);
                    if (!fact.Action.Succeeded) continue;
                    Call(w, "BeginFlight", fact, passer);
                    for (int i = 0; i < 500 && w.BallState == BallPhase.Travelling; i++) match.Advance(TimeSpan.FromMilliseconds(50));
                    Assert.That(w.PendingKeeper.Context.Restriction, Is.Not.Null);
                    Assert.That(w.PendingKeeper.Options.All(p => p.Kind == KeeperAction.ShortKick || p.Kind == KeeperAction.LongKick), Is.True);
                    Assert.That(w.PendingKeeper.Options.Count, Is.GreaterThan(0)); received = true;
                }
            }
            Assert.That(received, Is.True);
        }
    }
}
