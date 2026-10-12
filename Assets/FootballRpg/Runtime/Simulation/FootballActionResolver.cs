using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace FootballRpg.Simulation
{
    public enum FootballActionKind { Control, FirstTimePass, Dribble, Cross, Shot, HeaderPass, HeaderShot, Clearance }
    public enum BallSituation { Controlled, Arriving }
    public enum FootballOutcome { Controlled, TeammateControls, DeliveryArrives, DefenderBeaten, Advanced, Goal, Cleared, Dispossessed, LooseBall, Wide }

    public sealed class FootballActionContext
    {
        public PassActor Actor { get; }
        public BallSituation Situation { get; }
        public double Height { get; }
        public double Speed { get; }
        public int Direction { get; }
        public IReadOnlyList<PassActor> Opponents { get; }
        public FootballActionContext(PassActor actor, BallSituation situation, double height, double speed, int direction, IEnumerable<PassActor> opponents)
        {
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
            if (!Enum.IsDefined(typeof(BallSituation), situation) || double.IsNaN(height) || double.IsInfinity(height) || height < 0 ||
                double.IsNaN(speed) || double.IsInfinity(speed) || speed < 0 || Math.Abs(direction) != 1 || opponents == null)
                throw new ArgumentException("Invalid ball context.");
            var array = opponents.ToArray();
            if (array.Any(p => p == null || p.ClubId == actor.ClubId) || array.Select(p => p.Id).Distinct().Count() != array.Length)
                throw new ArgumentException("Use unique opposing players.");
            Situation = situation; Height = height; Speed = speed; Direction = direction;
            Opponents = Array.AsReadOnly(array);
        }
    }

    public sealed class FootballActionPreview
    {
        public FootballActionContext Situation { get; }
        public FootballActionKind Kind { get; }
        public string Variant { get; }
        public PassActor Receiver { get; }
        public PitchPoint Target { get; }
        public string OptionId => Kind + ":" + Variant + ":" + (Receiver?.Id ?? "space");
        public int Percent { get; }
        public float Chance => Percent / 100f;
        public FootballOutcome Success { get; }
        public FootballOutcome Failure { get; }
        public string OpponentId { get; }
        public PitchPoint FailureTarget { get; }
        public double DeliveryHeight { get; }
        public IReadOnlyDictionary<string, float> SkillAttribution { get; }
        public IReadOnlyDictionary<string, string> Context { get; }
        public string ActionId => Kind == FootballActionKind.HeaderPass || Kind == FootballActionKind.HeaderShot ? "header"
            : Kind == FootballActionKind.FirstTimePass ? "pass-to-feet" : Kind == FootballActionKind.Clearance ? "clear" : Kind == FootballActionKind.Shot ? "shoot" : Kind.ToString().ToLowerInvariant();
        internal FootballActionPreview(FootballActionContext situation, FootballActionKind kind, string variant, PassActor receiver,
            PitchPoint target, double chance, FootballOutcome success, FootballOutcome failure, PassActor opponent,
            PitchPoint failureTarget, double deliveryHeight, Dictionary<string, float> skills, Dictionary<string, string> context)
        {
            Situation = situation; Kind = kind; Variant = variant; Receiver = receiver; Target = target;
            Percent = (int)Math.Round(Math.Max(.05, Math.Min(.98, chance)) * 100, MidpointRounding.AwayFromZero);
            Success = success; Failure = failure; OpponentId = opponent?.Id; FailureTarget = failureTarget; DeliveryHeight = deliveryHeight;
            SkillAttribution = new ReadOnlyDictionary<string, float>(skills);
            context["displayed-percent"] = Percent.ToString(CultureInfo.InvariantCulture);
            context["success"] = success.ToString(); context["failure"] = failure.ToString();
            context["target"] = PassResolver.PointText(target); context["failure-target"] = PassResolver.PointText(failureTarget);
            context["opponent"] = OpponentId ?? "none";
            Context = new ReadOnlyDictionary<string, string>(context);
        }
    }

    public sealed class FootballActionResolved : GameEvent
    {
        public FootballActionPreview Preview { get; }
        public ActionResolved Action { get; }
        public FootballOutcome Outcome => Action.Succeeded ? Preview.Success : Preview.Failure;
        public PitchPoint Destination => Action.Succeeded ? Preview.Target : Preview.FailureTarget;
        public string ReceivingPlayerId => Action.Succeeded
            ? (Outcome == FootballOutcome.Controlled || Outcome == FootballOutcome.DefenderBeaten || Outcome == FootballOutcome.Advanced ? PlayerId : Preview.Receiver?.Id)
            : (Outcome == FootballOutcome.Dispossessed ? Preview.OpponentId : null);
        public IReadOnlyDictionary<string, float> SkillAttribution => Preview.SkillAttribution;
        public IReadOnlyDictionary<string, string> Context => Preview.Context;
        internal FootballActionResolved(FootballActionPreview preview, ActionResolved action) : base(action.EventId, action.PlayerId, action.Sequence)
        { Preview = preview; Action = action; }
    }

    public static class FootballActionResolver
    {
        public static string[] Variants(FootballActionKind kind)
        {
            switch (kind)
            {
                case FootballActionKind.Control: return new[] { "cushion", "turn" };
                case FootballActionKind.FirstTimePass: return new[] { "feet" };
                case FootballActionKind.Dribble: return new[] { "carry", "take-on" };
                case FootballActionKind.Cross: return new[] { "low", "lofted" };
                case FootballActionKind.Shot: return new[] { "placed", "power" };
                case FootballActionKind.HeaderPass: return new[] { "direct", "flick" };
                case FootballActionKind.HeaderShot: return new[] { "downward", "power" };
                case FootballActionKind.Clearance: return new[] { "long", "wide" };
                default: throw new ArgumentException("Unknown action.");
            }
        }

        public static bool IsAvailable(FootballActionContext context, FootballActionKind kind, string variant, PassActor receiver)
        {
            if (context == null || !Enum.IsDefined(typeof(FootballActionKind), kind) || !Variants(kind).Contains(variant)) return false;
            bool controlled = context.Situation == BallSituation.Controlled;
            double reach = context.Actor.HeightCm / 100 + .9;
            bool aerial = !controlled && context.Height >= 1.2 && context.Height <= reach;
            bool teammate = receiver != null && receiver.Id != context.Actor.Id && receiver.ClubId == context.Actor.ClubId && receiver.Position.Distance(context.Actor.Position) >= .1;
            switch (kind)
            {
                case FootballActionKind.Control: return !controlled && context.Height <= context.Actor.HeightCm / 100 + .25;
                case FootballActionKind.FirstTimePass: return !controlled && context.Height < 1.2 && teammate;
                case FootballActionKind.Dribble: return controlled && context.Actor.Position.X * context.Direction < 49 &&
                    (variant == "carry" || context.Opponents.Any(p => p.Position.Distance(context.Actor.Position) <= 8));
                case FootballActionKind.Cross: return controlled && Math.Abs(context.Actor.Position.Z) >= 16 && teammate;
                case FootballActionKind.Shot: return controlled;
                case FootballActionKind.HeaderPass: return aerial && teammate;
                case FootballActionKind.HeaderShot: return aerial;
                case FootballActionKind.Clearance: return controlled || context.Height <= reach;
                default: return false;
            }
        }

        public static FootballActionPreview Preview(FootballActionContext context, FootballActionKind kind, string variant, PassActor receiver = null)
        {
            if (!IsAvailable(context, kind, variant, receiver)) throw new ArgumentException("Action is not feasible in this ball situation.");
            var actor = context.Actor;
            var opponent = context.Opponents.OrderBy(p => p.Position.Distance(actor.Position)).ThenBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
            double pressure = opponent == null ? 0 : Math.Max(0, 1 - opponent.Position.Distance(actor.Position) / 8);
            double chance; string skill; FootballOutcome success; double height = 0;
            PitchPoint target;
            switch (kind)
            {
                case FootballActionKind.Control:
                    target = variant == "turn" ? new PitchPoint(actor.Position.X + 2 * context.Direction, actor.Position.Z).Bounded() : actor.Position;
                    skill = "ball-control"; chance = .88 - (variant == "turn" ? .12 : 0); success = FootballOutcome.Controlled; break;
                case FootballActionKind.FirstTimePass:
                    target = receiver.Position; skill = "short-passing"; chance = .85; success = FootballOutcome.TeammateControls; break;
                case FootballActionKind.Dribble:
                    target = new PitchPoint(actor.Position.X + (variant == "carry" ? 5 : 8) * context.Direction,
                        actor.Position.Z + (variant == "take-on" ? (actor.Position.Z >= 0 ? -3 : 3) : 0)).Bounded();
                    skill = "dribbling"; chance = variant == "carry" ? .87 : .73; success = variant == "take-on" ? FootballOutcome.DefenderBeaten : FootballOutcome.Advanced; break;
                case FootballActionKind.Cross:
                    target = receiver.Position; skill = "crossing"; chance = variant == "low" ? .84 : .77;
                    height = variant == "low" ? .35 : 1.8; success = FootballOutcome.DeliveryArrives; break;
                case FootballActionKind.Shot:
                    target = new PitchPoint(52.5 * context.Direction, variant == "placed" ? 2 : 0);
                    skill = actor.Position.Distance(target) > 24 ? "long-shots" : "finishing";
                    chance = variant == "placed" ? .76 : .70; success = FootballOutcome.Goal; height = variant == "placed" ? .5 : 1; break;
                case FootballActionKind.HeaderPass:
                    target = receiver.Position; skill = "heading"; chance = variant == "direct" ? .77 : .65;
                    success = FootballOutcome.TeammateControls; height = .5; break;
                case FootballActionKind.HeaderShot:
                    target = new PitchPoint(52.5 * context.Direction, variant == "downward" ? 1.5 : 0);
                    skill = "heading"; chance = variant == "downward" ? .69 : .64;
                    success = FootballOutcome.Goal; height = .4; break;
                default:
                    target = variant == "wide" ? new PitchPoint(actor.Position.X + 15 * context.Direction, actor.Position.Z >= 0 ? -32 : 32).Bounded()
                        : new PitchPoint(actor.Position.X + 28 * (actor.Position.X * context.Direction > 23 ? -context.Direction : context.Direction), actor.Position.Z * .7).Bounded();
                    skill = "long-passing"; chance = .89; success = FootballOutcome.Cleared; height = .3; break;
            }
            double distance = actor.Position.Distance(target);
            chance += (actor.Attributes[skill] - 50) * .003 - pressure * .18;
            chance += (actor.Confidence - .5) * .10 + (actor.Morale - .5) * .04 - actor.Fatigue * .15;
            chance -= context.Speed * .005 + context.Height * .025;
            if (kind == FootballActionKind.Dribble)
                chance += (actor.Attributes["agility"] - 50) * .001 + (actor.Attributes["strength"] - 50) * .001
                    - Math.Max(0, actor.WeightKg - 75) * .001 - (opponent == null ? 0 : (opponent.Attributes["tackling"] - 50) * .002);
            else chance -= distance * (success == FootballOutcome.Goal ? .014 : .003);
            if (receiver != null)
                chance += (receiver.Attributes["off-ball-movement"] - 50) * .001 - receiver.Fatigue * .05;
            if (kind == FootballActionKind.HeaderPass || kind == FootballActionKind.HeaderShot)
                chance += (actor.HeightCm - 180) * .002 + (actor.Attributes["jumping"] - 50) * .0015
                    + (actor.Attributes["strength"] - 50) * .001;
            if (success == FootballOutcome.Goal)
            {
                var keeper = context.Opponents.Where(p => p.RoleId == "gk").OrderBy(p => p.Position.Distance(target)).FirstOrDefault();
                chance -= Math.Abs(actor.Position.Z) * .004;
                if (keeper != null) chance -= (keeper.Attributes["reflexes"] + keeper.Attributes["positioning"]) * .001;
            }
            var failure = success == FootballOutcome.Goal ? FootballOutcome.Wide : opponent != null && pressure > .25 ? FootballOutcome.Dispossessed : FootballOutcome.LooseBall;
            var failureTarget = failure == FootballOutcome.Wide ? new PitchPoint(53 * context.Direction, 5)
                : failure == FootballOutcome.Dispossessed ? opponent.Position : actor.Position.MoveTowards(target, distance * .4).Bounded();
            var skills = new Dictionary<string, float> { [skill] = .8f, ["decisions"] = .2f };
            var details = new Dictionary<string, string>
            {
                ["formula"] = "football-actions-v1", ["variant"] = variant, ["situation"] = context.Situation.ToString(),
                ["height"] = context.Height.ToString("R", CultureInfo.InvariantCulture), ["speed"] = context.Speed.ToString("R", CultureInfo.InvariantCulture),
                ["height-cm"] = actor.HeightCm.ToString("R", CultureInfo.InvariantCulture), ["weight-kg"] = actor.WeightKg.ToString("R", CultureInfo.InvariantCulture),
                ["skill"] = skill, ["skill-value"] = actor.Attributes[skill].ToString(CultureInfo.InvariantCulture),
                ["pressure"] = pressure.ToString("R", CultureInfo.InvariantCulture), ["unrounded-chance"] = chance.ToString("R", CultureInfo.InvariantCulture)
            };
            return new FootballActionPreview(context, kind, variant, receiver, target, chance, success, failure, opponent, failureTarget, height, skills, details);
        }

        public static FootballActionResolved Commit(MatchSession session, string requestId, FootballActionPreview preview, bool automatic)
        {
            if (session == null || preview == null) throw new ArgumentNullException();
            var request = new ActionRequest(requestId, preview.Situation.Actor.Id, preview.ActionId, preview.Receiver?.Id ?? PassResolver.PointText(preview.Target),
                preview.Chance, preview.Success.ToString(), preview.Failure.ToString(), preview.Context.ToDictionary(p => p.Key, p => p.Value));
            var result = automatic ? session.ResolveAutomatic(request) : session.BeginResolution(request);
            return new FootballActionResolved(preview, result.Result);
        }
    }
}
