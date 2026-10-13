using System;
using System.Collections.Generic;
using System.Linq;

namespace FootballRpg.Simulation
{
    [Serializable] public sealed class TrainingFocusDefinition
    {
        public string id, name;
        public bool usePosition;
        public string[] attributeIds;
    }
    public sealed class TrainingFocus
    {
        public string Id { get; }
        public string Name { get; }
        public bool UsePosition { get; }
        public IReadOnlyList<string> AttributeIds { get; }
        internal TrainingFocus(TrainingFocusDefinition d)
        { Id = d.id; Name = d.name; UsePosition = d.usePosition; AttributeIds = Array.AsReadOnly((string[])(d.attributeIds ?? Array.Empty<string>()).Clone()); }
    }
    public sealed class TrainingRules
    {
        public double BaseXpPerSlot { get; }
        public double QualityFloor { get; }
        public double QualityWeight { get; }
        public double LevelPenalty { get; }
        public double FocusShare { get; }
        public TrainingRules(double baseXpPerSlot = 120, double qualityFloor = .5, double qualityWeight = 1, double levelPenalty = .75, double focusShare = .7)
        {
            foreach (double value in new[] { baseXpPerSlot, qualityFloor, qualityWeight, levelPenalty, focusShare })
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (baseXpPerSlot == 0 || baseXpPerSlot > 1000000 || qualityFloor == 0 || qualityFloor > 1 || qualityWeight > 1 || levelPenalty > 1 || focusShare > 1)
                throw new ArgumentOutOfRangeException(nameof(baseXpPerSlot));
            BaseXpPerSlot = baseXpPerSlot; QualityFloor = qualityFloor; QualityWeight = qualityWeight; LevelPenalty = levelPenalty; FocusShare = focusShare;
        }
        public double Quality(float value) => QualityFloor + QualityWeight * value;
        public double Level(SkillExperience skill) => 1 - LevelPenalty * (skill.Level - skill.Floor) / (double)(skill.Cap - skill.Floor);
    }
    public sealed class TrainingSkillGain
    {
        public string SkillId { get; }
        public double Share { get; }
        public double LevelMultiplier { get; }
        public double Xp { get; }
        public int PreviousLevel { get; }
        public int NewLevel { get; }
        internal TrainingSkillGain(string skill, double share, double levelMultiplier, double xp, int before, int after)
        { SkillId = skill; Share = share; LevelMultiplier = levelMultiplier; Xp = xp; PreviousLevel = before; NewLevel = after; }
    }
    public sealed class TrainingReport
    {
        public string AttendanceId { get; }
        public string FocusName { get; }
        public double BaseXp { get; }
        public double CoachMultiplier { get; }
        public double StaffMultiplier { get; }
        public double FacilityMultiplier { get; }
        public double ReadinessMultiplier { get; }
        public double ParticipatedSlots { get; }
        public IReadOnlyList<TrainingSkillGain> Gains { get; }
        public double TotalXp => Gains.Sum(g => g.Xp);
        internal TrainingReport(string attendanceId, string focusName, double baseXp, double coach, double staff, double facility,
            double readiness, double slots, List<TrainingSkillGain> gains)
        {
            AttendanceId = attendanceId; FocusName = focusName; BaseXp = baseXp; CoachMultiplier = coach; StaffMultiplier = staff;
            FacilityMultiplier = facility; ReadinessMultiplier = readiness; ParticipatedSlots = slots; Gains = gains.AsReadOnly();
        }
    }
    /// <summary>Calendar-driven broad training. Readiness snapshots precede workload; XP is owned by SkillProgression.</summary>
    public sealed class CareerTraining
    {
        private readonly CareerState career;
        private readonly Dictionary<string, string[]> roleSkills;
        private readonly List<TrainingReport> results = new List<TrainingReport>();
        private readonly HashSet<string> applied = new HashSet<string>();
        public TrainingRules Rules { get; }
        public IReadOnlyList<TrainingReport> Results { get; }
        internal CareerTraining(CareerState career, PositionDefinition[] positions, TrainingRules rules)
        {
            this.career = career; Rules = rules; Results = results.AsReadOnly();
            roleSkills = positions.ToDictionary(p => p.id, p => (string[])p.recommendedAttributeIds.Clone());
        }
        internal static void Validate(CalendarDefinition calendar)
        {
            var focuses = calendar.trainingFocuses ?? Array.Empty<TrainingFocusDefinition>();
            if (focuses.Any(f => f == null || string.IsNullOrWhiteSpace(f.id) || string.IsNullOrWhiteSpace(f.name) ||
                (!f.usePosition && (f.attributeIds == null || f.attributeIds.Length < 2 || f.attributeIds.Any(string.IsNullOrWhiteSpace) || f.attributeIds.Distinct().Count() != f.attributeIds.Length)) ||
                (f.usePosition && f.attributeIds != null && f.attributeIds.Length != 0)) || focuses.Select(f => f.id).Distinct().Count() != focuses.Length)
                throw new ArgumentException("Invalid or duplicate broad training focus.");
            foreach (var a in calendar.activities)
                if (a.training && focuses.Length > 0 ? !focuses.Any(f => f.id == a.trainingFocusId) : !string.IsNullOrEmpty(a.trainingFocusId))
                    throw new ArgumentException("Training activity must reference a configured focus.");
            foreach (var c in calendar.commitments.Where(c => !string.IsNullOrEmpty(c.trainingFocusId)))
                if (!calendar.activities.Single(a => a.id == c.activityId).training || !focuses.Any(f => f.id == c.trainingFocusId))
                    throw new ArgumentException("Scheduled training focus is invalid.");
        }
        public TrainingReport Preview(CalendarChoice choice)
        {
            if (choice == null || choice.Owner != career.Calendar) throw new ArgumentException("Use this career's calendar preview.");
            if (choice.Focus == null) return null;
            return Calculate(choice.Focus, null, career.Readiness.TrainingMultiplier(career.UserPlayerId),
                (choice.ActivityEndMinute - choice.StartMinute) / (double)career.Calendar.MinutesPerSlot);
        }
        internal void ApplyCalendar(CalendarDecision decision)
        {
            if (decision.Choice.Focus == null) return;
            var attendance = decision.Attendance.Single(r => r.ParticipatedMinutes > 0 && r.Fact.ActivityId == decision.Choice.Activity.Id);
            if (applied.Contains(attendance.Fact.EventId)) return;
            var readiness = career.Readiness.Training.Single(t => t.AttendanceId == attendance.Fact.EventId);
            var report = Calculate(decision.Choice.Focus, attendance.Fact.EventId, readiness.Multiplier, readiness.ParticipatedSlots);
            career.Progression.ApplyTraining(career.UserPlayerId, attendance.Fact.EventId, report.Gains.ToDictionary(g => g.SkillId, g => g.Xp));
            applied.Add(attendance.Fact.EventId); results.Add(report);
        }
        private TrainingReport Calculate(TrainingFocus focus, string attendanceId, double readiness, double slots)
        {
            var player = career.UserPlayer; var club = career.Clubs[player.ClubId]; var coach = career.Coaches[club.CoachId];
            double coachFactor = Rules.Quality(coach.CoachingQuality), staffFactor = Rules.Quality(club.StaffQuality), facilityFactor = Rules.Quality(club.FacilityQuality);
            var role = roleSkills[player.PrimaryPositionId]; var chosen = focus.UsePosition ? role : focus.AttributeIds.ToArray();
            var shares = new Dictionary<string, double>();
            foreach (var id in role) shares[id] = (1 - Rules.FocusShare) / role.Length;
            foreach (var id in chosen) { shares.TryGetValue(id, out double value); shares[id] = value + Rules.FocusShare / chosen.Length; }
            double amount = Rules.BaseXpPerSlot * slots * readiness * coachFactor * staffFactor * facilityFactor;
            var gains = new List<TrainingSkillGain>();
            foreach (var share in shares.Where(p => p.Value > 0))
            {
                var skill = career.Progression.ForPlayer(player.Id)[share.Key]; double levelFactor = Rules.Level(skill);
                double xp = Math.Min(amount * share.Value * levelFactor, career.Progression.Rules.Threshold(skill.Cap, skill.Floor) - skill.TotalXp);
                int level = skill.Level;
                while (level < skill.Cap && skill.TotalXp + xp >= career.Progression.Rules.Threshold(level + 1, skill.Floor)) level++;
                gains.Add(new TrainingSkillGain(share.Key, share.Value, levelFactor, xp, skill.Level, level));
            }
            return new TrainingReport(attendanceId, focus.Name, Rules.BaseXpPerSlot, coachFactor, staffFactor, facilityFactor, readiness, slots, gains);
        }
    }
}
