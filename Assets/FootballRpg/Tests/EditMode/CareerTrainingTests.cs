using System;
using System.Linq;
using System.Reflection;
using FootballRpg.Content;
using FootballRpg.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace FootballRpg.Tests
{
    public sealed class CareerTrainingTests
    {
        private CareerState career;
        [SetUp] public void Setup() { career = PrototypeContent.CreateCareer(); }
        private static CalendarDecision Choose(CareerState c, CalendarChoiceKind kind, string id = null, string focus = null) => c.Calendar.Commit(c.Calendar.Preview(kind, id, focus));
        private static double Xp(CareerState c) => c.Progression.ForPlayer(c.UserPlayerId).Values.Sum(s => s.TotalXp);
        private static TrainingReport Preview(CareerState c, string focus = null) => c.Training.Preview(c.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training", focus));
        [Test] public void AttendingAwardsBroadXpWithAnExactBreakdownAndOneReadinessUpdate()
        {
            var before = Xp(career); var fatigue = career.UserPlayer.Fatigue; var preview = Preview(career, "passing");
            var choice = career.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training", "passing");
            career.Calendar.Commit(choice); var report = career.Training.Results.Single();
            Assert.That(Xp(career) - before, Is.EqualTo(report.TotalXp).Within(1e-6));
            Assert.That(report.TotalXp, Is.EqualTo(preview.TotalXp));
            Assert.That(report.Gains.Count(g => g.Xp > 0), Is.GreaterThan(1));
            Assert.That(report.Gains.Sum(g => g.Share), Is.EqualTo(1).Within(1e-6));
            Assert.That(career.UserPlayer.Fatigue - fatigue, Is.EqualTo(.14).Within(1e-6));
            Assert.That(career.Calendar.Minute, Is.EqualTo(360)); Assert.That(career.Readiness.Training.Count, Is.EqualTo(1));
            foreach (var gain in report.Gains)
                Assert.That(gain.Xp, Is.EqualTo(report.BaseXp * report.ParticipatedSlots * report.ReadinessMultiplier * report.CoachMultiplier * report.StaffMultiplier * report.FacilityMultiplier * gain.Share * gain.LevelMultiplier).Within(1e-6));
            career.Calendar.Commit(choice);
            Assert.That(career.Training.Results.Count, Is.EqualTo(1)); Assert.That(Xp(career) - before, Is.EqualTo(report.TotalXp).Within(1e-6));
            Assert.That(career.Calendar.Minute, Is.EqualTo(360)); Assert.That(career.Readiness.Training.Count, Is.EqualTo(1));
        }
        [TestCase(CalendarChoiceKind.Skip)] [TestCase(CalendarChoiceKind.Wait)] [TestCase(CalendarChoiceKind.Replace)]
        public void SkippedAndReplacedSessionsGrantNoTrainingXp(CalendarChoiceKind kind)
        {
            double before = Xp(career);
            Choose(career, kind, kind == CalendarChoiceKind.Skip ? "mon-training" : kind == CalendarChoiceKind.Replace ? "rest" : null);
            Assert.That(Xp(career), Is.EqualTo(before)); Assert.That(career.Training.Results, Is.Empty);
        }
        [Test] public void AuthoredScheduledFocusAndExplicitChoiceAreIndependentAndPreviewIsInert()
        {
            double before = Xp(career); var fixedChoice = career.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training", "defending");
            for (int i = 0; i < 50; i++) Preview(career, "passing");
            Assert.That(Xp(career), Is.EqualTo(before)); Assert.That(career.Calendar.Minute, Is.Zero);
            career.Calendar.Commit(fixedChoice);
            Assert.That(career.Training.Results[0].FocusName, Is.EqualTo("Defending"));
            while (career.Calendar.Minute < 1440) Choose(career, CalendarChoiceKind.Wait);
            Assert.That(career.Calendar.Preview(CalendarChoiceKind.Attend, "tue-training").Focus.Id, Is.EqualTo("passing"));
            Assert.That(career.Calendar.Preview(CalendarChoiceKind.Attend, "tue-training", "goalkeeping").Focus.Id, Is.EqualTo("goalkeeping"));
        }
        [TestCase("coach")] [TestCase("staff")] [TestCase("facility")] [TestCase("all")]
        public void StrongerClubEnvironmentIncreasesXp(string component)
        {
            CareerState Club(float quality)
            {
                var d = PrototypeContent.Load(); var p = d.players.Single(p => p.id == d.userPlayerId); var club = d.clubs.Single(c => c.id == p.clubId);
                if (component == "staff" || component == "all") club.staffQuality = quality;
                if (component == "facility" || component == "all") club.facilityQuality = quality;
                if (component == "coach" || component == "all") d.coaches.Single(c => c.id == club.coachId).coachingQuality = quality;
                return CareerState.Create(d);
            }
            var small = Club(.1f); var strong = Club(.9f);
            Choose(small, CalendarChoiceKind.Attend, "mon-training"); Choose(strong, CalendarChoiceKind.Attend, "mon-training");
            Assert.That(strong.Training.Results[0].TotalXp, Is.GreaterThan(small.Training.Results[0].TotalXp));
        }
        [Test] public void FatigueReducesActualXpAndUsesThePreWorkloadSnapshot()
        {
            var tired = PrototypeContent.CreateCareer(); career.SetPlayerCondition(career.UserPlayerId, .5f, .5f, 0); tired.SetPlayerCondition(tired.UserPlayerId, .5f, .5f, .8f);
            Choose(career, CalendarChoiceKind.Attend, "mon-training"); Choose(tired, CalendarChoiceKind.Attend, "mon-training");
            Assert.That(career.Training.Results[0].ReadinessMultiplier, Is.EqualTo(1));
            Assert.That(tired.Training.Results[0].TotalXp, Is.LessThan(career.Training.Results[0].TotalXp));
            Assert.That(tired.Training.Results[0].ReadinessMultiplier, Is.GreaterThan(tired.Readiness.TrainingMultiplier(tired.UserPlayerId)));
        }
        [Test] public void HigherLevelsEarnLessXpAndHaveHigherNextLevelCosts()
        {
            CareerState AtLevel(int level)
            {
                var d = PrototypeContent.Load(); d.players.Single(p => p.id == d.userPlayerId).attributes = d.footballAttributes.Select(a => new ValueOverride { id = a.id, value = level }).ToArray();
                return CareerState.Create(d);
            }
            var low = AtLevel(10); var high = AtLevel(90);
            Choose(low, CalendarChoiceKind.Attend, "mon-training"); Choose(high, CalendarChoiceKind.Attend, "mon-training");
            Assert.That(high.Training.Results[0].TotalXp, Is.LessThan(low.Training.Results[0].TotalXp));
            Assert.That(high.Progression.ForPlayer(high.UserPlayerId)["vision"].NextLevelXp, Is.GreaterThan(low.Progression.ForPlayer(low.UserPlayerId)["vision"].NextLevelXp));
        }
        [Test] public void LateTrainingAwardsOnlyActualParticipationAndExtraPracticeSpendsNextSlot()
        {
            Choose(career, CalendarChoiceKind.Attend, "mon-training"); Choose(career, CalendarChoiceKind.ExtraPractice, "extra-practice", "set-pieces");
            Assert.That(career.Calendar.Minute, Is.EqualTo(720)); Assert.That(career.Training.Results.Count, Is.EqualTo(2));
            Assert.That(career.Training.Results[1].FocusName, Is.EqualTo("Set pieces"));
            Choose(career, CalendarChoiceKind.Skip, "mon-media"); Choose(career, CalendarChoiceKind.Replace, "party");
            var preview = career.Training.Preview(career.Calendar.Preview(CalendarChoiceKind.Attend, "tue-training"));
            Choose(career, CalendarChoiceKind.Attend, "tue-training");
            Assert.That(career.Training.Results.Last().ParticipatedSlots, Is.EqualTo(5d / 6));
            Assert.That(career.Training.Results.Last().TotalXp, Is.EqualTo(preview.TotalXp));
        }
        [Test] public void InjuredTrainingAwardsZeroWithoutRaisingStableTraits()
        {
            typeof(PlayerState).GetProperty("InjuryDaysRemaining").SetValue(career.UserPlayer, 1);
            double before = Xp(career); var traits = career.UserPlayer.PersonalityTraits.ToArray();
            Choose(career, CalendarChoiceKind.Attend, "mon-training");
            Assert.That(career.Training.Results[0].TotalXp, Is.Zero); Assert.That(Xp(career), Is.EqualTo(before));
            Assert.That(career.UserPlayer.PersonalityTraits.ToArray(), Is.EqualTo(traits));
        }
        [Test] public void LargeTrainingAwardsUpdateSharedLevelsClampCapsAndNeverChangePersonality()
        {
            var fast = CareerState.Create(PrototypeContent.Load(), trainingRules: new TrainingRules(baseXpPerSlot: 1000000));
            var traits = fast.UserPlayer.PersonalityTraits.ToArray();
            var snapshot = new PassActor(fast.UserPlayer, new PitchPoint(0, 0), new PitchPoint(1, 0));
            Choose(fast, CalendarChoiceKind.Attend, "mon-training");
            var result = fast.Training.Results.Single(); Assert.That(result.Gains.Any(g => g.NewLevel > g.PreviousLevel), Is.True);
            foreach (var g in result.Gains)
            {
                Assert.That(fast.UserPlayer.FootballAttributes[g.SkillId], Is.EqualTo(g.NewLevel));
                Assert.That(g.NewLevel, Is.LessThanOrEqualTo(fast.Progression.ForPlayer(fast.UserPlayerId)[g.SkillId].Cap));
            }
            Assert.That(fast.UserPlayer.PersonalityTraits.ToArray(), Is.EqualTo(traits));
            Assert.That(snapshot.Attributes["vision"], Is.LessThan(fast.UserPlayer.FootballAttributes["vision"]));
            Assert.That(fast.Progression.Changes, Is.Empty, "Training is not a fabricated match action.");
        }
        [TestCaseSource(typeof(PositionIds), nameof(PositionIds.All))]
        public void EveryPositionHasBroadRoleTrainingAndCanChooseOtherPositionSkills(string position)
        {
            var d = PrototypeContent.Load(); d.players.Single(p => p.id == d.userPlayerId).positionId = position;
            var c = CareerState.Create(d); var role = d.positions.Single(p => p.id == position).recommendedAttributeIds;
            Assert.That(Preview(c).Gains.Select(g => g.SkillId), Is.EquivalentTo(role));
            Choose(c, CalendarChoiceKind.Attend, "mon-training", position == "gk" ? "attacking" : "goalkeeping");
            Assert.That(c.Training.Results[0].Gains.Any(g => g.SkillId == (position == "gk" ? "finishing" : "handling") && g.Xp > 0), Is.True);
        }
        [Test] public void AllFocusedAndScheduledConfigurationIsCopiedThroughJsonRoundTrip()
        {
            var d = JsonUtility.FromJson<GameDefinitions>(JsonUtility.ToJson(PrototypeContent.Load())); var c = CareerState.Create(d);
            d.calendar.trainingFocuses.Single(f => f.id == "passing").attributeIds[0] = "charisma";
            d.calendar.commitments.Single(x => x.id == "mon-training").trainingFocusId = "missing";
            d.positions.Single(p => p.id == c.UserPlayer.PrimaryPositionId).recommendedAttributeIds[0] = "charisma";
            Assert.That(Preview(c, "passing").Gains.Any(g => g.SkillId == "charisma"), Is.False);
            Choose(c, CalendarChoiceKind.Attend, "mon-training"); Assert.That(c.Training.Results[0].TotalXp, Is.GreaterThan(0));
        }
        [TestCase("trait")] [TestCase("duplicate")] [TestCase("single-skill")] [TestCase("session")] [TestCase("activity")] [TestCase("non-training")]
        public void InvalidTrainingContentIsRejected(string kind)
        {
            var d = PrototypeContent.Load(); var f = d.calendar.trainingFocuses[1];
            switch (kind)
            {
                case "trait": f.attributeIds[0] = "charisma"; break;
                case "duplicate": f.id = d.calendar.trainingFocuses[0].id; break;
                case "single-skill": f.attributeIds = new[] { "finishing" }; break;
                case "session": d.calendar.commitments[0].trainingFocusId = "missing"; break;
                case "activity": d.calendar.activities[0].trainingFocusId = "missing"; break;
                case "non-training": d.calendar.activities.Single(a => a.id == "rest").trainingFocusId = "role"; break;
            }
            Assert.Throws<ArgumentException>(() => CareerState.Create(d));
        }
        [Test] public void InvalidFocusOrForeignPreviewCannotConsumeTimeOrAwardXp()
        {
            Assert.Throws<ArgumentException>(() => career.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training", "missing"));
            Assert.Throws<ArgumentException>(() => career.Calendar.Preview(CalendarChoiceKind.Replace, "rest", "passing"));
            Assert.Throws<ArgumentException>(() => career.Training.Preview(PrototypeContent.CreateCareer().Calendar.Preview(CalendarChoiceKind.Attend, "mon-training")));
            Assert.That(career.Calendar.Minute, Is.Zero); Assert.That(career.Training.Results, Is.Empty);
        }
        [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(-1)] [TestCase(0)] [TestCase(1000001)]
        public void InvalidRulesAreRejected(double value) => Assert.Throws<ArgumentOutOfRangeException>(() => new TrainingRules(baseXpPerSlot: value));
    }
}
