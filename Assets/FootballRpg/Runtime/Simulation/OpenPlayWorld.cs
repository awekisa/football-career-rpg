using System;
using System.Collections.Generic;
using System.Linq;

namespace FootballRpg.Simulation
{
    public readonly struct PitchPoint
    {
        public double X { get; }
        public double Z { get; }
        public PitchPoint(double x, double z) { X = x; Z = z; }
        public double Distance(PitchPoint other) => Math.Sqrt((X - other.X) * (X - other.X) + (Z - other.Z) * (Z - other.Z));
        public PitchPoint MoveTowards(PitchPoint target, double distance)
        {
            double length = Distance(target);
            double amount = length <= distance || length == 0 ? 1 : distance / length;
            return new PitchPoint(X + (target.X - X) * amount, Z + (target.Z - Z) * amount);
        }
        public PitchPoint Bounded() => new PitchPoint(Math.Max(-51, Math.Min(51, X)), Math.Max(-32, Math.Min(32, Z)));
    }

    public sealed class FootballerMotion
    {
        public PlayerState Player { get; }
        public bool IsHome { get; }
        public int Direction { get; internal set; }
        public PitchPoint Anchor { get; internal set; }
        public PitchPoint Position { get; internal set; }
        public PitchPoint Target { get; internal set; }
        public PitchPoint Facing { get; internal set; }
        internal FootballerMotion(PlayerState player, bool home, PitchPoint anchor)
        { Player = player; IsHome = home; Direction = home ? 1 : -1; Anchor = anchor; Position = anchor; Target = anchor; Facing = new PitchPoint(home ? 1 : -1, 0); }
    }

    public enum BallPhase { Owned, Travelling, Loose, OutOfPlay, Incoming, KeeperIntervention }

    /// <summary>Seeded open-play sandbox with frozen user decisions and temporary movement intentions.</summary>
    public sealed partial class OpenPlayWorld : IDisposable
    {
        private readonly MatchSession session;
        private readonly string controlledPlayerId;
        private long possessionSequence;
        private readonly Dictionary<string, PassResolved> committedDecisions = new Dictionary<string, PassResolved>();
        public PassDecision PendingDecision { get; private set; }
        public int DecisionCount { get; private set; }
        private readonly List<FootballerMotion> players = new List<FootballerMotion>();
        private FootballerMotion receiver;
        private PitchPoint destination;
        private double flightDuration;
        private PitchPoint flightOrigin;
        private FootballerMotion restartPlayer;
        private double restartSeconds;
        public PassResolved LastPass { get; private set; }
        public event Action<PassResolved> PassCommitted;
        private double heldSeconds;
        private double flightSeconds;
        private double challengeSeconds;
        private string lastClub;
        private long actionSequence;
        public IReadOnlyList<FootballerMotion> Players { get; }
        public PitchPoint BallPosition { get; private set; }
        public BallPhase BallState { get; private set; }
        public string OwnerId { get; private set; }
        public string IntendedReceiverId => receiver?.Player.Id;
        public string PossessionClubId { get; private set; }
        public int CompletedPasses { get; private set; }
        public int Recoveries { get; private set; }
        public int PossessionChanges { get; private set; }

