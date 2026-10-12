using System;
using System.Collections.Generic;
using System.Linq;

namespace FootballRpg.Simulation
{
    public static class DefinitionValidator
    {
        public static void Validate(GameDefinitions data)
        {
            Require(data != null, "Content is missing.");
            Require(data.schemaVersion == 1, "Unsupported content schemaVersion.");
            var attributes = Index(data.footballAttributes, v => v.id, "football attributes");
            var traits = Index(data.personalityTraits, v => v.id, "personality traits");
            var positions = Index(data.positions, p => p.id, "positions");
            var actions = Index(data.actions, a => a.id, "actions");
            var players = Index(data.players, p => p.id, "players");
            var coaches = Index(data.coaches, c => c.id, "coaches");
            var clubs = Index(data.clubs, c => c.id, "clubs");
            foreach (var value in attributes.Values.Concat(traits.Values))
            {
                Text(value.name, value.id + ": name");
                Text(value.group, value.id + ": group");
                Require(value.minimum < value.maximum, value.id + ": invalid bounds.");
                Require(value.defaultValue >= value.minimum && value.defaultValue <= value.maximum,
                    value.id + ": default outside bounds.");
            }
            Require(!attributes.Keys.Intersect(traits.Keys).Any(), "Attribute and trait IDs must be distinct.");
            foreach (var id in PositionIds.All)
                Require(positions.ContainsKey(id), "Missing supported position: " + id);
            foreach (var position in positions.Values)
            {
                Text(position.name, position.id + ": name");
                References(position.recommendedAttributeIds, attributes, position.id + ": recommended attributes");
            }
            foreach (var action in actions.Values)
            {
                Text(action.name, action.id + ": name");
                Text(action.successMeaning, action.id + ": success meaning");
                References(action.attributeIds, attributes, action.id + ": action attributes");
            }
            foreach (var player in players.Values)
            {
                Text(player.name, player.id + ": name");
                Text(player.appearanceId, player.id + ": appearance preset");
                Require(player.age >= 14 && player.age <= 60, player.id + ": invalid age.");
                Range(player.heightCm, 120, 230, player.id + ": height");
                Range(player.weightKg, 35, 160, player.id + ": weight");
                Require(new[] { "left", "right", "both" }.Contains(player.preferredFoot), player.id + ": invalid preferred foot.");
                Require(positions.ContainsKey(player.positionId ?? ""), player.id + ": unknown position.");
                Require(clubs.ContainsKey(player.clubId ?? ""), player.id + ": unknown club.");
                Overrides(player.attributes, attributes, player.id + ": attributes");
                Overrides(player.traits, traits, player.id + ": traits");
                Unit(player.confidence, player.id + ": confidence");
                Unit(player.morale, player.id + ": morale");
                Unit(player.fatigue, player.id + ": fatigue");
                Require(player.weeklyWageMinor >= 0, player.id + ": negative wage.");
            }
            foreach (var coach in coaches.Values)
            {
                Require(!players.ContainsKey(coach.id), "Player and coach IDs must be distinct.");
                Text(coach.name, coach.id + ": name");
                Text(coach.preferredFormation, coach.id + ": formation");
                Unit(coach.coachingQuality, coach.id + ": quality");
                Unit(coach.disciplinePreference, coach.id + ": discipline");
                Unit(coach.creativityPreference, coach.id + ": creativity");
            }
            var membership = new HashSet<string>(StringComparer.Ordinal);
            var assignedCoaches = new HashSet<string>(StringComparer.Ordinal);
            foreach (var club in clubs.Values)
            {
                Text(club.name, club.id + ": name");
                Require(coaches.ContainsKey(club.coachId ?? ""), club.id + ": unknown coach.");
                Require(assignedCoaches.Add(club.coachId), club.id + ": coach assigned twice.");
                Require(club.currencyCode != null && club.currencyCode.Length == 3 &&
                    club.currencyCode.All(c => c >= 'A' && c <= 'Z'), club.id + ": invalid currency code.");
                Unit(club.staffQuality, club.id + ": staff quality");
                Unit(club.facilityQuality, club.id + ": facility quality");
                References(club.squadPlayerIds, players, club.id + ": squad");
                if (club.setPieces != null)
                {
                    foreach (var takers in new[] { club.setPieces.penaltyTakers, club.setPieces.freeKickTakers, club.setPieces.cornerTakers })
                    {
                        Require(takers != null && takers.Length > 0 && takers.Distinct().Count() == takers.Length && takers.All(id => club.squadPlayerIds.Contains(id)), club.id + ": invalid set-piece takers.");
                    }
                    Require(club.setPieces.roles != null, club.id + ": missing set-piece roles.");
                    var roles = new HashSet<string>();
                    foreach (var role in club.setPieces.roles)
                        Require(role != null && positions.ContainsKey(role.positionId ?? "") && roles.Add(role.positionId) &&
                            Enum.GetNames(typeof(SetPieceDuty)).Contains(role.attackingDuty) && Enum.GetNames(typeof(SetPieceDuty)).Contains(role.defendingDuty), club.id + ": invalid set-piece role.");
                }
                foreach (var id in club.squadPlayerIds)
                {
                    Require(membership.Add(id), id + ": assigned to multiple squads.");
                    Require(players[id].clubId == club.id, id + ": squad and membership disagree.");
                }
            }
            Require(membership.Count == players.Count, "Every prototype player must belong to a squad.");
            Require(players.ContainsKey(data.userPlayerId ?? ""), "Unknown userPlayerId.");
            var fixture = data.fixture;
            Require(fixture != null, "Fixture is missing.");
            Text(fixture.id, "fixture ID");
            Require(clubs.ContainsKey(fixture.homeClubId ?? "") && clubs.ContainsKey(fixture.awayClubId ?? ""), "Fixture references unknown club.");
            Require(fixture.homeClubId != fixture.awayClubId, "Fixture teams must be different.");
            Require(fixture.playersPerTeam == 11, "Prototype requires 11 players per team.");
            Require(fixture.benchSize >= 1 && fixture.substitutionLimit >= 0 && fixture.substitutionLimit <= fixture.benchSize,
                "Invalid bench or substitution limits.");
            foreach (var clubId in new[] { fixture.homeClubId, fixture.awayClubId })
            {
                var squad = clubs[clubId].squadPlayerIds.Select(id => players[id]).ToArray();
                Require(squad.Length >= fixture.playersPerTeam + fixture.benchSize, clubId + ": not enough substitutes.");
                Require(squad.Count(p => p.positionId == "gk") >= 2, clubId + ": starting and reserve keeper required.");
                Require(squad.Count(p => p.positionId != "gk") >= 10, clubId + ": ten outfield players required.");
            }
            Require(players[data.userPlayerId].clubId == fixture.homeClubId || players[data.userPlayerId].clubId == fixture.awayClubId,
                "User club must participate in fixture.");
        }

