using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FootballRpg.Simulation
{
    public sealed class ProgressionRules
    {
        public double BaseThreshold { get; }
        public double ThresholdIncrease { get; }
        public double SuccessXp { get; }
        public double FailureXp { get; }
        public int RewardedRepetitions { get; }
        public double SkillGainCapPerMatch { get; }
        public ProgressionRules(double baseThreshold = 100, double thresholdIncrease = 10, double successXp = 20,
            double failureXp = 12, int rewardedRepetitions = 5, double skillGainCapPerMatch = 200)
        {
            foreach (double value in new[] { baseThreshold, thresholdIncrease, successXp, failureXp, skillGainCapPerMatch })
                if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (rewardedRepetitions < 1) throw new ArgumentOutOfRangeException(nameof(rewardedRepetitions));
            BaseThreshold = baseThreshold; ThresholdIncrease = thresholdIncrease; SuccessXp = successXp;
            FailureXp = failureXp; RewardedRepetitions = rewardedRepetitions; SkillGainCapPerMatch = skillGainCapPerMatch;
        }
        public double Cost(int level, int floor) => BaseThreshold + (level - floor) * ThresholdIncrease;
        public double Threshold(int level, int floor)
        {
            double steps = level - floor;
            return steps * BaseThreshold + steps * (steps - 1) * ThresholdIncrease / 2;
        }
        public double OutcomeXp(float chance, bool succeeded, double significance)
        {
            DefinitionValidator.Unit(chance, nameof(chance));
            if (double.IsNaN(significance) || double.IsInfinity(significance) || significance < .25 || significance > 3)
                throw new ArgumentOutOfRangeException(nameof(significance));
            return significance * (succeeded ? SuccessXp * (1 + 3 * (1 - chance)) : -FailureXp * (.5 + chance));
        }
    }

    public sealed class SkillExperience
    {
        private readonly ProgressionRules rules;
        public int Floor { get; }
        public int Cap { get; }
        public int Level { get; private set; }
        public double TotalXp { get; private set; }
        public double WithinLevelXp => TotalXp - rules.Threshold(Level, Floor);
        public double NextLevelXp => Level == Cap ? 0 : rules.Cost(Level, Floor);
        internal SkillExperience(int initial, ValueDefinition definition, ProgressionRules rules)
        {
            this.rules = rules; Floor = definition.minimum; Cap = definition.maximum;
            Level = initial; TotalXp = rules.Threshold(initial, Floor);
        }
        internal double Apply(double delta)
        {
            double before = TotalXp;
            TotalXp = Math.Max(0, Math.Min(rules.Threshold(Cap, Floor), TotalXp + delta));
            while (Level < Cap && TotalXp >= rules.Threshold(Level + 1, Floor)) Level++;
            while (Level > Floor && TotalXp < rules.Threshold(Level, Floor)) Level--;
            return TotalXp - before;
        }
    }

    public sealed class SkillXpChange
    {
        public string MatchId { get; }
        public string EventId { get; }
        public string PlayerId { get; }
        public string ActionId { get; }
        public IReadOnlyDictionary<string, double> Deltas { get; }
        public bool Succeeded { get; }
        internal SkillXpChange(string matchId, ActionResolved action, Dictionary<string, double> deltas)
        {
            MatchId = matchId; EventId = action.EventId; PlayerId = action.PlayerId; ActionId = action.ActionId;
            Succeeded = action.Succeeded; Deltas = new ReadOnlyDictionary<string, double>(deltas);
        }
    }

    /// <summary>Career-owned XP and exactly-once match ledger. No RNG or temporary-condition effects.</summary>
    public sealed class SkillProgression
    {
        private readonly CareerState career;
        private readonly Dictionary<string, IReadOnlyDictionary<string, SkillExperience>> skills = new Dictionary<string, IReadOnlyDictionary<string, SkillExperience>>();
        private readonly Dictionary<(string match, string eventId), SkillXpChange> applied = new Dictionary<(string, string), SkillXpChange>();
        private readonly Dictionary<(string match, string player, string action), int> repetitions = new Dictionary<(string, string, string), int>();
        private readonly Dictionary<(string match, string player, string skill), double> awarded = new Dictionary<(string, string, string), double>();
        private readonly HashSet<string> matches = new HashSet<string>();
        private readonly List<SkillXpChange> changes = new List<SkillXpChange>();
        public ProgressionRules Rules { get; }
        public IReadOnlyList<SkillXpChange> Changes { get; }
        internal SkillProgression(CareerState career, ValueDefinition[] definitions, ProgressionRules rules)
        {
            this.career = career; Rules = rules;
            Changes = changes.AsReadOnly();
            foreach (var player in career.Players.Values)
                skills.Add(player.Id, new ReadOnlyDictionary<string, SkillExperience>(definitions.ToDictionary(d => d.id,
                    d => new SkillExperience(player.FootballAttributes[d.id], d, rules))));
        }
        public IReadOnlyDictionary<string, SkillExperience> ForPlayer(string id) => skills[id];
        public string BeginMatch()
        {
            string id = "career-match-" + (matches.Count + 1);
            matches.Add(id); return id;
        }
        public SkillXpChange Apply(string matchId, ActionResolved action, IReadOnlyDictionary<string, float> attribution, double significance = 1)
        {
            if (matchId == null || !matches.Contains(matchId) || action == null) throw new ArgumentException("A career match and action are required.");
            var key = (matchId, action.EventId);
            if (applied.TryGetValue(key, out var previous)) return previous;
            if (!skills.TryGetValue(action.PlayerId, out var playerSkills) || attribution == null || attribution.Count == 0 ||
                attribution.Any(p => !playerSkills.ContainsKey(p.Key) || float.IsNaN(p.Value) || float.IsInfinity(p.Value) || p.Value <= 0) ||
                Math.Abs(attribution.Values.Sum(v => (double)v) - 1) > .0001)
                throw new ArgumentException("Attribution must contain football skills with positive weights totalling one.");
            double amount = Rules.OutcomeXp(action.SuccessProbability, action.Succeeded, significance);
            var repetitionKey = (matchId, action.PlayerId, action.ActionId);
            repetitions.TryGetValue(repetitionKey, out int count);
            // Failures retain their full cost; changing target/variant cannot reset a repeated action family.
            if (action.Succeeded) amount *= Math.Max(0, 1 - count / (double)Rules.RewardedRepetitions);
            repetitions[repetitionKey] = count + 1;
            var deltas = new Dictionary<string, double>();
            foreach (var share in attribution)
            {
                var awardKey = (matchId, action.PlayerId, share.Key);
                awarded.TryGetValue(awardKey, out double gained);
                double delta = amount * share.Value;
                if (delta > 0) delta = Math.Min(delta, Math.Max(0, Rules.SkillGainCapPerMatch - gained));
                double actual = playerSkills[share.Key].Apply(delta);
                if (actual > 0) awarded[awardKey] = gained + actual;
                career.Players[action.PlayerId].SetFootballLevel(share.Key, playerSkills[share.Key].Level);
                deltas.Add(share.Key, actual);
            }
            var change = new SkillXpChange(matchId, action, deltas);
            applied.Add(key, change); changes.Add(change); return change;
        }
    }
}