        public OpenPlayWorld(CareerState career, MatchSession match, string controlledPlayerId = null, bool startWithControlledPlayer = false, double? openingArrivalHeight = null)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            session = match ?? throw new ArgumentNullException(nameof(match));
            this.controlledPlayerId = controlledPlayerId;
            foreach (bool home in new[] { true, false })
            {
                string club = home ? career.Fixture.HomeClubId : career.Fixture.AwayClubId;
                var squad = career.Clubs[club].SquadPlayerIds.Take(11).Select(id => career.Players[id]).ToArray();
                if (squad.Length != 11) throw new ArgumentException("Open play requires eleven players per team.");
                foreach (var player in squad)
                {
                    var sameRole = squad.Where(p => p.PrimaryPositionId == player.PrimaryPositionId).ToArray();
                    int index = Array.IndexOf(sameRole, player);
                    var anchor = RoleAnchor(player.PrimaryPositionId);
                    double spread = (index - (sameRole.Length - 1) * .5) * 14;
                    int direction = home ? 1 : -1;
                    players.Add(new FootballerMotion(player, home,
                        new PitchPoint(anchor.X * direction, (anchor.Z + spread) * direction).Bounded()));
                }
            }
            Players = players.AsReadOnly();
            if (controlledPlayerId != null && !players.Any(p => p.Player.Id == controlledPlayerId))
                throw new ArgumentException("Controlled player must be in the starting lineup.");
            if (startWithControlledPlayer && controlledPlayerId == null)
                throw new ArgumentException("An opening controlled possession requires a controlled player.");
            if (openingArrivalHeight.HasValue && (!startWithControlledPlayer || double.IsNaN(openingArrivalHeight.Value) || double.IsInfinity(openingArrivalHeight.Value) || openingArrivalHeight.Value < 0 || openingArrivalHeight.Value > 3))
                throw new ArgumentException("Opening reception requires a controlled player and a reachable height.");
            if (openingArrivalHeight.HasValue && openingArrivalHeight.Value > players.Single(p => p.Player.Id == controlledPlayerId).Player.HeightCm / 100 + .9)
                throw new ArgumentException("Opening arrival is above the player's aerial reach.");
            if (openingArrivalHeight.HasValue)
                BeginArrival(players.Single(p => p.Player.Id == controlledPlayerId), openingArrivalHeight.Value, 12);
            else GiveBall(startWithControlledPlayer ? players.Single(p => p.Player.Id == controlledPlayerId)
                : players.First(p => p.IsHome && p.Player.PrimaryPositionId != "gk"));
            session.TickAdvanced += Step;
        }

        public static PitchPoint RoleAnchor(string role)
        {
            switch (role)
            {
                case "gk": return new PitchPoint(-48, 0);
                case "cb": return new PitchPoint(-34, 0);
                case "lb": return new PitchPoint(-30, -25);
                case "rb": return new PitchPoint(-30, 25);
                case "lwb": return new PitchPoint(-22, -27);
                case "rwb": return new PitchPoint(-22, 27);
                case "dm": return new PitchPoint(-22, -10);
                case "cm": return new PitchPoint(-17, 0);
                case "am": return new PitchPoint(-10, 12);
                case "lm": return new PitchPoint(-15, -25);
                case "rm": return new PitchPoint(-15, 25);
                case "lw": return new PitchPoint(-7, -24);
                case "rw": return new PitchPoint(-7, 24);
                case "st": return new PitchPoint(-4, 0);
                default: throw new ArgumentException("Unsupported position: " + role);
            }
        }

