using System;
using System.Collections.Generic;
using System.Linq;

namespace FootballRpg.Simulation
{
    public sealed partial class OpenPlayWorld
    {
        private readonly Dictionary<string, FootballActionResolved> committedFootballDecisions = new Dictionary<string, FootballActionResolved>();
        private FootballActionResolved activeFootballAction;
        private double flightStartHeight, flightArc, incomingSpeed;
        public double BallHeight { get; private set; } = .3;
        public string IncomingPlayerId { get; private set; }
        public FootballActionResolved LastFootballAction { get; private set; }
        public event Action<FootballActionResolved> FootballActionCommitted;
        public int HomeGoals { get; private set; }
        public int AwayGoals { get; private set; }

        public IReadOnlyList<FootballActionPreview> AvailableActions(FootballerMotion actor)
        {
            bool incoming = BallState == BallPhase.Incoming && IncomingPlayerId == actor.Player.Id;
            if (!incoming && (BallState != BallPhase.Owned || OwnerId != actor.Player.Id)) return Array.Empty<FootballActionPreview>();
            var context = new FootballActionContext(Snapshot(actor), incoming ? BallSituation.Arriving : BallSituation.Controlled,
                incoming ? BallHeight : 0, incoming ? incomingSpeed : 0, actor.Direction,
                players.Where(p => p.IsHome != actor.IsHome).Select(Snapshot));
            var options = new List<FootballActionPreview>();
            foreach (FootballActionKind kind in Enum.GetValues(typeof(FootballActionKind)))
            {
                bool targeted = kind == FootballActionKind.Cross || kind == FootballActionKind.FirstTimePass || kind == FootballActionKind.HeaderPass;
                var targets = targeted ? players.Where(p => p.IsHome == actor.IsHome && p != actor).Select(Snapshot).ToArray() : new PassActor[] { null };
                foreach (var target in targets)
                    foreach (string variant in FootballActionResolver.Variants(kind))
                        if (FootballActionResolver.IsAvailable(context, kind, variant, target)) options.Add(FootballActionResolver.Preview(context, kind, variant, target));
            }
            return options.AsReadOnly();
        }

        private void BeginArrival(FootballerMotion player, double height, double speed)
        {
            BallState = BallPhase.Incoming;
            OwnerId = null;
            IncomingPlayerId = player.Player.Id;
            BallPosition = player.Position;
            BallHeight = height;
            incomingSpeed = speed;
            PossessionClubId = player.Player.ClubId;
            possessionSequence++;
            RefreshIntent();
            if (player.Player.Id == controlledPlayerId)
            {
                session.Pause();
                PendingDecision = new PassDecision(session.SessionId + ":arrival-" + possessionSequence,
                    session.Tick, player.Player.Id, Array.Empty<PassPreview>(), AvailableActions(player));
                DecisionCount++;
            }
        }

        private void ChooseAiReception()
        {
            var actor = players.Single(p => p.Player.Id == IncomingPlayerId);
            var options = AvailableActions(actor);
            var preferred = options.Where(p => p.Kind == FootballActionKind.HeaderShot && actor.Position.X * actor.Direction > 30)
                .Concat(options.Where(p => p.Kind == FootballActionKind.HeaderPass && actionSequence % 2 == 0))
                .Concat(options.Where(p => p.Kind == FootballActionKind.FirstTimePass && actionSequence % 2 == 0))
                .Concat(options.Where(p => p.Kind == FootballActionKind.Control))
                .Concat(options.Where(p => p.Kind == FootballActionKind.HeaderPass)).Concat(options).First();
            StartAutomaticFootballAction(actor, preferred);
        }

        private bool ChooseAiAction(FootballerMotion actor)
        {
            var options = AvailableActions(actor);
            FootballActionPreview choice = null;
            if (actor.Position.X * actor.Direction > 28)
                choice = options.FirstOrDefault(p => p.Kind == FootballActionKind.Shot);
            if (choice == null && Math.Abs(actor.Position.Z) > 18 && actor.Position.X * actor.Direction > 0)
                choice = options.Where(p => p.Kind == FootballActionKind.Cross && p.Variant == (actionSequence % 2 == 0 ? "lofted" : "low") && Math.Abs(p.Receiver.Position.Z) < 16)
                    .OrderByDescending(p => p.Receiver.Position.X * actor.Direction).FirstOrDefault();
            if (choice == null && actor.Position.X * actor.Direction < -28 && players.Any(p => p.IsHome != actor.IsHome && p.Position.Distance(actor.Position) < 5))
                choice = options.FirstOrDefault(p => p.Kind == FootballActionKind.Clearance);
            if (choice == null && actionSequence % 4 == 0)
                choice = options.FirstOrDefault(p => p.Kind == FootballActionKind.Dribble && p.Variant == "carry");
            if (choice == null) return false;
            StartAutomaticFootballAction(actor, choice);
            return true;
        }

