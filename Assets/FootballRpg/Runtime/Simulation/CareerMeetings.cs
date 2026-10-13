using System;
using System.Collections.Generic;
using System.Linq;

namespace FootballRpg.Simulation
{
    [Serializable] public sealed class MeetingEffectDefinition { public int affinity, trust, standing; }
    [Serializable] public sealed class MeetingChoiceDefinition
    {
        public string id, text, trait, focus, preference, successText, failureText;
        public int difficulty;
        public MeetingEffectDefinition success, failure;
    }
    [Serializable] public sealed class MeetingDefinition
    {
        public string id, commitmentId, role, opening;
        public MeetingChoiceDefinition[] choices;
        public MeetingEffectDefinition attended, late, skipped, leave;
    }
    public enum CareerPreference { PlayingTime, Development, Challenge }
    public sealed class MeetingOption
    {
        public string Id { get; }
        public string Text { get; }
        public string Trait { get; }
        public int Difficulty { get; }
        public string Focus { get; }
        public CareerPreference? Preference { get; }
        public string SuccessText { get; }
        public string FailureText { get; }
        public SocialEffect Success { get; }
        public SocialEffect Failure { get; }
        internal MeetingOption(MeetingChoiceDefinition d)
        {
            Id = d.id; Text = d.text; Trait = d.trait; Difficulty = d.difficulty; Focus = d.focus;
            Preference = string.IsNullOrEmpty(d.preference) ? (CareerPreference?)null : (CareerPreference)Enum.Parse(typeof(CareerPreference), d.preference);
            SuccessText = d.successText; FailureText = d.failureText; Success = CareerMeetings.Effect(d.success); Failure = CareerMeetings.Effect(d.failure);
        }
    }
    public sealed class Meeting
    {
        public string Id { get; }
        public string CommitmentId { get; }
        public string ContactId { get; }
        public SocialRole Role { get; }
        public string Opening { get; }
        public IReadOnlyList<MeetingOption> Options { get; }
        public SocialEffect Attended { get; }
        public SocialEffect Late { get; }
        public SocialEffect Skipped { get; }
        public SocialEffect Leave { get; }
        internal Meeting(MeetingDefinition d, string contact)
        {
            Id = d.id; CommitmentId = d.commitmentId; ContactId = contact; Role = (SocialRole)Enum.Parse(typeof(SocialRole), d.role); Opening = d.opening;
            Options = Array.AsReadOnly(d.choices.Select(c => new MeetingOption(c)).ToArray());
            Attended = CareerMeetings.Effect(d.attended); Late = CareerMeetings.Effect(d.late); Skipped = CareerMeetings.Effect(d.skipped); Leave = CareerMeetings.Effect(d.leave);
        }
    }
    public sealed class MeetingPreview
    {
        internal CareerMeetings Owner { get; }
        internal string Context { get; }
        public Meeting Meeting { get; }
        public MeetingOption Option { get; }
        public int Percent { get; }
        public float Chance => Percent / 100f;
        public string TrainingFocusId { get; }
        internal MeetingPreview(CareerMeetings owner, string context, Meeting meeting, MeetingOption option, int percent, string focus)
        { Owner = owner; Context = context; Meeting = meeting; Option = option; Percent = percent; TrainingFocusId = focus; }
    }
    public sealed class MeetingResult
    {
        public Meeting Meeting { get; }
        public string ChoiceId { get; }
        public string Text { get; }
        public float? Roll { get; }
        public int? Percent { get; }
        public bool? Succeeded => Roll.HasValue ? Roll.Value < Percent.Value / 100f : (bool?)null;
        public string FocusId { get; }
        public CareerPreference? Preference { get; }
        public IReadOnlyList<RelationshipChanged> Changes { get; }
        internal MeetingResult(Meeting meeting, string choice, string text, float? roll, int? percent, string focus, CareerPreference? preference, IReadOnlyList<RelationshipChanged> changes)
        { Meeting = meeting; ChoiceId = choice; Text = text; Roll = roll; Percent = percent; FocusId = focus; Preference = preference; Changes = changes; }
    }
    /// <summary>Authored appointments spend time via the calendar; one frozen conversation resolves their social consequences.</summary>
    public sealed class CareerMeetings
    {
        private readonly CareerState career;
        private readonly SeededRandom random;
        private readonly Dictionary<string, MeetingResult> resolved = new Dictionary<string, MeetingResult>();
        private readonly List<MeetingResult> results = new List<MeetingResult>();
        private readonly HashSet<string> attendance = new HashSet<string>();
        public IReadOnlyList<Meeting> Meetings { get; }
        public IReadOnlyList<MeetingResult> Results { get; }
        public Meeting Pending { get; private set; }
        public string PreferredTrainingFocusId => career.Calendar?.PreferredTrainingFocusId;
        public CareerPreference? Preference { get; private set; }
        public long DrawCount { get; private set; }
        internal static SocialEffect Effect(MeetingEffectDefinition d) => new SocialEffect(d.affinity, d.trust, d.standing);
        internal CareerMeetings(CareerState career, MeetingDefinition[] definitions, ulong seed)
        {
            this.career = career; random = new SeededRandom(seed); Results = results.AsReadOnly();
            Meetings = Array.AsReadOnly((definitions ?? Array.Empty<MeetingDefinition>()).Select(d =>
            {
                var role = (SocialRole)Enum.Parse(typeof(SocialRole), d.role);
                var contact = career.Social.Relationships.Values.Where(r => r.Role == role)
                    .OrderByDescending(r => role == SocialRole.Teammate ? career.Players[r.PersonId].Age : 0).First();
                return new Meeting(d, contact.PersonId);
            }).ToArray());
        }
        public static void Validate(GameDefinitions d)
        {
            var meetings = d.meetings ?? Array.Empty<MeetingDefinition>();
            if (meetings.Length == 0) return;
            if (d.calendar == null || meetings.Any(m => m == null || string.IsNullOrWhiteSpace(m.id) || string.IsNullOrWhiteSpace(m.opening) ||
                !Enum.TryParse(m.role, out SocialRole role) || !Enum.IsDefined(typeof(SocialRole), role) || m.choices == null || m.choices.Length == 0 ||
                !d.calendar.commitments.Any(c => c.id == m.commitmentId))) throw new ArgumentException("Invalid meeting or appointment reference.");
            if (meetings.Select(m => m.id).Distinct().Count() != meetings.Length || meetings.Select(m => m.commitmentId).Distinct().Count() != meetings.Length) throw new ArgumentException("Duplicate meeting/appointment.");
            foreach (var m in meetings)
            {
                var appointment = d.calendar.commitments.Single(c => c.id == m.commitmentId);
                var activity = d.calendar.activities.Single(a => a.id == appointment.activityId);
                if (activity.training || d.calendar.commitments.Any(c => c.id != appointment.id && c.startSlot < appointment.startSlot + activity.durationSlots &&
                    c.startSlot + d.calendar.activities.Single(a => a.id == c.activityId).durationSlots > appointment.startSlot)) throw new ArgumentException("Meeting appointments must have their own non-training slot.");
                if (m.choices.Select(c => c?.id).Distinct().Count() != m.choices.Length) throw new ArgumentException("Duplicate meeting choice.");
                foreach (var c in m.choices)
                {
                    if (c == null || string.IsNullOrWhiteSpace(c.id) || string.IsNullOrWhiteSpace(c.text) || string.IsNullOrWhiteSpace(c.successText) || string.IsNullOrWhiteSpace(c.failureText) ||
                        !d.personalityTraits.Any(t => t.id == c.trait) || c.difficulty < 1 || c.difficulty > 100 ||
                        !string.IsNullOrEmpty(c.focus) && (m.role != "Coach" || (c.focus == "selected" ? (d.calendar.trainingFocuses?.Length ?? 0) == 0 : !(d.calendar.trainingFocuses ?? Array.Empty<TrainingFocusDefinition>()).Any(f => f.id == c.focus))) ||
                        !string.IsNullOrEmpty(c.preference) && (m.role != "Agent" || !Enum.TryParse(c.preference, out CareerPreference preference) || !Enum.IsDefined(typeof(CareerPreference), preference)))
                        throw new ArgumentException("Invalid authored meeting choice.");
                    ValidateEffect(c.success); ValidateEffect(c.failure);
                }
                ValidateEffect(m.attended); ValidateEffect(m.late); ValidateEffect(m.skipped); ValidateEffect(m.leave);
            }
        }
        private static void ValidateEffect(MeetingEffectDefinition e)
        { if (e == null || new[] { e.affinity, e.trust, e.standing }.Any(v => v < -100 || v > 100)) throw new ArgumentException("Invalid meeting effect."); }
        internal void ApplyCalendar(CalendarDecision decision)
        {
            foreach (var a in decision.Attendance)
            {
                var m = Meetings.SingleOrDefault(m => m.CommitmentId == a.CommitmentId);
                if (m == null || !attendance.Add(a.Fact.EventId)) continue;
                var effect = a.Fact.Attendance == AttendanceOutcome.Skipped ? m.Skipped : a.Fact.Attendance == AttendanceOutcome.Late ? m.Late : m.Attended;
                var changes = career.Social.ApplyRelationshipEffect(a.Fact.EventId + ":meeting-attendance", m.ContactId, effect);
                if (a.Fact.Attendance == AttendanceOutcome.Skipped)
                {
                    var result = new MeetingResult(m, "skipped", "You missed the appointment. Existing focus and career preferences are unchanged.", null, null, PreferredTrainingFocusId, Preference, changes);
                    resolved.Add(m.Id, result); results.Add(result);
                }
                else { Pending = m; career.Calendar.AwaitingMeeting = true; }
            }
        }
        public string Introduction(Meeting m)
        {
            if (m == null || !Meetings.Contains(m)) throw new ArgumentException("Unknown meeting.");
            if (m.Role != SocialRole.Coach) return m.Opening;
            return m.Opening + "\n\nAssigned role: " + career.Coach.AssignedRole.ToUpperInvariant() + ". " + career.Coach.Select().Reason +
                $"\nCoach values discipline {career.Coach.Profile.DisciplinePreference:P0} and creativity {career.Coach.Profile.CreativityPreference:P0}. Attend training, arrive on time, and follow instructions or explain your choices through their results." +
                "\nRecommended practice: " + GuidedPracticeSession.Recommended(career.Coach.AssignedRole) + ". " + GuidedPracticeSession.Guidance(GuidedPracticeSession.Recommended(career.Coach.AssignedRole));
        }
        private string Context(Meeting m) => string.Join("/", career.Calendar.Minute, career.Social.Revision, m.Id, PreferredTrainingFocusId, Preference,
            string.Join(",", m.Options.Select(o => career.UserPlayer.PersonalityTraits[o.Trait])));
        public MeetingPreview Preview(string optionId, string focusId = null)
        {
            var m = Pending ?? throw new InvalidOperationException("Attend a scheduled conversation first.");
            var option = m.Options.SingleOrDefault(o => o.Id == optionId) ?? throw new ArgumentException("Unknown conversation choice.");
            string focus = option.Focus == "selected" ? focusId : string.IsNullOrEmpty(option.Focus) ? null : option.Focus;
            if (option.Focus == "selected" && focus == null || focus != null && !career.Calendar.TrainingFocuses.Any(f => f.Id == focus)) throw new ArgumentException("Select an available training focus.");
            double chance = .5 + (career.UserPlayer.PersonalityTraits[option.Trait] - option.Difficulty) * career.Social.Rules.AttributeWeight;
            int percent = (int)Math.Round(Math.Max(.05, Math.Min(.95, chance)) * 100, MidpointRounding.AwayFromZero);
            return new MeetingPreview(this, Context(m), m, option, percent, focus);
        }
        public MeetingResult Commit(MeetingPreview preview)
        {
            if (preview == null || preview.Owner != this) throw new ArgumentException("Use this career's preview.");
            if (resolved.TryGetValue(preview.Meeting.Id, out var previous))
            {
                if (previous.ChoiceId != preview.Option.Id || previous.FocusId != (preview.TrainingFocusId ?? previous.FocusId)) throw new InvalidOperationException("This meeting has already ended differently.");
                return previous;
            }
            if (Pending != preview.Meeting || preview.Context != Context(Pending)) throw new InvalidOperationException("Context changed; inspect a new conversation choice.");
            float roll = random.NextUnitFloat(); DrawCount++; bool success = roll < preview.Chance;
            // Stated preferences are the player's choice; the check only decides the contact's response.
            if (preview.TrainingFocusId != null) career.Calendar.PreferredTrainingFocusId = preview.TrainingFocusId;
            if (preview.Option.Preference.HasValue) Preference = preview.Option.Preference;
            var changes = career.Social.ApplyRelationshipEffect("meeting:" + preview.Meeting.Id + ":choice", preview.Meeting.ContactId, success ? preview.Option.Success : preview.Option.Failure);
            return Finish(new MeetingResult(preview.Meeting, preview.Option.Id, success ? preview.Option.SuccessText : preview.Option.FailureText, roll, preview.Percent, PreferredTrainingFocusId, Preference, changes));
        }
        public MeetingResult Leave()
        {
            var m = Pending ?? throw new InvalidOperationException("No conversation is pending.");
            var changes = career.Social.ApplyRelationshipEffect("meeting:" + m.Id + ":leave", m.ContactId, m.Leave);
            return Finish(new MeetingResult(m, "leave", "You leave without agreeing a plan. Existing preferences are unchanged; time is not refunded.", null, null, PreferredTrainingFocusId, Preference, changes));
        }
        private MeetingResult Finish(MeetingResult result)
        { resolved.Add(result.Meeting.Id, result); results.Add(result); Pending = null; career.Calendar.AwaitingMeeting = false; return result; }
    }
}