        private void Step(long tick)
        {
            // Defend against direct clock resume: a pending user choice may never be delegated to AI.
            if (HasPendingDecision || OffsideNoticePending) { session.Pause(); return; }
            if (StepFixture()) return;
            if (CheckOffsideInterference()) return;
            RefreshIntent();
            if (BallState == BallPhase.Incoming) { ChooseAiReception(); return; }
            double dt = session.FixedStep.TotalSeconds;
            challengeSeconds = Math.Max(0, challengeSeconds - dt);
            var owner = players.FirstOrDefault(p => p.Player.Id == OwnerId);
            if (owner != null && HoldingKeeperId == null) RecordOffsideTouch(owner);
            if (TryKeeperThreat(owner)) return;
            if (TryOpenDefense(owner)) return;
            if (owner != null && owner.Player.PrimaryPositionId == "gk" && owner.Player.Id != controlledPlayerId && heldSeconds >= .5)
            { OpenKeeperPossession(owner); return; }
            // Calculate every target from the same snapshot before integrating anyone's movement.
            foreach (var player in players) player.Target = IntendedTarget(player, owner);
            foreach (var player in players)
            {
                double speed = 3.5 + player.Player.FootballAttributes["pace"] * .035;
                if (session.Tick < beatenUntilTick && player.Player.Id == controlledPlayerId) speed *= .25;
                if (session.Tick < containedUntilTick && player.Player.Id == containedPlayerId) speed *= .25;
                var previous = player.Position;
                player.Position = player.Position.MoveTowards(player.Target, speed * dt).Bounded();
                if (previous.Distance(player.Position) > .001)
                    player.Facing = new PitchPoint(player.Position.X - previous.X, player.Position.Z - previous.Z);
            }
            if (keeperFlight != null) { StepKeeperDistribution(dt); return; }
            if (BallState == BallPhase.Travelling)
            {
                var previousBall = BallPosition; double previousHeight = BallHeight;
                flightSeconds += dt;
                BallPosition = flightOrigin.MoveTowards(destination, flightOrigin.Distance(destination) * Math.Min(1, flightSeconds / flightDuration));
                if (activeFootballAction != null)
                {
                    double t = Math.Min(1, flightSeconds / flightDuration);
                    BallHeight = flightStartHeight * (1 - t) + activeFootballAction.Preview.DeliveryHeight * t + Math.Sin(Math.PI * t) * flightArc;
                }
                if (CheckOffsideInterference() || CheckBoundary(previousBall, previousHeight)) return;
                if (flightSeconds >= flightDuration)
                {
                    if (activeFootballAction != null) { FinishFootballAction(); return; }
                    if (LastPass.Outcome == PassOutcome.Received)
                    { if (GiveBall(receiver)) CompletedPasses++; }
                    else if (LastPass.Outcome == PassOutcome.Intercepted) GiveBall(receiver);
                    else if (LastPass.Outcome == PassOutcome.OutOfPlay)
                    {
                        BallState = BallPhase.OutOfPlay;
                        PossessionClubId = null;
                        receiver = null;
                        restartSeconds = 0;
                        RefreshIntent();
                    }
                    else MakeLoose();
                }
                return;
            }
            if (BallState == BallPhase.OutOfPlay)
            {
                // Sandbox reset only: formal throw-ins/corners are implemented with match laws later.
                restartSeconds += dt;
                if (restartSeconds >= 1)
                {
                    restartPlayer.Position = BallPosition.Bounded();
                    GiveBall(restartPlayer);
                }
                return;
            }
            if (BallState == BallPhase.Loose)
            {
                var nearest = players.Where(p => !directRestart.HasValue || p.Player.Id != Handling.LastPlayerId).OrderBy(p => p.Position.Distance(BallPosition)).First();
                if (nearest.Position.Distance(BallPosition) <= 1.4) { Recoveries++; GiveBall(nearest); }
                return;
            }
            BallPosition = owner.Position;
            heldSeconds += dt;
            var challenger = players.Where(p => p.IsHome != owner.IsHome).OrderBy(p => p.Position.Distance(BallPosition)).First();
            if (HoldingKeeperId != owner.Player.Id && challenger.Player.Id != controlledPlayerId && challenger.Position.Distance(BallPosition) < 2 && challengeSeconds <= 0)
            {
                challengeSeconds = 1.2;
                var preview = DefenseResolver.Preview(Snapshot(challenger), Snapshot(owner), challenger.Direction, DefensiveAction.Tackle);
                var result = DefenseResolver.Commit(session, "open-play-" + (++actionSequence), preview, true);
                if (result.Action.Succeeded) { GiveBall(challenger); return; }
            }
            if (session.Tick < containedUntilTick && owner.Player.Id == containedPlayerId) return;
            if (heldSeconds >= 2.5 && !ChooseAiAction(owner)) Pass(owner);
        }

        private PitchPoint TacticalTarget(FootballerMotion player, FootballerMotion owner)
        {
            bool attacking = player.Player.ClubId == PossessionClubId;
            var ball = BallPosition;
            if (BallState == BallPhase.Loose)
            {
                var chaser = players.Where(p => p.IsHome == player.IsHome).OrderBy(p => p.Position.Distance(ball)).First();
                if (player == chaser) return ball;
            }
            if (BallState == BallPhase.Travelling && player == receiver) return destination;
            if (player.Player.Id == HoldingKeeperId) return player.Position;
            if (player == owner)
                return new PitchPoint(player.Position.X + 8 * player.Direction, player.Position.Z * .9).Bounded();
            if (player.Player.PrimaryPositionId == "gk")
                return new PitchPoint((-48 + (attacking ? 3 : 0)) * player.Direction, ball.Z * .13);
            if (!attacking && owner != null)
            {
                var presser = players.Where(p => p.IsHome == player.IsHome && p.Player.PrimaryPositionId != "gk")
                    .OrderBy(p => p.Position.Distance(ball)).First();
                if (player == presser) return ball;
            }
            double x = player.Anchor.X + ball.X * .27 + (attacking ? 14 : -2) * player.Direction;
            double z = player.Anchor.Z * (attacking ? 1 : .8) + ball.Z * .18;
            // Defenders shade the nearest opposing runner; supporting players avoid crowding teammates.
            if (!attacking)
            {
                var opponent = players.Where(p => p.IsHome != player.IsHome).OrderBy(p => p.Position.Distance(player.Position)).First();
                x += (opponent.Position.X - player.Position.X) * .12;
                z += (opponent.Position.Z - player.Position.Z) * .12;
            }
            foreach (var teammate in players.Where(p => p.IsHome == player.IsHome && p != player))
            {
                double distance = player.Position.Distance(teammate.Position);
                if (distance < 5 && distance > .01)
                { x += (player.Position.X - teammate.Position.X) / distance * (5 - distance); z += (player.Position.Z - teammate.Position.Z) / distance * (5 - distance); }
            }
            return new PitchPoint(x, z).Bounded();
        }

