using System.Linq;

namespace FootballRpg.Simulation
{
    public enum HighlightFocus { Quiet, Decision, BallInvolvement, DefensiveThreat, AttackingSupport, MovementIntent, MatchEvent, Interval }

    public sealed partial class OpenPlayWorld
    {
        internal MatchSession PlaybackSession => session;
        private long? viewingEventTick;
        internal bool ViewingMustWait => HasPendingDecision || RefereeNoticePending || OffsideNoticePending ||
            (controlledPlayerId != null && Restart?.TakerId == controlledPlayerId) || FixturePhase == FixturePhase.HalfTime || FixturePhase == FixturePhase.FullTime;
        private void RememberViewingEvent(string kind)
        {
            if (kind == "Goal" || kind == "Yellow" || kind == "Red" || kind == "SecondYellow" || kind == "Substitution" || kind == "Injury" || kind == "Abandoned")
                viewingEventTick = session.Tick;
        }
        /// <summary>Read-only anticipation, based on current role, reach and instruction; never commits an action.</summary>
        public HighlightFocus ViewingFocus
        {
            get
            {
                if (FixturePhase == FixturePhase.HalfTime || FixturePhase == FixturePhase.FullTime) return HighlightFocus.Interval;
                if (ViewingMustWait) return HighlightFocus.Decision;
                if (viewingEventTick.HasValue && session.Tick - viewingEventTick.Value < Ticks(3)) return HighlightFocus.MatchEvent;
                var user = Controlled;
                if (user == null) return HighlightFocus.Quiet;
                if (OwnerId == controlledPlayerId || IncomingPlayerId == controlledPlayerId || IntendedReceiverId == controlledPlayerId || HoldingKeeperId == controlledPlayerId)
                    return HighlightFocus.BallInvolvement;
                if (BallState == BallPhase.Travelling && (activeFootballAction?.Preview.Situation.Actor.Id == controlledPlayerId ||
                    (activeFootballAction == null && keeperFlight == null && LastPass?.Preview.Passer.Id == controlledPlayerId))) return HighlightFocus.BallInvolvement;
                if (keeperFlight?.Preview.Context.Keeper.Id == controlledPlayerId) return HighlightFocus.BallInvolvement;
                if (Restart != null) return HighlightFocus.Quiet;
                double distance = user.Position.Distance(BallPosition);
                bool defending = PossessionClubId != null && PossessionClubId != user.Player.ClubId;
                bool attacking = PossessionClubId == user.Player.ClubId;
                if (BallState == BallPhase.KeeperIntervention && keeperThreat != null && keeperThreat.Preview.Situation.Actor.ClubId != user.Player.ClubId && user.IsGoalkeeper)
                    return HighlightFocus.DefensiveThreat;
                // Keepers watch threats approaching their area, even before a save/rush-out becomes available.
                if (user.IsGoalkeeper)
                    return (defending || BallState == BallPhase.Loose || BallState == BallPhase.Travelling) &&
                        BallPosition.X * user.Direction < -20 && System.Math.Abs(BallPosition.Z) < 30
                        ? HighlightFocus.DefensiveThreat : HighlightFocus.Quiet;
                var intent = ActiveIntent;
                if (intent != null)
                {
                    var marked = players.FirstOrDefault(p => p.Player.Id == intent.TargetId);
                    if (marked != null && user.Position.Distance(marked.Position) <= 22 && marked.Position.Distance(BallPosition) <= 20)
                        return HighlightFocus.MovementIntent;
                    if (intent.Kind == OffBallIntent.Press && defending && distance <= 22) return HighlightFocus.MovementIntent;
                    if ((intent.Kind == OffBallIntent.FindSpace || intent.Kind == OffBallIntent.Support || intent.Kind == OffBallIntent.ForwardRun || intent.Kind == OffBallIntent.Counterattack) && attacking && distance <= 30)
                        return HighlightFocus.MovementIntent;
                    if (intent.Kind == OffBallIntent.HoldZone && intent.Zone.Distance(BallPosition) <= 20) return HighlightFocus.MovementIntent;
                }
                string role = user.PositionRoleId;
                double defensiveReach = role == "cb" || role == "lb" || role == "rb" || role == "lwb" || role == "rwb" || role == "dm" ? 24 :
                    role == "cm" || role == "am" || role == "lm" || role == "rm" ? 20 : 14;
                if ((defending || BallState == BallPhase.Loose) && distance <= defensiveReach) return HighlightFocus.DefensiveThreat;
                double supportReach = role == "st" || role == "lw" || role == "rw" || role == "am" ? 24 : 18;
                if (attacking && distance <= supportReach) return HighlightFocus.AttackingSupport;
                return HighlightFocus.Quiet;
            }
        }
    }
}
