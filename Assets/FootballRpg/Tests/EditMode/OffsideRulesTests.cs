using System;
using System.Linq;
using FootballRpg.Simulation;
using NUnit.Framework;

namespace FootballRpg.Tests
{
    [Category("FootballVerification")]
    public sealed class OffsideRulesTests
    {
        private static OffsidePhase Phase(double attacker = 40, double ball = 20, double second = 30, double last = 50, int direction = 1, RestartKind? restart = null, long tick = 10, string toucherClub = "A", double radius = 0)
        {
            var passer = new OffsideBody("passer", toucherClub, new PitchPoint(ball * direction, 0), radius);
            var bodies = new[] { passer, new OffsideBody("forward", "A", new PitchPoint(attacker * direction, 0), radius),
                new OffsideBody("keeper", "B", new PitchPoint(second * direction, 0), radius), new OffsideBody("defender", "B", new PitchPoint(last * direction, 0), radius),
                new OffsideBody("support", "A", new PitchPoint(-10 * direction, 0), radius) };
            return new OffsidePhase("play-" + tick, tick, passer, new PitchPoint(ball * direction, 0), direction, bodies, restart);
        }
        [TestCase(40, 20, 30, 50, true)] [TestCase(30, 20, 30, 50, false)] [TestCase(29.9, 20, 30, 50, false)]
        [TestCase(40, 45, 30, 50, false)] [TestCase(40, 40, 30, 50, false)] [TestCase(-.1, -5, -2, -1, false)]
        [TestCase(0, -5, -2, -1, false)] [TestCase(40, 20, 45, 50, false)] [TestCase(50, 50, 30, 45, false)]
        public void PositionTableUsesBallSecondLastOpponentAndHalfInBothDirections(double attacker, double ball, double second, double last, bool offside)
        {
            foreach (int direction in new[] { 1, -1 })
            {
                var p = Phase(attacker, ball, second, last, direction);
                Assert.That(p.Candidates.ContainsKey("forward"), Is.EqualTo(offside));
                Assert.That(p.Candidates.ContainsKey("passer"), Is.False);
                Assert.That(p.OpponentLine, Is.EqualTo(Math.Min(second, last)));
            }
        }
        [Test]
        public void PlayableBodyExtentAndBallEdgeAreUsedWithoutAnArmPosition()
        {
            Assert.That(Phase(20, 20, 10, 50, radius: .3).Candidates.ContainsKey("forward"), Is.True);
            Assert.That(Phase(20, 20, 10, 50, radius: 0).Candidates.ContainsKey("forward"), Is.False);
            Assert.That(Phase(30, 20, 30, 50, radius: .3).Candidates.ContainsKey("forward"), Is.False);
        }
        [TestCase(OffsideInvolvement.None, false)] [TestCase(OffsideInvolvement.PlayingBall, true)]
        [TestCase(OffsideInvolvement.ChallengingOpponent, true)] [TestCase(OffsideInvolvement.ObstructingVision, true)]
        [TestCase(OffsideInvolvement.AttemptAffectingOpponent, true)] [TestCase(OffsideInvolvement.ObviousActionAffectingOpponent, true)]
        public void PositionAloneIsNotAnOffenceAndInterferenceRequiresEvidence(OffsideInvolvement involvement, bool expected)
        {
            var t = new OffsideTracker(); t.RecordTouch(Phase(), OffsideTouch.DeliberatePlay);
            var fact = t.Evaluate("forward", new PitchPoint(-5, 8), 20, involvement, "keeper", true);
            Assert.That(fact != null, Is.EqualTo(expected));
            if (expected) { Assert.That(fact.Location.X, Is.EqualTo(-5)); Assert.That(fact.Phase.TouchTick, Is.EqualTo(10)); Assert.That(fact.Explanation, Does.Contain("second-last")); }
            if (involvement != OffsideInvolvement.PlayingBall)
            {
                Assert.That(t.Evaluate("forward", new PitchPoint(40, 0), 20, involvement, "keeper", false), Is.Null);
                Assert.That(t.Evaluate("forward", new PitchPoint(40, 0), 20, involvement, "support", true), Is.Null);
            }
            Assert.That(t.Evaluate("forward", new PitchPoint(40, 0), 9, involvement, "keeper", true), Is.Null);
        }
        [TestCase(OffsideTouch.DeliberatePlay, false)] [TestCase(OffsideTouch.Deflection, true)] [TestCase(OffsideTouch.Save, true)]
        public void DeliberateOpponentPlayResetsButDeflectionsAndSavesDoNot(OffsideTouch touch, bool expected)
        {
            var t = new OffsideTracker(); var original = Phase(); t.RecordTouch(original, OffsideTouch.DeliberatePlay);
            t.RecordTouch(Phase(tick: 12, toucherClub: "B"), touch);
            var fact = t.Evaluate("forward", new PitchPoint(42, 0), 15, OffsideInvolvement.PlayingBall);
            Assert.That(fact != null, Is.EqualTo(expected));
            if (expected) { Assert.That(fact.Phase, Is.SameAs(original)); Assert.That(fact.Rebound, Is.Not.EqualTo(OffsideRebound.None)); }
        }
        [TestCase(OffsideRebound.GoalFrame)] [TestCase(OffsideRebound.MatchOfficial)]
        public void NeutralReboundsRetainTheOriginalPhase(OffsideRebound rebound)
        {
            var t = new OffsideTracker(); var original = Phase(); t.RecordTouch(original, OffsideTouch.DeliberatePlay); t.RecordNeutralRebound(rebound);
            Assert.That(t.Evaluate("forward", new PitchPoint(42, 0), 15, OffsideInvolvement.PlayingBall).Rebound, Is.EqualTo(rebound));
            Assert.That(t.Phase, Is.SameAs(original));
        }
        [TestCase(RestartKind.ThrowIn, true)] [TestCase(RestartKind.Corner, true)] [TestCase(RestartKind.GoalKick, true)]
        [TestCase(RestartKind.Kickoff, false)] [TestCase(RestartKind.IndirectFreeKick, false)]
        public void RestartExemptionsEndAtTheNextTeammateTouch(RestartKind restart, bool exempt)
        {
            var t = new OffsideTracker(); t.RecordTouch(Phase(restart: restart), OffsideTouch.DeliberatePlay);
            Assert.That(t.Phase.Exempt, Is.EqualTo(exempt));
            Assert.That(t.Evaluate("forward", new PitchPoint(40, 0), 11, OffsideInvolvement.PlayingBall) == null, Is.EqualTo(exempt));
            t.RecordTouch(Phase(tick: 12), OffsideTouch.Deflection);
            Assert.That(t.Evaluate("forward", new PitchPoint(40, 0), 13, OffsideInvolvement.PlayingBall), Is.Not.Null);
        }
        [Test]
        public void NewTeammateTouchReplacesOldPositionAndStoppageClearsEverything()
        {
            var t = new OffsideTracker(); t.RecordTouch(Phase(), OffsideTouch.DeliberatePlay);
            t.RecordTouch(Phase(attacker: 25, tick: 12), OffsideTouch.Deflection);
            Assert.That(t.Evaluate("forward", new PitchPoint(45, 0), 13, OffsideInvolvement.PlayingBall), Is.Null);
            t.RecordTouch(Phase(tick: 15), OffsideTouch.DeliberatePlay); t.EndPhase();
            Assert.That(t.Phase, Is.Null); Assert.That(t.Evaluate("forward", new PitchPoint(40, 0), 16, OffsideInvolvement.PlayingBall), Is.Null);
        }
    }
}
