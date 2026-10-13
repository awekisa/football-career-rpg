using System;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Presentation;
using FootballRpg.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace FootballRpg.Tests
{
    public sealed class CareerSocialTests
    {
        private static CareerState Create(int charisma = 50, int discipline = 50, int threshold = 8, ulong seed = 1)
        {
            var d = PrototypeContent.Load(); var p = d.players.Single(p => p.id == d.userPlayerId);
            p.traits = p.traits.Where(t => t.id != "charisma" && t.id != "discipline").Concat(new[] { new ValueOverride { id = "charisma", value = charisma }, new ValueOverride { id = "discipline", value = discipline } }).ToArray();
            return CareerState.Create(d, socialRules: new SocialRules(evidencePerTraitPoint: threshold, seed: seed));
        }
        private static string Contact(CareerState c, SocialRole role = SocialRole.Coach) => c.Social.Relationships.Values.First(p => p.Role == role).PersonId;
        private static DialogueResult Choose(CareerState c, string option, SocialRole role = SocialRole.Coach) => c.Social.Commit(c.Social.Preview(Contact(c, role), option));
        private static void Wait(CareerState c) => c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Wait));
        [Test] public void ContactsAreCareerOwnedAndHaveIndependentDimensions()
        {
            var c = Create(); Assert.That(c.Social.Relationships.Count, Is.EqualTo(19));
            foreach (SocialRole role in Enum.GetValues(typeof(SocialRole)))
            {
                var id = Contact(c, role); var r = c.Social.Relationships[id];
                Assert.That(r.Affinity, Is.Zero); Assert.That(r.ProfessionalTrust, Is.EqualTo(50)); Assert.That(r.Standing, Is.EqualTo(50));
                Choose(c, "connect", role); Assert.That(r.ProfessionalTrust, Is.EqualTo(50)); Assert.That(r.Standing, Is.EqualTo(50));
            }
            var other = Create(); Assert.That(other.Social.Relationships.Values.All(r => r.Affinity == 0), Is.True);
            Assert.That(c.Calendar.Minute, Is.Zero); Assert.That(c.Calendar.Attendance, Is.Empty);
        }
        [Test] public void HighAndLowCharismaUseTheDisplayedChanceAndSameSeededRoll()
        {
            var high = Create(100); var low = Create(1);
            var a = Choose(high, "connect"); var b = Choose(low, "connect");
            Assert.That(a.Preview.Percent, Is.GreaterThan(b.Preview.Percent)); Assert.That(a.Roll, Is.EqualTo(b.Roll));
            Assert.That(a.Succeeded, Is.True); Assert.That(b.Succeeded, Is.False);
            Assert.That(a.Succeeded, Is.EqualTo(a.Roll < a.Preview.Chance)); Assert.That(b.Succeeded, Is.EqualTo(b.Roll < b.Preview.Chance));
        }
        [Test] public void FriendshipAndRivalryDoNotAutomaticallyGrantTrust()
        {
            var friend = Create(100); var rival = Create(100);
            for (int i = 0; i < 12; i++) { Choose(friend, "connect"); Choose(rival, "challenge"); Wait(friend); Wait(rival); }
            var f = friend.Social.Relationships[Contact(friend)]; var r = rival.Social.Relationships[Contact(rival)];
            Assert.That(friend.Social.Bond(f.PersonId), Is.EqualTo("Friendship")); Assert.That(f.ProfessionalTrust, Is.EqualTo(50)); Assert.That(f.Standing, Is.EqualTo(50));
            Assert.That(rival.Social.Bond(r.PersonId), Is.EqualTo("Rivalry")); Assert.That(r.Affinity, Is.GreaterThanOrEqualTo(-100));
        }
        [Test] public void ContradictoryChoicesRemainAvailableAndCanLowerTrustWhileIncreasingLiking()
        {
            var c = Create(100, 100); Assert.That(c.Social.Options.Any(o => o.Id == "deflect"), Is.True);
            var result = Choose(c, "deflect"); Assert.That(result.Succeeded, Is.True);
            var r = c.Social.Relationships[Contact(c)]; Assert.That(r.Affinity, Is.GreaterThan(0)); Assert.That(r.ProfessionalTrust, Is.LessThan(50));
            Assert.That(result.Behavior.Direction, Is.EqualTo(-1)); Assert.That(c.UserPlayer.PersonalityTraits["discipline"], Is.EqualTo(100));
        }
        [Test] public void RepeatedBehaviorChangesPersonalitySlowlyAndTrainingDoesNotContribute()
        {
            var c = Create(threshold: 3); var initial = c.UserPlayer.PersonalityTraits["discipline"];
            Choose(c, "help"); Assert.That(c.UserPlayer.PersonalityTraits["discipline"], Is.EqualTo(initial));
            c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training"));
            Assert.That(c.Social.BehaviorEvidence["discipline"], Is.EqualTo(1));
            Choose(c, "help"); Wait(c); Choose(c, "help");
            Assert.That(c.UserPlayer.PersonalityTraits["discipline"], Is.EqualTo(initial + 1)); Assert.That(c.Social.BehaviorEvidence["discipline"], Is.Zero);
            Assert.That(c.UserPlayer.PersonalityTraits["charisma"], Is.EqualTo(50)); Assert.That(c.UserPlayer.Confidence, Is.EqualTo(.65f));
        }
        [Test] public void MultipleContactsCannotFarmPersonalityEvidenceInOneSlot()
        {
            var c = Create(threshold: 2);
            foreach (var id in c.Social.Relationships.Keys) c.Social.Commit(c.Social.Preview(id, "help"));
            Assert.That(c.Social.BehaviorEvidence["discipline"], Is.EqualTo(1)); Assert.That(c.UserPlayer.PersonalityTraits["discipline"], Is.EqualTo(50));
            Assert.That(c.Social.Results.Count(r => r.Behavior.Counted), Is.EqualTo(1));
            Wait(c); var result = Choose(c, "help"); Assert.That(result.Behavior.CurrentValue, Is.EqualTo(51));
        }
        [Test] public void OpposingBehaviorCancelsEvidenceAndDecreasesWithinTraitBounds()
        {
            var c = Create(discipline: 2, threshold: 2);
            Choose(c, "help"); Wait(c); Choose(c, "deflect"); Assert.That(c.Social.BehaviorEvidence["discipline"], Is.Zero);
            for (int i = 0; i < 4; i++) { Wait(c); Choose(c, "deflect"); }
            Assert.That(c.UserPlayer.PersonalityTraits["discipline"], Is.EqualTo(1));
            var capped = Create(discipline: 100, threshold: 2); Choose(capped, "help"); Wait(capped); Choose(capped, "help");
            Assert.That(capped.UserPlayer.PersonalityTraits["discipline"], Is.EqualTo(100)); Assert.That(capped.Social.BehaviorEvidence["discipline"], Is.Zero);
        }
        [Test] public void InspectionAndDuplicateCommitNeverDrawOrApplyTwice()
        {
            var c = Create(); var p = c.Social.Preview(Contact(c), "help"); var alternative = c.Social.Preview(Contact(c), "deflect");
            for (int i = 0; i < 100; i++) c.Social.Preview(Contact(c), "connect");
            Assert.That(c.Social.DrawCount, Is.Zero); Assert.That(c.Social.Changes, Is.Empty); Assert.That(c.Calendar.Minute, Is.Zero);
            var result = c.Social.Commit(p); Assert.That(c.Social.Commit(p), Is.SameAs(result));
            Assert.Throws<InvalidOperationException>(() => c.Social.Commit(alternative));
            Assert.Throws<InvalidOperationException>(() => c.Social.Preview(Contact(c), "connect"));
            Assert.That(c.Social.DrawCount, Is.EqualTo(1)); Assert.That(c.Social.Results.Count, Is.EqualTo(1));
            foreach (var change in result.Changes) { Assert.That(change.CauseEventId, Is.EqualTo(p.Id)); Assert.That(change.OtherPersonId, Is.EqualTo(p.PersonId)); }
        }
        [Test] public void StaleAndForeignPreviewsAreRejectedBeforeAnyMutation()
        {
            var c = Create(); var foreign = Create();
            Assert.Throws<ArgumentException>(() => c.Social.Commit(foreign.Social.Preview(Contact(foreign), "help")));
            var old = c.Social.Preview(Contact(c), "help"); Wait(c);
            Assert.Throws<InvalidOperationException>(() => c.Social.Commit(old));
            var otherPerson = c.Social.Preview(Contact(c, SocialRole.Agent), "help"); Choose(c, "connect");
            Assert.Throws<InvalidOperationException>(() => c.Social.Commit(otherPerson)); Assert.That(c.Social.DrawCount, Is.EqualTo(1));
        }
        [Test] public void PendingPracticeAndFinishedWeekBlockNewExchanges()
        {
            var c = Create(); var p = c.Social.Preview(Contact(c), "help");
            c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training", practice: PracticeFamily.Passing));
            Assert.That(c.Social.UnavailableReason(Contact(c)), Does.Contain("practice")); Assert.Throws<InvalidOperationException>(() => c.Social.Commit(p));
            c.Training.FinishPractice(); while (!c.Calendar.Finished) Wait(c);
            Assert.Throws<InvalidOperationException>(() => c.Social.Preview(Contact(c), "help")); Assert.That(c.Social.DrawCount, Is.Zero);
        }
        [Test] public void TrustAndStandingClampAndAllEffectsHaveSeparateCauseRecords()
        {
            var c = Create(100, 100);
            for (int i = 0; i < 27; i++) { Choose(c, "deflect"); Wait(c); }
            var r = c.Social.Relationships[Contact(c)]; Assert.That(r.ProfessionalTrust, Is.Zero); Assert.That(r.Standing, Is.Zero);
            Assert.That(c.Social.Changes.All(x => x.DimensionId == "affinity" ? x.CurrentValue >= -100 : x.CurrentValue >= 0), Is.True);
            Assert.That(c.Social.Changes.All(x => x.CurrentValue <= 100), Is.True);
            Assert.That(c.Social.Changes.Select(x => x.EventId).Distinct().Count(), Is.EqualTo(c.Social.Changes.Count));
        }
        [Test] public void InvalidRulesAndUnknownInputsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SocialRules(evidencePerTraitPoint: 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SocialRules(attributeWeight: double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SocialRules(friendshipThreshold: 0));
            var c = Create(); Assert.Throws<InvalidOperationException>(() => c.Social.Preview("nobody", "help"));
            Assert.Throws<ArgumentException>(() => c.Social.Preview(Contact(c), "bogus")); Assert.That(c.Social.DrawCount, Is.Zero);
        }
        [Test] public void PresentationUsesTheSharedLedgerAndReopeningDoesNotReplayConsequences()
        {
            var root = new GameObject("Social test");
            try
            {
                var launch = root.AddComponent<LaunchScreen>(); var c = Create(); typeof(LaunchScreen).GetProperty("Career").SetValue(launch, c);
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                launch.OpenSocial(); typeof(LaunchScreen).GetMethod("PreviewSocial", flags).Invoke(launch, new object[] { 0 });
                typeof(LaunchScreen).GetMethod("ConfirmSocial", flags).Invoke(launch, null);
                launch.OpenSocial(); typeof(LaunchScreen).GetMethod("ConfirmSocial", flags).Invoke(launch, null);
                Assert.That(c.Social.Results.Count, Is.EqualTo(1)); Assert.That(c.Social.DrawCount, Is.EqualTo(1)); Assert.That(c.Calendar.Minute, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
