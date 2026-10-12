using System;

namespace FootballRpg.Simulation
{
    // Authoring DTOs only. Runtime sessions copy their data; never use them as mutable save state.
    [Serializable]
    public sealed class GameDefinitions
    {
        public int schemaVersion;
        public string userPlayerId;
        public ValueDefinition[] footballAttributes;
        public ValueDefinition[] personalityTraits;
        public PositionDefinition[] positions;
        public ActionDefinition[] actions;
        public PlayerDefinition[] players;
        public CoachDefinition[] coaches;
        public ClubDefinition[] clubs;
        public FixtureDefinition fixture;
        public CalendarDefinition calendar;
    }

    [Serializable]
    public sealed class ValueDefinition
    {
        public string id;
        public string name;
        public string group;
        public int minimum;
        public int maximum;
        public int defaultValue;
    }

    [Serializable]
    public sealed class PositionDefinition
    {
        public string id;
        public string name;
        public string[] recommendedAttributeIds;
    }

    [Serializable]
    public sealed class ActionDefinition
    {
        public string id;
        public string name;
        public string successMeaning;
        public string[] attributeIds;
        // No skill thresholds: availability is contextual, never unlocked by attribute level.
    }

    [Serializable]
    public sealed class ValueOverride
    {
        public string id;
        public int value;
    }

    [Serializable]
    public sealed class PlayerDefinition
    {
        public string id;
        public string name;
        public int age;
        public float heightCm;
        public float weightKg;
        public string preferredFoot;
        public string appearanceId;
        public string positionId;
        public string clubId;
        public ValueOverride[] attributes;
        public ValueOverride[] traits;
        public float confidence;
        public float morale;
        public float fatigue;
        public long balanceMinor;
        public long weeklyWageMinor;
    }

    [Serializable]
    public sealed class CoachDefinition
    {
        public string id;
        public string name;
        public string preferredFormation;
        public float coachingQuality;
        public float disciplinePreference;
        public float creativityPreference;
    }

    [Serializable]
    public sealed class ClubDefinition
    {
        public string id;
        public string name;
        public string coachId;
        public string currencyCode;
        public float staffQuality;
        public float facilityQuality;
        public string[] squadPlayerIds;
        public SetPieceDefinition setPieces;
    }

    [Serializable]
    public sealed class FixtureDefinition
    {
        public string id;
        public string homeClubId;
        public string awayClubId;
        public int playersPerTeam;
        public int benchSize;
        public int substitutionLimit;
    }

    public static class PositionIds
    {
        // Stable save/content identifiers, not display names or array indices.
        public static string[] All => new[]
        {
            "gk", "cb", "lb", "rb", "lwb", "rwb", "dm", "cm", "am", "lm", "rm", "lw", "rw", "st"
        };
    }
}
