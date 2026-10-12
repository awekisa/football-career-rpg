using System;
using System.Collections.Generic;
using System.Linq;

namespace FootballRpg.Simulation
{
    public sealed partial class OpenPlayWorld
    {
        public HandlingRules Handling { get; } = new HandlingRules();
        public string HoldingKeeperId { get; private set; }
        public KeeperDecision PendingKeeper { get; private set; }
        public KeeperResolved LastKeeperAction { get; private set; }
        public event Action<KeeperResolved> KeeperCommitted;
        private readonly Dictionary<string, KeeperResolved> committedKeeper = new Dictionary<string, KeeperResolved>();
        private readonly Dictionary<string, long> keeperCooldowns = new Dictionary<string, long>();
        private FootballActionResolved keeperThreat;
        private KeeperResolved keeperFlight;
        private long keeperDecisionSequence;

        private KeeperContext KeeperSnapshot(FootballerMotion keeper, KeeperSituation situation, PitchPoint ball, double height, double speed) =>
            new KeeperContext(Snapshot(keeper), situation, ball, height, speed, keeper.Direction, Handling, HoldingKeeperId == keeper.Player.Id,
                players.Where(p => p.IsHome != keeper.IsHome).Select(Snapshot));

        private IReadOnlyList<KeeperPreview> KeeperOptions(KeeperContext c)
        {
            var options = new List<KeeperPreview>();
            foreach (KeeperAction kind in Enum.GetValues(typeof(KeeperAction)))
            {
                var recipients = GoalkeeperResolver.IsDistribution(kind)
                    ? players.Where(p => p.Player.ClubId == c.Keeper.ClubId && p.Player.Id != c.Keeper.Id).Select(Snapshot).ToArray()
                    : new PassActor[] { null };
                foreach (var recipient in recipients)
                    if ((kind != KeeperAction.HandleIllegally || Rules != null) && GoalkeeperResolver.IsAvailable(c, kind, recipient)) options.Add(GoalkeeperResolver.Preview(c, kind, recipient));
            }
            return options.AsReadOnly();
        }
        private bool OpenKeeperChoice(KeeperContext context)
        {
            var options = KeeperOptions(context);
            if (options.Count == 0) return false;
            if (context.Keeper.Id == controlledPlayerId)
            {
                session.Pause();
                PendingKeeper = new KeeperDecision(session.SessionId + ":keeper-" + (++keeperDecisionSequence), session.Tick, context, options);
                DecisionCount++; return true;
            }
            // AI uses the same previews and outcome application. Prefer security under pressure.
            var choice = options.FirstOrDefault(p => (p.Kind == KeeperAction.HoldSave || p.Kind == KeeperAction.Claim) && p.Chance >= .45f)
                ?? options.FirstOrDefault(p => p.Kind == KeeperAction.RushOut && (context.Situation == KeeperSituation.LooseBall || context.Keeper.Position.Distance(context.Ball) < 4))
                ?? options.Where(p => p.Kind != KeeperAction.HandleIllegally).OrderByDescending(p => p.Chance).First();
            var result = GoalkeeperResolver.Commit(session, "open-play-" + (++actionSequence), choice, true);
            ApplyKeeperAction(result); return true;
        }
        private void OpenKeeperPossession(FootballerMotion keeper)
        {
            OpenKeeperChoice(KeeperSnapshot(keeper, KeeperSituation.Distribution, BallPosition, BallHeight, 0));
            if (keeper.Player.Id == controlledPlayerId && HoldingKeeperId != keeper.Player.Id) OpenDecision(false);
        }

        private bool TryKeeperThreat(FootballerMotion owner)
        {
            if (HoldingKeeperId != null || (BallState != BallPhase.Owned && BallState != BallPhase.Loose)) return false;
            foreach (var keeper in players.Where(p => p.IsGoalkeeper))
            {
                if (owner != null && owner.IsHome == keeper.IsHome) continue;
                if (directRestart.HasValue && keeper.Player.Id == Handling.LastPlayerId) continue;
                if (keeperCooldowns.TryGetValue(keeper.Player.Id, out var expiry) && session.Tick < expiry) continue;
                if (!HandlingRules.InOwnArea(BallPosition, keeper.Direction) || keeper.Position.Distance(BallPosition) > 12) continue;
                if (owner != null && players.Any(p => p.IsHome == keeper.IsHome && p != keeper && p.Position.Distance(owner.Position) < 4)) continue;
                var situation = owner == null ? KeeperSituation.LooseBall : KeeperSituation.OneOnOne;
                keeperCooldowns[keeper.Player.Id] = session.Tick + Ticks(3);
                if (OpenKeeperChoice(KeeperSnapshot(keeper, situation, BallPosition, BallHeight, owner == null ? 3 : 6))) return true;
            }
            return false;
        }

