using System;
using System.Linq;

namespace FootballRpg.Simulation
{
    public sealed partial class OpenPlayWorld
    {
        private CareerCoach careerCoach;
        public bool CoachEnabled { get; private set; }
        public CoachSelection CoachSelection { get; private set; }
        private long nextCoachDeviationTick;
        private string coachDeviationCause, coachDeviationChoice;
        private bool coachDeviationJustified;
        private double coachDeviationDanger;
        private long coachDeviationExpires;
        public bool LateChase => Rules != null && FixturePhase == FixturePhase.SecondHalf && HalfElapsedSeconds >= Rules.HalfSeconds * .7 &&
            (namedPlayers[controlledPlayerId].ClubId == homeClubId ? HomeGoals < AwayGoals : AwayGoals < HomeGoals);
        public OffBallIntent CoachInstruction => !CoachEnabled || Controlled == null ? OffBallIntent.Automatic :
            careerCoach.Instruction(PossessionClubId == Controlled.Player.ClubId, LateChase, Controlled.PositionRoleId);
        public string CoachMessage => !CoachEnabled ? "Scenario line-up" : Controlled == null ? "Watch the match — " + UserParticipation :
            "Coach: " + CoachInstruction + " · Role " + Controlled.PositionRoleId.ToUpperInvariant() + (LateChase ? " · Chase a late goal" : "");
        private void InitializeCoachSelection()
        {
            if (controlledPlayerId == null || controlledPlayerId != careerUserId) throw new InvalidOperationException("Career selection requires the career player.");
            CoachEnabled = true; careerCoach.MatchActive = true; CoachSelection = careerCoach.Select();
            var user = Controlled;
            if (CoachSelection.Outcome != SelectionOutcome.Starter && user != null)
            {
                var reserve = BestReserve(user);
                // Keep a legal eleven whenever a fit named replacement exists. Pre-match changes use no substitution window.
                if (reserve != null)
                {
                    players.Remove(user); participation[user.Player.Id] = ParticipationState.Bench; AddReplacement(user, reserve);
                }
                else CoachSelection = new CoachSelection(SelectionOutcome.Starter, CoachSelection.Role, CoachSelection.Ability, CoachSelection.Score, CoachSelection.RequiredScore, "Required to start: no fit replacement available.");
            }
            else if (CoachSelection.Outcome == SelectionOutcome.Starter && user == null && Participation(controlledPlayerId) == ParticipationState.Bench)
            {
                var incoming = namedPlayers[controlledPlayerId];
                var outgoing = players.Where(p => p.Player.ClubId == incoming.ClubId && p.IsGoalkeeper == (CoachSelection.Role == "gk"))
                    .OrderByDescending(p => p.PositionRoleId == CoachSelection.Role).ThenBy(p => careerCoach.Ability(p.Player, CoachSelection.Role)).First();
                players.Remove(outgoing); participation[outgoing.Player.Id] = ParticipationState.Bench; AddReplacement(outgoing, incoming);
            }
            user = Controlled;
            if (user != null) AssignCoachRole(user);
            ParticipationMessage = CoachSelection.Reason;
        }
        private string careerUserId;
        private void AssignCoachRole(FootballerMotion user)
        {
            string role = careerCoach.AssignedRole;
            // Moving into/out of goalkeeper trades roles with another player, preserving exactly one keeper.
            if ((role == "gk") != user.IsGoalkeeper)
            {
                var other = players.First(p => p != user && p.IsHome == user.IsHome && p.IsGoalkeeper == (role == "gk"));
                other.IsGoalkeeper = user.IsGoalkeeper;
                other.PositionRoleId = user.PositionRoleId;
                other.Anchor = user.Anchor; other.Position = other.Target = other.Anchor;
            }
            user.PositionRoleId = role; user.IsGoalkeeper = role == "gk";
            var anchor = RoleAnchor(role); user.Anchor = new PitchPoint(anchor.X * user.Direction, anchor.Z * user.Direction);
            user.Position = user.Target = user.Anchor;
        }
        private PitchPoint? CoachTarget(FootballerMotion player)
        {
            if (!CoachEnabled || player.Player.Id != controlledPlayerId || player.IsGoalkeeper) return null;
            switch (CoachInstruction)
            {
                case OffBallIntent.HoldZone: return new PitchPoint(player.Anchor.X + BallPosition.X * .15, player.Anchor.Z + BallPosition.Z * .1).Bounded();
                case OffBallIntent.Press: return BallPosition;
                case OffBallIntent.ForwardRun: return new PitchPoint(38 * player.Direction, player.Anchor.Z).Bounded();
                case OffBallIntent.FindSpace: return new[] { new PitchPoint(BallPosition.X + 8 * player.Direction, -18).Bounded(), new PitchPoint(BallPosition.X + 8 * player.Direction, 18).Bounded() }
                    .OrderByDescending(p => players.Where(o => o.IsHome != player.IsHome).Min(o => o.Position.Distance(p))).First();
                default: return new PitchPoint(BallPosition.X - 7 * player.Direction, player.Anchor.Z).Bounded();
            }
        }
        private void BeginCoachDeviation(string choice, bool justified = false)
        {
            if (!CoachEnabled || Controlled == null || session.Tick < nextCoachDeviationTick || coachDeviationCause != null) return;
            coachDeviationCause = progressionMatchId + ":deviation:" + session.Tick;
            coachDeviationChoice = choice; coachDeviationJustified = justified || LateChase;
            coachDeviationDanger = Math.Max(0, Math.Min(1, -BallPosition.X * Controlled.Direction / 52.5));
            coachDeviationExpires = session.Tick + Ticks(20); nextCoachDeviationTick = coachDeviationExpires;
        }
        private void FinishCoachDeviation(bool success)
        {
            if (coachDeviationCause == null) return;
            careerCoach.Deviation(coachDeviationCause, success, coachDeviationJustified, coachDeviationDanger, coachDeviationChoice);
            coachDeviationCause = null;
        }
        private void StepCoach()
        {
            if (!CoachEnabled || coachDeviationCause == null) return;
            if (Controlled == null || session.Tick >= coachDeviationExpires || FixturePhase == FixturePhase.FullTime)
                FinishCoachDeviation(Controlled != null && PossessionClubId == Controlled.Player.ClubId);
        }
        private void CoachAction(ActionResolved action, RestartKind? restart)
        {
            if (!CoachEnabled || action.PlayerId != controlledPlayerId) return;
            FinishCoachDeviation(action.Succeeded);
            if (restart.HasValue) careerCoach.SetPieceResult(progressionMatchId + ":coach:" + action.EventId, restart.Value, action.Succeeded);
        }
    }
}
