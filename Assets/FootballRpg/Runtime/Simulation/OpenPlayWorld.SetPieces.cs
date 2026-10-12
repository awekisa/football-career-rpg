using System;
using System.Collections.Generic;
using System.Linq;

namespace FootballRpg.Simulation
{
    public sealed partial class OpenPlayWorld
    {
        private readonly Dictionary<string, ClubState> setPieceClubs = new Dictionary<string, ClubState>();
        private readonly Dictionary<string, SetPieceAssignment> setPieceAssignments = new Dictionary<string, SetPieceAssignment>();
        private readonly Dictionary<string, SetPieceAssignment> setPieceChoices = new Dictionary<string, SetPieceAssignment>();
        private readonly Dictionary<string, string> committedDuties = new Dictionary<string, string>();
        private readonly List<SetPieceRecord> setPieceRecords = new List<SetPieceRecord>();
        private readonly Dictionary<string, FootballActionResolved> restartDeliveries = new Dictionary<string, FootballActionResolved>();
        public IReadOnlyList<SetPieceRecord> SetPieceRecords => setPieceRecords.AsReadOnly();
        public IReadOnlyDictionary<string, SetPieceAssignment> SetPieceAssignments => new System.Collections.ObjectModel.ReadOnlyDictionary<string, SetPieceAssignment>(setPieceAssignments);
        public SetPieceAssignment PendingSetPiece { get; private set; }
        public string SetPieceRestartId { get; private set; }
        private long setPieceExpiry;
        private bool setPieceReleased;
        private string takingEventId, takingPlayerId;
        private bool takingOutcomeRecorded;

