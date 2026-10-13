using System;
using System.Collections.Generic;
using System.Linq;

namespace FootballRpg.Simulation
{
    [Serializable] public sealed class CalendarDefinition
    {
        public string[] days;
        public string[] slots;
        public int minutesPerSlot;
        public CalendarActivityDefinition[] activities;
        public CalendarCommitmentDefinition[] commitments;
        public TrainingFocusDefinition[] trainingFocuses;
    }
    [Serializable] public sealed class CalendarActivityDefinition
    {
        public string id, name;
        public int durationSlots = 1;
        public int delayMinutes;
        public bool selectable;
        public bool training;
        public bool extraPractice;
        public float fatiguePerSlot, moralePerSlot;
        public string trainingFocusId;
    }
    [Serializable] public sealed class CalendarCommitmentDefinition
    {
        public string id, activityId;
        public int startSlot;
        public bool required;
        public string trainingFocusId;
    }
    public sealed class CalendarActivity
    {
        public string Id { get; }
        public string Name { get; }
        public int DurationSlots { get; }
        public int DelayMinutes { get; }
        public bool Selectable { get; }
        public bool Training { get; }
        public bool ExtraPractice { get; }
        public string TrainingFocusId { get; }
        public float FatiguePerSlot { get; }
        public float MoralePerSlot { get; }
        internal CalendarActivity(CalendarActivityDefinition d)
        { Id = d.id; Name = d.name; DurationSlots = d.durationSlots; DelayMinutes = d.delayMinutes; Selectable = d.selectable; Training = d.training; ExtraPractice = d.extraPractice; TrainingFocusId = string.IsNullOrEmpty(d.trainingFocusId) ? null : d.trainingFocusId; FatiguePerSlot = d.fatiguePerSlot; MoralePerSlot = d.moralePerSlot; }
    }
    public sealed class CalendarCommitment
    {
        public string Id { get; }
        public CalendarActivity Activity { get; }
        public int StartMinute { get; }
        public int EndMinute { get; }
        public bool Required { get; }
        public string TrainingFocusId { get; }
        internal CalendarCommitment(CalendarCommitmentDefinition d, CalendarActivity activity, int minutesPerSlot)
        { Id = d.id; Activity = activity; StartMinute = d.startSlot * minutesPerSlot; EndMinute = StartMinute + activity.DurationSlots * minutesPerSlot; Required = d.required; TrainingFocusId = string.IsNullOrEmpty(d.trainingFocusId) ? null : d.trainingFocusId; }
    }
    public enum CalendarChoiceKind { Attend, Skip, Replace, Wait, ExtraPractice }
    public sealed class CalendarImpact
    {
        public CalendarCommitment Commitment { get; }
        public AttendanceOutcome Outcome { get; }
        public int LateMinutes { get; }
        internal CalendarImpact(CalendarCommitment c, AttendanceOutcome outcome, int lateMinutes)
        { Commitment = c; Outcome = outcome; LateMinutes = lateMinutes; }
    }
    public sealed class CalendarChoice
    {
        internal CareerCalendar Owner { get; }
        internal int Revision { get; }
        public CalendarChoiceKind Kind { get; }
        public TrainingFocus Focus { get; }
        public PracticeFamily? Practice { get; }
        public CalendarActivity Activity { get; }
        public CalendarCommitment Commitment { get; }
        public int StartMinute { get; }
        public int ActivityEndMinute { get; }
        public int EndMinute { get; }
        public int CostMinutes => EndMinute - StartMinute;
        public IReadOnlyList<CalendarImpact> Impacts { get; }
        internal CalendarChoice(CareerCalendar owner, int revision, CalendarChoiceKind kind, CalendarActivity activity,
            CalendarCommitment commitment, int start, int activityEnd, int end, List<CalendarImpact> impacts, TrainingFocus focus, PracticeFamily? practice)
        { Owner = owner; Revision = revision; Kind = kind; Activity = activity; Commitment = commitment; StartMinute = start;
            ActivityEndMinute = activityEnd; EndMinute = end; Impacts = impacts.AsReadOnly(); Focus = focus; Practice = practice; }
    }
    public sealed class CalendarAttendance
    {
        public ActivityResolved Fact { get; }
        public string CommitmentId { get; }
        public bool Required { get; }
        public int ParticipatedMinutes { get; }
        public int LateMinutes { get; }
        internal CalendarAttendance(ActivityResolved fact, string commitmentId, bool required, int participated, int late)
        { Fact = fact; CommitmentId = commitmentId; Required = required; ParticipatedMinutes = participated; LateMinutes = late; }
    }
    public sealed class CalendarDecision
    {
        public CalendarChoice Choice { get; }
        public IReadOnlyList<CalendarAttendance> Attendance { get; }
        internal CalendarDecision(CalendarChoice choice, List<CalendarAttendance> attendance)
        { Choice = choice; Attendance = attendance.AsReadOnly(); }
    }

