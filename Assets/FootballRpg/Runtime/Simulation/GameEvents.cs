using System;
using System.Linq;

namespace FootballRpg.Simulation
{
    // Facts emitted after a subsystem commits a change. Events do not mutate state themselves.
    // Action event IDs come from MatchSession's deterministic sequence, never Guid/time here.
    public abstract class GameEvent
    {
        public string EventId { get; }
        public string PlayerId { get; }
        public long Sequence { get; }

        protected GameEvent(string eventId, string playerId, long sequence)
        {
            EventId = Required(eventId);
            PlayerId = Required(playerId);
            if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            Sequence = sequence;
        }

        protected static string Required(string value) =>
            !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Event reference is required.");
    }

    public enum AttendanceOutcome { Attended, Skipped, Late }
    public enum SelectionOutcome { Starter, Substitute, NotSelected }

    public sealed class ActivityResolved : GameEvent
    {
        public string ActivityId { get; }
        public string CalendarSlotId { get; }
        public AttendanceOutcome Attendance { get; }

        public ActivityResolved(string eventId, string playerId, long sequence, string activityId,
            string calendarSlotId, AttendanceOutcome attendance) : base(eventId, playerId, sequence)
        {
            if (!Enum.IsDefined(typeof(AttendanceOutcome), attendance)) throw new ArgumentException("Unknown attendance.");
            ActivityId = Required(activityId);
            CalendarSlotId = Required(calendarSlotId);
            Attendance = attendance;
        }
    }

    public sealed class ActionResolved : GameEvent
    {
        public string ActionId { get; }
        public string TargetId { get; }
        public float SuccessProbability { get; }
        public float Roll { get; }
        public bool Succeeded => Roll < SuccessProbability;
        public string OutcomeId { get; }

        public ActionResolved(string eventId, string playerId, long sequence, string actionId,
            string targetId, float successProbability, float roll, string outcomeId) : base(eventId, playerId, sequence)
        {
            DefinitionValidator.Unit(successProbability, nameof(successProbability));
            DefinitionValidator.Unit(roll, nameof(roll));
            if (roll >= 1) throw new ArgumentOutOfRangeException(nameof(roll), "Rolls are in [0,1).");
            ActionId = Required(actionId);
            TargetId = Required(targetId); // Entity or stable pitch-area ID; never a Unity object.
            SuccessProbability = successProbability;
            Roll = roll;
            OutcomeId = Required(outcomeId);
        }
    }

    public sealed class ProgressionApplied : GameEvent
    {
        public string AttributeId { get; }
        public string CauseEventId { get; }
        public int XpDelta { get; }
        public int PreviousLevel { get; }
        public int CurrentLevel { get; }

        public ProgressionApplied(string eventId, string playerId, long sequence, string attributeId,
            string causeEventId, int xpDelta, int previousLevel, int currentLevel) : base(eventId, playerId, sequence)
        {
            AttributeId = Required(attributeId);
            CauseEventId = Required(causeEventId);
            XpDelta = xpDelta;
            PreviousLevel = previousLevel;
            CurrentLevel = currentLevel;
        }
    }

    public sealed class SelectionAnnounced : GameEvent
    {
        public string FixtureId { get; }
        public string CoachId { get; }
        public string AssignedPositionId { get; }
        public SelectionOutcome Selection { get; }

        public SelectionAnnounced(string eventId, string playerId, long sequence, string fixtureId,
            string coachId, string assignedPositionId, SelectionOutcome selection) : base(eventId, playerId, sequence)
        {
            if (!Enum.IsDefined(typeof(SelectionOutcome), selection)) throw new ArgumentException("Unknown selection.");
            if (selection != SelectionOutcome.NotSelected && !PositionIds.All.Contains(assignedPositionId))
                throw new ArgumentException("Selected players require a supported assigned position.");
            FixtureId = Required(fixtureId);
            CoachId = Required(coachId);
            AssignedPositionId = selection == SelectionOutcome.NotSelected ? null : assignedPositionId;
            Selection = selection;
        }
    }

    public sealed class RelationshipChanged : GameEvent
    {
        public string OtherPersonId { get; }
        public string DimensionId { get; }
        public string CauseEventId { get; }
        public int PreviousValue { get; }
        public int CurrentValue { get; }

        public RelationshipChanged(string eventId, string playerId, long sequence, string otherPersonId,
            string dimensionId, string causeEventId, int previousValue, int currentValue) : base(eventId, playerId, sequence)
        {
            OtherPersonId = Required(otherPersonId);
            DimensionId = Required(dimensionId); // e.g. affinity, professional-trust, rivalry
            CauseEventId = Required(causeEventId);
            PreviousValue = previousValue;
            CurrentValue = currentValue;
        }
    }
}
