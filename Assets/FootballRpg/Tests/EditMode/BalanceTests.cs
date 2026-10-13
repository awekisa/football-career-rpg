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
    [Category("Balance")]
    public sealed class BalanceTests
    {
        [Serializable] private sealed class WeekEvidence
        {
            public string role, strategy, selection;
            public bool strong, abandoned;
            public int decisions, dismissals, goals, trust, practices;
            public double trainingXp, matchXp, kickoffFatigue, endFatigue;
        }
        private static CareerState Create(string role, bool strong)
        {
            var d = PrototypeContent.Load();
            if (strong)
            {
                var club = d.clubs.Single(c => c.id == d.fixture.homeClubId);
                club.staffQuality = club.facilityQuality = .9f;
                d.coaches.Single(c => c.id == club.coachId).coachingQuality = .9f;
            }
            var draft = CharacterCreation.NewDraft(d); draft.positionId = role;
            var rules = JsonUtility.FromJson<CreationRules>(Resources.Load<TextAsset>("CharacterCreation").text);
            void Spend(ValueOverride[] values, int budget)
            { while (budget > 0) foreach (var value in values) if (budget > 0 && value.value < 100) { value.value++; budget--; } }
            Spend(draft.attributes, rules.footballBudget); Spend(draft.traits, rules.personalityBudget);
            return CharacterCreation.CreateCareer(d, draft, rules);
        }
        private static void Week(CareerState c, string strategy)
        {
            c.Week.Begin();
            while (c.Week.Phase == CareerWeekPhase.ClubWeek)
            {
                var commitment = c.Calendar.CurrentCommitments.FirstOrDefault();
                bool extra = strategy == "ambitious" && commitment == null && c.UserPlayer.Fatigue < .5 &&
                    c.Calendar.History.Count(h => h.Choice.Kind == CalendarChoiceKind.ExtraPractice) < 2 &&
                    c.Calendar.History.LastOrDefault()?.Choice.Activity?.Training == true;
                CalendarChoice choice;
                if (extra) choice = c.Calendar.Preview(CalendarChoiceKind.ExtraPractice, "extra-practice", practice: GuidedPracticeSession.Recommended(c.UserPlayer.PrimaryPositionId));
                else if (strategy == "recovery" && commitment?.Activity.Training == true && c.UserPlayer.Fatigue >= .1)
                    choice = c.Calendar.Preview(CalendarChoiceKind.Replace, "rest");
                else choice = c.Calendar.Preview(commitment == null ? CalendarChoiceKind.Replace : CalendarChoiceKind.Attend, commitment?.Id ?? "rest");
                c.Calendar.Commit(choice);
                while (c.Calendar.AwaitingPractice)
                {
                    var p = c.Training.ActivePractice.Decision;
                    c.Training.ChoosePractice(p.Id, p.Options.OrderByDescending(o => o.Percent).First().Id);
                    if (c.Calendar.AwaitingPractice) c.Training.ActivePractice.Next();
                }
                if (c.Meetings.Pending != null) c.Meetings.Commit(c.Meetings.Preview(c.Meetings.Pending.Options[0].Id));
            }
        }
        [TestCase("cm", "reliable", false)] [TestCase("cm", "ambitious", false)] [TestCase("cm", "recovery", false)]
        [TestCase("gk", "reliable", false)] [TestCase("gk", "ambitious", false)] [TestCase("gk", "recovery", false)]
        [TestCase("cb", "reliable", false)] [TestCase("cb", "ambitious", false)] [TestCase("cb", "recovery", false)]
        [TestCase("st", "reliable", false)] [TestCase("st", "ambitious", false)] [TestCase("st", "recovery", false)]
        [TestCase("cm", "reliable", true)] [TestCase("cm", "ambitious", true)] [TestCase("cm", "recovery", true)]
        public void CompleteWeekStrategiesRecordActualConsequences(string role, string strategy, bool strong)
        {
            var c = Create(role, strong);
            c.SetPlayerCondition(c.UserPlayerId, .5f, .5f, .35f); // Common residual workload makes recovery a real tradeoff.
            Week(c, strategy);
            var evidence = new WeekEvidence { role = role, strategy = strategy, strong = strong,
                trainingXp = c.Training.Results.Sum(r => r.TotalXp), kickoffFatigue = c.UserPlayer.Fatigue,
                practices = c.Calendar.History.Count(h => h.Choice.Kind == CalendarChoiceKind.ExtraPractice) };
            var view = new GameObject("Balance week").AddComponent<PitchPreview>();
            try
            {
                c.Week.StartFixture(new CompetitionRules(600, 0, .1), new MatchFitnessRules(injuryRatePerSecond: 0, coachChanges: false));
                evidence.selection = c.Week.KickoffSelection.Outcome.ToString();
                if (!strong) Assert.That(c.Week.KickoffSelection.Outcome, Is.EqualTo(SelectionOutcome.Starter));
                view.Initialize(c, careerFixture: true); view.GetComponent<MatchDriver>().enabled = false;
                view.Playback.SetMode(MatchViewingMode.Highlights); view.Match.Resume();
                for (int i = 0; i < 100000 && c.Week.Phase != CareerWeekPhase.Complete; i++)
                {
                    var w = view.World;
                    if (w.HasPendingDecision || view.UserRestart || w.RefereeNoticePending || w.OffsideNoticePending || w.FixturePhase == FixturePhase.HalfTime)
                    {
                        if (w.HasPendingDecision) evidence.decisions++;
                        if (w.RefereeNoticePending || w.OffsideNoticePending || w.FixturePhase == FixturePhase.HalfTime || w.PendingSetPiece != null || view.UserRestart)
                            Assert.That(view.CommitChosenPass(), Is.True);
                        else if (w.PendingKeeper != null)
                        { var p = w.PendingKeeper; w.CommitKeeperDecision(p.Id, p.Options.OrderByDescending(o => o.Percent).First().OptionId); }
                        else if (w.PendingDefense != null)
                        {
                            var p = w.PendingDefense;
                            var option = strategy == "ambitious" ? p.Options.FirstOrDefault(o => o.Kind == DefensiveAction.Tackle && o.Percent >= 50) : null;
                            w.CommitDefense(p.Id, (option ?? p.Options.OrderByDescending(o => o.Percent).First()).Kind);
                        }
                        else if (w.PendingDecision != null)
                        {
                            var p = w.PendingDecision;
                            var action = strategy == "ambitious" ? p.Actions.FirstOrDefault(a => (a.Kind == FootballActionKind.Shot || a.Kind == FootballActionKind.HeaderShot) && a.Percent >= 35) : null;
                            var pass = strategy == "ambitious" ? p.Options.Where(o => o.Kind == PassKind.ThroughBall && o.Percent >= 45).OrderByDescending(o => o.Percent).FirstOrDefault() : null;
                            if (action != null) w.CommitFootballDecision(p.Id, action.OptionId);
                            else if (pass != null) w.CommitDecision(p.Id, pass.Receiver.Id, pass.Kind);
                            else if (p.Options.Count > 0) { pass = p.Options.OrderByDescending(o => o.Percent).First(); w.CommitDecision(p.Id, pass.Receiver.Id, pass.Kind); }
                            else { action = p.Actions.OrderByDescending(a => a.Percent).First(); w.CommitFootballDecision(p.Id, action.OptionId); }
                        }
                    }
                    view.Playback.Advance(TimeSpan.FromMilliseconds(233));
                }
                Assert.That(c.Week.Phase, Is.EqualTo(CareerWeekPhase.Complete));
                evidence.abandoned = view.World.Abandoned;
                evidence.dismissals = view.World.FixtureEvents.Count(e => e.Kind == "Red" || e.Kind == "SecondYellow");
                evidence.goals = view.World.HomeGoals + view.World.AwayGoals;
                evidence.matchXp = c.Progression.Changes.Where(x => x.PlayerId == c.UserPlayerId).Sum(x => x.Deltas.Values.Sum());
                evidence.endFatigue = c.UserPlayer.Fatigue; evidence.trust = c.Coach.Relationship.ProfessionalTrust;
                Assert.That(evidence.trainingXp, Is.GreaterThan(0));
                Assert.That(c.Reports.Week, Is.Not.Null);
                Assert.That(evidence.abandoned, Is.False, "A normal strategy must complete this seeded fixture.");
                TestContext.Out.WriteLine("BALANCE_WEEK " + JsonUtility.ToJson(evidence));
            }
            finally { view.Close(); c.Week.World?.Dispose(); }
        }
        [TestCase(1UL)] [TestCase(42UL)] [TestCase(987UL)]
        public void FullLengthAutonomousFixturesRemainPlayableAcrossSeeds(ulong seed)
        {
            var c = PrototypeContent.CreateCareer(); var m = new MatchSession("balance-long", seed);
            using var w = new OpenPlayWorld(c, m);
            w.StartFixture(new CompetitionRules(2700, 0, .1), new MatchFitnessRules(injuryRatePerSecond: 0, coachChanges: false));
            for (int i = 0; i < 60000 && w.FixturePhase != FixturePhase.FullTime; i++)
            {
                if (w.FixturePhase == FixturePhase.HalfTime) w.ContinueSecondHalf();
                m.Advance(TimeSpan.FromMilliseconds(100));
            }
            Assert.That(w.FixturePhase, Is.EqualTo(FixturePhase.FullTime));
            Assert.That(w.Abandoned, Is.False);
            Assert.That(w.CompletedPasses, Is.GreaterThan(10));
            Assert.That(m.Diagnostics.Count(d => d.Result.ActionId == "tackle"), Is.GreaterThan(0), "Tuning must not disable AI challenges.");
            TestContext.Out.WriteLine($"BALANCE_LONG seed={seed} goals={w.HomeGoals + w.AwayGoals} reds={w.FixtureEvents.Count(e => e.Kind == "Red" || e.Kind == "SecondYellow")} yellows={w.FixtureEvents.Count(e => e.Kind == "Yellow")} passes={w.CompletedPasses} tackles={m.Diagnostics.Count(d => d.Result.ActionId == "tackle")}");
        }
        [Test] public void RepeatedFailuresRetainRegressionButHaveBoundedTotalExposure()
        {
            var c = PrototypeContent.CreateCareer(); var xp = c.Progression; var scope = xp.BeginMatch();
            double loss = 0;
            for (int i = 0; i < 30; i++)
            {
                var action = new ActionResolved("loss-" + i, c.UserPlayerId, i, "pass", "target-" + i, .9f, .99f, "failure");
                var delta = xp.Apply(scope, action, new Dictionary<string, float> { ["short-passing"] = 1 }).Deltas.Values.Sum();
                Assert.That(delta, Is.LessThan(0)); loss -= delta;
            }
            Assert.That(loss, Is.LessThan(-xp.Rules.OutcomeXp(.9f, false, 1) * (xp.Rules.RewardedRepetitions + 1)));
        }
        [TestCase(44, false, false)] [TestCase(45, false, true)]
        [TestCase(54, true, false)] [TestCase(55, true, true)]
        public void AiUsesOpportunityAndBookingToDecideWhetherToLunge(int percent, bool booked, bool expected)
            => Assert.That(DefenseResolver.AiShouldTackle(percent, booked), Is.EqualTo(expected));

        [Test] public void RestGeneralAndFocusedTrainingTradeReadinessForSpecificDevelopment()
        {
            var general = Create("cm", false); var focused = Create("cm", false); var rest = Create("cm", false); var strong = Create("cm", true);
            foreach (var c in new[] { general, focused, rest, strong }) c.SetPlayerCondition(c.UserPlayerId, .5f, .5f, .65f);
            general.Calendar.Commit(general.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training"));
            strong.Calendar.Commit(strong.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training"));
            rest.Calendar.Commit(rest.Calendar.Preview(CalendarChoiceKind.Replace, "rest"));
            focused.Calendar.Commit(focused.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training", practice: PracticeFamily.Passing));
            for (int i = 0; i < 3; i++)
            {
                var d = focused.Training.ActivePractice.Decision;
                focused.Training.ChoosePractice(d.Id, d.Options.OrderByDescending(o => o.Percent).First().Id);
                if (i < 2) focused.Training.ActivePractice.Next();
            }
            var a = general.Training.Results.Single(); var b = focused.Training.Results.Single();
            Assert.That(b.Gains.Single(g => g.SkillId == "short-passing").Xp, Is.GreaterThan(a.Gains.Single(g => g.SkillId == "short-passing").Xp));
            Assert.That(strong.Training.Results.Single().TotalXp, Is.GreaterThan(a.TotalXp));
            Assert.That(rest.Training.Results, Is.Empty);
            Assert.That(rest.UserPlayer.Fatigue, Is.LessThan(general.UserPlayer.Fatigue));
            Assert.That(rest.Coach.Relationship.ProfessionalTrust, Is.LessThan(general.Coach.Relationship.ProfessionalTrust), "Skipping required practice still has a professional cost.");
            Assert.That(rest.Readiness.TrainingMultiplier(rest.UserPlayerId), Is.GreaterThan(general.Readiness.TrainingMultiplier(general.UserPlayerId)));
            TestContext.Out.WriteLine($"BALANCE_TRAINING general={a.TotalXp:F2} focused={b.TotalXp:F2} strong={strong.Training.Results.Single().TotalXp:F2} trainedFatigue={general.UserPlayer.Fatigue:F2} restFatigue={rest.UserPlayer.Fatigue:F2}");
        }
        [Test] public void CoachRiskDistinguishesStarsSuccessDangerAndJustification()
        {
            var c = Create("cm", false); var coach = c.Coach.Profile;
            int risky = CareerCoach.DeviationPenalty(coach, 50, false, false, .8);
            int success = CareerCoach.DeviationPenalty(coach, 50, true, false, .8);
            int star = CareerCoach.DeviationPenalty(coach, 90, false, false, .8);
            int justified = CareerCoach.DeviationPenalty(coach, 50, true, true, .8);
            Assert.That(risky, Is.GreaterThan(success).And.GreaterThan(star).And.GreaterThan(justified));
            TestContext.Out.WriteLine($"BALANCE_COACH risky={risky} successful={success} star={star} justifiedSuccess={justified}");
        }
        [TestCase(.9f, .5)] [TestCase(.55f, 1.5)] [TestCase(.4f, 2)]
        public void SeededRepeatedActionsRecordRiskAndVariance(float chance, double significance)
        {
            var totals = new List<double>(); var legacy = new List<double>();
            for (int seed = 1; seed <= 100; seed++)
            {
                var c = PrototypeContent.CreateCareer(); var xp = c.Progression; var match = xp.BeginMatch(); var rng = new System.Random(seed);
                double oldTotal = 0;
                for (int i = 0; i < 80; i++)
                {
                    float roll = (float)rng.NextDouble();
                    var action = new ActionResolved("risk-" + i, c.UserPlayerId, i, "pass", "target-" + i, chance, roll, roll < chance ? "success" : "failure");
                    oldTotal += xp.Rules.OutcomeXp(chance, action.Succeeded, significance) * (action.Succeeded ? Math.Max(0, 1 - i / 5d) : 1);
                    xp.Apply(match, action, new Dictionary<string, float> { ["short-passing"] = 1 }, significance);
                }
                totals.Add(xp.Changes.Sum(x => x.Deltas.Values.Sum())); legacy.Add(oldTotal);
            }
            double mean = totals.Average();
            Assert.That(mean, Is.GreaterThan(0), "Coherent strategies should develop over the seed ensemble.");
            TestContext.Out.WriteLine($"BALANCE_RISK p={chance} significance={significance} legacyMean={legacy.Average():F2} mean={mean:F2} min={totals.Min():F2} max={totals.Max():F2} sd={Math.Sqrt(totals.Average(x => (x - mean) * (x - mean))):F2}");
        }
    }
}
