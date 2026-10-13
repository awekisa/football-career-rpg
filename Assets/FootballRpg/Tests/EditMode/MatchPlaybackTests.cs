using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Presentation;
using FootballRpg.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace FootballRpg.Tests
{
    [Category("FootballVerification")]
    public sealed class MatchPlaybackTests
    {
        private static void Call(OpenPlayWorld w, string name, params object[] args) => GoalkeeperTests.Call(w, name, args);
        private static void Position(FootballerMotion p, double x, double z = 0) => typeof(FootballerMotion).GetProperty("Position").SetValue(p, new PitchPoint(x, z));
        private static void Property(OpenPlayWorld w, string name, object value) => typeof(OpenPlayWorld).GetProperty(name).SetValue(w, value);
        private static CareerState Career(string role)
        {
            var d = PrototypeContent.Load(); d.userPlayerId = role == "gk" ? "harbour-player-01" : "harbour-player-08";
            d.players.Single(p => p.id == d.userPlayerId).positionId = role; return CareerState.Create(d);
        }
        [TestCase("gk")] [TestCase("cb")] [TestCase("lb")] [TestCase("rb")] [TestCase("lwb")] [TestCase("rwb")]
        [TestCase("dm")] [TestCase("cm")] [TestCase("am")] [TestCase("lm")] [TestCase("rm")]
        [TestCase("lw")] [TestCase("rw")] [TestCase("st")]
        public void EveryRoleAnticipatesReachableThreatAndIgnoresRemotePlay(string role)
        {
            var c = Career(role); var m = new MatchSession("role", 42); using var w = new OpenPlayWorld(c, m, c.UserPlayerId);
            var viewing = new MatchPlayback(w); viewing.SetMode(MatchViewingMode.Highlights);
            Call(w, "ClearPlay"); var user = w.Players.Single(p => p.Player.Id == c.UserPlayerId);
            var opponent = w.Players.Single(p => p.Player.Id == "meadow-player-08");
            Position(user, role == "gk" ? -48 : 0); Position(opponent, role == "gk" ? -25 : 10); Call(w, "GiveBall", opponent);
            Assert.That(w.HasPendingDecision, Is.False); Assert.That(w.ViewingFocus, Is.EqualTo(HighlightFocus.DefensiveThreat)); Assert.That(viewing.EffectiveSpeed, Is.EqualTo(1));
            Position(opponent, 48, 30); Position(user, -40, -25); Call(w, "GiveBall", opponent);
            Assert.That(w.ViewingFocus, Is.EqualTo(HighlightFocus.Quiet)); Assert.That(viewing.EffectiveSpeed, Is.EqualTo(32));
            Assert.That(m.DrawCount, Is.Zero);
        }
        [TestCase("gk")] [TestCase("cb")] [TestCase("lb")] [TestCase("rb")] [TestCase("lwb")] [TestCase("rwb")]
        [TestCase("dm")] [TestCase("cm")] [TestCase("am")] [TestCase("lm")] [TestCase("rm")]
        [TestCase("lw")] [TestCase("rw")] [TestCase("st")]
        public void SameSeedAndChoicesProduceIdenticalFullFastAndHighlightMatches(string role)
        {
            var full = Replay(role, MatchViewingMode.FullMatch, 1);
            CollectionAssert.AreEqual(full, Replay(role, MatchViewingMode.FullMatch, 8));
            CollectionAssert.AreEqual(full, Replay(role, MatchViewingMode.Highlights, 1));
        }
        private static string[] Replay(string role, MatchViewingMode mode, int speed)
        {
            var c = Career(role); var m = new MatchSession("replay", 42); using var w = new OpenPlayWorld(c, m, c.UserPlayerId);
            w.StartFixture(new CompetitionRules(12, 0, .1), new MatchFitnessRules(injuryRatePerSecond: .002));
            var playback = new MatchPlayback(w); playback.SetMode(mode); playback.SetFullMatchSpeed(speed);
            for (int frame = 0; frame < 30000 && w.FixturePhase != FixturePhase.FullTime; frame++)
            {
                Choose(w, c.UserPlayerId); playback.Advance(TimeSpan.FromMilliseconds(17));
            }
            Assert.That(w.FixturePhase, Is.EqualTo(FixturePhase.FullTime));
            var result = new List<string> { m.Tick + ":" + m.RandomState + ":" + w.HomeGoals + ":" + w.AwayGoals + ":" + w.DecisionCount,
                w.BallState + ":" + w.BallPosition.X.ToString("R", CultureInfo.InvariantCulture) + ":" + w.BallPosition.Z.ToString("R", CultureInfo.InvariantCulture) };
            result.AddRange(m.Diagnostics.Select(d => d.ToJsonLine()));
            result.AddRange(w.FixtureEvents.Select(e => e.Id + e.Kind + e.Tick + e.IsHome + e.Restart));
            result.AddRange(w.ParticipationEvents.Select(e => e.Kind + e.PlayerId + e.ReplacementId + e.Severity + e.Tick));
            result.AddRange(w.SetPieceRecords.Select(e => e.RestartId + e.PlayerId + e.Kind + e.Choice + e.Succeeded + e.Tick));
            result.AddRange(w.Players.OrderBy(p => p.Player.Id).Select(p => p.Player.Id + ":" + p.Position.X.ToString("R", CultureInfo.InvariantCulture) + ":" + p.Position.Z.ToString("R", CultureInfo.InvariantCulture)));
            result.AddRange(c.Players.Values.Select(p => p.Id + ":" + p.Fatigue.ToString("R", CultureInfo.InvariantCulture) + ":" + p.Confidence.ToString("R", CultureInfo.InvariantCulture) + ":" + p.Morale.ToString("R", CultureInfo.InvariantCulture) + ":" + p.InjuryDaysRemaining));
            result.AddRange(c.Readiness.Changes.Select(e => e.PlayerId + ":" + e.CauseId + ":" + e.Reason + ":" +
                e.FatigueDelta.ToString("R", CultureInfo.InvariantCulture) + ":" + e.ConfidenceDelta.ToString("R", CultureInfo.InvariantCulture) + ":" +
                e.MoraleDelta.ToString("R", CultureInfo.InvariantCulture) + ":" + e.InjuryDaysDelta));
            Assert.That(c.Progression.Changes.Count, Is.GreaterThan(0));
            Assert.That(c.Progression.Changes.Count, Is.EqualTo(m.Diagnostics.Count), "Every committed football action is attributed once.");
            Assert.That(c.Progression.Changes.Select(e => e.EventId).Distinct().Count(), Is.EqualTo(c.Progression.Changes.Count));
            result.AddRange(c.Players.Values.SelectMany(p => c.Progression.ForPlayer(p.Id).Select(skill => p.Id + ":" + skill.Key + ":" + skill.Value.Level + ":" + skill.Value.TotalXp.ToString("R", CultureInfo.InvariantCulture))));
            result.AddRange(c.Progression.Changes.SelectMany(e => e.Deltas.Select(d => e.EventId + ":" + e.PlayerId + ":" + d.Key + ":" + d.Value.ToString("R", CultureInfo.InvariantCulture))));
            return result.ToArray();
        }
        internal static void Choose(OpenPlayWorld w, string user)
        {
            if (w.RefereeNoticePending) w.AcknowledgeReferee();
            if (w.OffsideNoticePending) w.AcknowledgeOffside();
            if (w.FixturePhase == FixturePhase.HalfTime) w.ContinueSecondHalf();
            if (w.PendingSetPiece != null) { var a = w.PendingSetPiece; w.CommitSetPieceDuty(w.Restart.Id, a.Duty, a.Duty == SetPieceDuty.MarkPlayer ? a.MarkId : null); }
            if (w.Restart?.TakerId == user)
            {
                if (w.Restart.Kind == RestartKind.Penalty) w.ShootRestart(w.Restart.Id);
                else w.ReleaseRestart(w.Restart.Id, w.RestartRecipients().OrderByDescending(p => p.Chance).First().Receiver.Id);
            }
            else if (w.PendingKeeper != null) w.CommitKeeperDecision(w.PendingKeeper.Id, w.PendingKeeper.Options.First().OptionId);
            else if (w.PendingDefense != null) w.CommitDefense(w.PendingDefense.Id, w.PendingDefense.Options.First().Kind);
            else if (w.PendingDecision != null)
            {
                var d = w.PendingDecision;
                if (d.Options.Count > 0) { var p = d.Options.OrderByDescending(x => x.Chance).First(); w.CommitDecision(d.Id, p.Receiver.Id, p.Kind); }
                else w.CommitFootballDecision(d.Id, d.Actions.First().OptionId);
            }
        }
        [TestCase(OffBallIntent.Press)] [TestCase(OffBallIntent.PersonalMark)] [TestCase(OffBallIntent.Track)]
        [TestCase(OffBallIntent.ForwardRun)] [TestCase(OffBallIntent.FindSpace)] [TestCase(OffBallIntent.Support)] [TestCase(OffBallIntent.Counterattack)] [TestCase(OffBallIntent.HoldZone)]
        public void OffBallInstructionsWidenMeaningfulInvolvement(OffBallIntent intent)
        {
            var c = Career("st"); var m = new MatchSession("intent", 42); using var w = new OpenPlayWorld(c, m, c.UserPlayerId);
            Call(w, "ClearPlay"); var user = w.Players.Single(p => p.Player.Id == c.UserPlayerId); Position(user, 0);
            bool defending = intent == OffBallIntent.Press || intent == OffBallIntent.PersonalMark || intent == OffBallIntent.Track || intent == OffBallIntent.HoldZone;
            var owner = w.Players.Single(p => p.Player.Id == (defending ? "meadow-player-08" : "harbour-player-07"));
            Position(owner, defending ? 19 : 28); Call(w, "GiveBall", owner);
            Assert.That(w.ViewingFocus, Is.EqualTo(HighlightFocus.Quiet));
            w.SetIntent(intent, defending ? owner.Player.Id : null);
            Assert.That(w.ViewingFocus, Is.EqualTo(HighlightFocus.MovementIntent)); Assert.That(m.DrawCount, Is.Zero);
        }
        [Test]
        public void LargeFastFrameYieldsAtFirstNewThreatWithoutRunningThroughIt()
        {
            var c = Career("gk"); var m = new MatchSession("arrival", 42); using var w = new OpenPlayWorld(c, m, c.UserPlayerId);
            var playback = new MatchPlayback(w); playback.SetMode(MatchViewingMode.Highlights); playback.SetQuietSpeed(64);
            var owner = w.Players.Single(p => p.Player.Id == "meadow-player-08"); Position(owner, 35); Call(w, "GiveBall", owner);
            Assert.That(playback.Focus, Is.EqualTo(HighlightFocus.Quiet));
            m.TickAdvanced += tick => { if (tick == 5) { Position(owner, -25); Call(w, "GiveBall", owner); } };
            playback.Advance(TimeSpan.FromSeconds(5));
            Assert.That(m.Tick, Is.EqualTo(5)); Assert.That(playback.Focus, Is.EqualTo(HighlightFocus.DefensiveThreat)); Assert.That(m.State, Is.EqualTo(MatchRunState.Running));
            playback.Advance(TimeSpan.FromMilliseconds(49)); Assert.That(m.Tick, Is.EqualTo(5));
            playback.Advance(TimeSpan.FromMilliseconds(1)); Assert.That(m.Tick, Is.EqualTo(6));
        }
        [TestCase("pass")] [TestCase("keeper")] [TestCase("restart")] [TestCase("setpiece")]
        [TestCase("defense")] [TestCase("halftime")] [TestCase("fulltime")]
        public void RequiredStopsCannotBeBypassedBySpeedModeOrDirectResume(string kind)
        {
            string user = kind == "keeper" ? "harbour-player-01" : kind == "restart" ? "harbour-player-11" : "harbour-player-08";
            var m = new MatchSession("stop", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m, user, true);
            if (kind == "restart" || kind == "setpiece")
            { w.StartFixture(); if (kind == "setpiece") Call(w, "AwardRestart", RestartKind.Corner, false, new PitchPoint(-52, 33.5)); }
            if (kind == "halftime" || kind == "fulltime") { Call(w, "ClearPlay"); Property(w, "FixturePhase", kind == "halftime" ? FixturePhase.HalfTime : FixturePhase.FullTime); }
            if (kind == "defense")
            {
                Call(w, "ClearPlay"); var defender = w.Players.Single(p => p.Player.Id == user); var attacker = w.Players.Single(p => p.Player.Id == "meadow-player-08");
                Position(defender, 0); Position(attacker, 1); Call(w, "GiveBall", attacker); Call(w, "TryOpenDefense", attacker);
                Assert.That(w.PendingDefense, Is.Not.Null);
            }
            var playback = new MatchPlayback(w); playback.SetFullMatchSpeed(8); playback.SetMode(MatchViewingMode.Highlights); playback.SetQuietSpeed(64);
            long tick = m.Tick, draws = m.DrawCount; m.Resume();
            Assert.That(playback.Advance(TimeSpan.FromHours(1)), Is.Zero); Assert.That(m.Tick, Is.EqualTo(tick)); Assert.That(m.DrawCount, Is.EqualTo(draws)); Assert.That(m.State, Is.EqualTo(MatchRunState.Paused));
            playback.SetMode(MatchViewingMode.FullMatch); Assert.That(playback.Advance(TimeSpan.FromHours(1)), Is.Zero);
        }
        [Test]
        public void InspectionAndModeChangesPreserveFrozenProbabilitiesAndDropOldElapsedTime()
        {
            var m = new MatchSession("inspection", 42); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m, "harbour-player-08", true);
            var playback = new MatchPlayback(w); var pending = w.PendingDecision; var chances = pending.Options.Select(p => p.Chance).ToArray();
            playback.Advance(TimeSpan.FromMinutes(5)); playback.SetMode(MatchViewingMode.Highlights); playback.SetQuietSpeed(64);
            CollectionAssert.AreEqual(chances, w.PendingDecision.Options.Select(p => p.Chance)); Assert.That(w.PendingDecision, Is.SameAs(pending));
            var p = pending.Options.First(); w.CommitDecision(pending.Id, p.Receiver.Id, p.Kind);
            Assert.That(playback.Focus, Is.EqualTo(HighlightFocus.BallInvolvement));
            playback.Advance(TimeSpan.FromMilliseconds(49)); Assert.That(m.Tick, Is.Zero);
            playback.Advance(TimeSpan.FromMilliseconds(1)); Assert.That(m.Tick, Is.EqualTo(1)); Assert.That(m.DrawCount, Is.EqualTo(1));
        }
        [Test]
        public void IncomingPassIsShownBeforeTheUserMustChoose()
        {
            var c = Career("cm"); var m = new MatchSession("incoming", 42); using var w = new OpenPlayWorld(c, m, c.UserPlayerId);
            var owner = w.Players.Single(p => p.Player.Id == "harbour-player-07"); Call(w, "GiveBall", owner);
            var p = w.PreviewPass(c.UserPlayerId, PassKind.ToFeet); var result = PassResolver.Commit(m, "incoming", p, true);
            Call(w, "BeginFlight", result, owner);
            // Deterministic seed delivers this pass to the controlled receiver.
            Assert.That(w.IntendedReceiverId, Is.EqualTo(c.UserPlayerId)); Assert.That(w.PendingDecision, Is.Null);
            Assert.That(w.ViewingFocus, Is.EqualTo(HighlightFocus.BallInvolvement));
        }
        [Test]
        public void BenchSpectatorFastForwardsButShowsImportantEventsAndHalftimeEntry()
        {
            var c = PrototypeContent.CreateCareer(); var m = new MatchSession("spectator", 42); using var w = new OpenPlayWorld(c, m, "harbour-player-17");
            w.StartFixture(new CompetitionRules(3, 0, .1), new MatchFitnessRules(injuryRatePerSecond: 0));
            var p = new MatchPlayback(w); p.SetMode(MatchViewingMode.Highlights);
            Assert.That(p.Focus, Is.EqualTo(HighlightFocus.Quiet)); Call(w, "RecordFixture", "Goal", (bool?)true, null);
            Assert.That(p.Focus, Is.EqualTo(HighlightFocus.MatchEvent)); Assert.That(p.EffectiveSpeed, Is.EqualTo(1));
            for (int i = 0; i < 5000 && w.FixturePhase != FixturePhase.HalfTime; i++) { if (w.RefereeNoticePending) w.AcknowledgeReferee(); if (w.OffsideNoticePending) w.AcknowledgeOffside(); p.Advance(TimeSpan.FromMilliseconds(50)); }
            Assert.That(w.FixturePhase, Is.EqualTo(FixturePhase.HalfTime)); Assert.That(w.UserParticipation, Is.EqualTo(ParticipationState.Playing)); Assert.That(p.Focus, Is.EqualTo(HighlightFocus.Interval));
        }
        [Test]
        public void FullSpeedAndQuietSpeedRetainFractionalFramesAndBoundWork()
        {
            var m = new MatchSession("speed", 42, maxStepsPerAdvance: 10); using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), m);
            var p = new MatchPlayback(w); p.SetFullMatchSpeed(4);
            Assert.That(p.Advance(TimeSpan.FromMilliseconds(10)), Is.Zero); Assert.That(p.Advance(TimeSpan.FromMilliseconds(15)), Is.EqualTo(2));
            p.SetMode(MatchViewingMode.Highlights); Assert.That(p.Advance(TimeSpan.FromHours(1)), Is.EqualTo(10));
            Assert.That(p.Advance(TimeSpan.Zero), Is.Zero);
        }
        [Test]
        public void PresentationControlsWorkWhileInspectingAndKeepTheSameMatch()
        {
            var root = new GameObject("Viewing Test");
            try {
                var v = root.AddComponent<PitchPreview>(); v.Initialize(PrototypeContent.CreateCareer()); v.GetComponent<MatchDriver>().enabled = false;
                var decision = v.World.PendingDecision; var match = v.Match; v.ToggleViewingMode(); v.CycleViewingSpeed();
                v.SelectPlayer("meadow-player-08"); v.AdjustCamera(3, 2, 15, -2);
                Assert.That(v.Playback.Mode, Is.EqualTo(MatchViewingMode.Highlights)); Assert.That(v.Playback.QuietSpeed, Is.EqualTo(64));
                Assert.That(v.World.PendingDecision, Is.SameAs(decision)); Assert.That(v.Match, Is.SameAs(match)); Assert.That(v.Match.Tick, Is.Zero);
                v.ToggleViewingMode(); v.CycleViewingSpeed(); Assert.That(v.Playback.FullMatchSpeed, Is.EqualTo(2));
            } finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [TestCase(0)] [TestCase(3)] [TestCase(65)]
        public void InvalidSpeedsAreRejected(int speed)
        {
            using var w = new OpenPlayWorld(PrototypeContent.CreateCareer(), new MatchSession("invalid", 42)); var p = new MatchPlayback(w);
            Assert.Throws<ArgumentOutOfRangeException>(() => p.SetFullMatchSpeed(speed)); Assert.Throws<ArgumentOutOfRangeException>(() => p.SetQuietSpeed(speed));
            Assert.Throws<ArgumentOutOfRangeException>(() => p.SetMode((MatchViewingMode)99)); Assert.Throws<ArgumentOutOfRangeException>(() => p.Advance(TimeSpan.FromTicks(-1)));
        }
    }
}