        private static Dictionary<string, T> Index<T>(T[] entries, Func<T, string> key, string label) where T : class
        {
            Require(entries != null && entries.Length > 0, label + ": empty or missing.");
            var result = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                Require(entry != null, label + ": null entry.");
                string id = key(entry);
                Text(id, label + ": ID");
                Require(id.All(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-'), label + ": IDs must be lowercase slugs.");
                Require(!result.ContainsKey(id), label + ": duplicate ID " + id);
                result.Add(id, entry);
            }
            return result;
        }

        private static void References<T>(string[] ids, Dictionary<string, T> definitions, string label)
        {
            Require(ids != null && ids.Length > 0, label + ": missing references.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids)
                Require(id != null && definitions.ContainsKey(id) && seen.Add(id), label + ": unknown or duplicate reference " + id);
        }

        private static void Overrides(ValueOverride[] values, Dictionary<string, ValueDefinition> definitions, string label)
        {
            Require(values != null, label + ": missing override array (use [] for defaults).");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                Require(value != null && value.id != null && definitions.ContainsKey(value.id) && seen.Add(value.id), label + ": unknown or duplicate value.");
                var definition = definitions[value.id];
                Require(value.value >= definition.minimum && value.value <= definition.maximum, label + ": value outside bounds.");
            }
        }

        internal static void Unit(float value, string label) => Range(value, 0, 1, label);
        private static void Range(float value, float min, float max, string label) =>
            Require(!float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max, label + ": out of range.");
        private static void Text(string value, string label) => Require(!string.IsNullOrWhiteSpace(value), label + ": required.");
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new ArgumentException(message);
        }
    }
}
