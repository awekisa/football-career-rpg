using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FootballRpg.Simulation
{
    public enum OffsideTouch { DeliberatePlay, Deflection, Save }
    public enum OffsideInvolvement { None, PlayingBall, ChallengingOpponent, ObstructingVision, AttemptAffectingOpponent, ObviousActionAffectingOpponent }
    public enum OffsideRebound { None, OpponentDeflection, DeliberateSave, GoalFrame, MatchOfficial }

    /// <summary>Longitudinal envelope of playable body parts; arms/hands are deliberately excluded.</summary>
    public sealed class OffsideBody
    {
        public string Id { get; }
        public string ClubId { get; }
        public PitchPoint Position { get; }
        public double PlayableRadius { get; }
        public OffsideBody(string id, string clubId, PitchPoint position, double playableRadius = 0)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(clubId) || !PassActor.Finite(position) ||
                double.IsNaN(playableRadius) || double.IsInfinity(playableRadius) || playableRadius < 0) throw new ArgumentException("Invalid offside body.");
            Id = id; ClubId = clubId; Position = position; PlayableRadius = playableRadius;
        }
        public double Leading(int direction) => Position.X * direction + PlayableRadius;
    }
    public sealed class OffsidePhase
    {
        public string PlayId { get; }
        public long TouchTick { get; }
        public OffsideBody Toucher { get; }
        public PitchPoint Ball { get; }
        public int Direction { get; }
        public double BallLine { get; }
        public double OpponentLine { get; }
        public RestartKind? Restart { get; }
        public IReadOnlyDictionary<string, OffsideBody> Candidates { get; }
        public IReadOnlyList<string> OpponentIds { get; }
        public bool Exempt => Restart == RestartKind.ThrowIn || Restart == RestartKind.GoalKick || Restart == RestartKind.Corner;
        public OffsidePhase(string playId, long tick, OffsideBody toucher, PitchPoint ball, int direction, IEnumerable<OffsideBody> bodies, RestartKind? restart = null)
        {
            if (string.IsNullOrWhiteSpace(playId) || tick < 0 || toucher == null || !PassActor.Finite(ball) || Math.Abs(direction) != 1 || bodies == null)
                throw new ArgumentException("Invalid offside phase.");
            var all = bodies.ToArray();
            if (all.Any(b => b == null) || all.Select(b => b.Id).Distinct().Count() != all.Length || !all.Any(b => b.Id == toucher.Id && b.ClubId == toucher.ClubId))
                throw new ArgumentException("Expected unique bodies including the toucher.");
            var opponents = all.Where(b => b.ClubId != toucher.ClubId).OrderByDescending(b => b.Leading(direction)).ToArray();
            if (opponents.Length < 2) throw new ArgumentException("Offside requires at least two opponents.");
            OpponentIds = System.Array.AsReadOnly(opponents.Select(b => b.Id).ToArray());
            PlayId = playId; TouchTick = tick; Toucher = toucher; Ball = ball; Direction = direction; Restart = restart;
            BallLine = ball.X * direction + PitchBoundary.BallRadius; OpponentLine = opponents[1].Leading(direction);
            Candidates = new ReadOnlyDictionary<string, OffsideBody>(all.Where(b => !Exempt && b.ClubId == toucher.ClubId && b.Id != toucher.Id &&
                b.Leading(direction) > 0 && b.Leading(direction) > BallLine + 1e-9 && b.Leading(direction) > OpponentLine + 1e-9).ToDictionary(b => b.Id));
        }
    }
    public sealed class OffsideOffence
    {
        public const string LawsEdition = "IFAB 2026/27, Laws 11 and 13";
        public OffsidePhase Phase { get; }
        public string PlayerId { get; }
        public PitchPoint Location { get; }
        public long Tick { get; }
        public OffsideInvolvement Involvement { get; }
        public OffsideRebound Rebound { get; }
        public string OpponentId { get; }
        public string Explanation { get; }
        internal OffsideOffence(OffsidePhase phase, string player, PitchPoint location, long tick, OffsideInvolvement involvement, OffsideRebound rebound, string opponent)
        {
            Phase = phase; PlayerId = player; Location = location; Tick = tick; Involvement = involvement; Rebound = rebound; OpponentId = opponent;
            string action = involvement == OffsideInvolvement.PlayingBall ? "played the ball" : involvement == OffsideInvolvement.ChallengingOpponent ? "challenged an opponent for the ball"
                : involvement == OffsideInvolvement.ObstructingVision ? "blocked an opponent's view" : involvement == OffsideInvolvement.AttemptAffectingOpponent ? "attempted to play the ball and affected an opponent" : "made an action that affected an opponent";
            Explanation = "Beyond both the ball and second-last opponent in the attacking half at the teammate's touch; then " + action + ".";
            if (rebound != OffsideRebound.None) Explanation += " A save or deflection does not reset offside.";
        }
    }
    /// <summary>Position is frozen at a touch; only evidenced active involvement produces an offence.</summary>
    public sealed class OffsideTracker
    {
        public OffsidePhase Phase { get; private set; }
        public OffsideRebound Rebound { get; private set; }
        public void EndPhase() { Phase = null; Rebound = OffsideRebound.None; }
        public void RecordTouch(OffsidePhase next, OffsideTouch touch)
        {
            if (next == null || !Enum.IsDefined(typeof(OffsideTouch), touch)) throw new ArgumentException("Invalid touch evidence.");
            if (Phase != null && next.Toucher.ClubId != Phase.Toucher.ClubId && touch != OffsideTouch.DeliberatePlay)
            { Rebound = touch == OffsideTouch.Save ? OffsideRebound.DeliberateSave : OffsideRebound.OpponentDeflection; return; }
            Phase = next; Rebound = OffsideRebound.None;
        }
        public void RecordNeutralRebound(OffsideRebound rebound)
        {
            if (rebound != OffsideRebound.GoalFrame && rebound != OffsideRebound.MatchOfficial) throw new ArgumentException("Expected a neutral rebound.");
            if (Phase != null) Rebound = rebound;
        }
        public OffsideOffence Evaluate(string playerId, PitchPoint location, long tick, OffsideInvolvement involvement, string opponentId = null, bool clearImpact = false)
        {
            if (!PassActor.Finite(location) || tick < 0 || !Enum.IsDefined(typeof(OffsideInvolvement), involvement)) throw new ArgumentException("Invalid involvement evidence.");
            if (Phase == null || tick < Phase.TouchTick || playerId == null || !Phase.Candidates.ContainsKey(playerId) || involvement == OffsideInvolvement.None) return null;
            if (involvement != OffsideInvolvement.PlayingBall && (string.IsNullOrWhiteSpace(opponentId) || !Phase.OpponentIds.Contains(opponentId) || !clearImpact)) return null;
            return new OffsideOffence(Phase, playerId, location, tick, involvement, Rebound, opponentId);
        }
    }
}
