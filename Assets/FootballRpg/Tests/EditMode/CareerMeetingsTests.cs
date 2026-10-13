using System;
using System.Linq;
using System.Reflection;
using FootballRpg.Content;
using FootballRpg.Presentation;
using FootballRpg.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace FootballRpg.Tests
{
    public sealed class CareerMeetingsTests
    {
        private static CareerState Create(int trait = 100, ulong seed = 1)
        {
            var d = PrototypeContent.Load(); var p = d.players.Single(p => p.id == d.userPlayerId);
            p.traits = d.personalityTraits.Select(t => new ValueOverride { id = t.id, value = trait }).ToArray();
            return CareerState.Create(d, coachRules: new CoachRules(seed: 1), meetingSeed: seed);
        }
        private static void Until(CareerState c, int slot)
        { while (c.Calendar.Minute < slot * c.Calendar.MinutesPerSlot) c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Wait)); }
        private static CalendarChoice Attend(CareerState c, string id)
        {
            var m = c.Meetings.Meetings.Single(m => m.Id == id); var appointment = c.Calendar.Commitments.Single(a => a.Id == m.CommitmentId);
            Until(c, appointment.StartMinute / c.Calendar.MinutesPerSlot);
            var choice = c.Calendar.Preview(CalendarChoiceKind.Attend, m.CommitmentId); c.Calendar.Commit(choice); return choice;
        }
        private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        [Test] public void AuthoredDefinitionsAreCopiedAndResolveContactsInUsersClub()
        {
            var d = PrototypeContent.Load(); var c = CareerState.Create(d); d.meetings[0].choices[0].text = "changed";
            Assert.That(c.Meetings.Meetings.Count, Is.EqualTo(3)); Assert.That(c.Meetings.Meetings[0].Options[0].Text, Is.Not.EqualTo("changed"));
            foreach (var m in c.Meetings.Meetings) Assert.That(c.Social.Relationships[m.ContactId].Role, Is.EqualTo(m.Role));
            Assert.That(c.Meetings.Results, Is.Empty); Assert.That(c.Calendar.Minute, Is.Zero);
            Assert.That(c.Meetings.Introduction(c.Meetings.Meetings[0]), Does.Contain(c.Coach.AssignedRole.ToUpperInvariant()).And.Contain("Attend training").And.Contain("Recommended practice"));
        }
        [Test] public void AttendancePaysSlotOnceAndOpensAProtectedConversation()
        {
            var c = Create(); Until(c, 6); var coach = c.Coach.Relationship; int before = coach.ProfessionalTrust;
            var choice = c.Calendar.Preview(CalendarChoiceKind.Attend, "tue-coach"); Assert.That(c.Meetings.Pending, Is.Null);
            c.Calendar.Commit(choice); c.Calendar.Commit(choice);
            Assert.That(c.Calendar.Minute, Is.EqualTo(7 * 360)); Assert.That(c.Meetings.Pending.Id, Is.EqualTo("coach-plan")); Assert.That(coach.ProfessionalTrust, Is.EqualTo(before + 2));
            Assert.Throws<InvalidOperationException>(() => c.Calendar.Preview(CalendarChoiceKind.Wait));
            Assert.Throws<InvalidOperationException>(() => c.Social.Preview(coach.PersonId, "connect")); Assert.Throws<InvalidOperationException>(() => c.Coach.PreviewRequest(CoachRequest.Start));
            Assert.That(new ClubExploration(c, PrototypeContent.LoadClubVenue()).Inspect("coach").Available, Is.False);
        }
        [Test] public void CoachFocusPersistsAndDrivesNextTrainingUnlessExplicitlyOverridden()
        {
            var c = Create(); Attend(c, "coach-plan"); var preview = c.Meetings.Preview("tailored-plan", "passing");
            Assert.That(c.Meetings.PreferredTrainingFocusId, Is.Null); var r = c.Meetings.Commit(preview);
            Assert.That(r.Succeeded, Is.True); Assert.That(c.Meetings.PreferredTrainingFocusId, Is.EqualTo("passing")); Assert.That(c.Calendar.AwaitingMeeting, Is.False);
            Until(c, 12); var training = c.Calendar.Preview(CalendarChoiceKind.Attend, "thu-training");
            Assert.That(training.Focus.Id, Is.EqualTo("passing"));
            Assert.That(c.Calendar.Preview(CalendarChoiceKind.Attend, "thu-training", "fitness").Focus.Id, Is.EqualTo("fitness"));
            c.Calendar.Commit(training); Assert.That(c.Training.Results.Last().FocusName, Is.EqualTo("Passing and possession"));
        }
        [Test] public void AgreedRoleTrainingUsesAssignedPositionRatherThanOriginalPosition()
        {
            var c = Create(); var request = c.Coach.CommitRequest(c.Coach.PreviewRequest(CoachRequest.Role, "gk")); Assert.That(request.Succeeded, Is.True);
            Attend(c, "coach-plan"); c.Meetings.Commit(c.Meetings.Preview("role-plan")); Until(c, 12);
            var report = c.Training.Preview(c.Calendar.Preview(CalendarChoiceKind.Attend, "thu-training"));
            Assert.That(c.UserPlayer.PrimaryPositionId, Is.EqualTo("am")); Assert.That(c.Coach.AssignedRole, Is.EqualTo("gk"));
            var expected = PrototypeContent.Load().positions.Single(p => p.id == "gk").recommendedAttributeIds;
            Assert.That(report.Gains.Select(g => g.SkillId), Is.EquivalentTo(expected));
        }
        [Test] public void NegativeCoachResponseStillRecordsThePlayersChosenFocus()
        {
            var c = Create(1); Attend(c, "coach-plan"); var r = c.Meetings.Commit(c.Meetings.Preview("tailored-plan", "goalkeeping"));
            Assert.That(r.Succeeded, Is.False); Assert.That(c.Meetings.PreferredTrainingFocusId, Is.EqualTo("goalkeeping")); Assert.That(r.Changes.Any(x => x.CurrentValue < x.PreviousValue), Is.True);
        }
        [TestCase("support", "Friendship")] [TestCase("rival", "Rivalry")]
        public void ContrastingTeammateBranchesCreateDifferentBonds(string choice, string bond)
        {
            var c = Create(); Attend(c, "teammate-bond"); var m = c.Meetings.Pending;
            var r = c.Meetings.Commit(c.Meetings.Preview(choice)); Assert.That(r.Succeeded, Is.True); Assert.That(c.Social.Bond(m.ContactId), Is.EqualTo(bond));
            Assert.That(c.Social.Relationships[m.ContactId].ProfessionalTrust, Is.GreaterThan(50));
            Assert.That(c.Meetings.Preference, Is.Null);
        }
        [TestCase("minutes", CareerPreference.PlayingTime)] [TestCase("development", CareerPreference.Development)] [TestCase("challenge", CareerPreference.Challenge)]
        public void AgentRecordsEachPriorityWithoutChangingContract(string choice, CareerPreference preference)
        {
            var c = Create(); long wage = c.UserPlayer.WeeklyWageMinor, balance = c.UserPlayer.BalanceMinor;
            Attend(c, "agent-brief"); var m = c.Meetings.Pending; int before = c.Social.Relationships[m.ContactId].ProfessionalTrust;
            var r = c.Meetings.Commit(c.Meetings.Preview(choice));
            Assert.That(c.Meetings.Preference, Is.EqualTo(preference)); Assert.That(r.Preference, Is.EqualTo(preference)); Assert.That(c.Social.Relationships[m.ContactId].ProfessionalTrust, Is.Not.EqualTo(before));
            Assert.That(c.UserPlayer.WeeklyWageMinor, Is.EqualTo(wage)); Assert.That(c.UserPlayer.BalanceMinor, Is.EqualTo(balance));
            Assert.That(c.Meetings.Introduction(m), Does.Contain("primarily come through your agent"));
        }
        [Test] public void HighAndLowTraitsUseExactDisplayedChanceAndSameSeededRoll()
        {
            var high = Create(); var low = Create(1); Attend(high, "agent-brief"); Attend(low, "agent-brief");
            var a = high.Meetings.Preview("minutes"); var b = low.Meetings.Preview("minutes");
            for (int i = 0; i < 20; i++) high.Meetings.Preview("minutes"); Assert.That(high.Meetings.DrawCount, Is.Zero);
            var ar = high.Meetings.Commit(a); var br = low.Meetings.Commit(b);
            Assert.That(ar.Roll, Is.EqualTo(br.Roll)); Assert.That(a.Percent, Is.GreaterThan(b.Percent)); Assert.That(ar.Succeeded, Is.True); Assert.That(br.Succeeded, Is.False);
            Assert.That(ar.Succeeded, Is.EqualTo(ar.Roll < a.Chance)); Assert.That(low.Meetings.Preference, Is.EqualTo(CareerPreference.PlayingTime));
        }
        [Test] public void SkippedMeetingsResolveRelationshipsWithoutInventingPreferences()
        {
            var c = Create(); Until(c, 18);
            Assert.That(c.Meetings.Results.Count, Is.EqualTo(3)); Assert.That(c.Meetings.Results.All(r => r.ChoiceId == "skipped"), Is.True);
            foreach (var r in c.Meetings.Results) Assert.That(r.Changes.Any(x => x.CurrentValue < x.PreviousValue), Is.True);
            Assert.That(c.Meetings.PreferredTrainingFocusId, Is.Null); Assert.That(c.Meetings.Preference, Is.Null); Assert.That(c.Meetings.DrawCount, Is.Zero); Assert.That(c.Calendar.AwaitingMeeting, Is.False);
        }
        [Test] public void LateMeetingChargesRemainingTimeAndAppliesOnlyLateAttendanceEffect()
        {
            var c = Create(); Until(c, 5); c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Replace, "party"));
            Assert.That(c.Calendar.Minute, Is.EqualTo(6 * 360 + 60)); int trust = c.Coach.Relationship.ProfessionalTrust;
            c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Attend, "tue-coach"));
            Assert.That(c.Calendar.Minute, Is.EqualTo(7 * 360)); Assert.That(c.Calendar.ResultFor("tue-coach").Fact.Attendance, Is.EqualTo(AttendanceOutcome.Late));
            Assert.That(c.Coach.Relationship.ProfessionalTrust, Is.EqualTo(trust - 2)); Assert.That(c.Meetings.Pending, Is.Not.Null);
        }
        [Test] public void LeavingUnblocksCalendarWithoutRefundOrNewPlan()
        {
            var c = Create(); Attend(c, "coach-plan"); int minute = c.Calendar.Minute; var r = c.Meetings.Leave();
            Assert.That(r.ChoiceId, Is.EqualTo("leave")); Assert.That(c.Calendar.Minute, Is.EqualTo(minute)); Assert.That(c.Meetings.DrawCount, Is.Zero); Assert.That(c.Meetings.PreferredTrainingFocusId, Is.Null);
            c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Wait)); Assert.That(c.Calendar.Minute, Is.GreaterThan(minute));
        }
        [Test] public void DuplicateAlternativeForeignAndStalePreviewsCannotRepeatEffects()
        {
            var c = Create(); Attend(c, "coach-plan"); var p = c.Meetings.Preview("tailored-plan", "passing"); var other = c.Meetings.Preview("tailored-plan", "fitness");
            Assert.Throws<ArgumentException>(() => Create().Meetings.Commit(p));
            var r = c.Meetings.Commit(p); int count = c.Social.Changes.Count;
            Assert.That(c.Meetings.Commit(p), Is.SameAs(r)); Assert.That(c.Social.Changes.Count, Is.EqualTo(count)); Assert.That(c.Meetings.DrawCount, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => c.Meetings.Commit(other));
            var stale = Create(); Attend(stale, "coach-plan"); var s = stale.Meetings.Preview("role-plan");
            Call(stale.UserPlayer, "SetPersonalityTrait", "discipline", 10); Assert.Throws<InvalidOperationException>(() => stale.Meetings.Commit(s)); Assert.That(stale.Meetings.DrawCount, Is.Zero);
        }
        [Test] public void ClubAppointmentArrivalsOfferCalendarPreviewWithoutSpendingTime()
        {
            var c = Create(); Until(c, 6); var club = new ClubExploration(c, PrototypeContent.LoadClubVenue());
            var interaction = club.Inspect("coach"); Assert.That(interaction.Choice.Commitment.Id, Is.EqualTo("tue-coach")); Assert.That(c.Calendar.Minute, Is.EqualTo(6 * 360)); Assert.That(c.Meetings.Pending, Is.Null);
            c.Calendar.Commit(interaction.Choice); c.Meetings.Leave(); Until(c, 14);
            Assert.That(club.Inspect("teammates").Choice.Commitment.Id, Is.EqualTo("thu-teammate"));
        }
        [Test] public void InvalidAuthoredReferencesChoicesAndOverlappingAppointmentsAreRejected()
        {
            var d = PrototypeContent.Load(); d.meetings[0].commitmentId = "missing"; Assert.Throws<ArgumentException>(() => CareerState.Create(d));
            d = PrototypeContent.Load(); d.meetings[0].choices[0].trait = "missing"; Assert.Throws<ArgumentException>(() => CareerState.Create(d));
            d = PrototypeContent.Load(); d.meetings[0].choices[0].success = null; Assert.Throws<ArgumentException>(() => CareerState.Create(d));
            d = PrototypeContent.Load(); d.calendar.commitments.Single(c => c.id == "tue-coach").startSlot = 4; Assert.Throws<ArgumentException>(() => CareerState.Create(d));
            var c = Create(); Assert.Throws<InvalidOperationException>(() => c.Meetings.Preview("role-plan")); Attend(c, "coach-plan");
            Assert.Throws<ArgumentException>(() => c.Meetings.Preview("tailored-plan", "unknown")); Assert.That(c.Meetings.DrawCount, Is.Zero);
        }
        [Test] public void PresentationUsesSharedStateAndReopeningDoesNotRepeatConversation()
        {
            var root = new GameObject("Meeting screen test");
            try
            {
                var launch = root.AddComponent<LaunchScreen>(); var c = Create(); typeof(LaunchScreen).GetProperty("Career").SetValue(launch, c);
                Attend(c, "agent-brief"); launch.OpenMeetings(); Call(launch, "PreviewMeeting", 0); Call(launch, "ConfirmMeeting");
                int count = c.Social.Changes.Count; launch.OpenMeetings(); Call(launch, "ConfirmMeeting");
                Assert.That(c.Social.Changes.Count, Is.EqualTo(count)); Assert.That(c.Meetings.DrawCount, Is.EqualTo(1)); Assert.That(c.Meetings.Preference, Is.EqualTo(CareerPreference.PlayingTime));
                string contract = (string)Call(launch, "ContractSummary"); Assert.That(contract, Does.Contain("150.00").And.Contain("250.00").And.Contain(c.UserPlayer.CurrencyCode));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
