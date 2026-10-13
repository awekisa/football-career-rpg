using System;
using System.Linq;
using FootballRpg.Simulation;
using UnityEngine;

namespace FootballRpg.Presentation
{
    public sealed partial class LaunchScreen
    {
        private bool coachOpen;
        private int coachRoleIndex, coachReadyFrame;
        private CoachRequestPreview coachPreview;
        private string coachError;
        private Vector2 coachScroll;
        public void OpenCoach()
        {
            if (Career == null || preview != null || practiceView != null || clubView != null) return;
            coachOpen = true; coachReadyFrame = Time.frameCount + 1; coachPreview = null; coachError = null;
            coachRoleIndex = Array.IndexOf(PositionIds.All, Career.Coach.AssignedRole);
        }
        private void PreviewCoach(CoachRequest request)
        {
            try { coachPreview = Career.Coach.PreviewRequest(request, PositionIds.All[coachRoleIndex]); coachError = null; }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException) { coachPreview = null; coachError = e.Message; }
        }
        private void ConfirmCoach()
        {
            if (coachPreview == null) return;
            try { Career.Coach.CommitRequest(coachPreview); coachPreview = null; coachError = null; }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException) { coachPreview = null; coachError = e.Message; }
        }
        private void UpdateCoach()
        {
            if (Time.frameCount <= coachReadyFrame) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { coachOpen = false; return; }
            var shown = coachPreview;
            if (Input.GetKeyDown(KeyCode.Tab)) { coachRoleIndex = (coachRoleIndex + 1) % PositionIds.All.Length; coachPreview = null; }
            if (Input.GetKeyDown(KeyCode.Alpha1)) PreviewCoach(CoachRequest.Start);
            if (Input.GetKeyDown(KeyCode.Alpha2)) PreviewCoach(CoachRequest.Role);
            if (Input.GetKeyDown(KeyCode.Alpha3)) PreviewCoach(CoachRequest.Penalties);
            if (Input.GetKeyDown(KeyCode.Return) && shown == coachPreview) ConfirmCoach();
        }
        private void DrawCoach()
        {
            var coach = Career.Coach; var r = coach.Relationship; var s = coach.Select();
            GUI.Label(new Rect(60, 30, 1320, 60), "COACH  ·  " + coach.Profile.Name, body);
            GUI.Label(new Rect(60, 95, 1300, 110), $"Preferred formation {coach.Profile.PreferredFormation} · Discipline {coach.Profile.DisciplinePreference:P0} · Creativity {coach.Profile.CreativityPreference:P0}\nAffinity {r.Affinity} · Trust {r.ProfessionalTrust} · Standing {r.Standing} · {(coach.EstablishedStar ? "Established star" : "Building your standing")}", small);
            GUI.Label(new Rect(60, 205, 1320, 105), $"Projected selection: {s.Outcome} · {s.Role.ToUpperInvariant()} · Role ability {s.Ability:0.0}\nSelection score {s.Score:0.0} / required {s.RequiredScore:0.0}. {s.Reason}", small);
            GUI.Label(new Rect(60, 315, 1320, 65), $"Penalty priority reduction: {coach.ResponsibilityPenalty(RestartKind.Penalty)} · Corners: {coach.ResponsibilityPenalty(RestartKind.Corner)} · Free kicks: {coach.ResponsibilityPenalty(RestartKind.DirectFreeKick)}\nOne request per calendar slot. Agreements never bypass injury, fatigue or legal substitutions.", small);
            if (GUI.Button(new Rect(60, 390, 360, 48), "ROLE: " + PositionIds.All[coachRoleIndex].ToUpperInvariant() + " [TAB]", button)) { coachRoleIndex = (coachRoleIndex + 1) % PositionIds.All.Length; coachPreview = null; }
            GUI.enabled = coach.RequestUnavailable == null;
            if (GUI.Button(new Rect(60, 454, 390, 52), "ASK TO START [1]", button)) PreviewCoach(CoachRequest.Start);
            if (GUI.Button(new Rect(480, 454, 390, 52), "REQUEST ROLE [2]", button)) PreviewCoach(CoachRequest.Role);
            if (GUI.Button(new Rect(900, 454, 390, 52), "ASK FOR PENALTIES [3]", button)) PreviewCoach(CoachRequest.Penalties);
            GUI.enabled = true;
            string text = coachPreview == null ? coachError ?? ((coach.LastRequest == null ? "Inspect a request to see its chance and consequences." : $"Last request: {(coach.LastRequest.Succeeded ? "SUCCESS" : "DECLINED")} · {coach.LastRequest.Preview.Percent}% chance. {coach.LastRequest.Message}") + "\n" + coach.RequestUnavailable) :
                $"{coachPreview.Request} · {coachPreview.Role.ToUpperInvariant()} · {coachPreview.Percent}% chance\nSuccess: coach agrees to this request. Failure: current assignment stays. Either result uses this slot's request.";
            GUI.Label(new Rect(60, 530, 1260, 100), text, small);
            string history = string.Join("\n", coach.Reactions.Reverse().Take(20).Select(x => $"{x.Reason} · Trust {x.TrustDelta:+0;-0;0} · Standing {x.StandingDelta:+0;-0;0}"));
            coachScroll = GUI.BeginScrollView(new Rect(60, 640, 1260, 145), coachScroll, new Rect(0, 0, 1225, Math.Max(145, coach.Reactions.Count * 45)));
            GUI.Label(new Rect(0, 0, 1210, Math.Max(145, coach.Reactions.Count * 45)), history.Length == 0 ? "Coach reactions will appear here after attendance and match decisions." : history, small); GUI.EndScrollView();
            if (GUI.Button(new Rect(60, 820, 350, 55), "BACK [ESC]", button)) coachOpen = false;
            GUI.enabled = coachPreview != null;
            if (GUI.Button(new Rect(850, 820, 470, 55), "CONFIRM REQUEST [ENTER]", button)) ConfirmCoach();
            GUI.enabled = true;
        }
        private void VerifyCoach()
        {
            var original = Career;
            try
            {
                Career = FootballRpg.Content.PrototypeContent.CreateCareer(); OpenCoach(); PreviewCoach(CoachRequest.Start);
                if (coachPreview == null || Career.Coach.DrawCount != 0) throw new InvalidOperationException("Coach preview must be inert.");
                ConfirmCoach(); ConfirmCoach();
                if (Career.Coach.DrawCount != 1 || Career.Coach.LastRequest == null) throw new InvalidOperationException("Coach request did not commit once.");
                using (var world = new OpenPlayWorld(Career, new MatchSession("coach-smoke", 12), Career.UserPlayerId))
                {
                    world.StartFixture(new CompetitionRules(120), new MatchFitnessRules(injuryRatePerSecond: 0), applyCoachSelection: true);
                    if (!world.CoachEnabled || world.UserParticipation != ParticipationState.Playing || world.Players.Count != 22) throw new InvalidOperationException("Career coach selection failed.");
                }
                Debug.Log("FOOTBALL_RPG_COACH_PASSED");
            }
            finally { Career = original; coachOpen = false; coachPreview = null; coachError = null; }
        }
    }
}
