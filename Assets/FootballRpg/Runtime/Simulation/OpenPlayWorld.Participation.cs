using System;
using System.Collections.Generic;
using System.Linq;

namespace FootballRpg.Simulation
{
    public sealed partial class OpenPlayWorld
    {
        private readonly Dictionary<string, PlayerState> namedPlayers = new Dictionary<string, PlayerState>();
        private readonly Dictionary<string, ParticipationState> participation = new Dictionary<string, ParticipationState>();
        private readonly Dictionary<string, double> injuryExposure = new Dictionary<string, double>();
        private readonly Dictionary<string, string> coachRequests = new Dictionary<string, string>();
        private readonly List<ParticipationEvent> participationEvents = new List<ParticipationEvent>();
        private readonly int[] substitutions = new int[2], windows = new int[2];
        private readonly long[] lastWindow = { -1, -1 };
        private SeededRandom fitnessRandom;
        private int substitutionLimit;
        private long stoppage;
        private readonly HashSet<string> matchParticipants = new HashSet<string>();
        private bool rebuildingRoster;
        public MatchFitnessRules FitnessRules { get; private set; }
        public IReadOnlyList<ParticipationEvent> ParticipationEvents => participationEvents.AsReadOnly();
        public ParticipationState UserParticipation => Participation(controlledPlayerId);
        public string ParticipationMessage { get; private set; }
        public int SubstitutionsUsed(bool home) => substitutions[home ? 0 : 1];
        public int WindowsUsed(bool home) => windows[home ? 0 : 1];
        public ParticipationState Participation(string id) => id != null && Discipline.IsDismissed(id) ? ParticipationState.Dismissed
            : id != null && participation.TryGetValue(id, out var value) ? value : ParticipationState.NotSelected;
        public IReadOnlyList<PlayerState> Bench(bool home) => namedPlayers.Values.Where(p =>
            participation[p.Id] == ParticipationState.Bench && (p.ClubId == homeClubId) == home).ToArray();
        private string homeClubId;
        private void InitializeParticipation(CareerState career)
        {
            homeClubId = career.Fixture.HomeClubId; substitutionLimit = career.Fixture.SubstitutionLimit;
            fitnessRandom = new SeededRandom(session.Seed ^ 0x4649544E455353UL);
            foreach (var club in new[] { career.Fixture.HomeClubId, career.Fixture.AwayClubId })
            foreach (var id in career.Clubs[club].SquadPlayerIds.Take(11 + career.Fixture.BenchSize))
            {
                var player = career.Players[id]; namedPlayers.Add(id, player);
                participation.Add(id, player.InjuryDaysRemaining > 0 ? ParticipationState.InjuredOut :
                    players.Any(p => p.Player.Id == id) ? ParticipationState.Playing : ParticipationState.Bench);
                injuryExposure.Add(id, 0);
            }
            // A returning injured starter must never be put on the pitch in a new fixture.
            foreach (var p in players.Where(p => p.Player.InjuryDaysRemaining > 0).ToArray())
            {
                var reserve = BestReserve(p);
                players.Remove(p);
                if (reserve != null) AddReplacement(p, reserve);
            }
        }
        private void StepFitness(double seconds)
        {
            foreach (var p in players)
            {
                double effort = p.IsGoalkeeper ? .35 : 1;
                double stamina = p.Player.FootballAttributes["stamina"] / 100.0;
                matchParticipants.Add(p.Player.Id);
                double fatigue = FitnessRules.FatiguePerSecond * seconds * effort * (1.4 - .7 * stamina);
                readiness.Exertion(progressionMatchId, p.Player.Id, fatigue, seconds);
                injuryExposure[p.Player.Id] += seconds * effort * (1 + FitnessRules.FatigueRiskMultiplier * p.Player.Fatigue);
            }
        }
        public void RequestSubstitution(string outgoingId, string incomingId)
        {
            if (Rules == null || FixturePhase == FixturePhase.FullTime) throw new InvalidOperationException("No active fixture.");
            var outgoing = players.SingleOrDefault(p => p.Player.Id == outgoingId);
            if (outgoing == null || Participation(incomingId) != ParticipationState.Bench ||
                namedPlayers[incomingId].ClubId != outgoing.Player.ClubId || coachRequests.Any(p => p.Key != outgoingId && p.Value == incomingId))
                throw new ArgumentException("Choose an active player and an unused, fit substitute from the same team.");
            if (!CanReplace(outgoing.IsHome)) throw new InvalidOperationException("Substitution limit or available windows exhausted.");
            coachRequests[outgoingId] = incomingId;
            // Requests in live play wait for the next legal stoppage. Multiple changes share its window.
            if (Restart != null || FixturePhase == FixturePhase.HalfTime)
            {
                var restart = Restart;
                bool wasDecision = HasPendingDecision || restart?.TakerId == controlledPlayerId;
                ProcessRoster(false);
                if (restart != null && !Abandoned)
                {
                    rebuildingRoster = true;
                    try { AwardRestart(restart.Kind, restart.IsHome, restart.Spot); }
                    finally { rebuildingRoster = false; }
                    if (wasDecision && !HasPendingDecision && Restart?.TakerId != controlledPlayerId && !RefereeNoticePending && !OffsideNoticePending) session.Resume();
                }
            }
        }
        private PlayerState BestReserve(FootballerMotion outgoing) => Bench(outgoing.IsHome)
            .OrderByDescending(p => outgoing.IsGoalkeeper ? p.PrimaryPositionId == "gk" ? 3 : 0 : p.PrimaryPositionId == outgoing.PositionRoleId ? 3 : p.PrimaryPositionId != "gk" ? 1 : 0)
            .ThenBy(p => p.Fatigue).FirstOrDefault();
        private bool CanReplace(bool home)
        {
            int index = home ? 0 : 1;
            return substitutions[index] < substitutionLimit && (FixturePhase == FixturePhase.HalfTime || lastWindow[index] == stoppage || windows[index] < FitnessRules.SubstitutionWindows);
        }
        private void AddReplacement(FootballerMotion outgoing, PlayerState incoming)
        {
            var motion = new FootballerMotion(incoming, outgoing.IsHome, outgoing.Anchor)
            { Direction = outgoing.Direction, IsGoalkeeper = outgoing.IsGoalkeeper, PositionRoleId = outgoing.PositionRoleId, Position = outgoing.Position, Target = outgoing.Target, Facing = outgoing.Facing };
            players.Add(motion); participation[incoming.Id] = ParticipationState.Playing;
            if (incoming.Id == controlledPlayerId) { ActiveIntent = null; IntentStatus = "On the pitch — following team tactics"; }
        }
        private void Replace(FootballerMotion outgoing, PlayerState incoming, bool injured)
        {
            int index = outgoing.IsHome ? 0 : 1;
            players.Remove(outgoing); participation[outgoing.Player.Id] = injured ? ParticipationState.InjuredOut : ParticipationState.Replaced;
            AddReplacement(outgoing, incoming); substitutions[index]++;
            if (FixturePhase != FixturePhase.HalfTime && lastWindow[index] != stoppage) { windows[index]++; lastWindow[index] = stoppage; }
            RecordParticipation("Substitution", outgoing.Player.Id, incoming.Id);
        }
        private void RecordParticipation(string kind, string player, string replacement = null, InjurySeverity? severity = null)
        {
            participationEvents.Add(new ParticipationEvent(kind, player, replacement, session.Tick, severity));
            ParticipationMessage = kind + ": " + namedPlayers[player].Name + (replacement == null ? "" : " → " + namedPlayers[replacement].Name);
            RecordFixture(kind, namedPlayers[player].ClubId == homeClubId);
        }
        private void ProcessRoster(bool assessInjuries)
        {
            if (FitnessRules == null) return;
            foreach (var p in players.ToArray())
            {
                bool injuredOut = false;
                if (assessInjuries)
                {
                    double exposure = injuryExposure[p.Player.Id]; injuryExposure[p.Player.Id] = 0;
                    double chance = 1 - Math.Exp(-FitnessRules.InjuryRatePerSecond * exposure);
                    if (chance > 0 && fitnessRandom.NextUnitFloat() < chance)
                    {
                        injuredOut = fitnessRandom.NextUnitFloat() < FitnessRules.MatchEndingChance;
                        RecordParticipation("Injury", p.Player.Id, severity: injuredOut ? InjurySeverity.MatchEnding : InjurySeverity.Knock);
                        readiness.Injury(progressionMatchId, p.Player.Id, participationEvents.Count.ToString(), injuredOut ? FitnessRules.RecoveryDays : 0);
                    }
                }
                string requested; coachRequests.TryGetValue(p.Player.Id, out requested);
                bool coachChange = FitnessRules.CoachChanges && (p.Player.Fatigue >= FitnessRules.CoachFatigueThreshold ||
                    (FixturePhase == FixturePhase.HalfTime && controlledPlayerId != null && Participation(controlledPlayerId) == ParticipationState.Bench &&
                     namedPlayers[controlledPlayerId].ClubId == p.Player.ClubId && namedPlayers[controlledPlayerId].PrimaryPositionId == p.PositionRoleId));
                PlayerState reserve = requested != null && Participation(requested) == ParticipationState.Bench ? namedPlayers[requested] : null;
                if (reserve == null && (injuredOut || coachChange))
                    reserve = FixturePhase == FixturePhase.HalfTime && controlledPlayerId != null && Participation(controlledPlayerId) == ParticipationState.Bench &&
                        namedPlayers[controlledPlayerId].ClubId == p.Player.ClubId && namedPlayers[controlledPlayerId].PrimaryPositionId == p.PositionRoleId
                        ? namedPlayers[controlledPlayerId] : BestReserve(p);
                if (reserve != null && CanReplace(p.IsHome)) Replace(p, reserve, injuredOut);
                else if (injuredOut) { players.Remove(p); participation[p.Player.Id] = ParticipationState.InjuredOut; }
                else if (requested != null) RecordParticipation("Substitution unavailable", p.Player.Id);
            }
            coachRequests.Clear();
            foreach (bool home in new[] { true, false })
            {
                var team = players.Where(p => p.IsHome == home).ToArray();
                if (team.Length < 7) { Abandoned = true; ClearPlay(); Restart = null; FixturePhase = FixturePhase.FullTime; RecordFixture("Abandoned"); session.Pause(); return; }
                if (!team.Any(p => p.IsGoalkeeper))
                {
                    var keeper = team.OrderByDescending(p => p.Player.FootballAttributes["handling"]).First();
                    keeper.IsGoalkeeper = true; keeper.Anchor = new PitchPoint(-48 * keeper.Direction, 0);
                }
            }
            if (Controlled == null) { ActiveIntent = null; IntentStatus = UserParticipation + " — watching the remainder"; }
        }
    }
}
