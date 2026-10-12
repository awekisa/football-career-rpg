using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FootballRpg.Simulation
{
    public enum DefensiveAction { Tackle, Contain, Block }
    public sealed class DefensePreview
    {
        public PassActor Defender { get; }
        public PassActor Attacker { get; }
        public DefensiveAction Kind { get; }
        public PitchPoint Target { get; }
        public int Percent { get; }
        public float Chance => Percent / 100f;
        public IReadOnlyDictionary<string, float> SkillAttribution { get; }
        internal DefensePreview(PassActor defender, PassActor attacker, DefensiveAction kind, PitchPoint target, double chance, string skill)
        {
            Defender = defender; Attacker = attacker; Kind = kind; Target = target;
            Percent = (int)Math.Round(Math.Max(.05, Math.Min(.95, chance)) * 100, MidpointRounding.AwayFromZero);
            SkillAttribution = new ReadOnlyDictionary<string, float>(new Dictionary<string, float> { [skill] = .8f, ["decisions"] = .2f });
        }
    }
    public sealed class DefenseResolved : GameEvent
    {
        public DefensePreview Preview { get; }
        public ActionResolved Action { get; }
        public string Outcome => Action.OutcomeId;
        public IReadOnlyDictionary<string, float> SkillAttribution => Preview.SkillAttribution;
        internal DefenseResolved(DefensePreview preview, ActionResolved action) : base(action.EventId, action.PlayerId, action.Sequence)
        { Preview = preview; Action = action; }
    }
    public sealed class DefenseDecision
    {
        public string Id { get; }
        public long Tick { get; }
        public IReadOnlyList<DefensePreview> Options { get; }
        internal DefenseDecision(string id, long tick, IEnumerable<DefensePreview> options)
        { Id = id; Tick = tick; Options = Array.AsReadOnly(options.ToArray()); }
    }
    public static class DefenseResolver
    {
        public static bool IsAvailable(PassActor defender, PassActor attacker, int direction, DefensiveAction kind)
        {
            if (defender == null || attacker == null || defender.ClubId == attacker.ClubId || Math.Abs(direction) != 1 || !Enum.IsDefined(typeof(DefensiveAction), kind)) return false;
            double distance = defender.Position.Distance(attacker.Position);
            if (kind == DefensiveAction.Tackle) return distance <= 2;
            if (kind == DefensiveAction.Contain) return distance <= 6;
            var goal = new PitchPoint(-52.5 * direction, 0);
            var lane = attacker.Position.MoveTowards(goal, Math.Min(6, attacker.Position.Distance(goal)));
            return distance <= 6 && attacker.Position.Distance(goal) <= 30 &&
                (defender.Position.X - attacker.Position.X) * direction < 0 &&
                LaneDistance(defender.Position, attacker.Position, lane) <= 2.5;
        }
        private static double LaneDistance(PitchPoint point, PitchPoint start, PitchPoint end)
        {
            double x = end.X - start.X, z = end.Z - start.Z;
            double length = x * x + z * z;
            double t = length == 0 ? 0 : Math.Max(0, Math.Min(1, ((point.X - start.X) * x + (point.Z - start.Z) * z) / length));
            return point.Distance(new PitchPoint(start.X + t * x, start.Z + t * z));
        }
        public static DefensePreview Preview(PassActor defender, PassActor attacker, int direction, DefensiveAction kind)
        {
            if (!IsAvailable(defender, attacker, direction, kind)) throw new ArgumentException("Threat is outside this action's reach or shooting lane.");
            string skill = kind == DefensiveAction.Tackle ? "tackling" : "positioning";
            double basis = kind == DefensiveAction.Tackle ? .52 : kind == DefensiveAction.Contain ? .82 : .67;
            double chance = basis + (defender.Attributes[skill] - attacker.Attributes["dribbling"]) * .004
                + (defender.Attributes["anticipation"] - 50) * .001 + (defender.Confidence - .5) * .1
                - defender.Fatigue * .15 + attacker.Fatigue * .05 - defender.Position.Distance(attacker.Position) * .015;
            var target = kind == DefensiveAction.Tackle ? attacker.Position
                : attacker.Position.MoveTowards(new PitchPoint(-52.5 * direction, 0), kind == DefensiveAction.Contain ? 2 : 1).Bounded();
            return new DefensePreview(defender, attacker, kind, target, chance, skill);
        }
        public static DefenseResolved Commit(MatchSession session, string id, DefensePreview preview, bool automatic)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            string success = preview.Kind == DefensiveAction.Tackle ? "ball-won" : preview.Kind == DefensiveAction.Contain ? "attack-delayed" : "lane-blocked";
            var request = new ActionRequest(id, preview.Defender.Id, preview.Kind.ToString().ToLowerInvariant(), preview.Attacker.Id,
                preview.Chance, success, "beaten", new Dictionary<string, string> {
                    ["formula"] = "defense-v1", ["displayed-percent"] = preview.Percent.ToString(),
                    ["defender-position"] = PassResolver.PointText(preview.Defender.Position),
                    ["attacker-position"] = PassResolver.PointText(preview.Attacker.Position), ["target"] = PassResolver.PointText(preview.Target) });
            return new DefenseResolved(preview, (automatic ? session.ResolveAutomatic(request) : session.BeginResolution(request)).Result);
        }
    }
}
