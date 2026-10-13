using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FootballRpg.Simulation
{
    public enum SocialRole { Coach, Teammate, Agent }
    public sealed class SocialRules
    {
        public int FriendshipThreshold { get; }
        public int EvidencePerTraitPoint { get; }
        public double AttributeWeight { get; }
        public ulong Seed { get; }
        public SocialRules(int friendshipThreshold = 30, int evidencePerTraitPoint = 8, double attributeWeight = .006, ulong seed = 7130)
        {
            if (friendshipThreshold < 1 || friendshipThreshold > 100 || evidencePerTraitPoint < 2 || evidencePerTraitPoint > 1000 ||
                double.IsNaN(attributeWeight) || double.IsInfinity(attributeWeight) || attributeWeight < 0 || attributeWeight > .1) throw new ArgumentOutOfRangeException();
            FriendshipThreshold = friendshipThreshold; EvidencePerTraitPoint = evidencePerTraitPoint; AttributeWeight = attributeWeight; Seed = seed;
        }
    }
    public sealed class RelationshipState
    {
        public string PersonId { get; }
        public string Name { get; }
        public SocialRole Role { get; }
        public int Affinity { get; internal set; }
        public int ProfessionalTrust { get; internal set; } = 50;
        public int Standing { get; internal set; } = 50;
        internal RelationshipState(string id, string name, SocialRole role) { PersonId = id; Name = name; Role = role; }
    }
    public sealed class SocialEffect
    {
        public int Affinity { get; }
        public int Trust { get; }
        public int Standing { get; }
        public SocialEffect(int affinity, int trust, int standing) { Affinity = affinity; Trust = trust; Standing = standing; }
    }
    public sealed class DialogueOption
    {
        public string Id { get; }
        public string Text { get; }
        public string CheckTrait { get; }
        public int Difficulty { get; }
        public string BehaviorTrait { get; }
        public int BehaviorDirection { get; }
        public string SuccessText { get; }
        public string FailureText { get; }
        public SocialEffect Success { get; }
        public SocialEffect Failure { get; }
        internal DialogueOption(string id, string text, string trait, int difficulty, string behavior, int direction,
            string successText, string failureText, SocialEffect success, SocialEffect failure)
        { Id = id; Text = text; CheckTrait = trait; Difficulty = difficulty; BehaviorTrait = behavior; BehaviorDirection = direction; SuccessText = successText; FailureText = failureText; Success = success; Failure = failure; }
    }
    public sealed class DialoguePreview
    {
        internal CareerSocial Owner { get; }
        internal int Revision { get; }
        internal int Slot { get; }
        public string Id { get; }
        public string PersonId { get; }
        public DialogueOption Option { get; }
        public int TraitValue { get; }
        public int Percent { get; }
        public float Chance => Percent / 100f;
        internal DialoguePreview(CareerSocial owner, int revision, int slot, string id, string person, DialogueOption option, int trait, int percent)
        { Owner = owner; Revision = revision; Slot = slot; Id = id; PersonId = person; Option = option; TraitValue = trait; Percent = percent; }
    }
    public sealed class BehaviorObservation
    {
        public string CauseId { get; }
        public string TraitId { get; }
        public int Direction { get; }
        public bool Counted { get; }
        public int EvidenceAfter { get; }
        public int PreviousValue { get; }
        public int CurrentValue { get; }
        internal BehaviorObservation(string cause, string trait, int direction, bool counted, int evidence, int before, int after)
        { CauseId = cause; TraitId = trait; Direction = direction; Counted = counted; EvidenceAfter = evidence; PreviousValue = before; CurrentValue = after; }
    }
    public sealed class DialogueResult
    {
        public DialoguePreview Preview { get; }
        public float Roll { get; }
        public bool Succeeded => Roll < Preview.Chance;
        public string Outcome => Succeeded ? Preview.Option.SuccessText : Preview.Option.FailureText;
        public IReadOnlyList<RelationshipChanged> Changes { get; }
        public BehaviorObservation Behavior { get; }
        internal DialogueResult(DialoguePreview preview, float roll, List<RelationshipChanged> changes, BehaviorObservation behavior)
        { Preview = preview; Roll = roll; Changes = changes.AsReadOnly(); Behavior = behavior; }
    }
    /// <summary>Career-owned social state. Previewing is inert; each contact has one brief exchange per calendar slot.</summary>
    public sealed class CareerSocial
    {
        private readonly CareerState career;
        private readonly SeededRandom random;
        private readonly Dictionary<string, (int minimum, int maximum)> bounds;
        private readonly Dictionary<string, int> evidence = new Dictionary<string, int>();
        private readonly Dictionary<string, int> lastEvidenceSlot = new Dictionary<string, int>();
        private readonly Dictionary<string, DialogueResult> committed = new Dictionary<string, DialogueResult>();
        private readonly List<DialogueResult> results = new List<DialogueResult>();
        private readonly List<RelationshipChanged> changes = new List<RelationshipChanged>();
        private readonly Dictionary<string, RelationshipState> relationships;
        private int revision;
        public SocialRules Rules { get; }
        public IReadOnlyDictionary<string, RelationshipState> Relationships { get; }
        public IReadOnlyList<DialogueOption> Options { get; }
        public IReadOnlyList<DialogueResult> Results { get; }
        public IReadOnlyList<RelationshipChanged> Changes { get; }
        public IReadOnlyDictionary<string, int> BehaviorEvidence { get; }
        public long DrawCount { get; private set; }
        private int Slot => career.Calendar == null ? 0 : career.Calendar.Minute / career.Calendar.MinutesPerSlot;
        internal CareerSocial(CareerState career, ValueDefinition[] traits, SocialRules rules)
        {
            this.career = career; Rules = rules; random = new SeededRandom(rules.Seed);
            bounds = traits.ToDictionary(t => t.id, t => (t.minimum, t.maximum));
            foreach (var trait in traits) evidence.Add(trait.id, 0);
            BehaviorEvidence = new ReadOnlyDictionary<string, int>(evidence);
            var club = career.Clubs[career.UserPlayer.ClubId]; var coach = career.Coaches[club.CoachId];
            relationships = new Dictionary<string, RelationshipState> { [coach.Id] = new RelationshipState(coach.Id, coach.Name, SocialRole.Coach) };
            foreach (var p in career.Players.Values.Where(p => p.ClubId == club.Id && p.Id != career.UserPlayerId))
                relationships.Add(p.Id, new RelationshipState(p.Id, p.Name, SocialRole.Teammate));
            string agentId = "agent:" + career.UserPlayerId;
            relationships.Add(agentId, new RelationshipState(agentId, "Your agent", SocialRole.Agent));
            Relationships = new ReadOnlyDictionary<string, RelationshipState>(relationships);
            Options = Array.AsReadOnly(new[] {
                new DialogueOption("connect", "Show a personal interest", "charisma", 45, "charisma", 1,
                    "They enjoy the conversation. Personal warmth grows; professional trust is unchanged.", "Your approach feels awkward. Personal warmth slips; professional trust is unchanged.", new SocialEffect(12, 0, 0), new SocialEffect(-3, 0, 0)),
                new DialogueOption("help", "Offer practical help", "discipline", 50, "discipline", 1,
                    "Your offer is convincing. They trust your professionalism more.", "They doubt that you will follow through.", new SocialEffect(1, 10, 3), new SocialEffect(0, -4, -2)),
                new DialogueOption("challenge", "Challenge their view openly", "leadership", 55, "leadership", 1,
                    "Your argument carries weight, but they dislike being challenged.", "The challenge lands badly and damages your standing.", new SocialEffect(-10, 2, 8), new SocialEffect(-12, -5, -4)),
                new DialogueOption("deflect", "Make an excuse to avoid responsibility", "charisma", 55, "discipline", -1,
                    "The excuse eases the conversation, but avoiding responsibility reduces trust.", "They see through the excuse. Liking, trust and standing suffer.", new SocialEffect(4, -10, -4), new SocialEffect(-4, -12, -8))
            }.Where(o => bounds.ContainsKey(o.CheckTrait) && bounds.ContainsKey(o.BehaviorTrait)).ToArray());
            Results = results.AsReadOnly(); Changes = changes.AsReadOnly();
        }
        public string Bond(string personId) => relationships[personId].Affinity >= Rules.FriendshipThreshold ? "Friendship" : relationships[personId].Affinity <= -Rules.FriendshipThreshold ? "Rivalry" : "Neutral";
        private string ExchangeId(string personId) => career.UserPlayerId + ":social:" + Slot + ":" + personId;
        public string UnavailableReason(string personId)
        {
            if (!relationships.ContainsKey(personId)) return "Unknown contact.";
            if (career.Calendar?.Finished == true) return "The prototype week is complete.";
            if (career.Calendar?.AwaitingPractice == true) return "Finish guided practice first.";
            if (committed.ContainsKey(ExchangeId(personId))) return "This contact's exchange is complete for this slot. Advance the calendar before another exchange.";
            return null;
        }
        public DialoguePreview Preview(string personId, string optionId)
        {
            string unavailable = UnavailableReason(personId); if (unavailable != null) throw new InvalidOperationException(unavailable);
            var option = Options.SingleOrDefault(o => o.Id == optionId) ?? throw new ArgumentException("Unknown dialogue choice.");
            int trait = career.UserPlayer.PersonalityTraits[option.CheckTrait];
            double chance = Math.Max(.05, Math.Min(.95, .5 + (trait - option.Difficulty) * Rules.AttributeWeight));
            int percent = (int)Math.Round(chance * 100, MidpointRounding.AwayFromZero);
            return new DialoguePreview(this, revision, Slot, ExchangeId(personId), personId, option, trait, percent);
        }
        public DialogueResult Commit(DialoguePreview preview)
        {
            if (preview == null || preview.Owner != this) throw new ArgumentException("Use a preview from this career.");
            if (committed.TryGetValue(preview.Id, out var previous))
            {
                if (previous.Preview.Option.Id != preview.Option.Id) throw new InvalidOperationException("This exchange has already been decided.");
                return previous;
            }
            if (preview.Revision != revision || preview.Slot != Slot || UnavailableReason(preview.PersonId) != null)
                throw new InvalidOperationException("The context changed. Inspect a new choice.");
            float roll = random.NextUnitFloat(); DrawCount++;
            var effect = roll < preview.Chance ? preview.Option.Success : preview.Option.Failure;
            var relationship = relationships[preview.PersonId]; var applied = new List<RelationshipChanged>();
            Apply("affinity", relationship.Affinity, effect.Affinity, -100, value => relationship.Affinity = value, preview, applied);
            Apply("professional-trust", relationship.ProfessionalTrust, effect.Trust, 0, value => relationship.ProfessionalTrust = value, preview, applied);
            Apply("standing", relationship.Standing, effect.Standing, 0, value => relationship.Standing = value, preview, applied);
            var behavior = Observe(preview);
            var result = new DialogueResult(preview, roll, applied, behavior);
            committed.Add(preview.Id, result); results.Add(result); changes.AddRange(applied); revision++; return result;
        }
        private void Apply(string dimension, int before, int delta, int minimum, Action<int> set, DialoguePreview preview, List<RelationshipChanged> applied)
        {
            int after = Math.Max(minimum, Math.Min(100, before + delta)); set(after);
            if (before != after) applied.Add(new RelationshipChanged(preview.Id + ":" + dimension, career.UserPlayerId, results.Count + 1, preview.PersonId, dimension, preview.Id, before, after));
        }
        internal void ApplyCoachEffect(string cause, SocialEffect effect)
        {
            var contact = relationships.Values.Single(r => r.Role == SocialRole.Coach);
            var applied = new List<RelationshipChanged>();
            var preview = new DialoguePreview(this, revision, Slot, cause, contact.PersonId, null, 0, 0);
            Apply("affinity", contact.Affinity, effect.Affinity, -100, value => contact.Affinity = value, preview, applied);
            Apply("professional-trust", contact.ProfessionalTrust, effect.Trust, 0, value => contact.ProfessionalTrust = value, preview, applied);
            Apply("standing", contact.Standing, effect.Standing, 0, value => contact.Standing = value, preview, applied);
            changes.AddRange(applied); revision++;
        }
        private BehaviorObservation Observe(DialoguePreview preview)
        {
            string trait = preview.Option.BehaviorTrait; int before = career.UserPlayer.PersonalityTraits[trait], after = before;
            bool counted = !lastEvidenceSlot.TryGetValue(trait, out int lastSlot) || lastSlot != Slot;
            if (counted)
            {
                lastEvidenceSlot[trait] = Slot;
                int next = evidence[trait] + preview.Option.BehaviorDirection;
                if (Math.Abs(next) >= Rules.EvidencePerTraitPoint)
                {
                    after = Math.Max(bounds[trait].minimum, Math.Min(bounds[trait].maximum, before + Math.Sign(next)));
                    career.UserPlayer.SetPersonalityTrait(trait, after); next = 0;
                }
                evidence[trait] = next;
            }
            return new BehaviorObservation(preview.Id, trait, preview.Option.BehaviorDirection, counted, evidence[trait], before, after);
        }
    }
}
