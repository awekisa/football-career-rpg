using System;
using System.Collections.Generic;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Simulation;
using NUnit.Framework;

namespace FootballRpg.Tests
{
    public sealed class SkillProgressionTests
    {
        private CareerState career;
        private SkillProgression xp;
        private string match;
        private static readonly IReadOnlyDictionary<string, float> Passing = new Dictionary<string, float> { ["short-passing"] = .7f, ["decisions"] = .3f };
        private static readonly IReadOnlyDictionary<string, float> Single = new Dictionary<string, float> { ["short-passing"] = 1 };
        [SetUp] public void Setup() { career = PrototypeContent.CreateCareer(); xp = career.Progression; match = xp.BeginMatch(); }
        private ActionResolved Fact(string id, bool success, float chance = .9f, string player = null, string action = "pass-to-feet", string target = "receiver") =>
            new ActionResolved(id, player ?? career.UserPlayerId, 1, action, target, chance, success ? 0 : .999f, success ? "accurate" : "inaccurate");

        [Test] public void RewardsUseWeightedAttributionAndLeavePersonalityAndConditionUntouched()
        {
            var p = career.UserPlayer; var before = p.PersonalityTraits.ToArray();
            var condition = new[] { p.Confidence, p.Morale, p.Fatigue };
            var change = xp.Apply(match, Fact("success", true), Passing);
            Assert.That(change.Deltas["short-passing"], Is.EqualTo(xp.Rules.OutcomeXp(.9f, true, 1) * .7f).Within(.00001));
            Assert.That(change.Deltas["decisions"], Is.GreaterThan(0));
            Assert.That(p.PersonalityTraits.ToArray(), Is.EqualTo(before));
            Assert.That(new[] { p.Confidence, p.Morale, p.Fatigue }, Is.EqualTo(condition));
        }
        [Test] public void ThresholdsGrowAndFailuresCanCrossMultipleLevels()
        {
            var strong = CareerState.Create(PrototypeContent.Load(), new ProgressionRules(baseThreshold: 1, thresholdIncrease: 1, failureXp: 1000));
            var progression = strong.Progression; var skill = progression.ForPlayer(strong.UserPlayerId)["short-passing"];
            int before = skill.Level;
            Assert.That(progression.Rules.Cost(20, 1), Is.GreaterThan(progression.Rules.Cost(10, 1)));
            progression.Apply(progression.BeginMatch(), Fact("loss", false), Single, 3);
            Assert.That(skill.Level, Is.LessThan(before - 1));
            Assert.That(strong.UserPlayer.FootballAttributes["short-passing"], Is.EqualTo(skill.Level));
            Assert.That(skill.WithinLevelXp, Is.GreaterThanOrEqualTo(0).And.LessThan(skill.NextLevelXp));
        }
        [Test] public void GainCanCrossMultipleLevelsAndUpdatesFutureActionSnapshots()
        {
            var fast = CareerState.Create(PrototypeContent.Load(), new ProgressionRules(baseThreshold: 1, thresholdIncrease: .01, successXp: 1000, skillGainCapPerMatch: 10000));
            int before = fast.UserPlayer.FootballAttributes["short-passing"];
            var snapshot = new PassActor(fast.UserPlayer, new PitchPoint(0, 0), new PitchPoint(1, 0));
            fast.Progression.Apply(fast.Progression.BeginMatch(), Fact("gain", true, .4f), Single, 2);
            Assert.That(fast.UserPlayer.FootballAttributes["short-passing"], Is.GreaterThan(before + 1));
            Assert.That(snapshot.Attributes["short-passing"], Is.EqualTo(before), "Existing choices remain frozen.");
            Assert.That(new PassActor(fast.UserPlayer, new PitchPoint(0, 0), new PitchPoint(1, 0)).Attributes["short-passing"], Is.GreaterThan(before));
        }
        [TestCase(true)] [TestCase(false)] public void ConfiguredContentFloorsAndCapsClampWithoutHiddenOverflow(bool success)
        {
            var data = PrototypeContent.Load();
            foreach (var d in data.footballAttributes) { d.minimum = 10; d.maximum = 60; d.defaultValue = success ? 60 : 10; }
            foreach (var p in data.players) p.attributes = Array.Empty<ValueOverride>();
            var bounded = CareerState.Create(data); var system = bounded.Progression;
            var result = system.Apply(system.BeginMatch(), Fact("bounded", success), Single);
            Assert.That(result.Deltas["short-passing"], Is.Zero);
            var skill = system.ForPlayer(bounded.UserPlayerId)["short-passing"];
            Assert.That(skill.Level, Is.EqualTo(success ? 60 : 10));
            Assert.That(skill.WithinLevelXp, Is.Zero);
        }
        [Test] public void DuplicateEventDoesNotRewardOrConsumeRepetitionAgain()
        {
            var fact = Fact("one", true); var first = xp.Apply(match, fact, Single);
            var total = xp.ForPlayer(career.UserPlayerId)["short-passing"].TotalXp;
            Assert.That(xp.Apply(match, fact, Single), Is.SameAs(first));
            Assert.That(xp.ForPlayer(career.UserPlayerId)["short-passing"].TotalXp, Is.EqualTo(total));
            Assert.That(xp.Changes.Count, Is.EqualTo(1));
            var second = xp.Apply(match, Fact("two", true), Single);
            Assert.That(second.Deltas.Values.Single(), Is.EqualTo(first.Deltas.Values.Single() * .8).Within(.00001));
        }
        [Test] public void ReceiverFailureDoesNotUndoAccuratePassersReward()
        {
            xp.Apply(match, Fact("pass", true), Single);
            var before = xp.ForPlayer(career.UserPlayerId)["short-passing"].TotalXp;
            xp.Apply(match, Fact("receive", false, player: "harbour-player-07", action: "control"), new Dictionary<string, float> { ["ball-control"] = 1 });
            Assert.That(xp.ForPlayer(career.UserPlayerId)["short-passing"].TotalXp, Is.EqualTo(before));
            Assert.That(xp.Changes.Last().PlayerId, Is.EqualTo("harbour-player-07"));
        }
        [Test] public void ChangingTargetsCannotFarmRepeatedSuccessesAndFailuresStillCostXp()
        {
            double previous = double.MaxValue;
            for (int i = 0; i < 12; i++)
            {
                double reward = xp.Apply(match, Fact("repeat" + i, true, target: "target" + i), Single).Deltas.Values.Single();
                Assert.That(reward, Is.LessThanOrEqualTo(previous)); previous = reward;
            }
            Assert.That(previous, Is.Zero);
            Assert.That(xp.Apply(match, Fact("late-failure", false), Single).Deltas.Values.Single(), Is.LessThan(0));
        }
        [Test] public void SkillGainCapStopsAlternatingActionFamilyExploit()
        {
            var system = CareerState.Create(PrototypeContent.Load(), new ProgressionRules(skillGainCapPerMatch: 10)).Progression;
            string scope = system.BeginMatch();
            double gains = 0;
            for (int i = 0; i < 30; i++) gains += system.Apply(scope, Fact("event" + i, true, action: "family" + i), Single).Deltas.Values.Single();
            Assert.That(gains, Is.EqualTo(10).Within(.00001));
        }
        [Test] public void SeparateFixturesRetainXpButHaveIndependentEventAndRepetitionScopes()
        {
            var first = xp.Apply(match, Fact("same-session-action-1", true), Single);
            var before = xp.ForPlayer(career.UserPlayerId)["short-passing"].TotalXp;
            var next = xp.Apply(xp.BeginMatch(), Fact("same-session-action-1", true), Single);
            Assert.That(next.MatchId, Is.Not.EqualTo(first.MatchId));
            Assert.That(next.Deltas.Values.Single(), Is.EqualTo(first.Deltas.Values.Single()).Within(.00001));
            Assert.That(xp.ForPlayer(career.UserPlayerId)["short-passing"].TotalXp, Is.GreaterThan(before));
        }
        [Test] public void ReliableAndMeaningfulRiskyChoicesHavePositiveExpectedProgressWithDifferentVariance()
        {
            double Expected(float p, double significance) => p * xp.Rules.OutcomeXp(p, true, significance) + (1 - p) * xp.Rules.OutcomeXp(p, false, significance);
            Assert.That(Expected(.9f, .5), Is.GreaterThan(0));
            Assert.That(Expected(.4f, 2), Is.GreaterThan(Expected(.9f, .5)));
            Assert.That(xp.Rules.OutcomeXp(.4f, false, 2), Is.LessThan(xp.Rules.OutcomeXp(.9f, false, .5)));
        }
        [TestCase(0)] [TestCase(-1)] [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)]
        public void InvalidRulesAreRejected(double value) => Assert.Throws<ArgumentOutOfRangeException>(() => new ProgressionRules(baseThreshold: value));
        [TestCase("personality")] [TestCase("weight")] [TestCase("sum")] [TestCase("nan")]
        public void InvalidAttributionDoesNotConsumeEvent(string kind)
        {
            var values = new Dictionary<string, float> { [kind == "personality" ? "charisma" : "short-passing"] = kind == "weight" ? -1 : kind == "sum" ? .3f : kind == "nan" ? float.NaN : 1 };
            Assert.Throws<ArgumentException>(() => xp.Apply(match, Fact("invalid", true), values));
            Assert.That(xp.Changes.Count, Is.Zero);
            Assert.That(xp.Apply(match, Fact("invalid", true), Single).Deltas.Values.Single(), Is.GreaterThan(0));
        }
        [TestCase(RestartKind.Penalty, "penalties")] [TestCase(RestartKind.Corner, "corners")]
        [TestCase(RestartKind.DirectFreeKick, "free-kicks")]
        public void SetPieceDevelopmentUsesSpecialistSkillAndDoesNotDoubleCountDelivery(RestartKind restart, string skill)
        {
            using var world = new OpenPlayWorld(career, new MatchSession("setpiece-xp", 42));
            var action = Fact("specialist", false, action: "shot");
            GoalkeeperTests.Call(world, "Progress", action, Passing, 1d, (RestartKind?)restart);
            Assert.That(xp.Changes.Count, Is.Zero, "Practice does not grant match XP.");
            world.StartFixture();
            GoalkeeperTests.Call(world, "Progress", action, Passing, 1d, (RestartKind?)restart);
            var change = xp.Changes.Single();
            Assert.That(change.Deltas.Keys, Is.EquivalentTo(new[] { skill, "decisions" }));
            Assert.That(change.Deltas[skill], Is.LessThan(0));
            GoalkeeperTests.Call(world, "Progress", action, Passing, 1d, (RestartKind?)restart);
            Assert.That(xp.Changes.Count, Is.EqualTo(1));
        }
    }
}