        public PassPreview PreviewPass(string receiverId, PassKind kind)
        {
            if (BallState != BallPhase.Owned) throw new InvalidOperationException("No player owns the ball.");
            var owner = players.Single(p => p.Player.Id == OwnerId);
            var targetPlayer = players.Single(p => p.Player.Id == receiverId);
            var target = kind == PassKind.ToFeet ? targetPlayer.Position
                : new PitchPoint(targetPlayer.Position.X + owner.Direction * 8, targetPlayer.Position.Z).Bounded();
            if (target.Distance(owner.Position) < .1) target = targetPlayer.Position;
            double side = owner.Facing.X * (target.Z - owner.Position.Z) - owner.Facing.Z * (target.X - owner.Position.X);
            string foot = side >= 0 ? "right" : "left";
            return PassResolver.Preview(Snapshot(owner), Snapshot(targetPlayer), kind, target, foot,
                players.Where(p => p.IsHome != owner.IsHome).Select(Snapshot));
        }

        private static PassActor Snapshot(FootballerMotion player) => new PassActor(player.Player, player.Position, player.Facing);

        private void Pass(FootballerMotion owner)
        {
            var teammate = players.Where(p => p.IsHome == owner.IsHome && p != owner && p.Position.Distance(owner.Position) >= .1)
                .OrderByDescending(p => PassScore(owner, p)).First();
            var kind = (teammate.Position.X - owner.Position.X) * owner.Direction > 8 ? PassKind.ThroughBall : PassKind.ToFeet;
            var preview = PreviewPass(teammate.Player.Id, kind);
            var result = PassResolver.Commit(session, "open-play-" + (++actionSequence), preview, true);
            BeginFlight(result, owner);
            PassCommitted?.Invoke(result);
        }

        private void BeginFlight(PassResolved result, FootballerMotion owner) => BeginPassFlight(result, owner, null);

        private void BeginPassFlight(PassResolved result, FootballerMotion owner, RestartKind? restart)
        {
            RecordOffsideTouch(owner, OffsideTouch.DeliberatePlay, restart);
            activeFootballAction = null;
            HoldingKeeperId = null;
            Handling.RecordTouch(owner.Player.Id, owner.Player.ClubId, BallTouchKind.DeliberateKick, result.Preview.Receiver.Id);
            BallHeight = .3;
            LastPass = result;
            destination = LastPass.Destination;
            receiver = LastPass.ReceivingPlayerId == null ? null : players.Single(p => p.Player.Id == LastPass.ReceivingPlayerId);
            flightOrigin = BallPosition;
            flightDuration = Math.Max(session.FixedStep.TotalSeconds, flightOrigin.Distance(destination) / 19);
            if (receiver != null)
                flightDuration = Math.Max(flightDuration, receiver.Position.Distance(destination) / (3.5 + receiver.Player.FootballAttributes["pace"] * .035) + session.FixedStep.TotalSeconds);
            restartPlayer = players.Where(p => p.IsHome != owner.IsHome).OrderBy(p => p.Position.Distance(destination)).First();
            OwnerId = null;
            BallState = BallPhase.Travelling;
            flightSeconds = 0;
        }

        private double PassScore(FootballerMotion owner, FootballerMotion candidate)
        {
            double openness = players.Where(p => p.IsHome != owner.IsHome).Min(p => p.Position.Distance(candidate.Position));
            double progress = (candidate.Position.X - owner.Position.X) * owner.Direction;
            return Math.Min(12, openness) * 1.5 + progress * .25 - owner.Position.Distance(candidate.Position) * .2;
        }

