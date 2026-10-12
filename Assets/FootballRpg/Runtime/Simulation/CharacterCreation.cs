using System;
using System.Linq;

namespace FootballRpg.Simulation
{
    [Serializable]
    public sealed class CreationRules
    {
        public int footballBudget = 1200;
        public int personalityBudget = 300;
        public int minimumAge = 16;
        public int maximumAge = 21;
    }

    // Serializable draft uses stable content IDs and explicit values, never UI indices.
    [Serializable]
    public sealed class CharacterDraft
    {
        public string name = "New Prospect";
        public int age = 18;
        public float heightCm = 180;
        public float weightKg = 75;
        public string preferredFoot = "right";
        public string appearanceId = "placeholder-1";
        public string positionId = "cm";
        public ValueOverride[] attributes;
        public ValueOverride[] traits;
    }

    public static class CharacterCreation
    {
        public static CharacterDraft NewDraft(GameDefinitions content) => new CharacterDraft {
            attributes = content.footballAttributes.Select(v => new ValueOverride { id = v.id, value = v.minimum }).ToArray(),
            traits = content.personalityTraits.Select(v => new ValueOverride { id = v.id, value = v.minimum }).ToArray()
        };

        public static int Remaining(ValueDefinition[] definitions, ValueOverride[] values, int budget)
        {
            if (budget < 0 || budget > definitions.Sum(v => v.maximum - v.minimum))
                throw new ArgumentException("Invalid creation budget.");
            if (values == null || values.Length != definitions.Length || values.Any(v => v == null) ||
                values.Select(v => v.id).Distinct().Count() != values.Length)
                throw new ArgumentException("Every attribute needs exactly one value.");
            int spent = 0;
            foreach (var entry in values)
            {
                var definition = definitions.SingleOrDefault(v => v.id == entry.id);
                if (definition == null || entry.value < definition.minimum || entry.value > definition.maximum)
                    throw new ArgumentException("Attribute outside its allowed range: " + entry.id);
                spent += entry.value - definition.minimum;
            }
            return budget - spent;
        }

        public static void Validate(GameDefinitions content, CharacterDraft draft, CreationRules rules)
        {
            if (draft == null || rules == null) throw new ArgumentException("Missing character or creation rules.");
            if (rules.minimumAge < 14 || rules.maximumAge > 60 || rules.minimumAge > rules.maximumAge)
                throw new ArgumentException("Invalid starting age configuration.");
            if (string.IsNullOrWhiteSpace(draft.name) || draft.name.Trim().Length > 40)
                throw new ArgumentException("Enter a name of 1–40 characters.");
            if (draft.age < rules.minimumAge || draft.age > rules.maximumAge)
                throw new ArgumentException("Choose a young starting age within the shown range.");
            if (!content.positions.Any(p => p.id == draft.positionId)) throw new ArgumentException("Choose a supported position.");
            if (!new[] { "left", "right", "both" }.Contains(draft.preferredFoot)) throw new ArgumentException("Choose a preferred foot.");
            if (!new[] { "placeholder-1", "placeholder-2", "placeholder-3", "placeholder-4" }.Contains(draft.appearanceId))
                throw new ArgumentException("Choose an appearance preset.");
            if (float.IsNaN(draft.heightCm) || draft.heightCm < 120 || draft.heightCm > 230 ||
                float.IsNaN(draft.weightKg) || draft.weightKg < 35 || draft.weightKg > 160)
                throw new ArgumentException("Height must be 120–230 cm and weight 35–160 kg.");
            if (Remaining(content.footballAttributes, draft.attributes, rules.footballBudget) != 0 ||
                Remaining(content.personalityTraits, draft.traits, rules.personalityBudget) != 0)
                throw new ArgumentException("Spend both budgets exactly; points cannot transfer between them.");
        }

        public static CareerState CreateCareer(GameDefinitions content, CharacterDraft draft, CreationRules rules)
        {
            Validate(content, draft, rules);
            var club = content.clubs.Single(c => c.id == content.fixture.homeClubId);
            var starters = club.squadPlayerIds.Take(content.fixture.playersPerTeam).Select(id => content.players.Single(p => p.id == id));
            var slot = starters.FirstOrDefault(p => p.positionId == draft.positionId) ?? starters.First(p => p.positionId != "gk");
            var created = new PlayerDefinition {
                id = slot.id, clubId = club.id, name = draft.name.Trim(), age = draft.age,
                heightCm = draft.heightCm, weightKg = draft.weightKg, preferredFoot = draft.preferredFoot,
                appearanceId = draft.appearanceId, positionId = draft.positionId,
                attributes = draft.attributes, traits = draft.traits, confidence = .5f, morale = .5f, fatigue = 0,
                weeklyWageMinor = slot.weeklyWageMinor, balanceMinor = 0
            };
            // Copy the authoring envelope; neither source content nor its player slots are modified.
            var session = new GameDefinitions {
                schemaVersion = content.schemaVersion, userPlayerId = created.id,
                footballAttributes = content.footballAttributes, personalityTraits = content.personalityTraits,
                positions = content.positions, actions = content.actions, coaches = content.coaches,
                clubs = content.clubs, fixture = content.fixture, calendar = content.calendar,
                players = content.players.Select(p => p.id == slot.id ? created : p).ToArray()
            };
            return CareerState.Create(session);
        }
    }
}
