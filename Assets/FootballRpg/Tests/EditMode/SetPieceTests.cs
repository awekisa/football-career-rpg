using System;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Presentation;
using FootballRpg.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace FootballRpg.Tests
{
    public sealed class SetPieceTests
    {
        private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(50);
        private static void Call(OpenPlayWorld w, string method, params object[] args) => GoalkeeperTests.Call(w, method, args);
        private static void Award(OpenPlayWorld w, RestartKind kind, bool home = true)
        {
            int direction = w.Players.First(p => p.IsHome == home).Direction;
            Call(w, "AwardRestart", kind, home, kind == RestartKind.Corner ? new PitchPoint(52 * direction, 33.5) : kind == RestartKind.Penalty ? new PitchPoint(41.5 * direction, 0) : new PitchPoint(24 * direction, 3));
        }
        private static void Follow(OpenPlayWorld w)
        {
            if (w.RefereeNoticePending) w.AcknowledgeReferee();
            if (w.OffsideNoticePending) w.AcknowledgeOffside();
            var a = w.PendingSetPiece;
            if (a != null) w.CommitSetPieceDuty(w.Restart.Id, a.Duty, a.Duty == SetPieceDuty.MarkPlayer ? a.MarkId : null);
        }
        [TestCase(RestartKind.Corner, "harbour-player-09")]
        [TestCase(RestartKind.DirectFreeKick, "harbour-player-08")]
        [TestCase(RestartKind.IndirectFreeKick, "harbour-player-08")]
        [TestCase(RestartKind.Penalty, "harbour-player-11")]
        public void TeamDataSelectsTakerAndRecordsCoachAssignments(RestartKind kind, string expected)
        {
            var c = PrototypeContent.CreateCareer(); var m = new MatchSession("assigned", 42); using var w = new OpenPlayWorld(c, m); w.StartFixture(); Award(w, kind);
            Assert.That(w.Restart.TakerId, Is.EqualTo(expected)); Assert.That(w.SetPieceAssignments.Count, Is.EqualTo(22));
            Assert.That(w.SetPieceRecords.Count(r => r.RestartId == w.Restart.Id && r.Kind == "Assigned"), Is.EqualTo(22));
            Assert.That(w.SetPieceAssignments[expected].CoachId, Is.EqualTo(c.Clubs[c.Players[expected].ClubId].CoachId));
        }
        [Test]
        public void PlansAreCopiedValidatedAndCoverEveryPosition()
        {
            var definitions = PrototypeContent.Load(); var c = CareerState.Create(definitions); var club = c.Clubs[c.Fixture.HomeClubId];
            definitions.clubs[0].setPieces.penaltyTakers[0] = "not-a-player";
            Assert.That(club.SetPieces.PenaltyTakers[0], Is.EqualTo("harbour-player-11"));
            Assert.Throws<ArgumentException>(() => CareerState.Create(definitions));
            foreach (string role in PositionIds.All) foreach (bool attacking in new[] { true, false })
                Assert.That(Enum.IsDefined(typeof(SetPieceDuty), club.SetPieces.Duty(role, attacking)), Is.True);
        }
        [Test]
        public void UnavailableAssignedTakerFallsBackWithoutReintroducingDismissedPlayer()
        {
            var c = PrototypeContent.CreateCareer(); var m = new MatchSession("fallback", 42); using var w = new OpenPlayWorld(c, m); w.StartFixture();
            var player = w.Players.Single(p => p.Player.Id == "harbour-player-09");
            var v = FoulRules.Evaluate(new FoulIncident("red", player.Player.Id, null, player.Position, 1, FoulKind.Challenge, ContactSeverity.Excessive));
            Call(w, "ApplyCard", v, CardKind.Red); Award(w, RestartKind.Corner);
            Assert.That(w.Restart.TakerId, Is.EqualTo("harbour-player-10")); Assert.That(w.SetPieceAssignments.ContainsKey(player.Player.Id), Is.False);
        }
        [TestCase("harbour-player-01", true)] [TestCase("harbour-player-01", false)]
        [TestCase("harbour-player-04", true)] [TestCase("harbour-player-04", false)]
        [TestCase("harbour-player-11", true)] [TestCase("harbour-player-11", false)]
        public void KeeperDefenderAndForwardChooseAttackingAndDefendingDuties(string user, bool attacking)
        {
            var m = new MatchSession("roles", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m, user); w.StartFixture(); Award(w, RestartKind.Corner, attacking);
            var a = w.PendingSetPiece; Assert.That(a, Is.Not.Null); Assert.That(a.PlayerId, Is.EqualTo(user));
            var clock = w.MatchSeconds; var draws = m.DrawCount; m.Resume(); m.Advance(TimeSpan.FromSeconds(30));
            Assert.That(w.MatchSeconds, Is.EqualTo(clock)); Assert.That(m.DrawCount, Is.EqualTo(draws));
            Follow(w); Assert.That(w.PendingSetPiece, Is.Null);
            var record = w.SetPieceRecords.Last(r => r.Kind == "DutyChosen"); Assert.That(record.Deviated, Is.False);
            for (int i = 0; i < 200 && w.Restart != null; i++) m.Advance(Step);
            Assert.That(w.SetPieceRecords.Any(r => r.Kind == "TakingResult"), Is.True);
        }
        [TestCase(SetPieceDuty.FollowZone)] [TestCase(SetPieceDuty.MarkPlayer)] [TestCase(SetPieceDuty.AttackSpace)] [TestCase(SetPieceDuty.Counterattack)]
        public void DutyChoicesMoveThePlayerAndRecordDeviationOnce(SetPieceDuty duty)
        {
            var m = new MatchSession("deviation", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m, "harbour-player-04"); w.StartFixture(); Award(w, RestartKind.Corner, false);
            var expected = w.PendingSetPiece; string id = w.Restart.Id; string mark = duty == SetPieceDuty.MarkPlayer ? expected.MarkId : null;
            w.CommitSetPieceDuty(id, duty, mark); w.CommitSetPieceDuty(id, duty, mark);
            var r = w.SetPieceRecords.Single(r => r.RestartId == id && r.Kind == "DutyChosen"); Assert.That(r.Deviated, Is.EqualTo(duty != expected.Duty));
            var user = w.Players.Single(p => p.Player.Id == "harbour-player-04"); Assert.That(user.Position.Distance(w.Restart.Spot), Is.GreaterThanOrEqualTo(10.2 - 1e-8));
            Assert.That(m.DrawCount, Is.Zero);
            if (duty == SetPieceDuty.Counterattack) Assert.That(user.Position.X, Is.EqualTo(15));
            Assert.Throws<InvalidOperationException>(() => w.CommitSetPieceDuty(id, duty == SetPieceDuty.Counterattack ? SetPieceDuty.FollowZone : SetPieceDuty.Counterattack));
        }
        [Test]
        public void MarkingRequiresAnActiveOpponent()
        {
            var m = new MatchSession("mark", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m, "harbour-player-04"); w.StartFixture(); Award(w, RestartKind.Corner);
            Assert.Throws<ArgumentException>(() => w.CommitSetPieceDuty(w.Restart.Id, SetPieceDuty.MarkPlayer, "harbour-player-08"));
            Assert.That(w.PendingSetPiece, Is.Not.Null); Assert.That(m.DrawCount, Is.Zero);
        }
        [TestCase(RestartKind.Corner, "low")] [TestCase(RestartKind.Corner, "lofted")]
        [TestCase(RestartKind.DirectFreeKick, "lofted")] [TestCase(RestartKind.IndirectFreeKick, "low")]
        public void DeliveriesReuseFrozenOpenPlayProbabilitiesAndOffsideRules(RestartKind kind, string variant)
        {
            var m = new MatchSession("delivery", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture(); Award(w, kind);
            var restart = w.Restart; var p = w.RestartDeliveries().First(p => p.Variant == variant);
            var again = FootballActionResolver.Preview(p.Situation, FootballActionKind.Cross, variant, p.Receiver);
            Assert.That(p.Percent, Is.EqualTo(again.Percent));
            var result = w.DeliverRestart(restart.Id, p.OptionId);
            Assert.That(result.Action.SuccessProbability, Is.EqualTo(p.Chance)); Assert.That(m.DrawCount, Is.EqualTo(1));
            Assert.That(w.Offside.Phase.Exempt, Is.EqualTo(kind == RestartKind.Corner));
            Assert.That(w.DeliverRestart(restart.Id, p.OptionId), Is.SameAs(result)); Assert.That(m.DrawCount, Is.EqualTo(1));
            Assert.That(w.SetPieceRecords.Count(r => r.Kind == "TakingResult"), Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => w.DeliverRestart(restart.Id, "other"));
        }
        [TestCase("placed")] [TestCase("power")]
        public void PenaltyVariantsUseTheSharedShotAndKeeperPipeline(string variant)
        {
            var m = new MatchSession("penalty-choice", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m, "harbour-player-11"); w.StartFixture(); Award(w, RestartKind.Penalty);
            var id = w.Restart.Id; var p = w.RestartShotPreview(variant); var result = w.ShootRestart(id, variant);
            Assert.That(result.Preview.Percent, Is.EqualTo(p.Percent)); Assert.That(w.ShootRestart(id, variant), Is.SameAs(result));
            Assert.That(w.SetPieceRecords.Last().ActionEventId, Is.EqualTo(result.EventId));
            Assert.Throws<InvalidOperationException>(() => w.ShootRestart(id, variant == "placed" ? "power" : "placed"));
        }
        [Test]
        public void KeeperCannotLeaveGoalLineBeforeDefendingPenaltyRelease()
        {
            var m = new MatchSession("keeper-penalty", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m, "harbour-player-01"); w.StartFixture(); Award(w, RestartKind.Penalty, false);
            w.CommitSetPieceDuty(w.Restart.Id, SetPieceDuty.Counterattack);
            Assert.That(w.Players.Single(p => p.Player.Id == "harbour-player-01").Position, Is.EqualTo(new PitchPoint(-52.5, 0)));
        }
        [Test]
        public void DefensiveAerialContestOpensOrdinaryHeaderChoices()
        {
            var m = new MatchSession("contest", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m, "meadow-player-04"); w.StartFixture(); Award(w, RestartKind.Corner);
            Follow(w); var receiver = w.Players.Single(p => p.Player.Id == "harbour-player-11"); var defender = w.Players.Single(p => p.Player.Id == "meadow-player-04");
            DefenseTests.Place(defender, receiver.Position); typeof(OpenPlayWorld).GetProperty("BallPosition").SetValue(w, receiver.Position);
            typeof(OpenPlayWorld).GetProperty("Restart").SetValue(w, null);
            Call(w, "BeginDeliveryArrival", receiver, 1.8, 14d);
            Assert.That(w.IncomingPlayerId, Is.EqualTo(defender.Player.Id)); Assert.That(w.PendingDecision.Actions.Any(p => p.Kind == FootballActionKind.HeaderPass), Is.True);
            Assert.That(w.PendingDecision.Actions.Any(p => p.Kind == FootballActionKind.Clearance), Is.True);
            var header = w.PendingDecision.Actions.First(p => p.Kind == FootballActionKind.HeaderPass);
            var result = w.CommitFootballDecision(w.PendingDecision.Id, header.OptionId);
            Assert.That(result.Action.SuccessProbability, Is.EqualTo(header.Chance)); Assert.That(m.DrawCount, Is.EqualTo(1));
        }
        [Test]
        public void PresentationCommitsDefaultDutyAndAllowsDeliverySelection()
        {
            var root = new GameObject("set-piece-ui");
            try
            {
                var view = root.AddComponent<PitchPreview>(); view.Initialize(PrototypeContent.CreateCareer()); view.GetComponent<MatchDriver>().enabled = false; view.World.StartFixture(); Award(view.World, RestartKind.Corner);
                view.SyncWorldView(); Assert.That(view.CommitChosenPass(), Is.True); Assert.That(view.World.PendingSetPiece, Is.Null);
                Award(view.World, RestartKind.DirectFreeKick); view.SelectRestartOption(2);
                Assert.That(view.UserRestart, Is.True); Assert.That(view.CommitChosenPass(), Is.True);
                Assert.That(view.World.LastFootballAction.Preview.Variant, Is.EqualTo("lofted")); view.SyncWorldView();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [TestCase(RestartKind.IndirectFreeKick, false)] [TestCase(RestartKind.DirectFreeKick, true)]
        public void TakingOutcomeRespectsDirectGoalRestrictions(RestartKind kind, bool goal)
        {
            var m = new MatchSession("outcome-law", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m); w.StartFixture(); Award(w, kind);
            string restartId = w.Restart.Id;
            w.ReleaseRestart(restartId, w.RestartRecipients().First().Receiver.Id);
            var crossing = PitchBoundary.Cross(new PitchPoint(51, 0), .3, new PitchPoint(53, 0), .3).Value;
            Call(w, "ApplyBoundary", crossing);
            var record = w.SetPieceRecords.Single(r => r.RestartId == restartId && r.Kind == "DeliveryOutcome");
            Assert.That(record.Succeeded, Is.EqualTo(goal)); Assert.That(w.HomeGoals, Is.EqualTo(goal ? 1 : 0));
        }
        [Test]
        public void LaterOffBallInstructionOverridesTheSetPieceDuty()
        {
            var m = new MatchSession("override", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m, "harbour-player-04"); w.StartFixture(); Award(w, RestartKind.Corner);
            Follow(w); var option = w.RestartDeliveries().First(p => p.Receiver.Id == "harbour-player-11" && p.Variant == "lofted");
            w.DeliverRestart(w.Restart.Id, option.OptionId);
            w.SetIntent(OffBallIntent.HoldZone);
            Assert.That(w.SetPieceRecords.Last().Kind, Is.EqualTo("DutyOverridden"));
            var user = w.Players.Single(p => p.Player.Id == "harbour-player-04"); var zone = user.Position;
            m.Advance(Step); Assert.That(user.Target, Is.EqualTo(zone));
        }
    }
}