        private bool TryKeeperIntervention(FootballActionResolved threat)
        {
            bool shot = threat.Outcome == FootballOutcome.ShotOnTarget;
            if (!shot && threat.Outcome != FootballOutcome.DeliveryArrives) return false;
            var attacker = players.Single(p => p.Player.Id == threat.PlayerId);
            var keeper = players.Where(p => p.IsHome != attacker.IsHome && p.IsGoalkeeper)
                .OrderBy(p => p.Position.Distance(BallPosition)).FirstOrDefault();
            if (keeper == null) return false;
            // The flight's target is a shot destination or cross reception point, never a rerolled trajectory.
            var context = KeeperSnapshot(keeper, shot ? KeeperSituation.Shot : KeeperSituation.Cross, BallPosition, threat.Preview.DeliveryHeight,
                shot ? (threat.Preview.Variant == "power" ? 28 : 21) : 14);
            if (KeeperOptions(context).Count == 0) return false;
            keeperThreat = threat;
            // Prevent a pending save from entering the ordinary arrival or loose-ball AI path.
            BallState = BallPhase.KeeperIntervention;
            OpenKeeperChoice(context);
            return true;
        }

        public KeeperResolved CommitKeeperDecision(string id, string optionId)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (committedKeeper.TryGetValue(id, out var previous))
            {
                if (previous.Preview.OptionId != optionId) throw new InvalidOperationException("This goalkeeper decision already used another option.");
                return previous;
            }
            var d = PendingKeeper;
            if (d == null || d.Id != id || d.Tick != session.Tick || session.State != MatchRunState.Paused)
                throw new InvalidOperationException("Goalkeeper decision is no longer current.");
            var choice = d.Options.SingleOrDefault(p => p.OptionId == optionId);
            if (choice == null) throw new ArgumentException("Choose an available goalkeeper option.");
            var keeper = players.Single(p => p.Player.Id == d.Context.Keeper.Id);
            var current = KeeperSnapshot(keeper, d.Context.Situation, BallPosition, d.Context.Height, d.Context.Speed);
            if (!GoalkeeperResolver.IsAvailable(current, choice.Kind, choice.Receiver)) throw new InvalidOperationException("Goalkeeper option is no longer lawful or reachable.");
            var result = GoalkeeperResolver.Commit(session, id, choice, false);
            committedKeeper.Add(id, result); PendingKeeper = null; PendingDecision = null;
            session.CompleteResolution(result.EventId);
            ApplyKeeperAction(result);
            return result;
        }

