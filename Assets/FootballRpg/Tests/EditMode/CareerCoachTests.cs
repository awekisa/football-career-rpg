using System;
using System.Linq;
using System.Reflection;
using FootballRpg.Content;
using FootballRpg.Presentation;
using FootballRpg.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace FootballRpg.Tests
{
    public sealed class CareerCoachTests
    {
        private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
        private static CareerState Create(double strength = .35, float discipline = .8f, float creativity = .35f, int charisma = 50, ulong seed = 1)
        {
            var d = PrototypeContent.Load(); var p = d.players.Single(p => p.id == d.userPlayerId);
            p.traits = p.traits.Where(t => t.id != "charisma" && t.id != "leadership").Concat(new[] { new ValueOverride { id = "charisma", value = charisma }, new ValueOverride { id = "leadership", value = charisma } }).ToArray();
            d.coaches[0].disciplinePreference = discipline; d.coaches[0].creativityPreference = creativity;
            return CareerState.Create(d, coachRules: new CoachRules(strength, seed: seed));
        }
        private static void Set(object target, string property, object value) => target.GetType().GetProperty(property).SetValue(target, value);
        private static OpenPlayWorld Match(CareerState c, out MatchSession m)
        { m = new MatchSession("coach-test", 42); var w = new OpenPlayWorld(c, m, c.UserPlayerId); w.StartFixture(new CompetitionRules(120), new MatchFitnessRules(injuryRatePerSecond: 0), true); return w; }
        [Test] public void WeakClubStartsProspectWhileStrongClubRequiresDevelopment()
        {
            var weak = Create(); var strong = Create(1);
            Assert.That(weak.Coach.Select().Outcome, Is.EqualTo(SelectionOutcome.Starter));
            Assert.That(strong.Coach.Select().Outcome, Is.EqualTo(SelectionOutcome.Substitute));
            using var w = Match(weak, out _); Assert.That(w.UserParticipation, Is.EqualTo(ParticipationState.Playing));
            Assert.That(w.Players.Count, Is.EqualTo(22)); Assert.That(w.SubstitutionsUsed(true), Is.Zero);
        }
        [Test] public void LowAbilityFatigueInjuryAndConductChangeSelection()
        {
            var d = PrototypeContent.Load(); var player = d.players.Single(p => p.id == d.userPlayerId);
            player.attributes = d.footballAttributes.Select(a => new ValueOverride { id = a.id, value = 1 }).ToArray();
            Assert.That(CareerState.Create(d).Coach.Select().Outcome, Is.EqualTo(SelectionOutcome.Substitute));
            var c = Create(); c.SetPlayerCondition(c.UserPlayerId, .6f, .7f, .95f);
            using (var w = Match(c, out _)) Assert.That(w.UserParticipation, Is.EqualTo(ParticipationState.Bench));
            c.SetPlayerCondition(c.UserPlayerId, .6f, .7f, .1f); Set(c.UserPlayer, "InjuryDaysRemaining", 3);
            using (var w = Match(c, out _)) { Assert.That(w.UserParticipation, Is.EqualTo(ParticipationState.InjuredOut)); Assert.That(w.Players.Count, Is.EqualTo(22)); }
            Set(c.UserPlayer, "InjuryDaysRemaining", 0); Set(c.Coach.Relationship, "ProfessionalTrust", 10);
            using (var w = Match(c, out _)) Assert.That(w.UserParticipation, Is.EqualTo(ParticipationState.Bench));
        }
        [Test] public void CalendarConductChangesTrustOnceAndInvalidatesSocialPreview()
        {
            var c = Create(); var preview = c.Social.Preview(c.Coach.Profile.Id, "connect");
            var choice = c.Calendar.Preview(CalendarChoiceKind.Skip, "mon-training"); c.Calendar.Commit(choice); c.Calendar.Commit(choice);
            Assert.That(c.Coach.Relationship.ProfessionalTrust, Is.LessThan(50)); Assert.That(c.Coach.Relationship.Affinity, Is.Zero);
            Assert.That(c.Coach.Reactions.Count, Is.EqualTo(1)); Assert.That(c.Social.Changes.Any(x => x.CauseEventId == c.Coach.Reactions[0].CauseId), Is.True);
            Assert.Throws<InvalidOperationException>(() => c.Social.Commit(preview));
        }
        [Test] public void StrictCoachPenalizesSameDeviationMoreAndStarsHaveMoreFreedom()
        {
            var strict = Create(discipline: 1, creativity: 0); var creative = Create(discipline: 0, creativity: 1);
            int penalty = CareerCoach.DeviationPenalty(strict.Coach.Profile, 50, false, false, .8);
            Assert.That(penalty, Is.GreaterThan(CareerCoach.DeviationPenalty(creative.Coach.Profile, 50, false, false, .8)));
            Assert.That(penalty, Is.GreaterThan(CareerCoach.DeviationPenalty(strict.Coach.Profile, 90, false, false, .8)));
            Assert.That(penalty, Is.GreaterThan(CareerCoach.DeviationPenalty(strict.Coach.Profile, 50, true, true, .8)));
        }
        [Test] public void DeviationEffectsAreIdempotentClampedAndDoNotChangeAffinity()
        {
            var c = Create(); Call(c.Coach, "Deviation", "deviation-1", false, false, 1.0, "Counterattack");
            int trust = c.Coach.Relationship.ProfessionalTrust;
            Call(c.Coach, "Deviation", "deviation-1", false, false, 1.0, "Counterattack");
            Assert.That(c.Coach.Relationship.ProfessionalTrust, Is.EqualTo(trust)); Assert.That(c.Coach.Relationship.Affinity, Is.Zero); Assert.That(c.Coach.Reactions.Count, Is.EqualTo(1));
        }
        [Test] public void PersuasionUsesExactPreviewChanceAndOnlyOneRoll()
        {
            var high = Create(charisma: 100); var low = Create(charisma: 1);
            var a = high.Coach.PreviewRequest(CoachRequest.Start); var b = low.Coach.PreviewRequest(CoachRequest.Start);
            Assert.That(a.Percent, Is.GreaterThan(b.Percent)); Assert.That(high.Coach.DrawCount, Is.Zero);
            var ar = high.Coach.CommitRequest(a); var br = low.Coach.CommitRequest(b);
            Assert.That(ar.Roll, Is.EqualTo(br.Roll)); Assert.That(ar.Succeeded, Is.EqualTo(ar.Roll < a.Chance)); Assert.That(ar.Succeeded, Is.True); Assert.That(br.Succeeded, Is.False);
            Assert.That(high.Coach.CommitRequest(a), Is.SameAs(ar)); Assert.That(high.Coach.DrawCount, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => high.Coach.PreviewRequest(CoachRequest.Penalties)); Assert.That(high.Calendar.Minute, Is.Zero);
        }
        [Test] public void RequestsRejectForeignStaleAndMatchActiveContextsWithoutDraws()
        {
            var c = Create(); var p = c.Coach.PreviewRequest(CoachRequest.Role, "gk");
            Assert.Throws<ArgumentException>(() => Create().Coach.CommitRequest(p));
            c.SetPlayerCondition(c.UserPlayerId, .5f, .6f, .7f); Assert.Throws<InvalidOperationException>(() => c.Coach.CommitRequest(p));
            using (var w = Match(c, out _)) Assert.Throws<InvalidOperationException>(() => c.Coach.PreviewRequest(CoachRequest.Start));
            Assert.That(c.Coach.DrawCount, Is.Zero); Assert.That(c.Coach.MatchActive, Is.False);
        }
        [TestCase("gk")] [TestCase("cb")] [TestCase("st")] [TestCase("rwb")]
        public void AgreedRoleIsAppliedToActualMatchAndKeepsOneKeeper(string role)
        {
            var c = Create(strength: 0, charisma: 100); var result = c.Coach.CommitRequest(c.Coach.PreviewRequest(CoachRequest.Role, role)); Assert.That(result.Succeeded, Is.True);
            using var w = Match(c, out _);
            var user = w.Players.Single(p => p.Player.Id == c.UserPlayerId);
            Assert.That(user.PositionRoleId, Is.EqualTo(role)); Assert.That(w.Players.Count(p => p.IsHome && p.IsGoalkeeper), Is.EqualTo(1));
            Assert.That(w.Players.Count, Is.EqualTo(22));
        }
        [Test] public void StartPromiseNeverBypassesInjuryOrExhaustion()
        {
            var c = Create(charisma: 100); c.Coach.CommitRequest(c.Coach.PreviewRequest(CoachRequest.Start));
            c.SetPlayerCondition(c.UserPlayerId, .7f, .7f, 1); Assert.That(c.Coach.Select().Outcome, Is.EqualTo(SelectionOutcome.Substitute));
            Set(c.UserPlayer, "InjuryDaysRemaining", 1); Assert.That(c.Coach.Select().Outcome, Is.EqualTo(SelectionOutcome.NotSelected));
        }
        [Test] public void FailedSpecialistActionReducesNextTakerPriorityAndDuplicateDoesNotRepeat()
        {
            var c = Create(charisma: 100); c.Coach.CommitRequest(c.Coach.PreviewRequest(CoachRequest.Penalties));
            var squad = c.Players.Values.Where(p => p.ClubId == c.UserPlayer.ClubId).ToArray();
            Assert.That(c.Coach.ChooseTaker(RestartKind.Penalty, squad), Is.EqualTo(c.UserPlayerId));
            Call(c.Coach, "SetPieceResult", "miss", RestartKind.Penalty, false); Call(c.Coach, "SetPieceResult", "miss", RestartKind.Penalty, false);
            Assert.That(c.Coach.ResponsibilityPenalty(RestartKind.Penalty), Is.EqualTo(18)); Assert.That(c.Coach.ChooseTaker(RestartKind.Penalty, squad), Is.Not.EqualTo(c.UserPlayerId));
            Call(c.Coach, "SetPieceResult", "success", RestartKind.Penalty, true); Assert.That(c.Coach.ResponsibilityPenalty(RestartKind.Penalty), Is.EqualTo(14));
        }
        [Test] public void ActualRestartAssignmentAndOutcomeReachCoachLedger()
        {
            var c = Create(charisma: 100); c.Coach.CommitRequest(c.Coach.PreviewRequest(CoachRequest.Penalties));
            using var w = Match(c, out _); Call(w, "AwardRestart", RestartKind.Penalty, true, new PitchPoint(41.5, 0));
            Assert.That(w.Restart.TakerId, Is.EqualTo(c.UserPlayerId)); var id = w.Restart.Id;
            w.ShootRestart(id); int count = c.Coach.Reactions.Count; w.ShootRestart(id);
            Assert.That(count, Is.GreaterThan(0)); Assert.That(c.Coach.Reactions.Count, Is.EqualTo(count));
            Assert.That(c.Coach.Reactions.Any(r => r.Reason.StartsWith("Penalty")), Is.True);
        }
        [Test] public void InstructionsVaryWithPersonalityAndLateChaseAndMoveAutomaticPlayer()
        {
            var strict = Create(); var creative = Create(discipline: .1f, creativity: .9f);
            Assert.That(strict.Coach.Instruction(false, false, "cb"), Is.EqualTo(OffBallIntent.HoldZone));
            Assert.That(creative.Coach.Instruction(false, false, "cb"), Is.EqualTo(OffBallIntent.Press));
            Assert.That(strict.Coach.Instruction(true, true, "cb"), Is.EqualTo(OffBallIntent.ForwardRun));
            Assert.That(strict.Coach.Instruction(true, true, "gk"), Is.EqualTo(OffBallIntent.Automatic));
            using var w = Match(strict, out _); Call(w, "ClearPlay");
            var user = w.Players.Single(p => p.Player.Id == strict.UserPlayerId); var owner = w.Players.First(p => !p.IsHome && !p.IsGoalkeeper);
            Call(w, "GiveBall", owner);
            var target = (PitchPoint)Call(w, "IntendedTarget", user, owner);
            Assert.That(w.CoachInstruction, Is.EqualTo(OffBallIntent.HoldZone));
            w.SetIntent(OffBallIntent.Press); var altered = (PitchPoint)Call(w, "IntendedTarget", user, owner);
            Assert.That(altered.Distance(target), Is.GreaterThan(1));
            Call(w, "CoachAction", new ActionResolved("won", strict.UserPlayerId, 1, "tackle", owner.Player.Id, 1, 0, "success"), (RestartKind?)null);
            Assert.That(strict.Coach.Reactions.Count, Is.EqualTo(1));
        }
        [Test] public void CoachRemovalUsesExistingLegalStoppageAndSubstitutionBudget()
        {
            var c = Create(); using var w = Match(c, out _);
            c.SetPlayerCondition(c.UserPlayerId, .7f, .7f, .9f);
            Call(w, "ProcessRoster", false);
            Assert.That(w.UserParticipation, Is.EqualTo(ParticipationState.Replaced)); Assert.That(w.SubstitutionsUsed(true), Is.EqualTo(1)); Assert.That(w.WindowsUsed(true), Is.EqualTo(1));
            Assert.That(w.Players.Count, Is.EqualTo(22));
        }
        [Test] public void CoachScreenPreviewConfirmAndReopenCannotReplayRequest()
        {
            var root = new GameObject("Coach screen test");
            try
            {
                var launch = root.AddComponent<LaunchScreen>(); var c = Create(); Set(launch, "Career", c);
                launch.OpenCoach(); Call(launch, "PreviewCoach", CoachRequest.Start); Assert.That(c.Coach.DrawCount, Is.Zero);
                Call(launch, "ConfirmCoach"); launch.OpenCoach(); Call(launch, "ConfirmCoach");
                Assert.That(c.Coach.DrawCount, Is.EqualTo(1)); Assert.That(c.Calendar.Minute, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test] public void InvalidRulesAndUnsupportedRequestsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoachRules(double.NaN)); Assert.Throws<ArgumentOutOfRangeException>(() => new CoachRules(starStanding: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoachRules(setPieceFailurePenalty: 0));
            Assert.Throws<ArgumentException>(() => Create().Coach.PreviewRequest(CoachRequest.Role, "bad"));
        }
    }
}
