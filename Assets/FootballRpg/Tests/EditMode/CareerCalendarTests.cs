using System;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace FootballRpg.Tests
{
    public sealed class CareerCalendarTests
    {
        private CareerState career;
        private CareerCalendar calendar;
        [SetUp] public void Setup() { career = PrototypeContent.CreateCareer(); calendar = career.Calendar; }
        private CalendarDecision Choose(CalendarChoiceKind kind, string id = null) => calendar.Commit(calendar.Preview(kind, id));
        [Test] public void PreviewInspectionAndMatchClockDoNotAdvanceCareerTime()
        {
            for (int i = 0; i < 100; i++)
            {
                calendar.Preview(CalendarChoiceKind.Attend, "mon-training");
                calendar.Preview(CalendarChoiceKind.Replace, "party");
                calendar.TimeLabel(i); _ = calendar.CurrentCommitments; _ = calendar.ResultFor("mon-training");
            }
            var match = new MatchSession("separate-clock", 42); match.Advance(TimeSpan.FromHours(1));
            Assert.That(calendar.Minute, Is.Zero); Assert.That(calendar.Attendance, Is.Empty); Assert.That(calendar.History, Is.Empty);
        }
        [Test] public void AttendingConsumesItsWindowAndPublishesExactlyOneFact()
        {
            var preview = calendar.Preview(CalendarChoiceKind.Attend, "mon-training");
            Assert.That(preview.CostMinutes, Is.EqualTo(360));
            var decision = calendar.Commit(preview);
            Assert.That(calendar.Commit(preview), Is.SameAs(decision));
            Assert.That(calendar.Minute, Is.EqualTo(360)); Assert.That(calendar.Attendance.Count, Is.EqualTo(1));
            var r = decision.Attendance.Single();
            Assert.That(r.Fact.PlayerId, Is.EqualTo(career.UserPlayerId)); Assert.That(r.Fact.Attendance, Is.EqualTo(AttendanceOutcome.Attended));
            Assert.That(r.Required, Is.True); Assert.That(r.ParticipatedMinutes, Is.EqualTo(360));
            Assert.Throws<ArgumentException>(() => calendar.Preview(CalendarChoiceKind.Attend, "mon-training"));
        }
        [TestCase(CalendarChoiceKind.Skip)] [TestCase(CalendarChoiceKind.Wait)]
        public void SkippingOrWaitingPastRequiredAttendancePublishesMissedCommitment(CalendarChoiceKind kind)
        {
            var decision = Choose(kind, kind == CalendarChoiceKind.Skip ? "mon-training" : null);
            Assert.That(decision.Attendance.Single().Fact.Attendance, Is.EqualTo(AttendanceOutcome.Skipped));
            Assert.That(decision.Attendance.Single().ParticipatedMinutes, Is.Zero);
            Assert.That(calendar.Minute, Is.EqualTo(360));
        }
        [Test] public void ReplaceShowsConflictAndProducesOneReplacementRecordAndOneSkip()
        {
            var preview = calendar.Preview(CalendarChoiceKind.Replace, "rest");
            Assert.That(preview.Impacts.Single().Commitment.Id, Is.EqualTo("mon-training"));
            Assert.That(preview.Impacts.Single().Outcome, Is.EqualTo(AttendanceOutcome.Skipped));
            var result = calendar.Commit(preview);
            Assert.That(result.Attendance.Count, Is.EqualTo(2));
            Assert.That(result.Attendance.Count(r => r.Fact.Attendance == AttendanceOutcome.Attended), Is.EqualTo(1));
            Assert.That(result.Attendance.Single(r => r.CommitmentId == null).ParticipatedMinutes, Is.EqualTo(360));
        }
        [Test] public void ExtraPracticeConsumesNextFreeSlotAndCannotOverlapMedia()
        {
            Assert.Throws<InvalidOperationException>(() => calendar.Preview(CalendarChoiceKind.ExtraPractice, "extra-practice"));
            Choose(CalendarChoiceKind.Attend, "mon-training");
            var extra = calendar.Preview(CalendarChoiceKind.ExtraPractice, "extra-practice");
            Assert.That(extra.StartMinute, Is.EqualTo(360)); Assert.That(extra.EndMinute, Is.EqualTo(720));
            var result = calendar.Commit(extra); calendar.Commit(extra);
            Assert.That(calendar.Attendance.Count, Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => calendar.Preview(CalendarChoiceKind.ExtraPractice, "extra-practice"));
            Assert.Throws<ArgumentException>(() => calendar.Preview(CalendarChoiceKind.Replace, "extra-practice"));
        }
        [Test] public void ChoosingSomethingElseAfterTrainingForfeitsImmediateExtraPractice()
        {
            Choose(CalendarChoiceKind.Attend, "mon-training"); Choose(CalendarChoiceKind.Wait);
            Choose(CalendarChoiceKind.Skip, "mon-media");
            Assert.Throws<InvalidOperationException>(() => calendar.Preview(CalendarChoiceKind.ExtraPractice, "extra-practice"));
        }
        [Test] public void DecisionDelayWarnsAboutLatenessThenAttendingRecordsPartialParticipation()
        {
            Choose(CalendarChoiceKind.Attend, "mon-training"); Choose(CalendarChoiceKind.Wait); Choose(CalendarChoiceKind.Skip, "mon-media");
            var party = calendar.Preview(CalendarChoiceKind.Replace, "party");
            Assert.That(party.CostMinutes, Is.EqualTo(420));
            Assert.That(party.Impacts.Single().Commitment.Id, Is.EqualTo("tue-training"));
            Assert.That(party.Impacts.Single().Outcome, Is.EqualTo(AttendanceOutcome.Late));
            Assert.That(party.Impacts.Single().LateMinutes, Is.EqualTo(60));
            calendar.Commit(party);
            Assert.That(calendar.ResultFor("tue-training"), Is.Null, "A late warning is not yet attendance.");
            var late = Choose(CalendarChoiceKind.Attend, "tue-training").Attendance.Single();
            Assert.That(late.LateMinutes, Is.EqualTo(60)); Assert.That(late.ParticipatedMinutes, Is.EqualTo(300));
            Assert.That(late.Fact.Attendance, Is.EqualTo(AttendanceOutcome.Late)); Assert.That(calendar.Minute, Is.EqualTo(1800));
        }
        [Test] public void MultiSlotActivityConsumesTwoSlotsOnce()
        {
            while (calendar.Minute < 8 * 360) Choose(CalendarChoiceKind.Wait);
            var travel = calendar.Preview(CalendarChoiceKind.Attend, "wed-travel");
            Assert.That(travel.CostMinutes, Is.EqualTo(720));
            Assert.That(calendar.Commit(travel).Attendance.Single().ParticipatedMinutes, Is.EqualTo(720));
            Assert.That(calendar.CurrentCommitments, Is.Empty); Assert.That(calendar.Minute, Is.EqualTo(10 * 360));
        }
        [Test] public void OverlappingAuthoredCommitmentsRequireAChoiceAndExposeConflict()
        {
            var d = PrototypeContent.Load().calendar;
            d.commitments = new[] { new CalendarCommitmentDefinition { id = "a", activityId = "training", startSlot = 0, required = true },
                new CalendarCommitmentDefinition { id = "b", activityId = "media", startSlot = 0, required = false } };
            var c = new CareerCalendar("player", d);
            Assert.That(c.CurrentCommitments.Count, Is.EqualTo(2));
            var preview = c.Preview(CalendarChoiceKind.Attend, "a");
            Assert.That(preview.Impacts.Count, Is.EqualTo(2));
            c.Commit(preview);
            Assert.That(c.ResultFor("a").Fact.Attendance, Is.EqualTo(AttendanceOutcome.Attended));
            Assert.That(c.ResultFor("b").Fact.Attendance, Is.EqualTo(AttendanceOutcome.Skipped));
            Assert.That(c.ResultFor("b").Required, Is.False);
        }
        [Test] public void StaleAndForeignPreviewsCannotSpendTimeOrAwardAttendance()
        {
            var stale = calendar.Preview(CalendarChoiceKind.Replace, "rest");
            Choose(CalendarChoiceKind.Attend, "mon-training");
            Assert.Throws<InvalidOperationException>(() => calendar.Commit(stale));
            var other = PrototypeContent.CreateCareer().Calendar.Preview(CalendarChoiceKind.Wait);
            Assert.Throws<ArgumentException>(() => calendar.Commit(other));
            Assert.That(calendar.Minute, Is.EqualTo(360)); Assert.That(calendar.Attendance.Count, Is.EqualTo(1));
        }
        [Test] public void WeekBoundariesPreventOverrunAndUnlimitedActions()
        {
            while (calendar.Minute < calendar.TotalMinutes - 360) Choose(CalendarChoiceKind.Wait);
            Assert.Throws<InvalidOperationException>(() => calendar.Preview(CalendarChoiceKind.Replace, "party"));
            Choose(CalendarChoiceKind.Replace, "rest");
            Assert.That(calendar.Finished, Is.True); Assert.That(calendar.TimeLabel(calendar.Minute), Is.EqualTo("Week complete"));
            Assert.Throws<InvalidOperationException>(() => calendar.Preview(CalendarChoiceKind.Wait));
            Assert.That(calendar.Commitments.All(c => calendar.ResultFor(c.Id) != null), Is.True);
            Assert.That(calendar.Attendance.Select(r => r.Fact.EventId).Distinct().Count(), Is.EqualTo(calendar.Attendance.Count));
        }
        [Test] public void ConfigurationIsCopiedAndSurvivesJsonRoundTrip()
        {
            var definition = JsonUtility.FromJson<CalendarDefinition>(JsonUtility.ToJson(PrototypeContent.Load().calendar));
            var c = new CareerCalendar("player", definition);
            definition.days[0] = "Changed"; definition.activities[0].durationSlots = 20; definition.commitments[0].required = false;
            Assert.That(c.Days[0], Is.EqualTo("Monday")); Assert.That(c.Commitments[0].Required, Is.True);
            Assert.That(c.Preview(CalendarChoiceKind.Attend, "mon-training").CostMinutes, Is.EqualTo(360));
        }
        [TestCase("days")] [TestCase("duration")] [TestCase("delay")] [TestCase("activity-id")]
        [TestCase("commitment-id")] [TestCase("reference")] [TestCase("outside")] [TestCase("overflow")]
        [TestCase("extra")]
        public void InvalidConfigurationIsRejected(string field)
        {
            var d = PrototypeContent.Load().calendar;
            switch (field)
            {
                case "days": d.days = Array.Empty<string>(); break;
                case "duration": d.activities[0].durationSlots = 0; break;
                case "delay": d.activities[0].delayMinutes = -1; break;
                case "activity-id": d.activities[0].id = d.activities[1].id; break;
                case "commitment-id": d.commitments[0].id = d.commitments[1].id; break;
                case "reference": d.commitments[0].activityId = "unknown"; break;
                case "outside": d.commitments[0].startSlot = 28; break;
                case "overflow": d.commitments[0].startSlot = int.MaxValue; break;
                case "extra": d.activities[1].training = false; break;
            }
            Assert.Throws<ArgumentException>(() => new CareerCalendar("player", d));
        }
    }
}
