using System;
using System.Collections.Generic;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Simulation;
using NUnit.Framework;

namespace FootballRpg.Tests
{
    public sealed class GameDataTests
    {
        [Test]
        public void ShippedFixtureCoversEveryPositionAndTwoCompleteSquads()
        {
            var data = PrototypeContent.Load();
            CollectionAssert.AreEquivalent(PositionIds.All, data.positions.Select(p => p.id));
            CollectionAssert.IsSubsetOf(PositionIds.All, data.players.Select(p => p.positionId).Distinct());
            Assert.That(data.players.Length, Is.EqualTo(36));
            Assert.That(data.players.Single(p => p.id == data.userPlayerId).age, Is.EqualTo(17));
            foreach (var club in data.clubs)
            {
                Assert.That(club.squadPlayerIds.Length, Is.EqualTo(18));
                Assert.That(data.players.Count(p => p.clubId == club.id && p.positionId == "gk"), Is.EqualTo(2));
            }
            Assert.That(data.actions.Any(a => a.id == "save"), Is.True);
            Assert.That(data.actions.Any(a => a.id == "pass-to-feet"), Is.True);
        }

        [Test]
        public void ConditionUpdatesLeavePersonalityOtherPlayersAndOtherSessionsUntouched()
        {
            var definitions = PrototypeContent.Load();
            var session = CareerState.Create(definitions);
            var other = CareerState.Create(definitions);
            var originalTraits = session.UserPlayer.PersonalityTraits.ToArray();
            var teammate = session.Players.Values.First(p => p.Id != session.UserPlayerId);
            float teammateConfidence = teammate.Confidence;
            session.SetPlayerCondition(session.UserPlayerId, .1f, .2f, .9f);
            Assert.That(session.UserPlayer.Confidence, Is.EqualTo(.1f));
            Assert.That(session.UserPlayer.Fatigue, Is.EqualTo(.9f));
            CollectionAssert.AreEqual(originalTraits, session.UserPlayer.PersonalityTraits.ToArray());
            Assert.That(other.UserPlayer.Confidence, Is.EqualTo(.65f));
            Assert.That(teammate.Confidence, Is.EqualTo(teammateConfidence));
            Assert.That(definitions.players.Single(p => p.id == definitions.userPlayerId).confidence, Is.EqualTo(.65f));
        }

        [Test]
        public void RuntimeOwnsCopiesOfMutableAuthoringDataAndExposesReadOnlyCollections()
        {
            var definitions = PrototypeContent.Load();
            var session = CareerState.Create(definitions);
            var seed = definitions.players.Single(p => p.id == session.UserPlayerId);
            int oldValue = session.UserPlayer.FootballAttributes[seed.attributes[0].id];
            string firstSquadId = session.Clubs["harbour"].SquadPlayerIds[0];
            seed.attributes[0].value = 99;
            seed.name = "Edited asset";
            definitions.clubs[0].squadPlayerIds[0] = "broken";
            definitions.coaches[0].disciplinePreference = 0;
            definitions.fixture.benchSize = 99;
            Assert.That(session.UserPlayer.FootballAttributes[seed.attributes[0].id], Is.EqualTo(oldValue));
            Assert.That(session.UserPlayer.Name, Is.Not.EqualTo("Edited asset"));
            Assert.That(session.Clubs["harbour"].SquadPlayerIds[0], Is.EqualTo(firstSquadId));
            Assert.That(session.Coaches["harbour-coach"].DisciplinePreference, Is.EqualTo(.8f));
            Assert.That(session.Fixture.BenchSize, Is.EqualTo(7));
            Assert.Throws<NotSupportedException>(() =>
                ((IDictionary<string, int>)session.UserPlayer.FootballAttributes)["finishing"] = 99);
        }

