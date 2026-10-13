using System;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Simulation;
using FootballRpg.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace FootballRpg.Tests
{
    public sealed class GuidedPracticeTests
    {
        private static CalendarChoice Start(CareerState c, PracticeFamily family) { var choice = c.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training", practice: family); c.Calendar.Commit(choice); return choice; }
        private static ActionResolved Choose(CareerState c, int option = 0) { var d = c.Training.ActivePractice.Decision; return c.Training.ChoosePractice(d.Id, d.Options[option].Id); }
        private static void Complete(CareerState c, int option = 0)
        {
            for (int i = 0; i < 3; i++) { Choose(c, option); if (i < 2) c.Training.ActivePractice.Next(); }
        }
        private static double Xp(CareerState c) => c.Progression.ForPlayer(c.UserPlayerId).Values.Sum(s => s.TotalXp);
        [TestCase(PracticeFamily.Passing)] [TestCase(PracticeFamily.Defending)] [TestCase(PracticeFamily.Attacking)] [TestCase(PracticeFamily.Goalkeeping)]
        public void AllFamiliesAreAvailableToEveryPositionAndUseOneBoundedReward(PracticeFamily family)
        {
            foreach (var role in PrototypeContent.Load().positions)
            {
                var definitions = PrototypeContent.Load();
                definitions.players.Single(p => p.id == definitions.userPlayerId).positionId = role.id;
                var c = CareerState.Create(definitions); double before = Xp(c); float fatigue = c.UserPlayer.Fatigue;
                var choice = Start(c, family); var session = c.Training.ActivePractice;
                Assert.That(c.Calendar.AwaitingPractice, Is.True); Assert.That(Xp(c), Is.EqualTo(before));
                Assert.That(session.Results, Is.Empty); Assert.That(c.Training.Results, Is.Empty);
                for (int round = 0; round < 3; round++)
                {
                    var d = session.Decision; var option = d.Options[0];
                    var result = Choose(c);
                    Assert.That(result.SuccessProbability, Is.EqualTo(option.Percent / 100f));
                    Assert.That(c.Training.ChoosePractice(d.Id, option.Id), Is.SameAs(result));
                    Assert.Throws<InvalidOperationException>(() => c.Training.ChoosePractice(d.Id, d.Options[1].Id));
                    Assert.That(session.DrawCount, Is.EqualTo(round + 1));
                    if (round < 2) session.Next();
                }
                Assert.That(c.Calendar.AwaitingPractice, Is.False); Assert.That(session.Finished, Is.True);
                Assert.That(c.Calendar.Minute, Is.EqualTo(360)); Assert.That(c.Readiness.Training.Count, Is.EqualTo(1));
                Assert.That(c.UserPlayer.Fatigue - fatigue, Is.EqualTo(.14).Within(1e-6));
                Assert.That(c.Training.Results.Count, Is.EqualTo(1));
                Assert.That(Xp(c) - before, Is.EqualTo(c.Training.Results.Single().TotalXp).Within(1e-6));
                Assert.That(c.Training.Results.Single().Gains.Sum(g => g.Share), Is.InRange(.65, 1.00001));
                c.Calendar.Commit(choice); c.Training.FinishPractice();
                Assert.That(c.Training.Results.Count, Is.EqualTo(1));
                Assert.Throws<InvalidOperationException>(() => session.Next());
            }
        }
        [Test] public void CompetingPreviewsCannotCollectBothAttendanceModes()
        {
            var c = PrototypeContent.CreateCareer(); var general = c.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training");
            var guided = Start(c, PracticeFamily.Passing);
            Assert.Throws<InvalidOperationException>(() => c.Calendar.Commit(general));
            Assert.Throws<InvalidOperationException>(() => c.Calendar.Preview(CalendarChoiceKind.Wait));
            Complete(c);
            Assert.Throws<InvalidOperationException>(() => c.Calendar.Commit(general));
            c.Calendar.Commit(guided); Assert.That(c.Training.Results.Count, Is.EqualTo(1));
            var d = PrototypeContent.CreateCareer(); var stale = d.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training", practice: PracticeFamily.Passing);
            d.Calendar.Commit(d.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training"));
            Assert.Throws<InvalidOperationException>(() => d.Calendar.Commit(stale)); Assert.That(d.Training.ActivePractice, Is.Null);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void EarlyExitOnlyRewardsCompletedAttemptsAndDoesNotRefundTime(int count)
        {
            var c = PrototypeContent.CreateCareer(); Start(c, PracticeFamily.Passing); double before = Xp(c);
            for (int i = 0; i < count; i++) { Choose(c); if (i + 1 < count) c.Training.ActivePractice.Next(); }
            c.Training.FinishPractice(); c.Training.FinishPractice();
            Assert.That(c.Calendar.Minute, Is.EqualTo(360)); Assert.That(c.Calendar.AwaitingPractice, Is.False);
            Assert.That(c.Training.Results.Count, Is.EqualTo(1));
            Assert.That(c.Training.Results[0].Gains.Sum(g => g.Share), Is.InRange(count * .65 / 3, count / 3d + 1e-8));
            Assert.That(Xp(c) - before, Is.EqualTo(c.Training.Results[0].TotalXp).Within(1e-6));
            Assert.Throws<InvalidOperationException>(() => c.Training.ActivePractice.Next());
            c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.ExtraPractice, "extra-practice", practice: PracticeFamily.Defending));
            Assert.That(c.Calendar.Minute, Is.EqualTo(720)); Assert.That(c.Training.Results.Count, Is.EqualTo(1));
            Complete(c); Assert.That(c.Training.Results.Count, Is.EqualTo(2));
        }
        [Test] public void GuidedPassingConcentratesMoreXpInPracticedSkillThanSimulatedPassing()
        {
            var general = PrototypeContent.CreateCareer();
            general.Calendar.Commit(general.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training", "passing"));
            var c = PrototypeContent.CreateCareer(); Start(c, PracticeFamily.Passing); Complete(c);
            var report = c.Training.Results.Single(); Assert.That(report.Gains.Count, Is.EqualTo(2));
            Assert.That(report.Gains.Single(g => g.SkillId == "short-passing").Xp, Is.GreaterThan(general.Training.Results[0].Gains.Single(g => g.SkillId == "short-passing").Xp));
            Assert.That(report.ReadinessMultiplier, Is.EqualTo(general.Training.Results[0].ReadinessMultiplier));
        }
        [Test] public void InspectionIsInertAndSeededChoicesRepeatAcrossIdenticalCareers()
        {
            var a = PrototypeContent.CreateCareer(); var b = PrototypeContent.CreateCareer();
            for (int i = 0; i < 20; i++) a.Training.Preview(a.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training", practice: PracticeFamily.Attacking));
            Assert.That(a.Calendar.Minute, Is.Zero); Start(a, PracticeFamily.Attacking); Start(b, PracticeFamily.Attacking);
            var session = a.Training.ActivePractice;
            for (int i = 0; i < 20; i++) { Assert.That(session.Decision.Options.Count, Is.EqualTo(3)); Assert.That(session.DrawCount, Is.Zero); }
            Assert.Throws<InvalidOperationException>(() => session.Next());
            Assert.Throws<InvalidOperationException>(() => a.Training.ChoosePractice("stale", "placed"));
            Assert.Throws<ArgumentException>(() => a.Training.ChoosePractice(session.Decision.Id, "bogus"));
            Complete(a, 1); Complete(b, 1);
            Assert.That(a.Training.ActivePractice.Results.Select(r => r.Roll), Is.EqualTo(b.Training.ActivePractice.Results.Select(r => r.Roll)));
            Assert.That(a.Training.Results[0].TotalXp, Is.EqualTo(b.Training.Results[0].TotalXp));
        }
        [TestCase(PracticeFamily.Passing)] [TestCase(PracticeFamily.Defending)] [TestCase(PracticeFamily.Attacking)] [TestCase(PracticeFamily.Goalkeeping)]
        public void EveryOfferedOptionResolvesThroughAllRounds(PracticeFamily family)
        {
            for (int index = 0; index < (family == PracticeFamily.Defending || family == PracticeFamily.Attacking ? 3 : 2); index++)
            {
                var c = PrototypeContent.CreateCareer(); Start(c, family); Complete(c, index);
                Assert.That(c.Training.ActivePractice.Results.Count, Is.EqualTo(3)); Assert.That(c.Training.Results[0].TotalXp, Is.GreaterThan(0));
            }
        }
        [Test] public void InjuryPreventsPracticeBeforeAnySlotIsSpent()
        {
            var c = PrototypeContent.CreateCareer();
            var choice = c.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training", practice: PracticeFamily.Passing);
            typeof(PlayerState).GetProperty("InjuryDaysRemaining").SetValue(c.UserPlayer, 1);
            Assert.Throws<InvalidOperationException>(() => c.Calendar.Commit(choice));
            Assert.Throws<InvalidOperationException>(() => c.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training", practice: PracticeFamily.Passing));
            Assert.That(c.Calendar.Minute, Is.Zero); Assert.That(c.Calendar.AwaitingPractice, Is.False);
            c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training"));
            Assert.That(c.Training.ActivePractice, Is.Null);
        }
        [Test] public void PracticeViewInspectsWithoutResolvingAndClosingSafelyEndsAttendance()
        {
            var c = PrototypeContent.CreateCareer(); Start(c, PracticeFamily.Passing);
            GameObject cameraRoot = null;
            if (Camera.main == null) { cameraRoot = new GameObject("Original camera", typeof(Camera)); cameraRoot.tag = "MainCamera"; }
            var originalCamera = Camera.main;
            var root = new GameObject("Practice test");
            try
            {
                var view = root.AddComponent<GuidedPracticeView>(); view.Initialize(c, null);
                Assert.That(originalCamera.enabled, Is.False);
                Assert.That(view.ViewCamera.orthographic, Is.True);
                for (int i = 0; i < 10; i++) view.Select(i % 2);
                Assert.That(c.Training.ActivePractice.DrawCount, Is.Zero);
                view.Continue(); Assert.That(c.Training.ActivePractice.DrawCount, Is.EqualTo(1));
                view.Close();
                Assert.That(c.Calendar.AwaitingPractice, Is.False); Assert.That(c.Training.Results.Count, Is.EqualTo(1));
                Assert.That(originalCamera.enabled, Is.True);
            }
            finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); if (cameraRoot != null) UnityEngine.Object.DestroyImmediate(cameraRoot); }
        }
        [Test] public void NonTrainingChoicesCannotCreatePractice()
        {
            var c = PrototypeContent.CreateCareer();
            Assert.Throws<ArgumentException>(() => c.Calendar.Preview(CalendarChoiceKind.Replace, "rest", practice: PracticeFamily.Passing));
            Assert.Throws<ArgumentException>(() => c.Calendar.Preview(CalendarChoiceKind.Skip, "mon-training", practice: PracticeFamily.Passing));
            Assert.Throws<ArgumentException>(() => c.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training", practice: (PracticeFamily)99));
            Assert.That(c.Calendar.Minute, Is.Zero);
        }
    }
}
