using System;

namespace FootballRpg.Simulation
{
    public enum ParticipationState { Playing, Bench, Replaced, InjuredOut, Dismissed, NotSelected }
    public enum InjurySeverity { Knock, MatchEnding }

    /// <summary>Per match-second tuning. Injuries are assessed at stoppages from accumulated exposure.</summary>
    public sealed class MatchFitnessRules
    {
        public double FatiguePerSecond { get; }
        public double InjuryRatePerSecond { get; }
        public double FatigueRiskMultiplier { get; }
        public double MatchEndingChance { get; }
        public int RecoveryDays { get; }
        public double CoachFatigueThreshold { get; }
        public int SubstitutionWindows { get; }
        public bool CoachChanges { get; }
        public MatchFitnessRules(double fatiguePerSecond = .00013, double injuryRatePerSecond = .000006,
            double fatigueRiskMultiplier = 4, double matchEndingChance = .35, int recoveryDays = 7,
            double coachFatigueThreshold = .72, int substitutionWindows = 3, bool coachChanges = true)
        {
            foreach (var x in new[] { fatiguePerSecond, injuryRatePerSecond, fatigueRiskMultiplier, matchEndingChance, coachFatigueThreshold })
                if (double.IsNaN(x) || double.IsInfinity(x) || x < 0) throw new ArgumentOutOfRangeException(nameof(fatiguePerSecond));
            if (matchEndingChance > 1 || coachFatigueThreshold > 1 || recoveryDays < 1 || substitutionWindows < 0)
                throw new ArgumentOutOfRangeException(nameof(matchEndingChance));
            FatiguePerSecond = fatiguePerSecond; InjuryRatePerSecond = injuryRatePerSecond;
            FatigueRiskMultiplier = fatigueRiskMultiplier; MatchEndingChance = matchEndingChance;
            RecoveryDays = recoveryDays; CoachFatigueThreshold = coachFatigueThreshold;
            SubstitutionWindows = substitutionWindows; CoachChanges = coachChanges;
        }
    }
    public sealed class ParticipationEvent
    {
        public string Kind { get; }
        public string PlayerId { get; }
        public string ReplacementId { get; }
        public long Tick { get; }
        public InjurySeverity? Severity { get; }
        internal ParticipationEvent(string kind, string player, string replacement, long tick, InjurySeverity? severity = null)
        { Kind = kind; PlayerId = player; ReplacementId = replacement; Tick = tick; Severity = severity; }
    }
}
