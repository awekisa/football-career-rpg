using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FootballRpg.Simulation
{
    public enum PracticeFamily { Passing, Defending, Attacking, Goalkeeping }
    public sealed class PracticeOption
    {
        public string Id { get; }
        public string Guidance { get; }
        public int Percent { get; }
        public string PrimarySkill { get; }
        public PitchPoint Target { get; }
        private readonly Func<MatchSession, string, ActionResolved> resolve;
        internal PracticeOption(string id, string guidance, int percent, IReadOnlyDictionary<string, float> skills,
            PitchPoint target, Func<MatchSession, string, ActionResolved> resolve)
        {
            Id = id; Guidance = guidance; Percent = percent; Target = target; this.resolve = resolve;
            PrimarySkill = skills.OrderByDescending(s => s.Value).First().Key;
        }
        internal ActionResolved Commit(MatchSession match, string id) => resolve(match, id);
    }
    public sealed class PracticeDecision
    {
        public string Id { get; }
        public IReadOnlyList<PracticeOption> Options { get; }
        public IReadOnlyList<PassActor> Actors { get; }
        public PitchPoint Ball { get; }
        internal PracticeDecision(string id, List<PracticeOption> options, PassActor[] actors, PitchPoint ball)
        { Id = id; Options = options.AsReadOnly(); Actors = Array.AsReadOnly(actors); Ball = ball; }
    }
    /// <summary>Three paused match decisions. Calendar owns the slot; CareerTraining owns the one-time XP award.</summary>
    public sealed class GuidedPracticeSession
    {
        private readonly CareerState career;
        private readonly MatchSession match;
        private readonly List<ActionResolved> results = new List<ActionResolved>();
        private readonly Dictionary<string, (string option, ActionResolved result)> committed = new Dictionary<string, (string, ActionResolved)>();
        private readonly Dictionary<string, double> shares = new Dictionary<string, double>();
        public string Id { get; }
        public PracticeFamily Family { get; }
        public int AttemptLimit => 3;
        public bool Finished { get; private set; }
        public PracticeDecision Decision { get; private set; }
        public ActionResolved LastResult { get; private set; }
        public long DrawCount => match.DrawCount;
        public IReadOnlyList<ActionResolved> Results { get; }
        internal GuidedPracticeSession(CareerState career, string id, PracticeFamily family, ulong seed)
        {
            if (career == null || !Enum.IsDefined(typeof(PracticeFamily), family)) throw new ArgumentException("Invalid practice.");
            this.career = career; Id = id; Family = family; match = new MatchSession(id, seed);
            Results = results.AsReadOnly(); match.Pause(); BuildDecision();
        }
        public static PracticeFamily Recommended(string role) => role == "gk" ? PracticeFamily.Goalkeeping :
            new[] { "cb", "lb", "rb", "lwb", "rwb", "dm" }.Contains(role) ? PracticeFamily.Defending :
            new[] { "st", "lw", "rw" }.Contains(role) ? PracticeFamily.Attacking : PracticeFamily.Passing;
        public static string Guidance(PracticeFamily family) => family == PracticeFamily.Passing ?
            "Coach: protect possession with a close pass, or use vision and long passing to play ahead of a runner. Pressure and the passing lane affect the odds." :
            family == PracticeFamily.Defending ? "Coach: tackling can win possession. Positioning lets you contain the carrier or block the shooting lane; delaying an attack is also a successful defense." :
            family == PracticeFamily.Attacking ? "Coach: finishing and decisions help you place a shot; power trades accuracy for pace. Dribbling can improve your position. A successful shot means on target, not a guaranteed goal." :
            "Coach: handling helps you hold a shot, diving helps you parry it. Judge crosses with aerial command; choose positioning or one-on-ones when a striker breaks through.";
        public static IReadOnlyDictionary<string, double> PlannedShares(PracticeFamily family) =>
            new ReadOnlyDictionary<string, double>(family == PracticeFamily.Goalkeeping ? new Dictionary<string, double> { ["handling"] = .3, ["aerial-command"] = .3, ["positioning"] = .3, ["decisions"] = .1 } : new Dictionary<string, double> {
                [family == PracticeFamily.Passing ? "short-passing" : family == PracticeFamily.Defending ? "tackling" : family == PracticeFamily.Attacking ? "finishing" : "handling"] = .9,
                ["decisions"] = .1 });
        public ActionResolved Choose(string decisionId, string optionId)
        {
            if (decisionId != null && committed.TryGetValue(decisionId, out var prior))
            {
                if (prior.option != optionId) throw new InvalidOperationException("This decision has already been made.");
                return prior.result;
            }
            if (Finished || LastResult != null || Decision.Id != decisionId) throw new InvalidOperationException("This practice decision is no longer available.");
            var choice = Decision.Options.SingleOrDefault(o => o.Id == optionId) ?? throw new ArgumentException("Choose an available action.");
            var result = choice.Commit(match, decisionId);
            match.CompleteResolution(result.EventId); match.Pause();
            committed.Add(decisionId, (optionId, result)); results.Add(result); LastResult = result;
            // Learning from a failed drill is positive, but smaller; this is not competitive match XP.
            double amount = (result.Succeeded ? 1 : .65) / AttemptLimit;
            AddShare(choice.PrimarySkill, amount * .9); AddShare("decisions", amount * .1);
            if (results.Count == AttemptLimit) Finished = true;
            return result;
        }
        private void AddShare(string skill, double amount) { shares.TryGetValue(skill, out double current); shares[skill] = current + amount; }
        public void Next()
        {
            if (Finished || LastResult == null) throw new InvalidOperationException("Resolve the current drill before continuing.");
            LastResult = null; BuildDecision();
        }
        internal void End() { Finished = true; }
        internal IReadOnlyDictionary<string, double> RewardShares() => new ReadOnlyDictionary<string, double>(
            shares.Count == 0 ? PlannedShares(Family).ToDictionary(p => p.Key, p => 0d) : new Dictionary<string, double>(shares));
        private void BuildDecision()
        {
            var player = career.UserPlayer;
            var teammate = career.Players.Values.OrderBy(p => p.Id).First(p => p.ClubId == player.ClubId && p.Id != player.Id && p.PrimaryPositionId != "gk");
            var opponent = career.Players.Values.OrderBy(p => p.Id).First(p => p.ClubId != player.ClubId && p.PrimaryPositionId != "gk");
            double offset = results.Count * .3;
            var user = new PassActor(player, new PitchPoint(Family == PracticeFamily.Defending ? -35 : Family == PracticeFamily.Goalkeeping ? -49 : Family == PracticeFamily.Attacking ? 35 : 0, 0), new PitchPoint(1, 0), Family == PracticeFamily.Goalkeeping ? "gk" : null);
            var rival = new PassActor(opponent, new PitchPoint(user.Position.X + (Family == PracticeFamily.Goalkeeping ? 9 : 1.5), offset), new PitchPoint(-1, 0));
            var friend = new PassActor(teammate, new PitchPoint(user.Position.X + 8, 8), new PitchPoint(1, 0));
            var options = new List<PracticeOption>(); var ball = user.Position;
            if (Family == PracticeFamily.Passing)
                foreach (var kind in new[] { PassKind.ToFeet, PassKind.ThroughBall })
                {
                    var p = PassResolver.Preview(user, friend, kind, kind == PassKind.ToFeet ? friend.Position : new PitchPoint(20, 8), user.PreferredFoot == "left" ? "left" : "right", new[] { rival });
                    options.Add(new PracticeOption(kind.ToString(), kind == PassKind.ToFeet ? "Keep possession with a short pass to feet." : "Lead the runner into space; a longer, more demanding pass.", p.Percent, p.SkillAttribution, p.Target, (m, id) => PassResolver.Commit(m, id, p, false).Action));
                }
            else if (Family == PracticeFamily.Defending)
            {
                ball = rival.Position;
                foreach (var kind in new[] { DefensiveAction.Tackle, DefensiveAction.Contain, DefensiveAction.Block })
                {
                    var p = DefenseResolver.Preview(user, rival, 1, kind);
                    options.Add(new PracticeOption(kind.ToString(), kind == DefensiveAction.Tackle ? "Try to take the ball; being beaten leaves space behind." : kind == DefensiveAction.Contain ? "Stay goal-side and delay the attack." : "Step into the shooting lane.", p.Percent, p.SkillAttribution, p.Target, (m, id) => DefenseResolver.Commit(m, id, p, false).Action));
                }
            }
            else if (Family == PracticeFamily.Attacking)
                foreach (var variant in new[] { "placed", "power", "carry" })
                {
                    var context = new FootballActionContext(user, BallSituation.Controlled, 0, 0, 1, new[] { rival });
                    var p = FootballActionResolver.Preview(context, variant == "carry" ? FootballActionKind.Dribble : FootballActionKind.Shot, variant);
                    options.Add(new PracticeOption(variant, variant == "carry" ? "Carry into space to work on dribbling." : "Aim a " + variant + " shot on target. On target is the drill's success condition.", p.Percent, p.SkillAttribution, p.Target, (m, id) => FootballActionResolver.Commit(m, id, p, false).Action));
                }
            else
            {
                int round = results.Count;
                ball = new PitchPoint(round == 2 ? -43 : -46, round == 1 ? 2 : 0);
                var context = new KeeperContext(user, round == 0 ? KeeperSituation.Shot : round == 1 ? KeeperSituation.Cross : KeeperSituation.OneOnOne,
                    ball, round == 0 ? 1 : round == 1 ? 1.8 : .1, round == 0 ? 12 : round == 1 ? 8 : 5, 1, new HandlingRules(), false, new[] { rival });
                foreach (var kind in round == 0 ? new[] { KeeperAction.HoldSave, KeeperAction.Parry } : round == 1 ? new[] { KeeperAction.Claim, KeeperAction.Punch } : new[] { KeeperAction.SetPosition, KeeperAction.RushOut })
                {
                    var p = GoalkeeperResolver.Preview(context, kind);
                    options.Add(new PracticeOption(kind.ToString(), round == 0 ? "Hold the shot or deflect it away from danger." : round == 1 ? "Catch the cross or punch it clear." : "Set your position or rush out to meet the striker.", p.Percent, p.SkillAttribution, p.Target, (m, id) => GoalkeeperResolver.Commit(m, id, p, false).Action));
                }
            }
            Decision = new PracticeDecision(Id + ":drill:" + results.Count, options, Family == PracticeFamily.Passing ? new[] { user, friend, rival } : new[] { user, rival }, ball);
        }
    }
}
