using System;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace FootballRpg.Tests
{
    public sealed class CharacterCreationTests
    {
        private GameDefinitions content;
        private CreationRules rules;
        private CharacterDraft draft;
        [SetUp] public void Setup()
        {
            content = PrototypeContent.Load();
            rules = JsonUtility.FromJson<CreationRules>(Resources.Load<TextAsset>("CharacterCreation").text);
            draft = CharacterCreation.NewDraft(content);
            Spend(content.footballAttributes, draft.attributes, rules.footballBudget);
            Spend(content.personalityTraits, draft.traits, rules.personalityBudget);
        }
        private static void Spend(ValueDefinition[] definitions, ValueOverride[] values, int budget)
        {
            while (budget > 0) foreach (var v in values)
                if (budget > 0 && v.value < definitions.Single(d => d.id == v.id).maximum) { v.value++; budget--; }
        }
        [TestCaseSource(typeof(PositionIds), nameof(PositionIds.All))]
        public void EveryPositionStartsInSmallClubWithOwnedSerializableProfile(string position)
        {
            draft.positionId = position; draft.name = "  Test Prospect  "; draft.preferredFoot = "left";
            draft.appearanceId = "placeholder-4"; draft.heightCm = 196; draft.weightKg = 89;
            draft = JsonUtility.FromJson<CharacterDraft>(JsonUtility.ToJson(draft));
            var career = CharacterCreation.CreateCareer(content, draft, rules);
            Assert.That(career.UserPlayer.Name, Is.EqualTo("Test Prospect"));
            Assert.That(career.UserPlayer.PrimaryPositionId, Is.EqualTo(position));
            Assert.That(career.UserPlayer.HeightCm, Is.EqualTo(196));
            Assert.That(career.UserPlayer.WeightKg, Is.EqualTo(89));
            Assert.That(career.UserPlayer.PreferredFoot, Is.EqualTo("left"));
            Assert.That(career.UserPlayer.AppearanceId, Is.EqualTo("placeholder-4"));
            Assert.That(career.UserPlayer.ClubId, Is.EqualTo(content.fixture.homeClubId));
            using (var world = new OpenPlayWorld(career, new MatchSession("created", 42)))
            {
                Assert.That(world.Players.Count, Is.EqualTo(22));
                Assert.That(world.Players.Single(p => p.Player.Id == career.UserPlayerId).PositionRoleId, Is.EqualTo(position));
            }
            int saved = career.UserPlayer.FootballAttributes[draft.attributes[0].id];
            draft.attributes[0].value++;
            Assert.That(career.UserPlayer.FootballAttributes[draft.attributes[0].id], Is.EqualTo(saved));
            Assert.That(content.players.Any(p => p.name == "Test Prospect"), Is.False);
        }
        [TestCase(-1)] [TestCase(1)]
        public void BothBudgetsMustBeSpentIndependently(int delta)
        {
            draft.attributes[0].value += delta;
            draft.traits[0].value -= delta;
            Assert.Throws<ArgumentException>(() => CharacterCreation.CreateCareer(content, draft, rules));
        }
        [TestCase("name")] [TestCase("age")] [TestCase("height")] [TestCase("weight")] [TestCase("position")]
        [TestCase("appearance")] [TestCase("foot")] [TestCase("duplicate")] [TestCase("missing")] [TestCase("unknown")]
        [TestCase("below")] [TestCase("above")]
        public void RejectInvalidDrafts(string field)
        {
            switch(field)
            {
                case "name": draft.name = " "; break;
                case "age": draft.age = 40; break;
                case "height": draft.heightCm = float.NaN; break;
                case "weight": draft.weightKg = float.PositiveInfinity; break;
                case "position": draft.positionId = "bad"; break;
                case "appearance": draft.appearanceId = "bad"; break;
                case "foot": draft.preferredFoot = "bad"; break;
                case "duplicate": draft.attributes[0].id = draft.attributes[1].id; break;
                case "missing": draft.traits = null; break;
                case "unknown": draft.traits[0].id = "bad"; break;
                case "below": draft.attributes[0].value = 0; break;
                case "above": draft.attributes[0].value = 101; break;
            }
            Assert.Throws<ArgumentException>(() => CharacterCreation.CreateCareer(content, draft, rules));
        }
        [Test] public void ZeroBudgetAndMaximumBudgetAreConfigurable()
        {
            foreach (bool maximum in new[] { false, true })
            {
                rules.footballBudget = maximum ? content.footballAttributes.Sum(d => d.maximum - d.minimum) : 0;
                rules.personalityBudget = maximum ? content.personalityTraits.Sum(d => d.maximum - d.minimum) : 0;
                draft = CharacterCreation.NewDraft(content);
                Spend(content.footballAttributes, draft.attributes, rules.footballBudget);
                Spend(content.personalityTraits, draft.traits, rules.personalityBudget);
                Assert.DoesNotThrow(() => CharacterCreation.CreateCareer(content, draft, rules));
            }
        }
        [Test] public void PhysicalProfileChangesExistingHeadingChance()
        {
            float Chance(float height)
            {
                draft.heightCm = height;
                var career = CharacterCreation.CreateCareer(content, draft, rules);
                var actor = new PassActor(career.UserPlayer, new PitchPoint(40, 0), new PitchPoint(1, 0));
                var context = new FootballActionContext(actor, BallSituation.Arriving, 1.8, 5, 1, Array.Empty<PassActor>());
                return FootballActionResolver.Preview(context, FootballActionKind.HeaderShot, FootballActionResolver.Variants(FootballActionKind.HeaderShot).First()).Chance;
            }
            Assert.That(Chance(200), Is.GreaterThan(Chance(160)));
        }
        [Test] public void UnconventionalGoalkeeperCanInvestInOutfieldSkills()
        {
            draft.positionId = "gk";
            var high = draft.attributes.Single(v => v.id == "finishing");
            var low = draft.attributes.Single(v => v.id == "handling");
            int transfer = low.value - 1; low.value -= transfer; high.value += transfer;
            Assert.DoesNotThrow(() => CharacterCreation.CreateCareer(content, draft, rules));
        }
    }
}
