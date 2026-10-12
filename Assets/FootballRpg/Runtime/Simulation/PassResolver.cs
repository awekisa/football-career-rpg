using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace FootballRpg.Simulation
{
    public enum PassKind { ToFeet, ThroughBall }
    public enum PassOutcome { Received, Intercepted, LooseBall, OutOfPlay }

    /// <summary>Immutable position, skill and condition snapshot for preview/commit.</summary>
    public sealed class PassActor
    {
        public string Id { get; }
        public string ClubId { get; }
        public string RoleId { get; }
        public string PreferredFoot { get; }
        public float HeightCm { get; }
        public float WeightKg { get; }
        public PitchPoint Position { get; }
        public PitchPoint Facing { get; }
        public IReadOnlyDictionary<string, int> Attributes { get; }
        public float Confidence { get; }
        public float Morale { get; }
        public float Fatigue { get; }
        public PassActor(PlayerState player, PitchPoint position, PitchPoint facing, string roleOverride = null)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!Finite(position) || !Finite(facing) || facing.Distance(new PitchPoint(0, 0)) < .001)
                throw new ArgumentException("A finite position and nonzero facing are required.");
            Id = player.Id; ClubId = player.ClubId; RoleId = roleOverride ?? player.PrimaryPositionId; PreferredFoot = player.PreferredFoot;
            HeightCm = player.HeightCm; WeightKg = player.WeightKg;
            Position = position; Facing = facing;
            Attributes = new ReadOnlyDictionary<string, int>(player.FootballAttributes.ToDictionary(p => p.Key, p => p.Value));
            Confidence = player.Confidence; Morale = player.Morale; Fatigue = player.Fatigue;
        }
        internal static bool Finite(PitchPoint point) => !double.IsNaN(point.X) && !double.IsInfinity(point.X) &&
            !double.IsNaN(point.Z) && !double.IsInfinity(point.Z);
    }

    public sealed class PassPreview
    {
        public PassActor Passer { get; }
        public PassActor Receiver { get; }
        public PassKind Kind { get; }
        public PitchPoint Target { get; }
        public int Percent { get; }
        public float Chance => Percent / 100f;
        public string ActionId => Kind == PassKind.ToFeet ? "pass-to-feet" : "through-ball";
        public IReadOnlyDictionary<string, double> Modifiers { get; }
        public IReadOnlyDictionary<string, string> Context { get; }
        public IReadOnlyDictionary<string, float> SkillAttribution { get; }
        public PassOutcome Failure { get; }
        public string InterceptorId { get; }
        public PitchPoint FailureTarget { get; }
        internal PassPreview(PassActor passer, PassActor receiver, PassKind kind, PitchPoint target,
            Dictionary<string, double> modifiers, Dictionary<string, string> context, PassOutcome failure,
            string interceptor, PitchPoint failureTarget)
        {
            Passer = passer; Receiver = receiver; Kind = kind; Target = target;
            Modifiers = new ReadOnlyDictionary<string, double>(modifiers);
            Percent = (int)Math.Round(Math.Max(.05, Math.Min(.98, modifiers.Values.Sum())) * 100, MidpointRounding.AwayFromZero);
            Failure = failure; InterceptorId = interceptor; FailureTarget = failureTarget;
            context["displayed-percent"] = Percent.ToString(CultureInfo.InvariantCulture);
            context["failure"] = failure.ToString();
            context["failure-target"] = PassResolver.PointText(failureTarget);
            context["interceptor"] = interceptor ?? "none";
            Context = new ReadOnlyDictionary<string, string>(context);
            SkillAttribution = new ReadOnlyDictionary<string, float>(kind == PassKind.ToFeet
                ? new Dictionary<string, float> { ["short-passing"] = .7f, ["decisions"] = .3f }
                : new Dictionary<string, float> { ["long-passing"] = .5f, ["vision"] = .35f, ["decisions"] = .15f });
        }
    }

    public sealed class PassResolved : GameEvent
    {
        public PassPreview Preview { get; }
        public ActionResolved Action { get; }
        public PassOutcome Outcome { get; }
        public PitchPoint Destination { get; }
        public string ReceivingPlayerId { get; }
        public IReadOnlyDictionary<string, float> SkillAttribution => Preview.SkillAttribution;
        public IReadOnlyDictionary<string, string> Context => Preview.Context;
        internal PassResolved(PassPreview preview, ActionResolved action)
            : base(action.EventId, action.PlayerId, action.Sequence)
        {
            Preview = preview; Action = action;
            Outcome = action.Succeeded ? PassOutcome.Received : preview.Failure;
            Destination = action.Succeeded ? preview.Target : preview.FailureTarget;
            ReceivingPlayerId = action.Succeeded ? preview.Receiver.Id : preview.InterceptorId;
        }
    }

    public static class PassResolver
    {
        /// <summary>Success means the named receiver controls the ball at the frozen target.</summary>
        public static PassPreview Preview(PassActor passer, PassActor receiver, PassKind kind,
            PitchPoint target, string usedFoot, IEnumerable<PassActor> opponents)
        {
            if (passer == null || receiver == null || opponents == null) throw new ArgumentNullException();
            if (!Enum.IsDefined(typeof(PassKind), kind) || !PassActor.Finite(target) || Math.Abs(target.X) > 51 || Math.Abs(target.Z) > 32)
                throw new ArgumentException("Pass target must be inside the sandbox pitch.");
            if (passer.Id == receiver.Id || passer.ClubId != receiver.ClubId) throw new ArgumentException("Select a different teammate.");
            if (usedFoot != "left" && usedFoot != "right") throw new ArgumentException("Choose left or right foot.");
            if (kind == PassKind.ToFeet && target.Distance(receiver.Position) > .01)
                throw new ArgumentException("A pass to feet targets the receiver's current position.");
            var defenders = opponents.ToArray();
            if (defenders.Any(p => p == null || p.ClubId == passer.ClubId) || defenders.Select(p => p.Id).Distinct().Count() != defenders.Length)
                throw new ArgumentException("Opponent snapshots must be unique opposing players.");
            double distance = passer.Position.Distance(target);
            if (distance < .1) throw new ArgumentException("Pass target is too close to the ball.");
            double dx = target.X - passer.Position.X, dz = target.Z - passer.Position.Z;
            double facingLength = passer.Facing.Distance(new PitchPoint(0, 0));
            double angle = Math.Acos(Math.Max(-1, Math.Min(1, (dx * passer.Facing.X + dz * passer.Facing.Z) / (distance * facingLength)))) / Math.PI;
            double pressure = defenders.Length == 0 ? 0 : Math.Max(0, 1 - defenders.Min(p => p.Position.Distance(passer.Position)) / 8);
            double coverage = defenders.Length == 0 ? 0 : Math.Max(0, 1 - defenders.Min(p => p.Position.Distance(target)) / 8);
            var lane = defenders.OrderBy(p => LaneDistance(p.Position, passer.Position, target)).ThenBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
            double laneDistance = lane == null ? 99 : LaneDistance(lane.Position, passer.Position, target);
            string passingSkill = kind == PassKind.ToFeet ? "short-passing" : "long-passing";
            double skill = passer.Attributes[passingSkill] * .65 + passer.Attributes["vision"] * .2 + passer.Attributes["decisions"] * .15;
            string receptionSkill = kind == PassKind.ToFeet ? "ball-control" : "off-ball-movement";
            var modifiers = new Dictionary<string, double>
            {
                ["base"] = kind == PassKind.ToFeet ? .92 : .82,
                ["passer-skills"] = (skill - 50) * .003,
                ["receiver-skill"] = (receiver.Attributes[receptionSkill] - 50) * .0015,
                ["distance"] = -distance * .0035,
                ["angle"] = -angle * .12,
                ["pressure"] = -pressure * .16,
                ["receiver-coverage"] = -coverage * .10,
                ["lane"] = -Math.Max(0, 1 - laneDistance / 4) * .12,
                ["opponent-skill"] = lane == null ? 0 : (50 - (lane.Attributes["anticipation"] + lane.Attributes["positioning"]) * .5) * .0015 * Math.Max(0, 1 - laneDistance / 8),
                ["foot"] = passer.PreferredFoot == "both" || passer.PreferredFoot == usedFoot ? 0 : -.10,
                ["confidence"] = (passer.Confidence - .5) * .08,
                ["morale"] = (passer.Morale - .5) * .04,
                ["fatigue"] = -passer.Fatigue * .15 - receiver.Fatigue * .06,
                ["receiver-run"] = kind == PassKind.ThroughBall ? -receiver.Position.Distance(target) * .004 : 0
            };
            var context = new Dictionary<string, string>
            {
                ["formula"] = "pass-v1", ["origin"] = PointText(passer.Position), ["target"] = PointText(target),
                ["receiver-origin"] = PointText(receiver.Position), ["used-foot"] = usedFoot,
                ["success-definition"] = "receiver-controls-at-target"
            };
            context["lane-opponent"] = lane?.Id ?? "none";
            context["lane-distance"] = laneDistance.ToString("R", CultureInfo.InvariantCulture);
            context["lane-opponent-anticipation"] = lane == null ? "none" : lane.Attributes["anticipation"].ToString(CultureInfo.InvariantCulture);
            context["lane-opponent-positioning"] = lane == null ? "none" : lane.Attributes["positioning"].ToString(CultureInfo.InvariantCulture);
            context["passer-confidence"] = passer.Confidence.ToString("R", CultureInfo.InvariantCulture);
            context["passer-morale"] = passer.Morale.ToString("R", CultureInfo.InvariantCulture);
            context["passer-fatigue"] = passer.Fatigue.ToString("R", CultureInfo.InvariantCulture);
            context["receiver-fatigue"] = receiver.Fatigue.ToString("R", CultureInfo.InvariantCulture);
            foreach (string skillId in new[] { passingSkill, "vision", "decisions" })
                context["passer-" + skillId] = passer.Attributes[skillId].ToString(CultureInfo.InvariantCulture);
            context["receiver-" + receptionSkill] = receiver.Attributes[receptionSkill].ToString(CultureInfo.InvariantCulture);
            foreach (var modifier in modifiers) context["modifier-" + modifier.Key] = modifier.Value.ToString("R", CultureInfo.InvariantCulture);
            PassOutcome failure; PitchPoint failureTarget; string interceptor = null;
            if (lane != null && laneDistance < 3)
            { failure = PassOutcome.Intercepted; failureTarget = lane.Position; interceptor = lane.Id; }
            else if (Math.Abs(target.Z) > 27 || Math.Abs(target.X) > 47)
            {
                failure = PassOutcome.OutOfPlay;
                failureTarget = Math.Abs(target.Z) > 27 ? new PitchPoint(target.X, Math.Sign(target.Z) * 34.5)
                    : new PitchPoint(Math.Sign(target.X) * 53, target.Z);
            }
            else
            { failure = PassOutcome.LooseBall; failureTarget = passer.Position.MoveTowards(target, distance * .65).Bounded(); }
            return new PassPreview(passer, receiver, kind, target, modifiers, context, failure, interceptor, failureTarget);
        }

        public static PassResolved Commit(MatchSession session, string requestId, PassPreview preview, bool automatic)
        {
            if (session == null || preview == null) throw new ArgumentNullException();
            var request = new ActionRequest(requestId, preview.Passer.Id, preview.ActionId, preview.Receiver.Id,
                preview.Chance, PassOutcome.Received.ToString(), preview.Failure.ToString(), preview.Context.ToDictionary(p => p.Key, p => p.Value));
            var diagnostic = automatic ? session.ResolveAutomatic(request) : session.BeginResolution(request);
            return new PassResolved(preview, diagnostic.Result);
        }

        internal static string PointText(PitchPoint p) => p.X.ToString("R", CultureInfo.InvariantCulture) + "," + p.Z.ToString("R", CultureInfo.InvariantCulture);
        private static double LaneDistance(PitchPoint point, PitchPoint start, PitchPoint end)
        {
            double dx = end.X - start.X, dz = end.Z - start.Z;
            double t = Math.Max(0, Math.Min(1, ((point.X - start.X) * dx + (point.Z - start.Z) * dz) / (dx * dx + dz * dz)));
            return point.Distance(new PitchPoint(start.X + dx * t, start.Z + dz * t));
        }
    }
}