    /// <summary>Only committing an explicit choice advances career time. Queries/navigation are inert.</summary>
    public sealed class CareerCalendar
    {
        private readonly string playerId;
        private readonly List<CalendarAttendance> attendance = new List<CalendarAttendance>();
        private readonly List<CalendarDecision> history = new List<CalendarDecision>();
        private readonly Dictionary<string, CalendarAttendance> resolved = new Dictionary<string, CalendarAttendance>();
        private readonly Dictionary<CalendarChoice, CalendarDecision> committed = new Dictionary<CalendarChoice, CalendarDecision>();
        private int revision;
        internal event Action<CalendarDecision> Committed;
        internal Action<CalendarChoice> ValidateChoice;
        public bool AwaitingPractice { get; internal set; }
        public IReadOnlyList<string> Days { get; }
        public IReadOnlyList<string> Slots { get; }
        public IReadOnlyList<CalendarActivity> Activities { get; }
        public IReadOnlyList<TrainingFocus> TrainingFocuses { get; }
        public IReadOnlyList<CalendarCommitment> Commitments { get; }
        public IReadOnlyList<CalendarAttendance> Attendance { get; }
        public IReadOnlyList<CalendarDecision> History { get; }
        public int MinutesPerSlot { get; }
        public int Minute { get; private set; }
        public int TotalMinutes => Days.Count * Slots.Count * MinutesPerSlot;
        public bool Finished => Minute == TotalMinutes;
        public CareerCalendar(string playerId, CalendarDefinition definition)
        {
            this.playerId = ActionRequest.Required(playerId); Validate(definition);
            Days = Array.AsReadOnly((string[])definition.days.Clone()); Slots = Array.AsReadOnly((string[])definition.slots.Clone()); MinutesPerSlot = definition.minutesPerSlot;
            TrainingFocuses = Array.AsReadOnly((definition.trainingFocuses ?? Array.Empty<TrainingFocusDefinition>()).Select(d => new TrainingFocus(d)).ToArray());
            Activities = Array.AsReadOnly(definition.activities.Select(d => new CalendarActivity(d)).ToArray());
            Commitments = Array.AsReadOnly(definition.commitments.Select(d => new CalendarCommitment(d, Activities.Single(a => a.Id == d.activityId), MinutesPerSlot)).OrderBy(c => c.StartMinute).ToArray());
            Attendance = attendance.AsReadOnly(); History = history.AsReadOnly();
        }
        public static void Validate(CalendarDefinition d)
        {
            if (d == null || d.days == null || d.slots == null || d.days.Length < 1 || d.days.Length > 14 || d.slots.Length < 1 || d.slots.Length > 24 ||
                d.days.Concat(d.slots).Any(string.IsNullOrWhiteSpace) || d.minutesPerSlot < 1 || d.minutesPerSlot > 1440 || d.activities == null || d.commitments == null)
                throw new ArgumentException("Invalid calendar shape.");
            int count = d.days.Length * d.slots.Length;
            if (d.activities.Any(a => a == null || string.IsNullOrWhiteSpace(a.id) || string.IsNullOrWhiteSpace(a.name) || a.durationSlots < 1 || a.durationSlots > count ||
                    float.IsNaN(a.fatiguePerSlot) || Math.Abs(a.fatiguePerSlot) > 1 || float.IsNaN(a.moralePerSlot) || Math.Abs(a.moralePerSlot) > 1 || a.delayMinutes < 0 || a.delayMinutes > count * d.minutesPerSlot || (a.extraPractice && (!a.training || !a.selectable))) ||
                d.activities.Select(a => a.id).Distinct().Count() != d.activities.Length)
                throw new ArgumentException("Invalid or duplicate calendar activity.");
            if (d.commitments.Any(c => c == null || string.IsNullOrWhiteSpace(c.id) || !d.activities.Any(a => a.id == c.activityId) || c.startSlot < 0 || c.startSlot >= count ||
                c.startSlot + d.activities.Single(a => a.id == c.activityId).durationSlots > count) || d.commitments.Select(c => c.id).Distinct().Count() != d.commitments.Length)
                throw new ArgumentException("Invalid or duplicate calendar commitment.");
            CareerTraining.Validate(d);
        }
        public string TimeLabel(int minute)
        {
            if (minute < 0 || minute > TotalMinutes) throw new ArgumentOutOfRangeException(nameof(minute));
            if (minute == TotalMinutes) return "Week complete";
            int slot = minute / MinutesPerSlot;
            return Days[slot / Slots.Count] + " · " + Slots[slot % Slots.Count] + (minute % MinutesPerSlot == 0 ? "" : " +" + minute % MinutesPerSlot + " min");
        }
        public CalendarAttendance ResultFor(string commitmentId) => resolved.TryGetValue(commitmentId, out var value) ? value : null;
        public IReadOnlyList<CalendarCommitment> CurrentCommitments => Commitments.Where(c => !resolved.ContainsKey(c.Id) && c.StartMinute <= Minute && Minute < c.EndMinute).ToArray();
        public CalendarChoice Preview(CalendarChoiceKind kind, string id = null, string trainingFocusId = null, PracticeFamily? practice = null)
        {
            if (AwaitingPractice) throw new InvalidOperationException("Finish or end the current practice first.");
            if (Finished || !Enum.IsDefined(typeof(CalendarChoiceKind), kind)) throw new InvalidOperationException("No valid choice remains.");
            CalendarCommitment commitment = null; CalendarActivity activity = null;
            int activityEnd, end;
            if (kind == CalendarChoiceKind.Attend || kind == CalendarChoiceKind.Skip)
            {
                commitment = CurrentCommitments.SingleOrDefault(c => c.Id == id) ?? throw new ArgumentException("Select a current unresolved commitment.");
                activity = kind == CalendarChoiceKind.Attend ? commitment.Activity : null;
                activityEnd = commitment.EndMinute;
            }
            else if (kind == CalendarChoiceKind.Wait) activityEnd = Math.Min(TotalMinutes, (Minute / MinutesPerSlot + 1) * MinutesPerSlot);
            else
            {
                activity = Activities.SingleOrDefault(a => a.Id == id && a.Selectable) ?? throw new ArgumentException("Select an available activity.");
                if (activity.ExtraPractice != (kind == CalendarChoiceKind.ExtraPractice)) throw new ArgumentException("Extra practice requires its own choice.");
                activityEnd = Minute + activity.DurationSlots * MinutesPerSlot;
                if (kind == CalendarChoiceKind.ExtraPractice)
                {
                    var last = history.LastOrDefault();
                    if (last?.Choice.Activity?.Training != true || last.Choice.Kind == CalendarChoiceKind.Skip || Minute % MinutesPerSlot != 0 ||
                        Commitments.Any(c => !resolved.ContainsKey(c.Id) && c.StartMinute < activityEnd + activity.DelayMinutes && c.EndMinute > Minute))
                        throw new InvalidOperationException("Extra practice needs the next free slot immediately after training.");
                }
            }
            TrainingFocus focus = null;
            if (activity?.Training == true && TrainingFocuses.Count > 0)
            {
                string focusId = trainingFocusId ?? commitment?.TrainingFocusId ?? activity.TrainingFocusId;
                focus = TrainingFocuses.SingleOrDefault(f => f.Id == focusId) ?? throw new ArgumentException("Choose a valid training focus.");
            }
            else if (trainingFocusId != null) throw new ArgumentException("This choice does not support a training focus.");
            if (practice.HasValue && (focus == null || !Enum.IsDefined(typeof(PracticeFamily), practice.Value))) throw new ArgumentException("Choose an available training scenario.");
            end = activityEnd + (activity?.DelayMinutes ?? 0);
            if (end > TotalMinutes) throw new InvalidOperationException("This activity and its known delay do not fit in the remaining week.");
            var impacts = new List<CalendarImpact>();
            foreach (var c in Commitments.Where(c => !resolved.ContainsKey(c.Id) && c.StartMinute < end && c.EndMinute > Minute))
            {
                if (c == commitment)
                    impacts.Add(new CalendarImpact(c, kind == CalendarChoiceKind.Skip ? AttendanceOutcome.Skipped : Minute > c.StartMinute ? AttendanceOutcome.Late : AttendanceOutcome.Attended,
                        kind == CalendarChoiceKind.Skip ? 0 : Minute - c.StartMinute));
                else impacts.Add(new CalendarImpact(c, c.EndMinute <= end ? AttendanceOutcome.Skipped : AttendanceOutcome.Late, c.EndMinute <= end ? 0 : end - c.StartMinute));
            }
            var choice = new CalendarChoice(this, revision, kind, activity, commitment, Minute, activityEnd, end, impacts, focus, practice);
            ValidateChoice?.Invoke(choice); return choice;
        }
        public CalendarDecision Commit(CalendarChoice choice)
        {
            if (choice == null || choice.Owner != this) throw new ArgumentException("Use this calendar's preview.");
            if (committed.TryGetValue(choice, out var previous)) return previous;
            if (AwaitingPractice) throw new InvalidOperationException("Finish or end the current practice first.");
            ValidateChoice?.Invoke(choice);
            if (choice.Revision != revision || choice.StartMinute != Minute) throw new InvalidOperationException("Calendar changed; inspect a new preview.");
            var records = new List<CalendarAttendance>();
            foreach (var impact in choice.Impacts)
            {
                // A future late arrival is only a warning until the user attends or misses its window.
                if (impact.Commitment != choice.Commitment && impact.Outcome == AttendanceOutcome.Late) continue;
                var c = impact.Commitment;
                var record = Record(c.Activity.Id, c.Id, c.Required, c.StartMinute, impact.Outcome,
                    impact.Outcome == AttendanceOutcome.Skipped ? 0 : c.EndMinute - Minute, impact.LateMinutes);
                resolved.Add(c.Id, record); records.Add(record);
            }
            if (choice.Activity != null && choice.Commitment == null)
                records.Add(Record(choice.Activity.Id, null, false, Minute, AttendanceOutcome.Attended, choice.ActivityEndMinute - Minute, 0));
            Minute = choice.EndMinute; revision++; AwaitingPractice = choice.Practice.HasValue;
            var result = new CalendarDecision(choice, records); history.Add(result); committed.Add(choice, result); Committed?.Invoke(result); return result;
        }
        private CalendarAttendance Record(string activityId, string commitmentId, bool required, int start, AttendanceOutcome outcome, int participated, int late)
        {
            long sequence = attendance.Count + 1;
            var fact = new ActivityResolved("calendar:" + playerId + ":" + sequence, playerId, sequence, activityId, "slot-" + start / MinutesPerSlot, outcome);
            var record = new CalendarAttendance(fact, commitmentId, required, participated, late); attendance.Add(record); return record;
        }
    }
}
