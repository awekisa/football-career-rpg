using System;
using System.Collections.Generic;
using System.Linq;

namespace FootballRpg.Simulation
{
    public enum OffBallIntent { Automatic, Press, PersonalMark, HoldZone, Track, FindSpace, Support, ForwardRun, Counterattack }
    public sealed class MovementIntent
    {
        public OffBallIntent Kind { get; }
        public string TargetId { get; }
        public long ExpiresAtTick { get; }
        public PitchPoint Zone { get; }
        internal MovementIntent(OffBallIntent kind, string targetId, long expires, PitchPoint zone)
        { Kind = kind; TargetId = targetId; ExpiresAtTick = expires; Zone = zone; }
    }
    public sealed partial class OpenPlayWorld
    {
        private long defenseCooldownTick, beatenUntilTick, containedUntilTick;
        private string containedPlayerId;
        private readonly Dictionary<string, DefenseResolved> committedDefense = new Dictionary<string, DefenseResolved>();
        public DefenseDecision PendingDefense { get; private set; }
        public bool HasPendingDecision => PendingDecision != null || PendingDefense != null || PendingKeeper != null || PendingSetPiece != null;
        public DefenseResolved LastDefense { get; private set; }
        public event Action<DefenseResolved> DefenseCommitted;
        public MovementIntent ActiveIntent { get; private set; }
        public string IntentStatus { get; private set; } = "Following team tactics";
        public double IntentSecondsRemaining => ActiveIntent == null ? 0 : Math.Max(0, (ActiveIntent.ExpiresAtTick - session.Tick) * session.FixedStep.TotalSeconds);
        private long Ticks(double seconds) => (TimeSpan.FromSeconds(seconds).Ticks + session.FixedStep.Ticks - 1) / session.FixedStep.Ticks;
        private FootballerMotion Controlled => players.FirstOrDefault(p => p.Player.Id == controlledPlayerId);

        private bool TryOpenDefense(FootballerMotion owner)
        {
            var defender = Controlled;
            if (defender == null || (defender.IsGoalkeeper && HandlingRules.InOwnArea(BallPosition, defender.Direction)) || owner == null || owner.Player.Id == HoldingKeeperId || owner.IsHome == defender.IsHome || BallState != BallPhase.Owned || session.Tick < defenseCooldownTick) return false;
            var actor = Snapshot(defender); var threat = Snapshot(owner);
            var options = ((DefensiveAction[])Enum.GetValues(typeof(DefensiveAction)))
                .Where(k => DefenseResolver.IsAvailable(actor, threat, defender.Direction, k))
                .Select(k => DefenseResolver.Preview(actor, threat, defender.Direction, k)).ToArray();
            if (options.Length == 0) return false;
            session.Pause();
            PendingDefense = new DefenseDecision(session.SessionId + ":defense-" + session.Tick, session.Tick, options);
            DecisionCount++;
            return true;
        }

        public DefenseResolved CommitDefense(string decisionId, DefensiveAction kind)
        {
            if (decisionId == null) throw new ArgumentNullException(nameof(decisionId));
            if (committedDefense.TryGetValue(decisionId, out var previous))
            {
                if (previous.Preview.Kind != kind) throw new InvalidOperationException("This threat already used a different choice.");
                return previous;
            }
            var decision = PendingDefense;
            if (decision == null || decision.Id != decisionId || session.State != MatchRunState.Paused || session.Tick != decision.Tick || BallState != BallPhase.Owned)
                throw new InvalidOperationException("Defense decision is no longer current.");
            var preview = decision.Options.SingleOrDefault(p => p.Kind == kind);
            if (preview == null || OwnerId != preview.Attacker.Id) throw new ArgumentException("Choose an available action against the current threat.");
            var defender = Controlled; var attacker = players.Single(p => p.Player.Id == OwnerId);
            if (!DefenseResolver.IsAvailable(Snapshot(defender), Snapshot(attacker), defender.Direction, kind)) throw new InvalidOperationException("Threat is no longer reachable.");
            var result = DefenseResolver.Commit(session, decision.Id, preview, false);
            PendingDefense = null;
            committedDefense.Add(decisionId, result); LastDefense = result;
            defenseCooldownTick = session.Tick + Ticks(2.5);
            challengeSeconds = 1.2;
            // Finish the defensive fact before opening any new possession choice.
            session.CompleteResolution(result.EventId);
            if (JudgeChallenge(result, defender, attacker)) { DefenseCommitted?.Invoke(result); return result; }
            if (!result.Action.Succeeded) beatenUntilTick = session.Tick + Ticks(1.5);
            else if (kind == DefensiveAction.Tackle)
            { defender.Position = preview.Target; GiveBall(defender); }
            else if (kind == DefensiveAction.Contain)
            { containedPlayerId = attacker.Player.Id; containedUntilTick = session.Tick + Ticks(2); }
            else
            {
                RecordOffsideTouch(defender, OffsideTouch.Deflection);
                BallPosition = new PitchPoint(attacker.Position.X, attacker.Position.Z + (attacker.Position.Z >= 0 ? 4 : -4)).Bounded();
                Handling.RecordTouch(defender.Player.Id, defender.Player.ClubId, BallTouchKind.Other);
                MakeLoose();
            }
            RefreshIntent();
            DefenseCommitted?.Invoke(result);
            return result;
        }