        private bool GiveBall(FootballerMotion player) => ReceiveBall(player, true);

        private bool ReceiveBall(FootballerMotion player, bool deliberate)
        {
            if (CheckOffsideTouch(player)) return false;
            directRestart = null;
            if (lastClub != null && lastClub != player.Player.ClubId) PossessionChanges++;
            lastClub = PossessionClubId = player.Player.ClubId;
            BallHeight = HoldingKeeperId == player.Player.Id ? 1.2 : .3;
            IncomingPlayerId = null;
            OwnerId = player.Player.Id;
            receiver = null;
            BallPosition = player.Position;
            RecordOffsideTouch(player, deliberate ? OffsideTouch.DeliberatePlay : OffsideTouch.Save);
            BallState = BallPhase.Owned;
            heldSeconds = 0;
            possessionSequence++;
            RefreshIntent();
            if (player.Player.PrimaryPositionId != "gk") Handling.RecordTouch(player.Player.Id, player.Player.ClubId, BallTouchKind.Other);
            if (HoldingKeeperId != player.Player.Id) HoldingKeeperId = null;
            if (player.Player.Id == controlledPlayerId)
            { if (player.Player.PrimaryPositionId == "gk") OpenKeeperPossession(player); else OpenDecision(); }
            return true;
        }

        private void OpenDecision(bool count = true)
        {
            var owner = players.Single(p => p.Player.Id == OwnerId);
            var options = players.Where(p => p.IsHome == owner.IsHome && p != owner && p.Position.Distance(owner.Position) >= .1)
                .SelectMany(p => new[] { PreviewPass(p.Player.Id, PassKind.ToFeet), PreviewPass(p.Player.Id, PassKind.ThroughBall) }).ToArray();
            var actions = AvailableActions(owner);
            session.Pause();
            PendingDecision = new PassDecision(session.SessionId + ":possession-" + possessionSequence,
                session.Tick, OwnerId, options, actions);
            if (count) DecisionCount++;
        }

        public PassResolved CommitDecision(string decisionId, string receiverId, PassKind kind)
        {
            if (decisionId == null) throw new ArgumentNullException(nameof(decisionId));
            if (committedFootballDecisions.ContainsKey(decisionId)) throw new InvalidOperationException("A different action already used this decision.");
            if (committedDecisions.TryGetValue(decisionId, out var previous))
            {
                if (previous.Preview.Receiver.Id != receiverId || previous.Preview.Kind != kind)
                    throw new InvalidOperationException("This possession already committed a different pass.");
                return previous;
            }
            var decision = PendingDecision;
            if (decision == null || decision.Id != decisionId || session.State != MatchRunState.Paused ||
                session.Tick != decision.Tick || BallState != BallPhase.Owned || OwnerId != decision.PlayerId)
                throw new InvalidOperationException("The decision is no longer current or the match is not paused.");
            var preview = decision.Options.SingleOrDefault(p => p.Receiver.Id == receiverId && p.Kind == kind);
            if (preview == null) throw new ArgumentException("Select one of this possession's valid pass options.");
            var owner = players.Single(p => p.Player.Id == OwnerId);
            var result = PassResolver.Commit(session, decision.Id, preview, false);
            BeginFlight(result, owner);
            PendingDecision = null;
            PendingKeeper = null;
            committedDecisions.Add(decision.Id, result);
            // Resume into the committed flight. Movement represents the fact; it never resolves it again.
            session.CompleteResolution(result.EventId);
            PassCommitted?.Invoke(result);
            return result;
        }

        public void TogglePause()
        {
            if (FixturePhase == FixturePhase.FullTime || OffsideNoticePending) return;
            if (FixturePhase == FixturePhase.HalfTime) { ContinueSecondHalf(); return; }
            if (HasPendingDecision || (Restart != null && Restart.TakerId == controlledPlayerId)) return;
            if (session.State == MatchRunState.Paused) session.Resume();
            else session.Pause();
        }
        private void MakeLoose()
        {
            HoldingKeeperId = null;
            BallHeight = .3;
            IncomingPlayerId = null;
            OwnerId = null;
            receiver = null;
            PossessionClubId = null;
            BallState = BallPhase.Loose;
            RefreshIntent();
        }
        public void Dispose() => session.TickAdvanced -= Step;
    }
}
