using System;
using System.Collections.Generic;
using System.Linq;

namespace FootballRpg.Simulation
{
    /// <summary>Tuning for temporary condition. Activity workloads live in the calendar content.</summary>
    public sealed class ReadinessRules
    {
        public double IdleRecoveryPerSlot { get; }
        public double MissedRequiredMorale { get; }
        public double LateRequiredMorale { get; }
        public double SuccessConfidence { get; }
        public double FailureConfidence { get; }
        public double MatchConfidenceBudget { get; }
        public double ResultMorale { get; }
        public double InjuryMorale { get; }
        public double KnockFatigue { get; }
        public double TrainingFatiguePenalty { get; }
        public double TrainingConfidenceWeight { get; }
        public double TrainingMoraleWeight { get; }
        public ReadinessRules(double idleRecoveryPerSlot = .04, double missedRequiredMorale = .04, double lateRequiredMorale = .02,
            double successConfidence = .008, double failureConfidence = .012, double matchConfidenceBudget = .12,
            double resultMorale = .05, double injuryMorale = .04, double knockFatigue = .1,
            double trainingFatiguePenalty = .7, double trainingConfidenceWeight = .1, double trainingMoraleWeight = .1)
        {
            foreach (var value in new[] { idleRecoveryPerSlot, missedRequiredMorale, lateRequiredMorale, successConfidence,
                failureConfidence, matchConfidenceBudget, resultMorale, injuryMorale, knockFatigue, trainingFatiguePenalty,
                trainingConfidenceWeight, trainingMoraleWeight })
                if (double.IsNaN(value) || value < 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(value));
            IdleRecoveryPerSlot = idleRecoveryPerSlot; MissedRequiredMorale = missedRequiredMorale; LateRequiredMorale = lateRequiredMorale;
            SuccessConfidence = successConfidence; FailureConfidence = failureConfidence; MatchConfidenceBudget = matchConfidenceBudget;
            ResultMorale = resultMorale; InjuryMorale = injuryMorale; KnockFatigue = knockFatigue;
            TrainingFatiguePenalty = trainingFatiguePenalty; TrainingConfidenceWeight = trainingConfidenceWeight; TrainingMoraleWeight = trainingMoraleWeight;
        }
    }
    public sealed class ReadinessChange
    {
        public string PlayerId { get; }
        public string CauseId { get; }
        public string Reason { get; }
        public double FatigueDelta { get; }
        public double ConfidenceDelta { get; }
        public double MoraleDelta { get; }
        public int InjuryDaysDelta { get; }
        internal ReadinessChange(string player, string cause, string reason, double fatigue, double confidence, double morale, int injury = 0)
        { PlayerId = player; CauseId = cause; Reason = reason; FatigueDelta = fatigue; ConfidenceDelta = confidence; MoraleDelta = morale; InjuryDaysDelta = injury; }
    }
    /// <summary>Readiness-weighted participation, consumed by the future training XP system.</summary>
    public sealed class TrainingReadiness
    {
        public string AttendanceId { get; }
        public double Multiplier { get; }
        public double ParticipatedSlots { get; }
        public double EffectiveSlots => Multiplier * ParticipatedSlots;
        internal TrainingReadiness(string id, double multiplier, double slots)
        { AttendanceId = id; Multiplier = multiplier; ParticipatedSlots = slots; }
    }
    public sealed class CareerReadiness
    {
        private readonly CareerState career;
        private readonly List<ReadinessChange> changes = new List<ReadinessChange>();
        private readonly List<TrainingReadiness> training = new List<TrainingReadiness>();
        private readonly HashSet<(string scope, string cause)> applied = new HashSet<(string, string)>();
        private readonly Dictionary<(string match, string player), double> confidenceSpent = new Dictionary<(string, string), double>();
        private readonly Dictionary<string, int> injuryMinutes = new Dictionary<string, int>();
        private readonly Dictionary<(string match, string player), (double fatigue, double seconds, int batch)> exertion =
            new Dictionary<(string, string), (double, double, int)>();
        public ReadinessRules Rules { get; }
        public IReadOnlyList<ReadinessChange> Changes { get; }
        public IReadOnlyList<TrainingReadiness> Training { get; }
        internal CareerReadiness(CareerState career, ReadinessRules rules)
        { this.career = career; Rules = rules; Changes = changes.AsReadOnly(); Training = training.AsReadOnly(); }
        private static float Unit(double value) => (float)Math.Max(0, Math.Min(1, value));
        public double TrainingMultiplier(string playerId)
        {
            var p = career.Players[playerId];
            return p.InjuryDaysRemaining > 0 ? 0 : Math.Max(0, Math.Min(1.2, 1 - Rules.TrainingFatiguePenalty * p.Fatigue +
                Rules.TrainingConfidenceWeight * (p.Confidence - .5) + Rules.TrainingMoraleWeight * (p.Morale - .5)));
        }
        private void Change(string id, string cause, string reason, double fatigue = 0, double confidence = 0, double morale = 0, int injury = 0)
        {
            var p = career.Players[id]; float f = p.Fatigue, c = p.Confidence, m = p.Morale;
            p.SetCondition(Unit(c + confidence), Unit(m + morale), Unit(f + fatigue));
            changes.Add(new ReadinessChange(id, cause, reason, p.Fatigue - f, p.Confidence - c, p.Morale - m, injury));
        }
        internal void ApplyCalendar(CalendarDecision decision)
        {
            var calendar = career.Calendar; var choice = decision.Choice; var p = career.UserPlayer;
            string cause = "calendar-time:" + choice.StartMinute;
            if (!applied.Add(("calendar", cause))) return;
            // Snapshot readiness before workload, recovery or attendance mood changes. Late training earns only its actual duration.
            foreach (var r in decision.Attendance.Where(r => r.ParticipatedMinutes > 0 && calendar.Activities.Single(a => a.Id == r.Fact.ActivityId).Training))
                training.Add(new TrainingReadiness(r.Fact.EventId, TrainingMultiplier(p.Id), r.ParticipatedMinutes / (double)calendar.MinutesPerSlot));
            var activity = choice.Activity;
            double slots = (choice.ActivityEndMinute - choice.StartMinute) / (double)calendar.MinutesPerSlot;
            if (activity == null) Change(p.Id, cause, "Unscheduled recovery", -Rules.IdleRecoveryPerSlot * slots);
            else if (activity.Training && p.InjuryDaysRemaining > 0)
                Change(p.Id, cause, activity.Name + ": injured — no physical training yield or workload");
            else Change(p.Id, cause, activity.Name, activity.FatiguePerSlot * slots, morale: activity.MoralePerSlot * slots);
            foreach (var r in decision.Attendance.Where(r => r.Required && r.Fact.Attendance != AttendanceOutcome.Attended))
                Change(p.Id, r.Fact.EventId, calendar.Activities.Single(a => a.Id == r.Fact.ActivityId).Name + ": " + r.Fact.Attendance,
                    morale: -(r.Fact.Attendance == AttendanceOutcome.Skipped ? Rules.MissedRequiredMorale : Rules.LateRequiredMorale));
            // Injury recovery uses elapsed career days, never wall-clock time, navigation or the independent match clock.
            int dayMinutes = calendar.Slots.Count * calendar.MinutesPerSlot;
            foreach (var injured in career.Players.Values.Where(x => x.InjuryDaysRemaining > 0))
            {
                injuryMinutes.TryGetValue(injured.Id, out int elapsed); elapsed += choice.CostMinutes;
                int healed = Math.Min(injured.InjuryDaysRemaining, elapsed / dayMinutes);
                injured.InjuryDaysRemaining -= healed;
                injuryMinutes[injured.Id] = injured.InjuryDaysRemaining == 0 ? 0 : elapsed % dayMinutes;
                if (healed > 0) Change(injured.Id, cause, "Time healing injury", injury: -healed);
            }
        }
        internal void ApplyPerformance(string match, ActionResolved action, double significance)
        {
            if (significance < 1.5 || !applied.Add((match, "performance:" + action.EventId))) return;
            var key = (match, action.PlayerId); confidenceSpent.TryGetValue(key, out double spent);
            double amount = significance * (action.Succeeded ? Rules.SuccessConfidence * (1.5 - action.SuccessProbability)
                : Rules.FailureConfidence * (.5 + action.SuccessProbability));
            amount = Math.Min(amount, Math.Max(0, Rules.MatchConfidenceBudget - spent));
            confidenceSpent[key] = spent + amount;
            if (amount > 0) Change(action.PlayerId, match + ":" + action.EventId,
                action.ActionId + (action.Succeeded ? ": success" : ": failure"), confidence: action.Succeeded ? amount : -amount);
        }
        internal void MatchResult(string match, string player, int result)
        {
            if (!applied.Add((match, "result:" + player))) return;
            Change(player, match + ":result", result > 0 ? "Match won" : result < 0 ? "Match lost" : "Match drawn", morale: Rules.ResultMorale * Math.Sign(result));
        }
        internal void Injury(string match, string player, string eventId, int days)
        {
            if (!applied.Add((match, "injury:" + eventId))) return;
            var p = career.Players[player]; int before = p.InjuryDaysRemaining;
            p.InjuryDaysRemaining = Math.Max(before, days);
            if (days > 0) injuryMinutes[player] = 0;
            Change(player, match + ":injury:" + eventId, days > 0 ? "Match injury" : "Match knock", days == 0 ? Rules.KnockFatigue : 0,
                morale: -Rules.InjuryMorale, injury: p.InjuryDaysRemaining - before);
        }
        internal void Exertion(string match, string player, double fatigue, double seconds)
        {
            var p = career.Players[player]; float before = p.Fatigue;
            p.SetCondition(p.Confidence, p.Morale, Unit(before + fatigue));
            var key = (match, player); exertion.TryGetValue(key, out var pending);
            exertion[key] = (pending.fatigue + p.Fatigue - before, pending.seconds + seconds, pending.batch);
            if (pending.seconds + seconds >= 60) FlushExertionPlayer(key);
        }
        internal void FlushExertion(string match)
        { foreach (var key in exertion.Keys.Where(k => k.match == match).ToArray()) FlushExertionPlayer(key); }
        private void FlushExertionPlayer((string match, string player) key)
        {
            var pending = exertion[key]; if (pending.seconds <= 0) return;
            changes.Add(new ReadinessChange(key.player, key.match + ":exertion:" + pending.batch, "Match exertion", pending.fatigue, 0, 0));
            exertion[key] = (0, 0, pending.batch + 1);
        }
    }
}