        [Test]
        public void DefaultSkillsAndOverridesBothReachTheCanonicalPlayer()
        {
            var data = PrototypeContent.Load();
            var career = CareerState.Create(data);
            var user = career.UserPlayer;
            Assert.That(user.FootballAttributes.Count, Is.EqualTo(data.footballAttributes.Length));
            Assert.That(user.FootballAttributes["vision"], Is.EqualTo(42));
            Assert.That(user.FootballAttributes["penalties"], Is.EqualTo(30));
            Assert.That(career.Players[career.Clubs[user.ClubId].SquadPlayerIds[7]], Is.SameAs(user));
            Assert.That(user.WeeklyWageMinor, Is.EqualTo(15000));
            Assert.That(user.CurrencyCode, Is.EqualTo("EUR"));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-.1f)]
        [TestCase(1.1f)]
        public void InvalidConditionUpdateIsAtomic(float invalidFatigue)
        {
            var session = PrototypeContent.CreateCareer();
            Assert.Throws<ArgumentException>(() => session.SetPlayerCondition(session.UserPlayerId, .1f, .2f, invalidFatigue));
            Assert.That(session.UserPlayer.Confidence, Is.EqualTo(.65f));
            Assert.That(session.UserPlayer.Morale, Is.EqualTo(.7f));
        }

        [TestCase("duplicate")]
        [TestCase("unknown-attribute")]
        [TestCase("missing-position")]
        [TestCase("unknown-coach")]
        [TestCase("membership")]
        [TestCase("bench")]
        [TestCase("keeper")]
        [TestCase("bounds")]
        [TestCase("trait-confusion")]
        [TestCase("schema")]
        [TestCase("same-team")]
        [TestCase("nan")]
        [TestCase("null-player")]
        public void InvalidContentFailsBeforeACareerCanBeCreated(string defect)
        {
            var data = PrototypeContent.Load();
            switch (defect)
            {
                case "duplicate": data.players[1].id = data.players[0].id; break;
                case "unknown-attribute": data.actions[0].attributeIds[0] = "nonexistent"; break;
                case "missing-position": data.positions = data.positions.Where(p => p.id != "gk").ToArray(); break;
                case "unknown-coach": data.clubs[0].coachId = "missing"; break;
                case "membership": data.players[0].clubId = "meadow"; break;
                case "bench": data.fixture.benchSize = 8; break;
                case "keeper": data.players[11].positionId = "cb"; break;
                case "bounds": data.players[0].attributes[0].value = 101; break;
                case "trait-confusion": data.players[0].traits[0].id = "finishing"; break;
                case "schema": data.schemaVersion = 99; break;
                case "same-team": data.fixture.awayClubId = data.fixture.homeClubId; break;
                case "nan": data.players[0].heightCm = float.NaN; break;
                case "null-player": data.players[0] = null; break;
            }
            Assert.Throws<ArgumentException>(() => CareerState.Create(data));
        }

        [TestCase("")]
        [TestCase("{}")]
        [TestCase("not json")]
        public void MalformedOrIncompleteJsonFailsWithAUsefulException(string json)
        {
            Assert.Throws<ArgumentException>(() => PrototypeContent.Parse(json));
        }

        [TestCase(0f, 0f, false)]
        [TestCase(1f, .999f, true)]
        [TestCase(.9f, .89f, true)]
        [TestCase(.9f, .9f, false)]
        public void ActionFactsHaveOneConsistentProbabilityOutcome(float chance, float roll, bool success)
        {
            var fact = new ActionResolved("event-1", "player-1", 1, "pass-to-feet", "player-2", chance, roll, "resolved");
            Assert.That(fact.Succeeded, Is.EqualTo(success));
        }

        [Test]
        public void EventFactsCarryCausalityAndAllowFailureAndNonSelection()
        {
            var activity = new ActivityResolved("e-1", "p-1", 1, "training", "monday-am", AttendanceOutcome.Skipped);
            var progression = new ProgressionApplied("e-2", "p-1", 2, "penalties", "e-1", -20, 31, 30);
            var selection = new SelectionAnnounced("e-3", "p-1", 3, "match-1", "coach-1", null, SelectionOutcome.NotSelected);
            var relationship = new RelationshipChanged("e-4", "p-1", 4, "coach-1", "professional-trust", "e-1", 50, 45);
            Assert.That(activity.Attendance, Is.EqualTo(AttendanceOutcome.Skipped));
            Assert.That(progression.CauseEventId, Is.EqualTo(activity.EventId));
            Assert.That(progression.XpDelta, Is.LessThan(0));
            Assert.That(selection.AssignedPositionId, Is.Null);
            Assert.That(relationship.DimensionId, Is.EqualTo("professional-trust"));
            Assert.Throws<ArgumentException>(() => new ActionResolved("e-5", "p-1", 5, "shoot", "goal", 1.2f, .5f, "goal"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ActionResolved("e-5", "p-1", 5, "shoot", "goal", 1f, 1f, "goal"));
        }
    }
}