        private void StartAutomaticFootballAction(FootballerMotion actor, FootballActionPreview preview)
        {
            var result = FootballActionResolver.Commit(session, "open-play-" + (++actionSequence), preview, true);
            BeginFootballFlight(actor, result);
            FootballActionCommitted?.Invoke(result);
        }

        public FootballActionResolved CommitFootballDecision(string decisionId, string optionId)
        {
            if (decisionId == null) throw new ArgumentNullException(nameof(decisionId));
            if (committedDecisions.ContainsKey(decisionId)) throw new InvalidOperationException("This decision already committed a pass.");
            if (committedFootballDecisions.TryGetValue(decisionId, out var previous))
            {
                if (previous.Preview.OptionId != optionId) throw new InvalidOperationException("This decision already committed another action.");
                return previous;
            }
            var decision = PendingDecision;
            if (decision == null || decision.Id != decisionId || session.State != MatchRunState.Paused || session.Tick != decision.Tick ||
                (OwnerId != decision.PlayerId && IncomingPlayerId != decision.PlayerId)) throw new InvalidOperationException("Stale decision.");
            var preview = decision.Actions.SingleOrDefault(p => p.OptionId == optionId);
            if (preview == null) throw new ArgumentException("Unavailable action.");
            var actor = players.Single(p => p.Player.Id == decision.PlayerId);
            var result = FootballActionResolver.Commit(session, decisionId, preview, false);
            BeginFootballFlight(actor, result);
            PendingDecision = null;
            committedFootballDecisions.Add(decisionId, result);
            session.CompleteResolution(result.EventId);
            FootballActionCommitted?.Invoke(result);
            return result;
        }

        private void BeginFootballFlight(FootballerMotion actor, FootballActionResolved result)
        {
            LastFootballAction = activeFootballAction = result;
            destination = result.Destination;
            receiver = result.ReceivingPlayerId == null ? null : players.Single(p => p.Player.Id == result.ReceivingPlayerId);
            flightOrigin = BallPosition;
            flightStartHeight = BallHeight;
            flightArc = result.Preview.Kind == FootballActionKind.Cross && result.Preview.Variant == "lofted" ? 3
                : result.Preview.Kind == FootballActionKind.Clearance ? 4 : .3;
            flightDuration = Math.Max(.35, flightOrigin.Distance(destination) / 19);
            if (receiver != null)
                flightDuration = Math.Max(flightDuration, receiver.Position.Distance(destination) / (3.5 + receiver.Player.FootballAttributes["pace"] * .035) + session.FixedStep.TotalSeconds);
            restartPlayer = players.Where(p => p.IsHome != actor.IsHome).OrderBy(p => p.Position.Distance(destination)).First();
            OwnerId = IncomingPlayerId = null;
            BallState = BallPhase.Travelling;
            flightSeconds = 0;
        }

        private void FinishFootballAction()
        {
            var result = activeFootballAction;
            activeFootballAction = null;
            switch (result.Outcome)
            {
                case FootballOutcome.Controlled:
                case FootballOutcome.TeammateControls:
                case FootballOutcome.DefenderBeaten:
                case FootballOutcome.Advanced:
                case FootballOutcome.Dispossessed:
                    GiveBall(receiver); break;
                case FootballOutcome.DeliveryArrives:
                    BeginArrival(receiver, result.Preview.DeliveryHeight, 14); break;
                case FootballOutcome.Goal:
                    if (players.Single(p => p.Player.Id == result.PlayerId).IsHome) HomeGoals++; else AwayGoals++;
                    BallState = BallPhase.OutOfPlay; PossessionClubId = null; restartSeconds = 0; receiver = null; break;
                case FootballOutcome.Wide:
                    BallState = BallPhase.OutOfPlay; PossessionClubId = null; restartSeconds = 0; receiver = null; break;
                default: MakeLoose(); break;
            }
            RefreshIntent();
        }
    }
}