        private void ApplyKeeperAction(KeeperResolved result)
        {
            LastKeeperAction = result;
            var preview = result.Preview; var keeper = players.Single(p => p.Player.Id == result.PlayerId);
            var threat = keeperThreat; keeperThreat = null;
            if (preview.Kind == KeeperAction.HandleIllegally)
            {
                var kind = HandlingRules.InOwnArea(BallPosition, keeper.Direction) ? FoulKind.KeeperHandling : FoulKind.Handball;
                HandleFoul(new FoulIncident(result.EventId + ":handling", keeper.Player.Id, null, BallPosition, keeper.Direction, kind, attemptToPlayBall: false));
                KeeperCommitted?.Invoke(result); return;
            }
            if (!result.Action.Succeeded && preview.Kind == KeeperAction.RushOut && preview.Context.Situation == KeeperSituation.OneOnOne && OwnerId != null)
            {
                var attacker = players.Single(p => p.Player.Id == OwnerId);
                double miss = (result.Action.Roll - preview.Chance) / (1 - preview.Chance);
                if (miss > .7 && keeper.Position.Distance(attacker.Position) < 4)
                {
                    HandleFoul(new FoulIncident(result.EventId + ":rush", keeper.Player.Id, attacker.Player.Id, attacker.Position, keeper.Direction,
                        FoulKind.Challenge, miss > .95 ? ContactSeverity.Reckless : ContactSeverity.Careless, true, true));
                    KeeperCommitted?.Invoke(result); return;
                }
            }
            keeperCooldowns[keeper.Player.Id] = session.Tick + Ticks(3);
            if (GoalkeeperResolver.IsDistribution(preview.Kind)) BeginKeeperDistribution(result, keeper);
            else if (result.Outcome == KeeperOutcome.FeetControl)
            {
                HoldingKeeperId = null;
                Handling.RecordTouch(keeper.Player.Id, keeper.Player.ClubId, BallTouchKind.KeeperRelease);
                GiveBall(keeper);
            }
            else if (result.Outcome == KeeperOutcome.Held)
            {
                keeper.Position = preview.Target;
                Handling.RecordTouch(keeper.Player.Id, keeper.Player.ClubId, BallTouchKind.Other);
                HoldingKeeperId = keeper.Player.Id; ReceiveBall(keeper, preview.Context.Situation != KeeperSituation.Shot);
            }
            else if (result.Outcome == KeeperOutcome.Deflected)
            {
                RecordOffsideTouch(keeper, preview.Context.Situation == KeeperSituation.Shot ? OffsideTouch.Save : OffsideTouch.Deflection);
                Handling.RecordTouch(keeper.Player.Id, keeper.Player.ClubId, BallTouchKind.Other);
                BallPosition = new PitchPoint(BallPosition.X + 5 * keeper.Direction, BallPosition.Z >= 0 ? 8 : -8).Bounded();
                MakeLoose();
            }
            else if (preview.Context.Situation == KeeperSituation.Shot)
            { ScoreShot(threat); }
            else if (preview.Context.Situation == KeeperSituation.Cross)
            { BeginArrival(players.Single(p => p.Player.Id == threat.Preview.Receiver.Id), threat.Preview.DeliveryHeight, 14); }
            else if (preview.Context.Situation == KeeperSituation.Distribution) MakeLoose();
            else if (result.Outcome == KeeperOutcome.Positioned) keeper.Position = preview.Target;
            else if (preview.Kind == KeeperAction.RushOut)
            { keeper.Position = keeper.Position.MoveTowards(BallPosition, Math.Min(6, keeper.Position.Distance(BallPosition))).Bounded(); }
            RefreshIntent(); KeeperCommitted?.Invoke(result);
        }
        private void ScoreShot(FootballActionResolved shot)
        {
            if (shot == null) throw new InvalidOperationException("Missing committed shot.");
            if (Rules != null)
            {
                int direction = players.Single(p => p.Player.Id == shot.PlayerId).Direction;
                var previous = BallPosition;
                BallPosition = new PitchPoint((52.5 + PitchBoundary.BallRadius + .01) * direction, BallPosition.Z);
                CheckBoundary(previous, BallHeight); return;
            }
            if (players.Single(p => p.Player.Id == shot.PlayerId).IsHome) HomeGoals++; else AwayGoals++;
            BallState = BallPhase.OutOfPlay; OwnerId = null; HoldingKeeperId = null; PossessionClubId = null; restartSeconds = 0; receiver = null;
            RefreshIntent();
        }
        private void BeginKeeperDistribution(KeeperResolved result, FootballerMotion keeper)
        {
            RecordOffsideTouch(keeper);
            keeperFlight = result; activeFootballAction = null;
            receiver = result.Action.Succeeded ? players.Single(p => p.Player.Id == result.Preview.Receiver.Id) : null;
            destination = result.Action.Succeeded ? result.Preview.Target : keeper.Position.MoveTowards(result.Preview.Target, 6).Bounded();
            flightOrigin = keeper.Position;
            flightDuration = Math.Max(.35, flightOrigin.Distance(destination) / (result.Preview.Kind == KeeperAction.Roll ? 10 : 19));
            if (receiver != null) flightDuration = Math.Max(flightDuration, receiver.Position.Distance(destination) / (3.5 + receiver.Player.FootballAttributes["pace"] * .035) + session.FixedStep.TotalSeconds);
            Handling.RecordTouch(keeper.Player.Id, keeper.Player.ClubId, HoldingKeeperId == keeper.Player.Id ? BallTouchKind.KeeperRelease : BallTouchKind.DeliberateKick, result.Preview.Receiver.Id);
            HoldingKeeperId = null; OwnerId = IncomingPlayerId = null; BallState = BallPhase.Travelling; flightSeconds = 0;
        }
        private void StepKeeperDistribution(double dt)
        {
            flightSeconds += dt; double t = Math.Min(1, flightSeconds / flightDuration);
            BallPosition = flightOrigin.MoveTowards(destination, flightOrigin.Distance(destination) * t);
            BallHeight = .3 + Math.Sin(Math.PI * t) * (keeperFlight.Preview.Kind == KeeperAction.Roll ? 0 : keeperFlight.Preview.Kind == KeeperAction.LongKick ? 5 : 1.5);
            if (CheckOffsideInterference()) return;
            if (flightSeconds < flightDuration) return;
            var fact = keeperFlight; keeperFlight = null;
            if (fact.Action.Succeeded) { if (GiveBall(receiver)) CompletedPasses++; }
            else MakeLoose();
        }
    }
}