        private FootballerMotion AssignedTaker(RestartKind kind, bool home)
        {
            if (kind != RestartKind.Penalty && kind != RestartKind.Corner && kind != RestartKind.DirectFreeKick && kind != RestartKind.IndirectFreeKick) return null;
            var team = players.Where(p => p.IsHome == home).ToArray();
            return setPieceClubs[team[0].Player.ClubId].SetPieces.Takers(kind)
                .Select(id => team.FirstOrDefault(p => p.Player.Id == id)).FirstOrDefault(p => p != null);
        }
        private void ClearSetPiece()
        {
            PendingSetPiece = null; setPieceAssignments.Clear(); setPieceChoices.Clear(); SetPieceRestartId = null;
            setPieceReleased = false; takingEventId = takingPlayerId = null;
        }
        private void PrepareSetPiece()
        {
            if (Restart == null || Restart.Kind == RestartKind.Kickoff) return;
            SetPieceRestartId = Restart.Id; setPieceReleased = false;
            foreach (var p in players)
            {
                var club = setPieceClubs[p.Player.ClubId]; bool attacking = p.IsHome == Restart.IsHome;
                var duty = club.SetPieces.Duty(p.IsGoalkeeper ? "gk" : p.PositionRoleId, attacking);
                var opponent = players.Where(o => o.IsHome != p.IsHome && !o.IsGoalkeeper).OrderBy(o => o.Position.Distance(p.Position)).First();
                PitchPoint zone = p.Position;
                bool goalmouth = Restart.Kind == RestartKind.Corner || Restart.Spot.X * (attacking ? p.Direction : -p.Direction) > 15;
                if (goalmouth && Restart.Kind != RestartKind.Penalty && Restart.Kind != RestartKind.GoalKick && Restart.Kind != RestartKind.ThrowIn)
                {
                    int lane = players.Where(o => o.IsHome == p.IsHome).ToList().IndexOf(p);
                    zone = p.IsGoalkeeper ? new PitchPoint(-48 * p.Direction, 0) : new PitchPoint((attacking ? duty == SetPieceDuty.AttackSpace ? 40 : 20 : -42) * p.Direction, (lane % 5 - 2) * 4);
                }
                var assignment = new SetPieceAssignment(p.Player.Id, club.CoachId, duty, zone, opponent.Player.Id);
                setPieceAssignments.Add(p.Player.Id, assignment); setPieceChoices.Add(p.Player.Id, assignment);
                setPieceRecords.Add(new SetPieceRecord(Restart.Id, club.CoachId, p.Player.Id, "Assigned", p.Player.Id == Restart.TakerId ? "Taker" : duty.ToString(), false, null, null, session.Tick));
                if (p.Player.Id != Restart.TakerId) p.Position = p.Target = LegalSetPiecePosition(p, DutyTarget(p, assignment));
            }
            // Mark after every zone is placed, so references use the final setup rather than iteration order.
            foreach (var p in players.Where(p => p.Player.Id != Restart.TakerId && setPieceChoices[p.Player.Id].Duty == SetPieceDuty.MarkPlayer))
                p.Position = p.Target = LegalSetPiecePosition(p, DutyTarget(p, setPieceChoices[p.Player.Id]));
            if (Controlled != null && Restart.TakerId != controlledPlayerId)
            { PendingSetPiece = setPieceAssignments[controlledPlayerId]; session.Pause(); }
        }
        private PitchPoint LegalSetPiecePosition(FootballerMotion p, PitchPoint target)
        {
            target = target.Bounded(); if (Restart == null) return target;
            var taker = players.Single(x => x.Player.Id == Restart.TakerId);
            if (Restart.Kind == RestartKind.Penalty)
            {
                if (p.IsGoalkeeper && p.IsHome != Restart.IsHome) return new PitchPoint(52.5 * taker.Direction, 0);
                return new PitchPoint(Math.Min(target.X * taker.Direction, 31) * taker.Direction, target.Z);
            }
            if (p.IsHome == Restart.IsHome) return target;
            if ((Restart.Kind == RestartKind.GoalKick || (Restart.Kind == RestartKind.DirectFreeKick || Restart.Kind == RestartKind.IndirectFreeKick) && HandlingRules.InOwnArea(Restart.Spot, taker.Direction)) && HandlingRules.InOwnArea(target, taker.Direction))
                target = new PitchPoint(-35 * taker.Direction, target.Z);
            double distance = Restart.Kind == RestartKind.ThrowIn ? 2 : Restart.Kind == RestartKind.Corner ? 10.2 : 9.15;
            if (target.Distance(Restart.Spot) < distance)
            {
                var inward = new PitchPoint(0, 0); if (inward.Distance(Restart.Spot) < .01) inward = new PitchPoint(-p.Direction, 0);
                double length = inward.Distance(Restart.Spot);
                target = new PitchPoint(Restart.Spot.X + (inward.X - Restart.Spot.X) / length * distance,
                    Restart.Spot.Z + (inward.Z - Restart.Spot.Z) / length * distance).Bounded();
            }
            return target;
        }
        private PitchPoint DutyTarget(FootballerMotion p, SetPieceAssignment choice)
        {
            switch (choice.Duty)
            {
                case SetPieceDuty.MarkPlayer:
                    var marked = players.FirstOrDefault(o => o.Player.Id == choice.MarkId);
                    return marked == null ? choice.Zone : marked.Position.MoveTowards(new PitchPoint(-52.5 * p.Direction, 0), 1.2).Bounded();
                case SetPieceDuty.AttackSpace: return new PitchPoint(Math.Max(choice.Zone.X * p.Direction, 40) * p.Direction, choice.Zone.Z).Bounded();
                case SetPieceDuty.Counterattack: return new PitchPoint(15 * p.Direction, choice.Zone.Z * .5).Bounded();
                default: return choice.Zone;
            }
        }
        public void CommitSetPieceDuty(string restartId, SetPieceDuty duty, string markId = null)
        {
            string key = restartId + ":" + controlledPlayerId; string choiceKey = duty + ":" + (duty == SetPieceDuty.MarkPlayer ? markId : "");
            if (committedDuties.TryGetValue(key, out var cached))
            { if (cached != choiceKey) throw new InvalidOperationException("Set-piece duty already committed."); return; }
            if (PendingSetPiece == null || Restart?.Id != restartId || RefereeNoticePending || OffsideNoticePending || !Enum.IsDefined(typeof(SetPieceDuty), duty)) throw new InvalidOperationException("No current set-piece choice.");
            var user = Controlled;
            if (duty == SetPieceDuty.MarkPlayer && !players.Any(p => p.Player.Id == markId && p.IsHome != user.IsHome)) throw new ArgumentException("Choose an active opponent to mark.");
            var assigned = PendingSetPiece;
            bool deviated = duty != assigned.Duty || duty == SetPieceDuty.MarkPlayer && markId != assigned.MarkId;
            var choice = new SetPieceAssignment(user.Player.Id, assigned.CoachId, duty, assigned.Zone, markId);
            setPieceChoices[user.Player.Id] = choice; committedDuties.Add(key, choiceKey);
            user.Position = user.Target = LegalSetPiecePosition(user, DutyTarget(user, choice));
            setPieceRecords.Add(new SetPieceRecord(restartId, assigned.CoachId, user.Player.Id, "DutyChosen", duty + (markId == null ? "" : ":" + markId), deviated, null, null, session.Tick));
            PendingSetPiece = null; session.Resume();
        }
        private PitchPoint? SetPieceMovementTarget(FootballerMotion p)
        {
            if (!setPieceReleased || session.Tick >= setPieceExpiry || !setPieceChoices.TryGetValue(p.Player.Id, out var choice)) return null;
            if (p.Player.Id == OwnerId || p == receiver || p.Player.Id == HoldingKeeperId) return null;
            // Automatic keeper positioning remains responsive while following the coach's zone.
            if (p.IsGoalkeeper && choice.Duty == SetPieceDuty.FollowZone) return null;
            return DutyTarget(p, choice);
        }
        private void RecordSetPieceTaking(RestartState restart, ActionResolved action, string choice)
        {
            SetPieceRestartId = restart.Id; setPieceReleased = true; setPieceExpiry = session.Tick + Ticks(8); takingEventId = action.EventId; takingPlayerId = restart.TakerId; takingOutcomeRecorded = false;
            var club = setPieceClubs[players.Single(p => p.Player.Id == restart.TakerId).Player.ClubId];
            setPieceRecords.Add(new SetPieceRecord(restart.Id, club.CoachId, restart.TakerId, "TakingResult", choice + ":" + action.OutcomeId, false, action.EventId, action.Succeeded, session.Tick));
        }
        private void RecordSetPieceOutcome(string outcome, bool? success)
        {
            if (takingEventId == null || takingOutcomeRecorded) return;
            takingOutcomeRecorded = true; setPieceReleased = false;
            var actor = players.FirstOrDefault(p => p.Player.Id == takingPlayerId); if (actor == null) return;
            setPieceRecords.Add(new SetPieceRecord(SetPieceRestartId, setPieceClubs[actor.Player.ClubId].CoachId, takingPlayerId, "DeliveryOutcome", outcome, false, takingEventId, success, session.Tick));
        }
        public IReadOnlyList<FootballActionPreview> RestartDeliveries()
        {
            if (Restart == null || (Restart.Kind != RestartKind.Corner && Restart.Kind != RestartKind.DirectFreeKick && Restart.Kind != RestartKind.IndirectFreeKick)) return Array.Empty<FootballActionPreview>();
            var taker = players.Single(p => p.Player.Id == Restart.TakerId);
            var context = new FootballActionContext(Snapshot(taker), BallSituation.Controlled, 0, 0, taker.Direction, players.Where(p => p.IsHome != taker.IsHome).Select(Snapshot), Restart.Kind);
            return players.Where(p => p.IsHome == taker.IsHome && p != taker && p.Position.Distance(taker.Position) >= .1)
                .SelectMany(p => new[] { "low", "lofted" }.Select(v => FootballActionResolver.Preview(context, FootballActionKind.Cross, v, Snapshot(p)))).ToArray();
        }
        public FootballActionResolved DeliverRestart(string restartId, string optionId)
        {
            if (restartDeliveries.TryGetValue(restartId, out var cached))
            { if (cached.Preview.OptionId != optionId) throw new InvalidOperationException("Restart already delivered differently."); return cached; }
            if (Restart?.Id != restartId || PendingSetPiece != null || RefereeNoticePending || OffsideNoticePending) throw new InvalidOperationException("Restart is not ready.");
            var preview = RestartDeliveries().SingleOrDefault(p => p.OptionId == optionId); if (preview == null) throw new ArgumentException("Choose a delivery.");
            var restart = Restart; var taker = players.Single(p => p.Player.Id == restart.TakerId);
            session.Resume(); var result = FootballActionResolver.Commit(session, restartId, preview, true);
            restartDeliveries.Add(restartId, result); periodStarted = true; Restart = null; PossessionClubId = taker.Player.ClubId;
            BeginFootballFlightCore(taker, result, restart.Kind); directRestart = restart.Kind; directRestartHome = restart.IsHome;
            RecordSetPieceTaking(restart, result.Action, preview.Variant); RecordFixture("RestartReleased", restart.IsHome, restart.Kind);
            FootballActionCommitted?.Invoke(result); return result;
        }
        private void BeginDeliveryArrival(FootballerMotion intended, double height, double speed)
        {
            // A reachable marker may contest an aerial delivery using the ordinary header/clearance decision.
            var challenger = height >= 1.2 ? players.Where(p => p.IsHome != intended.IsHome && !p.IsGoalkeeper && p.Position.Distance(BallPosition) <= 2)
                .OrderBy(p => p.Position.Distance(BallPosition)).FirstOrDefault() : null;
            var recipient = challenger ?? intended;
            RecordSetPieceOutcome(challenger == null ? "Delivery reached target" : "Delivery contested", challenger == null ? (bool?)true : null);
            BeginArrival(recipient, height, speed);
        }
    }
}