        public bool CanSetIntent(OffBallIntent kind, string targetId = null)
        {
            var user = Controlled;
            if (user == null || !Enum.IsDefined(typeof(OffBallIntent), kind)) return false;
            if (kind == OffBallIntent.Automatic) return true;
            if (OwnerId == controlledPlayerId || IncomingPlayerId == controlledPlayerId || BallState == BallPhase.OutOfPlay || BallState == BallPhase.KeeperIntervention) return false;
            bool attacking = PossessionClubId == user.Player.ClubId;
            if (kind == OffBallIntent.Press || kind == OffBallIntent.PersonalMark || kind == OffBallIntent.Track)
            {
                if (attacking || PossessionClubId == null) return false;
                if (kind != OffBallIntent.Press && !players.Any(p => p.Player.Id == targetId && p.IsHome != user.IsHome)) return false;
            }
            if ((kind == OffBallIntent.FindSpace || kind == OffBallIntent.Support || kind == OffBallIntent.ForwardRun) && !attacking) return false;
            return true;
        }

        public void SetIntent(OffBallIntent kind, string targetId = null)
        {
            if (!CanSetIntent(kind, targetId)) throw new ArgumentException("Intent requires a valid phase and, for marking/tracking, an opposing starter.");
            if (kind == OffBallIntent.Automatic) { ClearIntent("Following team tactics"); return; }
            var user = Controlled;
            if (setPieceReleased && setPieceChoices.Remove(user.Player.Id))
                setPieceRecords.Add(new SetPieceRecord(SetPieceRestartId, setPieceClubs[user.Player.ClubId].CoachId, user.Player.Id,
                    "DutyOverridden", kind.ToString(), true, null, null, session.Tick));
            ActiveIntent = new MovementIntent(kind, kind == OffBallIntent.PersonalMark || kind == OffBallIntent.Track ? targetId : null,
                session.Tick + Ticks(20), user.Position);
            IntentStatus = "Active";
            user.Target = IntendedTarget(user, players.FirstOrDefault(p => p.Player.Id == OwnerId));
        }

        private void ClearIntent(string reason)
        {
            ActiveIntent = null; IntentStatus = reason;
            var user = Controlled;
            if (user != null) user.Target = TacticalTarget(user, players.FirstOrDefault(p => p.Player.Id == OwnerId));
        }
        private void RefreshIntent()
        {
            if (ActiveIntent == null) return;
            if (session.Tick >= ActiveIntent.ExpiresAtTick) ClearIntent("Instruction expired — following team tactics");
            else if (!CanSetIntent(ActiveIntent.Kind, ActiveIntent.TargetId)) ClearIntent("Instruction ended: possession or phase changed");
        }

        private PitchPoint IntendedTarget(FootballerMotion player, FootballerMotion owner)
        {
            var setPieceTarget = SetPieceMovementTarget(player);
            if (setPieceTarget.HasValue) return setPieceTarget.Value;
            if (player.Player.Id != controlledPlayerId) return TacticalTarget(player, owner);
            if (session.Tick < containedUntilTick && owner?.Player.Id == containedPlayerId)
                return owner.Position.MoveTowards(new PitchPoint(-52.5 * player.Direction, 0), 2).Bounded();
            var intent = ActiveIntent;
            if (intent == null || player == receiver) return TacticalTarget(player, owner);
            var goal = new PitchPoint(-52.5 * player.Direction, 0);
            var target = players.FirstOrDefault(p => p.Player.Id == intent.TargetId);
            switch (intent.Kind)
            {
                case OffBallIntent.Press: return BallPosition;
                case OffBallIntent.PersonalMark: return target.Position.MoveTowards(goal, 1.5).Bounded();
                case OffBallIntent.Track: return new PitchPoint(target.Position.X - player.Direction * 4, target.Position.Z * .85).Bounded();
                case OffBallIntent.HoldZone: return intent.Zone;
                case OffBallIntent.Support: return new PitchPoint(BallPosition.X - 7 * player.Direction, BallPosition.Z + (BallPosition.Z >= 0 ? -7 : 7)).Bounded();
                case OffBallIntent.ForwardRun: return new PitchPoint(intent.Zone.X + 18 * player.Direction, intent.Zone.Z).Bounded();
                case OffBallIntent.Counterattack: return new PitchPoint(25 * player.Direction, intent.Zone.Z * .7).Bounded();
                case OffBallIntent.FindSpace:
                    return new[] { new PitchPoint(BallPosition.X + 8 * player.Direction, BallPosition.Z - 12).Bounded(),
                        new PitchPoint(BallPosition.X + 8 * player.Direction, BallPosition.Z + 12).Bounded(),
                        new PitchPoint(BallPosition.X - 6 * player.Direction, BallPosition.Z).Bounded() }
                        .OrderByDescending(p => players.Where(x => x.IsHome != player.IsHome).Min(x => x.Position.Distance(p))).First();
                default: return TacticalTarget(player, owner);
            }
        }
    }
}
