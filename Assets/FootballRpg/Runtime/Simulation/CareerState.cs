using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FootballRpg.Simulation
{
    public sealed class PlayerState
    {
        public string Id { get; }
        public string Name { get; }
        public int Age { get; }
        public float HeightCm { get; }
        public float WeightKg { get; }
        public string PreferredFoot { get; }
        public string AppearanceId { get; }
        public string PrimaryPositionId { get; }
        public string ClubId { get; }
        public string CurrencyCode { get; }
        public long BalanceMinor { get; }
        public long WeeklyWageMinor { get; }
        public IReadOnlyDictionary<string, int> FootballAttributes { get; }
        public IReadOnlyDictionary<string, int> PersonalityTraits { get; }
        public float Confidence { get; private set; }
        public float Morale { get; private set; }
        public float Fatigue { get; private set; }
        public int InjuryDaysRemaining { get; internal set; }

        internal PlayerState(PlayerDefinition source, GameDefinitions definitions)
        {
            Id = source.id;
            Name = source.name;
            Age = source.age;
            HeightCm = source.heightCm;
            WeightKg = source.weightKg;
            PreferredFoot = source.preferredFoot;
            AppearanceId = source.appearanceId;
            PrimaryPositionId = source.positionId;
            ClubId = source.clubId;
            CurrencyCode = definitions.clubs.Single(c => c.id == ClubId).currencyCode;
            BalanceMinor = source.balanceMinor;
            WeeklyWageMinor = source.weeklyWageMinor;
            FootballAttributes = Values(definitions.footballAttributes, source.attributes);
            PersonalityTraits = Values(definitions.personalityTraits, source.traits);
            SetCondition(source.confidence, source.morale, source.fatigue);
        }

        internal void SetCondition(float confidence, float morale, float fatigue)
        {
            // Validate all inputs before mutation so an invalid update cannot partially apply.
            DefinitionValidator.Unit(confidence, nameof(confidence));
            DefinitionValidator.Unit(morale, nameof(morale));
            DefinitionValidator.Unit(fatigue, nameof(fatigue));
            Confidence = confidence;
            Morale = morale;
            Fatigue = fatigue;
        }

        private static IReadOnlyDictionary<string, int> Values(ValueDefinition[] definitions, ValueOverride[] overrides)
        {
            var values = definitions.ToDictionary(d => d.id, d => d.defaultValue, StringComparer.Ordinal);
            foreach (var entry in overrides) values[entry.id] = entry.value;
            return new ReadOnlyDictionary<string, int>(values);
        }
    }

    public sealed class CoachState
    {
        public string Id { get; }
        public string Name { get; }
        public string PreferredFormation { get; }
        public float CoachingQuality { get; }
        public float DisciplinePreference { get; }
        public float CreativityPreference { get; }

        internal CoachState(CoachDefinition source)
        {
            Id = source.id;
            Name = source.name;
            PreferredFormation = source.preferredFormation;
            CoachingQuality = source.coachingQuality;
            DisciplinePreference = source.disciplinePreference;
            CreativityPreference = source.creativityPreference;
        }
    }

    public sealed class ClubState
    {
        public string Id { get; }
        public string Name { get; }
        public string CoachId { get; }
        public string CurrencyCode { get; }
        public float StaffQuality { get; }
        public float FacilityQuality { get; }
        public IReadOnlyList<string> SquadPlayerIds { get; }
        public SetPiecePlan SetPieces { get; }

        internal ClubState(ClubDefinition source)
        {
            Id = source.id;
            Name = source.name;
            CoachId = source.coachId;
            CurrencyCode = source.currencyCode;
            StaffQuality = source.staffQuality;
            FacilityQuality = source.facilityQuality;
            SetPieces = new SetPiecePlan(source.setPieces);
            SquadPlayerIds = Array.AsReadOnly((string[])source.squadPlayerIds.Clone());
        }
    }

    public sealed class FixtureState
    {
        public string Id { get; }
        public string HomeClubId { get; }
        public string AwayClubId { get; }
        public int PlayersPerTeam { get; }
        public int BenchSize { get; }
        public int SubstitutionLimit { get; }

        internal FixtureState(FixtureDefinition source)
        {
            Id = source.id;
            HomeClubId = source.homeClubId;
            AwayClubId = source.awayClubId;
            PlayersPerTeam = source.playersPerTeam;
            BenchSize = source.benchSize;
            SubstitutionLimit = source.substitutionLimit;
        }
    }

    /// <summary>One owner per career session. Views resolve IDs, never clone players into squads.</summary>
    public sealed class CareerState
    {
        public string UserPlayerId { get; }
        public PlayerState UserPlayer => Players[UserPlayerId];
        public IReadOnlyDictionary<string, PlayerState> Players { get; }
        public IReadOnlyDictionary<string, ClubState> Clubs { get; }
        public IReadOnlyDictionary<string, CoachState> Coaches { get; }
        public FixtureState Fixture { get; }

        private CareerState(GameDefinitions definitions)
        {
            UserPlayerId = definitions.userPlayerId;
            Players = new ReadOnlyDictionary<string, PlayerState>(
                definitions.players.ToDictionary(p => p.id, p => new PlayerState(p, definitions), StringComparer.Ordinal));
            Clubs = new ReadOnlyDictionary<string, ClubState>(
                definitions.clubs.ToDictionary(c => c.id, c => new ClubState(c), StringComparer.Ordinal));
            Coaches = new ReadOnlyDictionary<string, CoachState>(
                definitions.coaches.ToDictionary(c => c.id, c => new CoachState(c), StringComparer.Ordinal));
            Fixture = new FixtureState(definitions.fixture);
        }

        public static CareerState Create(GameDefinitions definitions)
        {
            DefinitionValidator.Validate(definitions);
            return new CareerState(definitions);
        }

        public void SetPlayerCondition(string playerId, float confidence, float morale, float fatigue)
        {
            Players[playerId].SetCondition(confidence, morale, fatigue);
        }
    }
}
