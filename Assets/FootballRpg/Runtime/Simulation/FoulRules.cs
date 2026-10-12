using System;
using System.Collections.Generic;

namespace FootballRpg.Simulation
{
    public enum FoulKind { Challenge, Holding, DangerousPlay, Impeding, Handball, KeeperHandling, KeeperTimeLimit }
    public enum ContactSeverity { Fair, Careless, Reckless, Excessive }
    public enum CardKind { None, Yellow, SecondYellow, Red }

    public sealed class FoulIncident
    {
        public string Id { get; }
        public string OffenderId { get; }
        public string VictimId { get; }
        public PitchPoint Spot { get; }
        public int DefenderDirection { get; }
        public FoulKind Kind { get; }
        public ContactSeverity Severity { get; }
        public bool AttemptToPlayBall { get; }
        public bool ObviousOpportunity { get; }
        public bool PromisingAttack { get; }
        public FoulIncident(string id, string offender, string victim, PitchPoint spot, int defenderDirection, FoulKind kind,
            ContactSeverity severity = ContactSeverity.Careless, bool attemptToPlayBall = true, bool obviousOpportunity = false, bool promisingAttack = false)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(offender) || offender == victim || !PassActor.Finite(spot) ||
                Math.Abs(spot.X) > 52.5 || Math.Abs(spot.Z) > 34 || Math.Abs(defenderDirection) != 1 ||
                !Enum.IsDefined(typeof(FoulKind), kind) || !Enum.IsDefined(typeof(ContactSeverity), severity)) throw new ArgumentException("Invalid foul evidence.");
            Id = id; OffenderId = offender; VictimId = victim; Spot = spot; DefenderDirection = defenderDirection; Kind = kind;
            Severity = severity; AttemptToPlayBall = attemptToPlayBall; ObviousOpportunity = obviousOpportunity; PromisingAttack = promisingAttack;
        }
    }
    public sealed class FoulVerdict
    {
        public FoulIncident Incident { get; }
        public RestartKind Restart { get; }
        public PitchPoint Spot { get; }
        public CardKind Card { get; }
        public string Explanation { get; }
        internal FoulVerdict(FoulIncident incident, RestartKind restart, PitchPoint spot, CardKind card)
        {
            Incident = incident; Restart = restart; Spot = spot; Card = card;
            Explanation = incident.Kind == FoulKind.KeeperTimeLimit ? "Goalkeeper held the ball for more than eight seconds."
                : incident.Kind == FoulKind.KeeperHandling ? "Goalkeeper handled a back-pass, direct teammate throw or their own release."
                : incident.Kind == FoulKind.Challenge ? incident.Severity + " challenge."
                : incident.Kind + " offence.";
            if (incident.ObviousOpportunity) Explanation += " Denied an obvious goal-scoring opportunity.";
            else if (incident.PromisingAttack) Explanation += " Stopped a promising attack.";
        }
    }
    public static class FoulRules
    {
        public const string LawsEdition = "IFAB 2026/27, Laws 12–14";
        public static FoulVerdict Evaluate(FoulIncident incident, bool ballInPlay = true)
        {
            if (incident == null) throw new ArgumentNullException(nameof(incident));
            if (!ballInPlay || incident.Kind == FoulKind.Challenge && incident.Severity == ContactSeverity.Fair) return null;
            bool indirect = incident.Kind == FoulKind.DangerousPlay || incident.Kind == FoulKind.Impeding || incident.Kind == FoulKind.KeeperHandling;
            bool corner = incident.Kind == FoulKind.KeeperTimeLimit;
            bool penalty = !indirect && !corner && HandlingRules.InOwnArea(incident.Spot, incident.DefenderDirection);
            var kind = corner ? RestartKind.Corner : penalty ? RestartKind.Penalty : indirect ? RestartKind.IndirectFreeKick : RestartKind.DirectFreeKick;
            var spot = penalty ? new PitchPoint(-41.5 * incident.DefenderDirection, 0) : corner
                ? new PitchPoint(-52 * incident.DefenderDirection, incident.Spot.Z >= 0 ? 33.5 : -33.5) : incident.Spot;
            if (indirect && spot.X * incident.DefenderDirection < -47 && Math.Abs(spot.Z) < 9.16)
                spot = new PitchPoint(-47 * incident.DefenderDirection, spot.Z);
            CardKind card = incident.Severity == ContactSeverity.Excessive ? CardKind.Red
                : incident.Severity == ContactSeverity.Reckless ? CardKind.Yellow : CardKind.None;
            if (!corner && incident.Kind != FoulKind.KeeperHandling)
            {
                if (incident.ObviousOpportunity) card = penalty && incident.AttemptToPlayBall && incident.Kind != FoulKind.Handball && card != CardKind.Red ? CardKind.Yellow : CardKind.Red;
                else if (incident.PromisingAttack && !(penalty && incident.AttemptToPlayBall) && card == CardKind.None) card = CardKind.Yellow;
            }
            else card = CardKind.None;
            return new FoulVerdict(incident, kind, spot, card);
        }
        // Partition the already committed tackle failure roll: no second draw or altered win probability.
        public static ContactSeverity ChallengeSeverity(DefenseResolved result)
        {
            if (result.Preview.Kind != DefensiveAction.Tackle || result.Action.Succeeded) return ContactSeverity.Fair;
            var p = result.Preview;
            double failure = (result.Action.Roll - p.Chance) / (1 - p.Chance);
            double risk = failure + p.Defender.Fatigue * .06 + Math.Max(0, p.Defender.Position.Distance(p.Attacker.Position) - 1.5) * .06;
            return risk < .70 ? ContactSeverity.Fair : risk < .94 ? ContactSeverity.Careless : risk < 1.035 ? ContactSeverity.Reckless : ContactSeverity.Excessive;
        }
        public static CardKind AdvantageCard(FoulVerdict verdict) => verdict.Incident.Severity == ContactSeverity.Reckless ? CardKind.Yellow
            : verdict.Incident.PromisingAttack && !verdict.Incident.ObviousOpportunity ? CardKind.None : verdict.Card;
    }
    public sealed class DisciplineRecord
    {
        private readonly Dictionary<string, int> yellows = new Dictionary<string, int>();
        private readonly HashSet<string> dismissed = new HashSet<string>();
        private readonly HashSet<string> applied = new HashSet<string>();
        public int Yellows(string player) => yellows.TryGetValue(player, out var count) ? count : 0;
        public bool IsDismissed(string player) => player != null && dismissed.Contains(player);
        public CardKind Apply(string incidentId, string player, CardKind card)
        {
            if (string.IsNullOrWhiteSpace(incidentId) || string.IsNullOrWhiteSpace(player) || !Enum.IsDefined(typeof(CardKind), card)) throw new ArgumentException("Invalid discipline.");
            if (card == CardKind.None || dismissed.Contains(player) || !applied.Add(incidentId)) return CardKind.None;
            if (card == CardKind.Yellow && (yellows[player] = Yellows(player) + 1) >= 2) card = CardKind.SecondYellow;
            if (card == CardKind.Red || card == CardKind.SecondYellow) dismissed.Add(player);
            return card;
        }
    }
    public sealed class AdvantageState
    {
        public FoulVerdict Verdict { get; }
        public long ExpiresAtTick { get; }
        public string ClubId { get; }
        public int Direction { get; }
        internal AdvantageState(FoulVerdict verdict, long expiry, string club, int direction)
        { Verdict = verdict; ExpiresAtTick = expiry; ClubId = club; Direction = direction; }
    }
}
