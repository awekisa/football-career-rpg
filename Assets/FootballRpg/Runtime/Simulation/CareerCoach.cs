using System;
using System.Collections.Generic;
using System.Linq;

namespace FootballRpg.Simulation
{
    public sealed class CoachRules
    {
        public double ClubStrength { get; }
        public int StarStanding { get; }
        public int SetPieceFailurePenalty { get; }
        public ulong Seed { get; }
        public CoachRules(double clubStrength = .35, int starStanding = 80, int setPieceFailurePenalty = 18, ulong seed = 7131)
        {
            if (double.IsNaN(clubStrength) || double.IsInfinity(clubStrength) || clubStrength < 0 || clubStrength > 1 || starStanding < 51 || starStanding > 100 || setPieceFailurePenalty < 1 || setPieceFailurePenalty > 100) throw new ArgumentOutOfRangeException();
            ClubStrength = clubStrength; StarStanding = starStanding; SetPieceFailurePenalty = setPieceFailurePenalty; Seed = seed;
        }
    }
    public sealed class CoachSelection
    {
        public SelectionOutcome Outcome { get; }
        public string Role { get; }
        public double Ability { get; }
        public double Score { get; }
        public double RequiredScore { get; }
        public string Reason { get; }
        internal CoachSelection(SelectionOutcome outcome, string role, double ability, double score, double required, string reason)
        { Outcome = outcome; Role = role; Ability = ability; Score = score; RequiredScore = required; Reason = reason; }
    }
    public enum CoachRequest { Start, Role, Penalties }
    public sealed class CoachRequestPreview
    {
        internal CareerCoach Owner { get; }
        internal string Context { get; }
        public CoachRequest Request { get; }
        public string Role { get; }
        public int Percent { get; }
        public float Chance => Percent / 100f;
        internal CoachRequestPreview(CareerCoach owner, string context, CoachRequest request, string role, int percent)
        { Owner = owner; Context = context; Request = request; Role = role; Percent = percent; }
    }
    public sealed class CoachRequestResult
    {
        public CoachRequestPreview Preview { get; }
        public float Roll { get; }
        public bool Succeeded => Roll < Preview.Chance;
        public string Message { get; }
        internal CoachRequestResult(CoachRequestPreview p, float roll, string message) { Preview = p; Roll = roll; Message = message; }
    }
    public sealed class CoachReaction
    {
        public string CauseId { get; }
        public string Reason { get; }
        public int TrustDelta { get; }
        public int StandingDelta { get; }
        internal CoachReaction(string cause, string reason, int trust, int standing) { CauseId = cause; Reason = reason; TrustDelta = trust; StandingDelta = standing; }
    }
    /// <summary>Career coach policy. Match integration supplies committed facts; inspection never rolls or mutates.</summary>
    public sealed class CareerCoach
    {
        private readonly CareerState career;
        private readonly Dictionary<string, string[]> roleSkills;
        private readonly SeededRandom random;
        private readonly Dictionary<RestartKind, int> responsibility = new Dictionary<RestartKind, int>();
        private readonly Dictionary<string, CoachReaction> reactions = new Dictionary<string, CoachReaction>();
        private readonly List<CoachReaction> history = new List<CoachReaction>();
        private int revision, lastRequestSlot = -1;
        private string requestedRole;
        private bool startPromise, penaltyPromise;
        public CoachRules Rules { get; }
        public CoachState Profile { get; }
        public RelationshipState Relationship => career.Social.Relationships[Profile.Id];
        public bool EstablishedStar => Relationship.Standing >= Rules.StarStanding;
        public IReadOnlyList<CoachReaction> Reactions { get; }
        public CoachRequestResult LastRequest { get; private set; }
        public long DrawCount { get; private set; }
        public bool MatchActive { get; internal set; }
        private int Slot => career.Calendar == null ? 0 : career.Calendar.Minute / career.Calendar.MinutesPerSlot;
        public string AssignedRole => requestedRole ?? career.UserPlayer.PrimaryPositionId;
        internal CareerCoach(CareerState career, PositionDefinition[] positions, CoachRules rules)
        {
            this.career = career; Rules = rules; random = new SeededRandom(rules.Seed);
            Profile = career.Coaches[career.Clubs[career.UserPlayer.ClubId].CoachId];
            roleSkills = positions.ToDictionary(p => p.id, p => (string[])p.recommendedAttributeIds.Clone());
            Reactions = history.AsReadOnly();
        }
        public double Ability(PlayerState player, string role) => roleSkills[role].Average(id => player.FootballAttributes[id]);
        public CoachSelection Select()
        {
            var p = career.UserPlayer; double ability = Ability(p, AssignedRole);
            double required = 25 + 30 * Rules.ClubStrength;
            double score = ability + (Relationship.ProfessionalTrust - 50) * .15 + (Relationship.Standing - 50) * .1 + Relationship.Affinity * .025
                + (p.Morale - .5) * 4 - p.Fatigue * 25 + (startPromise ? 12 : 0);
            if (p.InjuryDaysRemaining > 0) return new CoachSelection(SelectionOutcome.NotSelected, AssignedRole, ability, score, required, "Unavailable through injury.");
            if (p.Fatigue >= .85) return new CoachSelection(SelectionOutcome.Substitute, AssignedRole, ability, score, required, "Needs recovery before starting.");
            if (Relationship.ProfessionalTrust < 20) return new CoachSelection(SelectionOutcome.Substitute, AssignedRole, ability, score, required, "Professional trust is too low to start.");
            bool start = score >= required;
            return new CoachSelection(start ? SelectionOutcome.Starter : SelectionOutcome.Substitute, AssignedRole, ability, score, required,
                start ? "Selected to start: ability, readiness and trust meet this club's expectations." : "On the bench: develop role skills, recover or rebuild trust.");
        }
        public string RequestUnavailable => MatchActive ? "Discuss requests before the match." : career.Calendar?.Finished == true ? "The week is complete." : career.Calendar?.AwaitingPractice == true ? "Finish guided practice first." : lastRequestSlot == Slot ? "One request per calendar slot; advance the calendar to discuss again." : null;
        private string Context => string.Join("/", revision, Slot, Relationship.Affinity, Relationship.ProfessionalTrust, Relationship.Standing,
            career.UserPlayer.Fatigue, career.UserPlayer.InjuryDaysRemaining, career.UserPlayer.PersonalityTraits["charisma"], career.UserPlayer.PersonalityTraits["leadership"], career.UserPlayer.FootballAttributes.Values.Sum());
        public CoachRequestPreview PreviewRequest(CoachRequest request, string role = null)
        {
            if (RequestUnavailable != null) throw new InvalidOperationException(RequestUnavailable);
            if (!Enum.IsDefined(typeof(CoachRequest), request) || request == CoachRequest.Role && !roleSkills.ContainsKey(role ?? "")) throw new ArgumentException("Select a valid request and position.");
            double skill = request == CoachRequest.Penalties ? career.UserPlayer.PersonalityTraits["leadership"] : career.UserPlayer.PersonalityTraits["charisma"];
            double chance = .45 + (skill - 50) * .006 + (Relationship.ProfessionalTrust - 50) * .003 + Relationship.Affinity * .001 + (Relationship.Standing - 50) * .002 - Profile.DisciplinePreference * .1;
            return new CoachRequestPreview(this, Context, request, request == CoachRequest.Role ? role : AssignedRole, (int)Math.Round(Math.Max(.05, Math.Min(.95, chance)) * 100, MidpointRounding.AwayFromZero));
        }
        public CoachRequestResult CommitRequest(CoachRequestPreview preview)
        {
            if (preview == null || preview.Owner != this) throw new ArgumentException("Use this coach's current preview.");
            if (LastRequest?.Preview == preview) return LastRequest;
            if (preview.Context != Context || RequestUnavailable != null) throw new InvalidOperationException("Context changed; inspect a fresh request.");
            float roll = random.NextUnitFloat(); DrawCount++; lastRequestSlot = Slot; revision++;
            bool success = roll < preview.Chance;
            if (success)
            {
                if (preview.Request == CoachRequest.Start) startPromise = true;
                else if (preview.Request == CoachRequest.Role) requestedRole = preview.Role;
                else { penaltyPromise = true; responsibility[RestartKind.Penalty] = 0; }
            }
            LastRequest = new CoachRequestResult(preview, roll, success ? "Coach agrees. Fitness and legal match restrictions still apply." : "Coach declines. Your existing assignment remains.");
            return LastRequest;
        }
        internal void ApplyCalendar(CalendarDecision decision)
        {
            foreach (var a in decision.Attendance.Where(a => a.Required && (a.Fact.ActivityId == "training" || a.Fact.ActivityId == "match" || a.Fact.ActivityId == "travel")))
            {
                int delta = a.Fact.Attendance == AttendanceOutcome.Attended ? 1 : a.Fact.Attendance == AttendanceOutcome.Late ? -(int)Math.Ceiling(2 + 3 * Profile.DisciplinePreference) : -(int)Math.Ceiling(4 + 6 * Profile.DisciplinePreference);
                Record(a.Fact.EventId, a.Fact.Attendance + " " + a.Fact.ActivityId, delta, delta < 0 ? -2 : 0);
            }
        }
        public static int DeviationPenalty(CoachState profile, int standing, bool succeeded, bool justified, double danger, int starStanding = 80)
        {
            if (profile == null || standing < 0 || standing > 100 || double.IsNaN(danger) || danger < 0 || danger > 1) throw new ArgumentOutOfRangeException();
            double cost = (2 + profile.DisciplinePreference * 5 - profile.CreativityPreference * 2) * (standing >= starStanding ? .3 : 1)
                * (succeeded ? .35 : 1) * (justified ? .25 : 1) * (1 + danger);
            return Math.Max(0, (int)Math.Round(cost, MidpointRounding.AwayFromZero));
        }
        internal CoachReaction Deviation(string cause, bool succeeded, bool justified, double danger, string choice)
        {
            int penalty = DeviationPenalty(Profile, Relationship.Standing, succeeded, justified, danger, Rules.StarStanding);
            return Record(cause, choice + (succeeded ? ": successful" : ": unsuccessful") + (justified ? " in a justified attacking context" : " outside the plan"), -penalty, succeeded ? 1 : -1);
        }
        private CoachReaction Record(string cause, string reason, int trust, int standing)
        {
            if (reactions.TryGetValue(cause, out var prior)) return prior;
            var beforeTrust = Relationship.ProfessionalTrust; var beforeStanding = Relationship.Standing;
            career.Social.ApplyCoachEffect(cause, new SocialEffect(0, trust, standing));
            var result = new CoachReaction(cause, reason, Relationship.ProfessionalTrust - beforeTrust, Relationship.Standing - beforeStanding);
            reactions.Add(cause, result); history.Add(result); revision++; return result;
        }
        internal void SetPieceResult(string cause, RestartKind kind, bool succeeded)
        {
            if (!Specialist(kind) || reactions.ContainsKey(cause)) return;
            kind = Normalize(kind);
            int penalty = ResponsibilityPenalty(kind);
            responsibility[kind] = Math.Max(0, Math.Min(100, penalty + (succeeded ? -4 : Rules.SetPieceFailurePenalty)));
            if (!succeeded && kind == RestartKind.Penalty) penaltyPromise = false;
            Record(cause, kind + (succeeded ? " succeeded; responsibility recovers" : " failed; future responsibility reduced"), succeeded ? 1 : -2, succeeded ? 1 : -1);
        }
        private static RestartKind Normalize(RestartKind kind) => kind == RestartKind.IndirectFreeKick ? RestartKind.DirectFreeKick : kind;
        private static bool Specialist(RestartKind kind) => kind == RestartKind.Penalty || kind == RestartKind.Corner || kind == RestartKind.DirectFreeKick || kind == RestartKind.IndirectFreeKick;
        public int ResponsibilityPenalty(RestartKind kind) => responsibility.TryGetValue(Normalize(kind), out int value) ? value : 0;
        public string ChooseTaker(RestartKind kind, IEnumerable<PlayerState> available)
        {
            if (!Specialist(kind)) return null;
            string skill = kind == RestartKind.Penalty ? "penalties" : kind == RestartKind.Corner ? "corners" : "free-kicks";
            var plan = career.Clubs[career.UserPlayer.ClubId].SetPieces.Takers(kind);
            return available.Where(p => p.ClubId == career.UserPlayer.ClubId && p.InjuryDaysRemaining == 0)
                .OrderByDescending(p => p.FootballAttributes[skill] + (plan.Contains(p.Id) ? 4 - Math.Min(3, plan.ToList().IndexOf(p.Id)) : 0)
                    + (p.Id == career.UserPlayerId ? (kind == RestartKind.Penalty && penaltyPromise ? 100 : 0) - ResponsibilityPenalty(kind) + (Relationship.Standing - 50) * .08 : 0))
                .ThenBy(p => p.Id, StringComparer.Ordinal).Select(p => p.Id).FirstOrDefault();
        }
        public OffBallIntent Instruction(bool attacking, bool lateChase, string role)
        {
            if (role == "gk") return OffBallIntent.Automatic;
            if (lateChase && attacking) return OffBallIntent.ForwardRun;
            if (!attacking) return Profile.DisciplinePreference >= Profile.CreativityPreference ? OffBallIntent.HoldZone : OffBallIntent.Press;
            if (role == "cb" || role == "dm") return OffBallIntent.HoldZone;
            return Profile.CreativityPreference > .5 ? OffBallIntent.FindSpace : OffBallIntent.Support;
        }
        public bool ShouldRemoveUser => Relationship.ProfessionalTrust < 20 || career.UserPlayer.Fatigue >= (EstablishedStar ? .82 : .72);
        public bool CanEnterFromBench => career.UserPlayer.InjuryDaysRemaining == 0 && career.UserPlayer.Fatigue < .65 && Relationship.ProfessionalTrust >= 20 && Select().Score >= Select().RequiredScore - 10;
    }
}
