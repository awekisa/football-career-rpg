using System.Linq;

namespace FootballRpg.Simulation
{
    public sealed partial class OpenPlayWorld
    {
        public OffsideTracker Offside { get; } = new OffsideTracker();
        public OffsideOffence LastOffside { get; private set; }
        public bool OffsideNoticePending { get; private set; }
        private long offsideTouchSequence;
        // Placeholder capsules use this playable body envelope, excluding their arms/hands.
        private static OffsideBody OffsideBodyOf(FootballerMotion p) => new OffsideBody(p.Player.Id, p.Player.ClubId, p.Position, .3);
        private void RecordOffsideTouch(FootballerMotion player, OffsideTouch touch = OffsideTouch.DeliberatePlay, RestartKind? restart = null)
        {
            directRestart = null;
            if (Rules == null) return;
            var phase = new OffsidePhase(session.SessionId + ":touch-" + (++offsideTouchSequence), session.Tick,
                OffsideBodyOf(player), BallPosition, player.Direction, players.Select(OffsideBodyOf), restart);
            Offside.RecordTouch(phase, touch);
        }
        private bool CheckOffsideTouch(FootballerMotion player) => PenalizeOffside(Offside.Evaluate(player.Player.Id, player.Position, session.Tick, OffsideInvolvement.PlayingBall));
        private bool PenalizeOffside(OffsideOffence offence)
        {
            if (Rules == null || offence == null) return false;
            if (Advantage != null) { RecallAdvantage(); return true; }
            RecordSetPieceOutcome("Offside", false);
            LastOffside = offence;
            bool offendingHome = players.Single(p => p.Player.Id == offence.PlayerId).IsHome;
            var awarded = players.First(p => p.IsHome != offendingHome);
            var spot = new PitchPoint(System.Math.Max(-52.5, System.Math.Min(52.5, offence.Location.X)), System.Math.Max(-34, System.Math.Min(34, offence.Location.Z)));
            // An attacking IFK in the opponents' goal area moves to the nearest point on the goal-area line.
            if (spot.X * awarded.Direction > 47 && System.Math.Abs(spot.Z) < 9.16)
                spot = new PitchPoint(47 * awarded.Direction, spot.Z);
            RecordFixture("Offside", offendingHome, RestartKind.IndirectFreeKick);
            AwardRestart(RestartKind.IndirectFreeKick, !offendingHome, spot);
            if (controlledPlayerId != null) { OffsideNoticePending = true; session.Pause(); }
            return true;
        }
        public void AcknowledgeOffside()
        {
            if (!OffsideNoticePending) return;
            OffsideNoticePending = false;
            if (Restart?.TakerId != controlledPlayerId) session.Resume();
        }
        private bool CheckOffsideInterference()
        {
            var phase = Offside.Phase;
            if (Rules == null || phase == null || phase.Candidates.Count == 0) return false;
            bool shot = activeFootballAction?.Outcome == FootballOutcome.ShotOnTarget;
            foreach (var candidate in players.Where(p => phase.Candidates.ContainsKey(p.Player.Id)))
            {
                bool attempting = candidate == receiver || (BallState == BallPhase.Loose && candidate == players.Where(p => p.IsHome == candidate.IsHome).OrderBy(p => p.Position.Distance(BallPosition)).First())
                    || (candidate == Controlled && ActiveIntent?.Kind == OffBallIntent.Press);
                var opponent = players.Where(p => p.IsHome != candidate.IsHome).OrderBy(p => p.Position.Distance(BallPosition)).First();
                if (attempting && BallHeight <= candidate.Player.HeightCm / 100 + .9 && candidate.Position.Distance(BallPosition) <= 1.5 &&
                    opponent.Position.Distance(BallPosition) <= 2 && candidate.Position.Distance(opponent.Position) <= 2)
                {
                    if (PenalizeOffside(Offside.Evaluate(candidate.Player.Id, candidate.Position, session.Tick, OffsideInvolvement.ChallengingOpponent, opponent.Player.Id, true))) return true;
                }
                if (!shot) continue;
                var keeper = players.First(p => p.IsHome != candidate.IsHome && p.IsGoalkeeper);
                // A capsule intersects the keeper-to-ball sightline at playable height in the immediate goalmouth.
                if (keeper.Position.Distance(candidate.Position) > 8 || keeper.Position.Distance(BallPosition) > 30) continue;
                double dx = BallPosition.X - keeper.Position.X, dz = BallPosition.Z - keeper.Position.Z;
                double length2 = dx * dx + dz * dz;
                if (length2 < .01) continue;
                double t = ((candidate.Position.X - keeper.Position.X) * dx + (candidate.Position.Z - keeper.Position.Z) * dz) / length2;
                if (t <= 0 || t >= 1) continue;
                var ray = new PitchPoint(keeper.Position.X + t * dx, keeper.Position.Z + t * dz);
                double height = 1.6 + t * (BallHeight - 1.6);
                if (candidate.Position.Distance(ray) <= .3 && height >= 0 && height <= candidate.Player.HeightCm / 100)
                    if (PenalizeOffside(Offside.Evaluate(candidate.Player.Id, candidate.Position, session.Tick, OffsideInvolvement.ObstructingVision, keeper.Player.Id, true))) return true;
            }
            return false;
        }
    }
}
