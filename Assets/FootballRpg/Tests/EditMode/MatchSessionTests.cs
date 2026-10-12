using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FootballRpg.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace FootballRpg.Tests
{
    public sealed class MatchSessionTests
    {
        private static ActionRequest Choice(string id = "choice-1", float chance = .9f,
            IDictionary<string, string> context = null) =>
            new ActionRequest(id, "harbour-player-08", "pass-to-feet", "harbour-player-07", chance,
                "completed-pass", "interception", context ?? new Dictionary<string, string>
                { ["pressure"] = "0.2", ["distanceMetres"] = "8" });

        [Test]
        public void FixedStepsRetainRemaindersAndDoNotDrawRandomNumbers()
        {
            var session = new MatchSession("match", 42);
            Assert.That(session.Advance(TimeSpan.FromMilliseconds(49)), Is.Zero);
            Assert.That(session.Advance(TimeSpan.FromMilliseconds(102)), Is.EqualTo(3));
            Assert.That(session.Tick, Is.EqualTo(3));
            Assert.That(session.Elapsed, Is.EqualTo(TimeSpan.FromMilliseconds(150)));
            Assert.That(session.Advance(TimeSpan.FromMilliseconds(49)), Is.EqualTo(1));
            Assert.That(session.RandomState, Is.EqualTo(42UL));
            Assert.That(session.DrawCount, Is.Zero);
        }

        [Test]
        public void StepBudgetRetainsBacklogInsteadOfDroppingSimulationTime()
        {
            var session = new MatchSession("match", 42, maxStepsPerAdvance: 3);
            Assert.That(session.Advance(TimeSpan.FromMilliseconds(500)), Is.EqualTo(3));
            session.Advance(TimeSpan.Zero);
            session.Advance(TimeSpan.Zero);
            session.Advance(TimeSpan.Zero);
            Assert.That(session.Tick, Is.EqualTo(10));
        }

        [Test]
        public void PauseDiscardsCatchupAndIdleTimeButCannotConsumeRandomness()
        {
            var session = new MatchSession("match", 17);
            session.TickAdvanced += tick => { if (tick == 3) session.Pause(); };
            session.Advance(TimeSpan.FromSeconds(30), 64);
            ulong before = session.RandomState;
            Assert.That(session.Tick, Is.EqualTo(3));
            Assert.That(session.Advance(TimeSpan.FromDays(2), 64), Is.Zero);
            session.Pause();
            Assert.That(session.RandomState, Is.EqualTo(before));
            Assert.That(session.DrawCount, Is.Zero);
            session.Resume();
            session.Resume();
            Assert.That(session.Advance(TimeSpan.Zero), Is.Zero);
            session.Advance(TimeSpan.FromMilliseconds(50));
            Assert.That(session.Tick, Is.EqualTo(4));
        }

        [Test]
        public void ResolutionWaitsForAnimationAndManualPauseCannotRerollIt()
        {
            var session = new MatchSession("match", 17);
            session.Pause();
            var first = session.BeginResolution(Choice());
            Assert.That(session.State, Is.EqualTo(MatchRunState.Resolving));
            Assert.That(session.Advance(TimeSpan.FromHours(1)), Is.Zero);
            session.Pause();
            Assert.That(session.State, Is.EqualTo(MatchRunState.Paused));
            Assert.Throws<InvalidOperationException>(() => session.CompleteResolution(first.Result.EventId));
            Assert.Throws<InvalidOperationException>(() => session.BeginResolution(Choice("choice-2")));
            Assert.That(session.BeginResolution(Choice()), Is.SameAs(first));
            session.Advance(TimeSpan.FromHours(1));
            session.Resume();
            Assert.That(session.State, Is.EqualTo(MatchRunState.Resolving));
            Assert.That(session.CompleteResolution(first.Result.EventId), Is.True);
            Assert.That(session.CompleteResolution(first.Result.EventId), Is.False);
            Assert.That(session.BeginResolution(Choice()), Is.SameAs(first));
            Assert.That(session.Diagnostics.Count, Is.EqualTo(1));
            Assert.That(session.DrawCount, Is.EqualTo(1));
            session.Advance(TimeSpan.FromMilliseconds(50));
            Assert.That(session.Tick, Is.EqualTo(1));
        }

        [Test]
        public void ConflictingRetryAndInvalidTransitionsDoNotMutateTheSession()
        {
            var session = new MatchSession("match", 42);
            Assert.Throws<InvalidOperationException>(() => session.BeginResolution(Choice()));
            Assert.That(session.RandomState, Is.EqualTo(42UL));
            session.Pause();
            var first = session.BeginResolution(Choice());
            ulong before = session.RandomState;
            Assert.Throws<InvalidOperationException>(() => session.BeginResolution(Choice(chance: .1f)));
            Assert.Throws<InvalidOperationException>(() => session.CompleteResolution("wrong-event"));
            Assert.That(session.RandomState, Is.EqualTo(before));
            Assert.That(session.Diagnostics.Count, Is.EqualTo(1));
            session.CompleteResolution(first.Result.EventId);
            session.Pause();
            var next = session.BeginResolution(Choice("choice-2"));
            Assert.That(session.CompleteResolution(first.Result.EventId), Is.False);
            Assert.That(session.State, Is.EqualTo(MatchRunState.Resolving));
            Assert.That(next.Result.Sequence, Is.EqualTo(2));
        }

        [Test]
        public void GeneratorMatchesKnownSplitMix64VectorsIncludingZeroSeed()
        {
            // Top 24 bits of the published SplitMix64 stream for state=0.
            var session = new MatchSession("vector", 0);
            foreach (uint value in new uint[] { 0xe220a8, 0x6e789e, 0x06c45d })
            {
                session.Pause();
                var result = session.BeginResolution(Choice("choice-" + session.DrawCount));
                Assert.That(result.Result.Roll, Is.EqualTo(value / 16777216f));
                session.CompleteResolution(result.Result.EventId);
            }
        }

        [TestCase(30, 1, 0)]
        [TestCase(60, 1, 5)]
        [TestCase(144, 1, 500)]
        [TestCase(30, 4, 30)]
        [TestCase(144, 16, 5)]
        public void SeedAndTickInputsReplayIdenticallyAcrossFramesSpeedAndPauses(int fps, int speed, int pauseSeconds)
        {
            var expected = Replay(60, 1, 0, 123);
            var actual = Replay(fps, speed, pauseSeconds, 123);
            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void DifferentSeedsChangeTheRecordedDraws()
        {
            CollectionAssert.AreNotEqual(Replay(60, 1, 0, 1), Replay(60, 1, 0, 2));
        }

        private static string[] Replay(int fps, int speed, int pauseSeconds, ulong seed)
        {
            var session = new MatchSession("replay", seed);
            var worldTrace = new List<string>();
            int position = 0;
            session.TickAdvanced += tick =>
            {
                position += 3; // A stand-in world system, advanced only by fixed ticks.
                worldTrace.Add("tick-" + tick + ":x=" + position);
                if (tick % 10 == 0) session.Pause();
            };
            long frame = 0;
            while (session.Tick < 100)
            {
                long before = frame * TimeSpan.TicksPerSecond / fps;
                long after = ++frame * TimeSpan.TicksPerSecond / fps;
                session.Advance(TimeSpan.FromTicks(after - before), speed);
                if (session.State != MatchRunState.Paused) continue;
                session.Advance(TimeSpan.FromSeconds(pauseSeconds), speed);
                var fact = session.BeginResolution(Choice("choice-" + session.Tick));
                session.Advance(TimeSpan.FromSeconds(pauseSeconds + 1), speed);
                worldTrace.Add(fact.ToJsonLine());
                session.CompleteResolution(fact.Result.EventId);
            }
            Assert.That(session.Elapsed, Is.EqualTo(TimeSpan.FromSeconds(5)));
            Assert.That(session.DrawCount, Is.EqualTo(10));
            return worldTrace.ToArray();
        }

        [Test]
        public void DiagnosticsFreezeContextAndProduceCultureIndependentEscapedJson()
        {
            var context = new Dictionary<string, string> { ["z"] = "quotes \" newline\n", ["a"] = "first" };
            var request = Choice(context: context);
            context["a"] = "mutated";
            var session = new MatchSession("session", 0);
            session.Pause();
            var diagnostic = session.BeginResolution(request);
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var line = diagnostic.ToJsonLine();
                var parsed = JsonUtility.FromJson<DiagnosticProbe>(line);
                Assert.That(parsed.eventId, Is.EqualTo("session:action-1"));
                Assert.That(parsed.chance, Is.EqualTo(.9f));
                Assert.That(parsed.roll, Is.EqualTo(diagnostic.Result.Roll));
                Assert.That(line, Does.Not.Contain("\n"));
                Assert.That(line, Does.Contain("\\u000a"));
                Assert.That(line, Does.Contain("\"a\":\"first\""));
                Assert.That(line.IndexOf("\"a\":", StringComparison.Ordinal), Is.LessThan(line.IndexOf("\"z\":", StringComparison.Ordinal)));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [TestCase(0f, false)]
        [TestCase(1f, true)]
        public void CertainOutcomesUseOneDrawAndMatchTheirOutcomeId(float chance, bool success)
        {
            var session = new MatchSession("match", 99);
            session.Pause();
            var result = session.BeginResolution(Choice(chance: chance)).Result;
            Assert.That(result.Succeeded, Is.EqualTo(success));
            Assert.That(result.OutcomeId, Is.EqualTo(success ? "completed-pass" : "interception"));
            Assert.That(session.DrawCount, Is.EqualTo(1));
        }

        [Test]
        public void InvalidInputsDoNotAdvanceTime()
        {
            var session = new MatchSession("match", 42);
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Advance(TimeSpan.FromTicks(-1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Advance(TimeSpan.FromSeconds(1), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Advance(TimeSpan.FromSeconds(1), 65));
            Assert.Throws<OverflowException>(() => session.Advance(TimeSpan.MaxValue, 64));
            Assert.That(session.Advance(TimeSpan.Zero), Is.Zero);
            Assert.Throws<ArgumentException>(() => Choice(chance: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MatchSession("match", 1, TimeSpan.Zero));
            Assert.That(session.Tick, Is.Zero);
            Assert.That(session.DrawCount, Is.Zero);
        }

        [Test]
        public void RecursiveOrFailedTickCallbackStopsFurtherAdvancement()
        {
            var session = new MatchSession("match", 1);
            session.TickAdvanced += _ => session.Advance(TimeSpan.FromSeconds(1));
            Assert.Throws<InvalidOperationException>(() => session.Advance(TimeSpan.FromSeconds(1)));
            Assert.That(session.Tick, Is.EqualTo(1));
            Assert.That(session.State, Is.EqualTo(MatchRunState.Paused));
            Assert.That(session.DrawCount, Is.Zero);
        }

        [Serializable]
        private sealed class DiagnosticProbe
        {
            public string eventId;
            public float chance;
            public float roll;
        }
    }
}
