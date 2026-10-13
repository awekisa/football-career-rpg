using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using FootballRpg.Content;
using FootballRpg.Simulation;
using FootballRpg.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace FootballRpg.Tests
{
    public sealed class CareerSaveTests
    {
        private string directory;
        private CareerSaveStore store;
        [SetUp] public void Setup() { directory = Path.Combine(Path.GetTempPath(), "football-save-" + Guid.NewGuid().ToString("N")); store = new CareerSaveStore(Path.Combine(directory, "career.json"), "test-content", PrototypeContent.Load()); }
        [TearDown] public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        private static CareerState Create() { var c = PrototypeContent.CreateCareer(); c.Week.Begin(); return c; }
        private CareerState RoundTrip(CareerState c) { var r = store.Decode(store.Encode(c)); Assert.That(r.Status, Is.EqualTo(SaveStatus.Loaded), r.Message); return r.Career; }
        private void Equal(CareerState a, CareerState b)
        {
            var left = store.Encode(a); var right = store.Encode(b);
            Assert.That(right, Is.EqualTo(left));
        }
        [Test] public void TrainingAndFutureCalendarConsequencesSurvive()
        {
            var c = Create(); c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training"));
            var copy = RoundTrip(c); Equal(c, copy);
            foreach (var x in new[] { c, copy }) x.Calendar.Commit(x.Calendar.Preview(CalendarChoiceKind.ExtraPractice, "extra-practice"));
            Equal(c, copy);
        }
        [Test] public void PendingMeetingPreservesChoiceAndOutcome()
        {
            var c = Create(); while (c.Calendar.Minute < 6 * 360) c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Wait));
            c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Attend, "tue-coach"));
            c.Meetings.Preview("role-plan"); var copy = RoundTrip(c);
            foreach (var x in new[] { c, copy }) x.Meetings.Commit(x.Meetings.Preview("role-plan"));
            Equal(c, copy);
        }
        [Test] public void AtomicSaveKeepsValidBackupWhenPrimaryIsCorrupt()
        {
            Assert.That(store.Load().Status, Is.EqualTo(SaveStatus.Missing)); var c = Create();
            Assert.That(store.Save(c).Status, Is.EqualTo(SaveStatus.Saved));
            c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training"));
            Assert.That(store.Save(c).Status, Is.EqualTo(SaveStatus.Saved));
            File.WriteAllText(store.Path, "broken"); var recovered = store.Load(); Assert.That(recovered.Status, Is.EqualTo(SaveStatus.Recovered)); Assert.That(recovered.Career.Calendar.Minute, Is.Zero);
            Assert.That(store.Save(c).Status, Is.EqualTo(SaveStatus.Saved));
            File.WriteAllText(store.Path, "broken again"); Assert.That(store.Load().Career.Calendar.Minute, Is.Zero);
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }
        [Test] public void RejectsCorruptAndIncompatibleData()
        {
            Assert.That(store.Decode("{broken").Status, Is.EqualTo(SaveStatus.Corrupt));
            var json = store.Encode(Create()); var envelope = JsonUtility.FromJson<CareerSaveEnvelope>(json);
            envelope.version++; Assert.That(store.Decode(JsonUtility.ToJson(envelope)).Status, Is.EqualTo(SaveStatus.Incompatible));
            envelope.version--; envelope.payload += " "; Assert.That(store.Decode(JsonUtility.ToJson(envelope)).Status, Is.EqualTo(SaveStatus.Corrupt));
            Assert.That(new CareerSaveStore(store.Path, "changed-content", PrototypeContent.Load()).Decode(json).Status, Is.EqualTo(SaveStatus.Incompatible));
        }
        [Test] public void GuidedPracticeRequiresStableBoundaryAndCanRestartAfterLoading()
        {
            var c = Create(); c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training", practice: PracticeFamily.Passing));
            Assert.That(store.Save(c).Status, Is.EqualTo(SaveStatus.Unavailable));
            c.Training.FinishPractice(); var copy = RoundTrip(c); Equal(c, copy);
            foreach (var x in new[] { c, copy })
            {
                while (x.Calendar.Minute < 4 * 360) x.Calendar.Commit(x.Calendar.Preview(CalendarChoiceKind.Wait));
                x.Calendar.Commit(x.Calendar.Preview(CalendarChoiceKind.Attend, "tue-training", practice: PracticeFamily.Defending));
                var d = x.Training.ActivePractice.Decision; x.Training.ChoosePractice(d.Id, d.Options[0].Id); x.Training.FinishPractice();
            }
            Equal(c, copy);
        }
        [Test] public void FailedWriteKeepsExistingSaveAndMissingLoadDoesNotInventCareer()
        {
            var c = Create(); Assert.That(store.Save(c).Success, Is.True); string saved = File.ReadAllText(store.Path);
            Directory.CreateDirectory(store.BackupPath); Assert.That(store.Save(c).Status, Is.EqualTo(SaveStatus.Failed));
            Assert.That(File.ReadAllText(store.Path), Is.EqualTo(saved)); Assert.That(store.Load().Success, Is.True);
        }
        [Test] public void UnknownSnapshotTypesAndBrokenReferencesAreRejected()
        {
            foreach (bool type in new[] { true, false })
            {
                var e = JsonUtility.FromJson<CareerSaveEnvelope>(store.Encode(Create())); var graph = JsonUtility.FromJson<SnapshotGraph>(e.payload);
                if (type) graph.nodes[graph.root - 1].type = "System.IO.File";
                else graph.nodes[graph.root - 1].references[0] = graph.nodes.Length + 1;
                e.payload = JsonUtility.ToJson(graph); e.checksum = CareerSaveStore.Hash(e.version + ":" + e.contentHash + ":" + e.payload);
                Assert.That(store.Decode(JsonUtility.ToJson(e)).Status, Is.EqualTo(SaveStatus.Corrupt));
            }
        }
        [Test] public void CharacterMoneyAndInjuryRecoveryRetainExactValues()
        {
            var d = PrototypeContent.Load(); var p = d.players.Single(x => x.id == d.userPlayerId);
            p.name = "Иван García"; p.balanceMinor = 9007199254740993L; p.weeklyWageMinor = 12345;
            var c = CareerState.Create(d); c.Week.Begin();
            typeof(CareerReadiness).GetMethod("Injury", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c.Readiness, new object[] { "test-match", c.UserPlayerId, "injury-1", 3 });
            c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Wait)); var copy = RoundTrip(c); Equal(c, copy);
            Assert.That(copy.UserPlayer.Name, Is.EqualTo(p.name)); Assert.That(copy.UserPlayer.BalanceMinor, Is.EqualTo(p.balanceMinor)); Assert.That(copy.UserPlayer.WeeklyWageMinor, Is.EqualTo(p.weeklyWageMinor));
            Assert.That(copy.UserPlayer.InjuryDaysRemaining, Is.EqualTo(3));
            foreach (var x in new[] { c, copy }) while (x.Calendar.Minute < 5 * 360) x.Calendar.Commit(x.Calendar.Preview(CalendarChoiceKind.Wait));
            Equal(c, copy); Assert.That(copy.UserPlayer.InjuryDaysRemaining, Is.EqualTo(2));
        }
        [Test] public void ResolvedActionsCannotRerollOrAwardXpAgainAfterLoading()
        {
            var c = Create(); while (c.Calendar.Minute < 25 * 360) c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Wait));
            c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Attend, "sun-match")); c.Week.StartFixture();
            Assert.That(store.Save(c).Status, Is.EqualTo(SaveStatus.Unavailable));
            c.Week.Match.Pause(); var request = new ActionRequest("saved-shot", c.UserPlayerId, "shoot", "harbour-player-07", .7f, "goal", "miss", new Dictionary<string, string>());
            var action = c.Week.Match.BeginResolution(request); Assert.That(store.Save(c).Status, Is.EqualTo(SaveStatus.Unavailable));
            c.Week.Match.Pause(); Assert.That(store.Save(c).Status, Is.EqualTo(SaveStatus.Unavailable)); c.Week.Match.Resume();
            var attribution = new Dictionary<string, float> { ["finishing"] = 1 }; string matchId = c.Progression.BeginMatch();
            c.Progression.Apply(matchId, action.Result, attribution); c.Week.Match.CompleteResolution(action.Result.EventId); c.Week.Match.Pause();
            var copy = RoundTrip(c); var before = store.Encode(copy);
            Assert.That(copy.Week.Match.BeginResolution(request), Is.SameAs(copy.Week.Match.Diagnostics.Last()));
            Assert.That(copy.Week.Match.CompleteResolution(action.Result.EventId), Is.False);
            var prior = copy.Progression.Changes.Last(); Assert.That(copy.Progression.Apply(matchId, copy.Week.Match.Diagnostics.Last().Result, attribution), Is.SameAs(prior));
            Assert.That(store.Encode(copy), Is.EqualTo(before)); c.Week.World.Dispose(); copy.Week.World.Dispose();
        }
        [Test] public void LoadControlsPreserveLiveCareerOnFailureAndReopenSavedFixture()
        {
            var c = Create(); while (c.Calendar.Minute < 25 * 360) c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Wait));
            c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Attend, "sun-match"));
            var go = new GameObject("Save controls test"); var launch = go.AddComponent<LaunchScreen>();
            typeof(LaunchScreen).GetProperty("Career").SetValue(launch, c);
            typeof(LaunchScreen).GetField("saveStore", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(launch, store);
            try
            {
                Assert.That(launch.LoadCareer().Status, Is.EqualTo(SaveStatus.Missing)); Assert.That(launch.Career, Is.SameAs(c));
                launch.ContinueWeek(); c.Week.Match.Pause(); Assert.That(launch.SaveCareer().Status, Is.EqualTo(SaveStatus.Saved));
                string snapshot = store.Encode(c); var load = launch.LoadCareer(); Assert.That(load.Success, Is.True); Assert.That(launch.Career, Is.SameAs(load.Career));
                Assert.That(store.Encode(launch.Career), Is.EqualTo(snapshot));
                File.WriteAllText(store.Path, "invalid"); Assert.That(launch.LoadCareer().Status, Is.EqualTo(SaveStatus.Corrupt)); Assert.That(launch.Career, Is.SameAs(load.Career));
            }
            finally
            {
                ((PitchPreview)typeof(LaunchScreen).GetField("preview", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(launch))?.Close();
                launch.Career.Week.World?.Dispose(); UnityEngine.Object.DestroyImmediate(go);
            }
        }
        [TestCase("am")] [TestCase("gk")]
        public void MatchDecisionsAndFutureOutcomesSurvive(string role)
        {
            var d = PrototypeContent.Load(); if (role == "gk") d.userPlayerId = "harbour-player-01";
            var c = CareerState.Create(d); c.Week.Begin();
            while (c.Week.Phase == CareerWeekPhase.ClubWeek)
            {
                var a = c.Calendar.CurrentCommitments.FirstOrDefault();
                c.Calendar.Commit(c.Calendar.Preview(a == null ? CalendarChoiceKind.Replace : CalendarChoiceKind.Attend, a?.Id ?? "rest"));
                if (c.Meetings.Pending != null) c.Meetings.Commit(c.Meetings.Preview(c.Meetings.Pending.Options[0].Id));
            }
            c.Week.StartFixture(new CompetitionRules(600, 0, .1), new MatchFitnessRules(injuryRatePerSecond: 0));
            var initial = new GameObject("Initial save decision").AddComponent<PitchPreview>(); initial.Initialize(c, careerFixture: true); initial.GetComponent<MatchDriver>().enabled = false;
            int decisions = 0;
            for (int i = 0; i < 25000 && decisions < 2; i++)
            {
                if (initial.World.HasPendingDecision) { decisions++; if (decisions == 2) break; }
                if (initial.World.HasPendingDecision || initial.UserRestart || initial.World.RefereeNoticePending || initial.World.OffsideNoticePending || initial.World.FixturePhase == FixturePhase.HalfTime) initial.CommitChosenPass();
                initial.Match.Advance(TimeSpan.FromMilliseconds(50));
            }
            Assert.That(decisions, Is.EqualTo(2)); Assert.That(c.Week.Match.DrawCount, Is.GreaterThan(0)); initial.Close();
            var copy = RoundTrip(c); Equal(c, copy);
            foreach (var x in new[] { c, copy })
            {
                var view = new GameObject("Save test").AddComponent<PitchPreview>(); view.Initialize(x, careerFixture: true); view.GetComponent<MatchDriver>().enabled = false;
                if (!view.World.HasPendingDecision && !view.UserRestart) view.Match.Resume();
                for (int i = 0; i < 50000 && x.Week.Phase != CareerWeekPhase.Complete; i++)
                {
                    if (view.World.HasPendingDecision || view.UserRestart || view.World.RefereeNoticePending || view.World.OffsideNoticePending || view.World.FixturePhase == FixturePhase.HalfTime) view.CommitChosenPass();
                    view.Match.Advance(TimeSpan.FromMilliseconds(50));
                }
                view.Close(); Assert.That(x.Week.Phase, Is.EqualTo(CareerWeekPhase.Complete));
            }
            Equal(c, copy); var completed = RoundTrip(c); Equal(c, completed);
            c.Week.World.Dispose(); copy.Week.World.Dispose(); completed.Week.World.Dispose();
        }
    }
}
