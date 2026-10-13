using System;
using System.Collections.Generic;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Presentation;
using FootballRpg.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace FootballRpg.Tests
{
    [Category("FootballVerification")]
    public sealed class FootballVerificationTests
    {
        [Serializable] private sealed class Evidence
        {
            public string role, digest;
            public bool abandoned;
            public int dismissals;
            public int decisions, keeperDecisions, actions, homeGoals, awayGoals;
            public long ticks;
        }
        private sealed class Run
        { public string snapshot; public string[] stops; public Evidence evidence; }
        private static CareerState Create(string role)
        {
            var content = PrototypeContent.Load(); var draft = CharacterCreation.NewDraft(content);
            var rules = JsonUtility.FromJson<CreationRules>(Resources.Load<TextAsset>("CharacterCreation").text);
            draft.positionId = role;
            void Spend(ValueDefinition[] definitions, ValueOverride[] values, int budget)
            {
                while (budget > 0) foreach (var v in values)
                    if (budget > 0 && v.value < definitions.Single(d => d.id == v.id).maximum) { v.value++; budget--; }
            }
            Spend(content.footballAttributes, draft.attributes, rules.footballBudget);
            Spend(content.personalityTraits, draft.traits, rules.personalityBudget);
            var c = CharacterCreation.CreateCareer(content, draft, rules); c.Week.Begin();
            while (c.Week.Phase == CareerWeekPhase.ClubWeek)
            {
                var commitment = c.Calendar.CurrentCommitments.FirstOrDefault();
                c.Calendar.Commit(c.Calendar.Preview(commitment == null ? CalendarChoiceKind.Replace : CalendarChoiceKind.Attend, commitment?.Id ?? "rest"));
                if (c.Meetings.Pending != null) c.Meetings.Commit(c.Meetings.Preview(c.Meetings.Pending.Options[0].Id));
            }
            return c;
        }
        [TestCaseSource(typeof(PositionIds), nameof(PositionIds.All))]
        public void CreatedCareerCompletesEveryRoleWithIdenticalFullAndHighlightConsequences(string role)
        {
            var full = Play(role, MatchViewingMode.FullMatch, 100);
            var highlights = Play(role, MatchViewingMode.Highlights, 233);
            Assert.That(highlights.stops, Is.EqualTo(full.stops), "Every meaningful stop must occur at the same tick and probability.");
            Assert.That(highlights.snapshot, Is.EqualTo(full.snapshot), "All career/rule/XP/RNG state must agree, not only the score.");
            TestContext.Out.WriteLine("FOOTBALL_POSITION_EVIDENCE " + JsonUtility.ToJson(full.evidence));
        }
        private static Run Play(string role, MatchViewingMode mode, int frameMilliseconds)
        {
            var c = Create(role); var stops = new List<string>(); int decisions = 0, keeper = 0;
            var view = new GameObject("Football verification " + role).AddComponent<PitchPreview>();
            try
            {
                c.Week.StartFixture(new CompetitionRules(600, 0, .1), new MatchFitnessRules(injuryRatePerSecond: 0, coachChanges: false));
                view.Initialize(c, careerFixture: true); view.GetComponent<MatchDriver>().enabled = false;
                Assert.That(c.Week.World.UserParticipation, Is.EqualTo(ParticipationState.Playing));
                Assert.That(view.World.Players.Single(p => p.Player.Id == c.UserPlayerId).PositionRoleId, Is.EqualTo(role));
                Assert.That(view.World.Players.Count(p => p.IsHome), Is.EqualTo(11)); Assert.That(view.World.Players.Count(p => !p.IsHome), Is.EqualTo(11));
                view.Playback.SetMode(mode); view.Match.Resume();
                for (int frame = 0; frame < 100000 && c.Week.Phase != CareerWeekPhase.Complete; frame++)
                {
                    var w = view.World;
                    if (w.HasPendingDecision || view.UserRestart || w.RefereeNoticePending || w.OffsideNoticePending || w.FixturePhase == FixturePhase.HalfTime)
                    {
                        long tick = view.Match.Tick, draws = view.Match.DrawCount;
                        string stamp = Stamp(view);
                        view.SelectPlayer(w.Players.First(p => !p.IsHome).Player.Id);
                        Assert.That(view.Playback.Advance(TimeSpan.FromHours(1)), Is.Zero);
                        Assert.That(view.Match.Tick, Is.EqualTo(tick)); Assert.That(view.Match.DrawCount, Is.EqualTo(draws));
                        Assert.That(Stamp(view), Is.EqualTo(stamp), "Inspection and waiting must preserve the frozen choice.");
                        stops.Add(stamp);
                        if (w.HasPendingDecision) decisions++;
                        if (w.PendingKeeper != null) { keeper++; Assert.That(w.Players.Single(p => p.Player.Id == c.UserPlayerId).IsGoalkeeper, Is.True, "Only the current match keeper gets hand/saving decisions, including emergency keepers."); }
                        Assert.That(view.CommitChosenPass(), Is.True, "Every required stop must have a reachable presentation action: " + stamp);
                    }
                    view.Playback.Advance(TimeSpan.FromMilliseconds(frameMilliseconds));
                }
                Assert.That(c.Week.Phase, Is.EqualTo(CareerWeekPhase.Complete), "No dead-end decision loops.");
                if (view.World.Abandoned) Assert.That(view.World.Players.Count(p => p.IsHome) < 7 || view.World.Players.Count(p => !p.IsHome) < 7, Is.True, "Abandonment must have a legal minimum-player cause.");
                Assert.That(decisions, Is.GreaterThan(0));
                Assert.That(c.Reports.Match.Actions.Count, Is.GreaterThan(0));
                if (role == "gk") Assert.That(keeper, Is.GreaterThan(0));
                Assert.That(c.Reports.Match.Role, Is.EqualTo(role));
                foreach (var metric in c.Reports.Week.Metrics) Assert.That(metric.Causes.Sum(x => x.Delta), Is.EqualTo(metric.Delta).Within(.00001));
                var store = new CareerSaveStore(System.IO.Path.Combine(Application.temporaryCachePath, "verification-unused.json"), "football-verification", PrototypeContent.Load());
                var snapshot = store.Encode(c);
                return new Run { snapshot = snapshot, stops = stops.ToArray(), evidence = new Evidence { role = role, digest = CareerSaveStore.Hash(snapshot),
                    abandoned = view.World.Abandoned, dismissals = view.World.FixtureEvents.Count(e => e.Kind == "Red" || e.Kind == "SecondYellow"), decisions = decisions, keeperDecisions = keeper, actions = c.Reports.Match.Actions.Count, homeGoals = view.World.HomeGoals, awayGoals = view.World.AwayGoals, ticks = view.Match.Tick } };
            }
            finally { view.Close(); c.Week.World?.Dispose(); }
        }
        private static string Stamp(PitchPreview v)
        {
            var w = v.World; var parts = new List<string> { v.Match.Tick.ToString(), w.FixturePhase.ToString(), w.RefereeNoticePending.ToString(), w.OffsideNoticePending.ToString(), w.Restart?.Id ?? "", w.PendingSetPiece?.Duty.ToString() ?? "" };
            if (w.PendingDecision != null)
            {
                parts.Add(w.PendingDecision.Id);
                parts.AddRange(w.PendingDecision.Options.Select(p => p.Receiver.Id + ":" + p.Kind + ":" + p.Percent));
                parts.AddRange(w.PendingDecision.Actions.Select(p => p.OptionId + ":" + p.Percent));
            }
            if (w.PendingDefense != null) { parts.Add(w.PendingDefense.Id); parts.AddRange(w.PendingDefense.Options.Select(p => p.Kind + ":" + p.Percent)); }
            if (w.PendingKeeper != null) { parts.Add(w.PendingKeeper.Id); parts.AddRange(w.PendingKeeper.Options.Select(p => p.OptionId + ":" + p.Percent)); }
            return string.Join("|", parts);
        }
    }
}
