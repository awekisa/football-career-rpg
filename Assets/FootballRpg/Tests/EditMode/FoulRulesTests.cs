using System;
using FootballRpg.Simulation;
using NUnit.Framework;

namespace FootballRpg.Tests
{
    public sealed class FoulRulesTests
    {
        [TestCase(ContactSeverity.Fair, CardKind.None, false)]
        [TestCase(ContactSeverity.Careless, CardKind.None, true)]
        [TestCase(ContactSeverity.Reckless, CardKind.Yellow, true)]
        [TestCase(ContactSeverity.Excessive, CardKind.Red, true)]
        public void ContactSeverityControlsSanction(ContactSeverity severity, CardKind card, bool foul)
        {
            var v = FoulRules.Evaluate(new FoulIncident("a", "d", "a", new PitchPoint(0, 0), 1, FoulKind.Challenge, severity));
            Assert.That(v != null, Is.EqualTo(foul));
            if (v != null) { Assert.That(v.Card, Is.EqualTo(card)); Assert.That(v.Restart, Is.EqualTo(RestartKind.DirectFreeKick)); }
        }
        [TestCase(1, -36, 20.16, true)] [TestCase(-1, 36, -20.16, true)]
        [TestCase(1, -35.99, 0, false)] [TestCase(1, -40, 20.17, false)]
        [TestCase(-1, -40, 0, false)]
        public void PenaltyAreaLinesAndDirection(int direction, double x, double z, bool penalty)
        {
            var v = FoulRules.Evaluate(new FoulIncident("a", "d", "a", new PitchPoint(x, z), direction, FoulKind.Challenge));
            Assert.That(v.Restart, Is.EqualTo(penalty ? RestartKind.Penalty : RestartKind.DirectFreeKick));
            if (penalty) Assert.That(v.Spot, Is.EqualTo(new PitchPoint(-41.5 * direction, 0)));
        }
        [TestCase(FoulKind.DangerousPlay, RestartKind.IndirectFreeKick)]
        [TestCase(FoulKind.Impeding, RestartKind.IndirectFreeKick)]
        [TestCase(FoulKind.KeeperHandling, RestartKind.IndirectFreeKick)]
        [TestCase(FoulKind.KeeperTimeLimit, RestartKind.Corner)]
        [TestCase(FoulKind.Handball, RestartKind.Penalty)]
        public void NonChallengeOffencesHaveAppropriateRestarts(FoulKind kind, RestartKind restart)
        {
            var v = FoulRules.Evaluate(new FoulIncident("a", "d", "a", new PitchPoint(-49, -3), 1, kind));
            Assert.That(v.Restart, Is.EqualTo(restart));
            if (restart == RestartKind.IndirectFreeKick) Assert.That(v.Spot.X, Is.EqualTo(-47));
        }
        [TestCase(false, true, CardKind.Red)] [TestCase(true, true, CardKind.Yellow)] [TestCase(true, false, CardKind.Red)]
        public void ObviousOpportunityPenaltyMitigation(bool inside, bool playsBall, CardKind card)
        {
            var v = FoulRules.Evaluate(new FoulIncident("a", "d", "a", new PitchPoint(inside ? -40 : -30, 0), 1, FoulKind.Challenge,
                attemptToPlayBall: playsBall, obviousOpportunity: true));
            Assert.That(v.Card, Is.EqualTo(card));
        }
        [Test]
        public void HandballDenialIsRedAndKeeperInsideHandlingIsNotACard()
        {
            foreach (var kind in new[] { FoulKind.Handball, FoulKind.KeeperHandling })
            {
                var v = FoulRules.Evaluate(new FoulIncident("a", "d", "a", new PitchPoint(-40, 0), 1, kind, obviousOpportunity: true));
                Assert.That(v.Card, Is.EqualTo(kind == FoulKind.Handball ? CardKind.Red : CardKind.None));
            }
        }
        [Test]
        public void DeadBallCannotGenerateAFreeKickAndAdvantageRemovesOnlyTacticalCaution()
        {
            var incident = new FoulIncident("a", "d", "a", new PitchPoint(0, 0), 1, FoulKind.Challenge, promisingAttack: true);
            Assert.That(FoulRules.Evaluate(incident, false), Is.Null);
            Assert.That(FoulRules.AdvantageCard(FoulRules.Evaluate(incident)), Is.EqualTo(CardKind.None));
            var reckless = new FoulIncident("b", "d", "a", new PitchPoint(0, 0), 1, FoulKind.Challenge, ContactSeverity.Reckless, promisingAttack: true);
            Assert.That(FoulRules.AdvantageCard(FoulRules.Evaluate(reckless)), Is.EqualTo(CardKind.Yellow));
        }
        [Test]
        public void DisciplineIsIdempotentAndSecondYellowDismisses()
        {
            var d = new DisciplineRecord();
            Assert.That(d.Apply("one", "player", CardKind.Yellow), Is.EqualTo(CardKind.Yellow));
            Assert.That(d.Apply("one", "player", CardKind.Yellow), Is.EqualTo(CardKind.None));
            Assert.That(d.Yellows("player"), Is.EqualTo(1));
            Assert.That(d.Apply("two", "player", CardKind.Yellow), Is.EqualTo(CardKind.SecondYellow));
            Assert.That(d.IsDismissed("player"), Is.True);
            Assert.That(d.Apply("three", "player", CardKind.Yellow), Is.EqualTo(CardKind.None));
        }
        [Test]
        public void InvalidEvidenceCannotReachTheReferee()
        {
            Assert.Throws<ArgumentException>(() => new FoulIncident("a", "d", "d", new PitchPoint(0, 0), 1, FoulKind.Challenge));
            Assert.Throws<ArgumentException>(() => new FoulIncident("a", "d", "a", new PitchPoint(double.NaN, 0), 1, FoulKind.Challenge));
        }
    }
}
