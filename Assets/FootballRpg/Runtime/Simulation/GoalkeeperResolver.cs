using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FootballRpg.Simulation
{
    public enum KeeperSituation { Shot, Cross, OneOnOne, LooseBall, Distribution }
    public enum KeeperAction { SetPosition, RushOut, HoldSave, Parry, Claim, Punch, Gather, Roll, Throw, ShortKick, LongKick, ReleaseToFeet, HandleIllegally }
    public enum KeeperOutcome { Positioned, Held, Deflected, TeammateControls, Missed, Fumbled, FeetControl }
    public enum BallTouchKind { Other, DeliberateKick, Header, ThrowIn, KeeperRelease }

    /// <summary>Law 12 handling eligibility; restarts/sanctions are supplied by the later match-rules system.</summary>
    public sealed class HandlingRules
    {
        public string LastPlayerId { get; private set; }
        public string LastClubId { get; private set; }
        public BallTouchKind LastKind { get; private set; }
        public string IntendedPlayerId { get; private set; }
        private string releasedBy;
        public void RecordTouch(string playerId, string clubId, BallTouchKind kind, string intendedPlayerId = null)
        {
            if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(clubId) || !Enum.IsDefined(typeof(BallTouchKind), kind)) throw new ArgumentException("Invalid touch.");
            if (playerId != releasedBy) releasedBy = null;
            if (kind == BallTouchKind.KeeperRelease) releasedBy = playerId;
            LastPlayerId = playerId; LastClubId = clubId; LastKind = kind; IntendedPlayerId = intendedPlayerId;
        }
        public static bool InOwnArea(PitchPoint ball, int direction) => Math.Abs(direction) == 1 && PassActor.Finite(ball) &&
            ball.X * direction >= -52.5 && ball.X * direction <= -36 && Math.Abs(ball.Z) <= 20.16;
        public string Restriction(PassActor keeper, PitchPoint ball, int direction)
        {
            if (keeper == null || keeper.RoleId != "gk") return "Only the assigned goalkeeper may handle";
            if (!InOwnArea(ball, direction)) return "Outside your penalty area — use your feet";
            if (releasedBy == keeper.Id) return "Another player must touch the ball after your release";
            if (LastClubId == keeper.ClubId && LastPlayerId != keeper.Id &&
                (LastKind == BallTouchKind.ThrowIn || LastKind == BallTouchKind.DeliberateKick && IntendedPlayerId == keeper.Id))
                return "Teammate back-pass/throw-in — use your feet";
            return null;
        }
    }
    public sealed class KeeperContext
    {
        public PassActor Keeper { get; }
        public KeeperSituation Situation { get; }
        public PitchPoint Ball { get; }
        public double Height { get; }
        public double Speed { get; }
        public int Direction { get; }
        public bool CanHandle { get; }
        public bool Holding { get; }
        public string Restriction { get; }
        public IReadOnlyList<PassActor> Opponents { get; }
        public KeeperContext(PassActor keeper, KeeperSituation situation, PitchPoint ball, double height, double speed,
            int direction, HandlingRules rules, bool holding, IEnumerable<PassActor> opponents)
        {
            if (keeper == null || keeper.RoleId != "gk" || !Enum.IsDefined(typeof(KeeperSituation), situation) || !PassActor.Finite(ball) ||
                double.IsNaN(height) || double.IsInfinity(height) || height < 0 || double.IsNaN(speed) || double.IsInfinity(speed) || speed < 0 || Math.Abs(direction) != 1 || rules == null || opponents == null)
                throw new ArgumentException("Invalid goalkeeper situation.");
            var actors = opponents.ToArray();
            if (actors.Any(p => p == null || p.ClubId == keeper.ClubId)) throw new ArgumentException("Expected opponents.");
            Keeper = keeper; Situation = situation; Ball = ball; Height = height; Speed = speed; Direction = direction; Holding = holding;
            Restriction = rules.Restriction(keeper, ball, direction); CanHandle = Restriction == null;
            Opponents = Array.AsReadOnly(actors);
        }
    }
    public sealed class KeeperPreview
    {
        public KeeperContext Context { get; }
        public KeeperAction Kind { get; }
        public PassActor Receiver { get; }
        public PitchPoint Target { get; }
        public int Percent { get; }
        public float Chance => Percent / 100f;
        public KeeperOutcome Success { get; }
        public KeeperOutcome Failure { get; }
        public string ActionId => GoalkeeperResolver.IsDistribution(Kind) ? "distribute" : Kind == KeeperAction.HoldSave || Kind == KeeperAction.Parry ? "save"
            : Kind == KeeperAction.Claim || Kind == KeeperAction.Punch ? "claim" : Kind == KeeperAction.RushOut ? "tackle" : Kind == KeeperAction.SetPosition ? "contain" : "control";
        public string OptionId => Kind + ":" + (Receiver?.Id ?? "ball");
        public IReadOnlyDictionary<string, float> SkillAttribution { get; }
        internal KeeperPreview(KeeperContext context, KeeperAction kind, PassActor receiver, PitchPoint target, double chance, KeeperOutcome success, string skill)
        {
            Context = context; Kind = kind; Receiver = receiver; Target = target;
            Percent = (int)Math.Round(Math.Max(.05, Math.Min(.98, chance)) * 100, MidpointRounding.AwayFromZero);
            Success = success; Failure = context.Situation == KeeperSituation.Distribution ? KeeperOutcome.Fumbled : KeeperOutcome.Missed;
            SkillAttribution = new ReadOnlyDictionary<string, float>(new Dictionary<string, float> { [skill] = .8f, ["decisions"] = .2f });
        }
    }
    public sealed class KeeperResolved : GameEvent
    {
        public KeeperPreview Preview { get; }
        public ActionResolved Action { get; }
        public KeeperOutcome Outcome => Action.Succeeded ? Preview.Success : Preview.Failure;
        public IReadOnlyDictionary<string, float> SkillAttribution => Preview.SkillAttribution;
        internal KeeperResolved(KeeperPreview preview, ActionResolved action) : base(action.EventId, action.PlayerId, action.Sequence) { Preview = preview; Action = action; }
    }
    public sealed class KeeperDecision
    {
        public string Id { get; }
        public long Tick { get; }
        public KeeperContext Context { get; }
        public IReadOnlyList<KeeperPreview> Options { get; }
        internal KeeperDecision(string id, long tick, KeeperContext context, IEnumerable<KeeperPreview> options)
        { Id = id; Tick = tick; Context = context; Options = Array.AsReadOnly(options.ToArray()); }
    }
    public static class GoalkeeperResolver
    {
        public static bool IsDistribution(KeeperAction kind) => kind == KeeperAction.Roll || kind == KeeperAction.Throw || kind == KeeperAction.ShortKick || kind == KeeperAction.LongKick;
        public static bool IsAvailable(KeeperContext c, KeeperAction kind, PassActor receiver = null)
        {
            if (c == null || !Enum.IsDefined(typeof(KeeperAction), kind)) return false;
            double distance = c.Keeper.Position.Distance(c.Ball);
            bool reachable = distance <= (c.Situation == KeeperSituation.Shot ? 8 : 7) && c.Height <= c.Keeper.HeightCm / 100 + 1.1;
            if (IsDistribution(kind))
                return c.Situation == KeeperSituation.Distribution && receiver != null && receiver.Id != c.Keeper.Id && receiver.ClubId == c.Keeper.ClubId &&
                    receiver.Position.Distance(c.Keeper.Position) >= .1 &&
                    ((kind != KeeperAction.Roll && kind != KeeperAction.Throw) || c.Holding && c.CanHandle) &&
                    (kind != KeeperAction.Roll || receiver.Position.Distance(c.Keeper.Position) <= 25) &&
                    (kind != KeeperAction.Throw || receiver.Position.Distance(c.Keeper.Position) <= 40);
            switch (kind)
            {
                case KeeperAction.ReleaseToFeet: return c.Situation == KeeperSituation.Distribution && c.Holding;
                case KeeperAction.HandleIllegally: return !c.CanHandle && !c.Holding && c.Situation == KeeperSituation.Distribution && distance <= 1.5;
                case KeeperAction.Gather: return c.Situation == KeeperSituation.Distribution && !c.Holding && c.CanHandle && distance <= 1.5;
                case KeeperAction.HoldSave:
                case KeeperAction.Parry: return c.Situation == KeeperSituation.Shot && c.CanHandle && reachable;
                case KeeperAction.Claim:
                case KeeperAction.Punch: return c.Situation == KeeperSituation.Cross && c.CanHandle && reachable && c.Height >= .8;
                case KeeperAction.SetPosition: return c.Situation == KeeperSituation.OneOnOne || c.Situation == KeeperSituation.LooseBall;
                case KeeperAction.RushOut: return (c.Situation == KeeperSituation.OneOnOne || c.Situation == KeeperSituation.LooseBall) && c.CanHandle && distance <= 12 && c.Height < 1.2;
                default: return false;
            }
        }
        public static KeeperPreview Preview(KeeperContext c, KeeperAction kind, PassActor receiver = null)
        {
            if (!IsAvailable(c, kind, receiver)) throw new ArgumentException("Goalkeeper action is unreachable or unlawful.");
            string skill; double basis; KeeperOutcome success; PitchPoint target = c.Ball;
            switch (kind)
            {
                case KeeperAction.SetPosition: skill = "positioning"; basis = .90; success = KeeperOutcome.Positioned;
                    target = new PitchPoint(-49 * c.Direction, Math.Max(-3, Math.Min(3, c.Ball.Z * .25))); break;
                case KeeperAction.RushOut: skill = "one-on-ones"; basis = .70; success = KeeperOutcome.Held; break;
                case KeeperAction.HoldSave: skill = "handling"; basis = .66; success = KeeperOutcome.Held; break;
                case KeeperAction.Parry: skill = "diving"; basis = .83; success = KeeperOutcome.Deflected; break;
                case KeeperAction.Claim: skill = "aerial-command"; basis = .72; success = KeeperOutcome.Held; break;
                case KeeperAction.Punch: skill = "aerial-command"; basis = .85; success = KeeperOutcome.Deflected; break;
                case KeeperAction.ReleaseToFeet: skill = "ball-control"; basis = .98; success = KeeperOutcome.FeetControl; break;
                case KeeperAction.HandleIllegally: skill = "handling"; basis = .98; success = KeeperOutcome.Held; break;
                case KeeperAction.Gather: skill = "handling"; basis = .96; success = KeeperOutcome.Held; break;
                default: skill = "distribution"; basis = kind == KeeperAction.Roll ? .96 : kind == KeeperAction.Throw ? .91 : kind == KeeperAction.ShortKick ? .89 : .78;
                    success = KeeperOutcome.TeammateControls; target = receiver.Position; break;
            }
            var k = c.Keeper;
            double pressure = c.Opponents.Count == 0 ? 0 : Math.Max(0, 1 - c.Opponents.Min(p => p.Position.Distance(c.Ball)) / 8);
            double chance = basis + (k.Attributes[skill] - 50) * .003 + (k.Confidence - .5) * .10 - k.Fatigue * .16 - pressure * .12;
            if (IsDistribution(kind)) chance -= k.Position.Distance(target) * (kind == KeeperAction.LongKick ? .002 : .004);
            else
            {
                chance -= k.Position.Distance(c.Ball) * .022 + c.Speed * .004 + c.Height * .02;
                chance += (k.Attributes["reflexes"] - 50) * .002 + (k.Attributes["positioning"] - 50) * .001;
                chance += (k.HeightCm - 180) * .0015 - Math.Max(0, k.WeightKg - 85) * .001;
                if (c.Situation == KeeperSituation.Cross) chance += (k.Attributes["jumping"] - 50) * .001;
            }
            return new KeeperPreview(c, kind, receiver, target.Bounded(), chance, success, skill);
        }
        public static KeeperResolved Commit(MatchSession match, string id, KeeperPreview p, bool automatic)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var request = new ActionRequest(id, p.Context.Keeper.Id, p.ActionId, p.Receiver?.Id ?? PassResolver.PointText(p.Target),
                p.Chance, p.Success.ToString(), p.Failure.ToString(), new Dictionary<string, string> {
                    ["formula"] = "keeper-v1", ["variant"] = p.Kind.ToString(), ["situation"] = p.Context.Situation.ToString(), ["displayed-percent"] = p.Percent.ToString(),
                    ["height"] = p.Context.Height.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["speed"] = p.Context.Speed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["handling-allowed"] = p.Context.CanHandle.ToString(), ["target"] = PassResolver.PointText(p.Target) });
            return new KeeperResolved(p, (automatic ? match.ResolveAutomatic(request) : match.BeginResolution(request)).Result);
        }
    }
}
