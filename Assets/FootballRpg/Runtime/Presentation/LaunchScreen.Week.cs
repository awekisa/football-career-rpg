using System;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Simulation;
using UnityEngine;

namespace FootballRpg.Presentation
{
    public sealed partial class LaunchScreen
    {
        private void JoinCreatedCareer()
        {
            var created = CharacterCreation.CreateCareer(creationContent, creationDraft, creationRules);
            created.Week.Begin(); Career?.Week.World?.Dispose(); Career = created; creationDraft = null;
            trainingFocus = null; showTrainingResults = false; returnToClub = false;
            reportsOpen = coachOpen = socialOpen = meetingsOpen = false;
            OpenCalendar();
        }
        public void ContinueWeek()
        {
            if (preview != null || practiceView != null || clubView != null || Career == null) return;
            if (Career.Week.Phase == CareerWeekPhase.ClubWeek) { OpenCalendar(); return; }
            if (Career.Week.Phase != CareerWeekPhase.FixtureReady && Career.Week.Phase != CareerWeekPhase.Match) return;
            calendarOpen = false;
            preview = new GameObject("Career Fixture").AddComponent<PitchPreview>();
            preview.Initialize(Career, () => { preview = null; if (Career.Week.Phase == CareerWeekPhase.Complete) OpenReports(); }, careerFixture: true, careerRules: new CompetitionRules(shortFixture ? 120 : 2700));
        }
        private void DrawWeek()
        {
            var week = Career.Week; var p = Career.UserPlayer;
            GUI.Label(new Rect(65, 50, 1300, 45), "YOUR FIRST WEEK  ·  " + p.Name, body);
            GUI.Label(new Rect(65, 110, 1260, 60), Career.Clubs[p.ClubId].Name + " · " + Career.Coach.AssignedRole.ToUpperInvariant() +
                $" · Fatigue {p.Fatigue:P0} · Confidence {p.Confidence:P0} · Coach trust {Career.Coach.Relationship.ProfessionalTrust}", small);
            string heading = week.Phase == CareerWeekPhase.Complete ? "WEEK COMPLETE" : week.Phase == CareerWeekPhase.Match ? "FIXTURE IN PROGRESS" :
                week.Phase == CareerWeekPhase.FixtureReady ? "MATCH DAY" : Career.Calendar.TimeLabel(Career.Calendar.Minute);
            GUI.Label(new Rect(65, 215, 1260, 40), heading, body);
            string text = week.Phase == CareerWeekPhase.Complete ?
                Career.Clubs[Career.Fixture.HomeClubId].Name + " " + week.HomeGoals + " – " + week.AwayGoals + " " + Career.Clubs[Career.Fixture.AwayClubId].Name +
                (week.Abandoned ? "\nFixture abandoned: the week has ended." : "\nThe final whistle ends the prototype week.") +
                "\nSelection: " + week.KickoffSelection.Outcome + " · " + week.KickoffSelection.Reason + "\nFinal participation: " + week.World.UserParticipation +
                "\nCompleted training sessions: " + Career.Training.Results.Count + " · Meeting decisions: " + Career.Meetings.Results.Count :
                (week.Phase == CareerWeekPhase.ClubWeek ? "Training, meetings and free time lead to squad selection on Saturday and preparation on Sunday. Attend, skip or make other plans; inspect the consequences before committing.\n\n" :
                    "Fixture attendance: " + week.FixtureAttendance + ". Your club still plays if you missed the appointment.\n\n") + week.Preparation;
            GUI.Label(new Rect(65, 280, 1240, 290), text, body);
            if (week.SelectionAnnouncement != null && week.Phase != CareerWeekPhase.Complete)
                GUI.Label(new Rect(65, 585, 1230, 70), "Selection briefing: " + week.SelectionAnnouncement.Outcome + " · " + week.SelectionAnnouncement.Reason, small);
            if (week.Phase != CareerWeekPhase.Complete && GUI.Button(new Rect(65, 695, 430, 55),
                (week.Phase == CareerWeekPhase.Match ? "RESUME FIXTURE" : week.Phase == CareerWeekPhase.FixtureReady ? "PLAY / WATCH FIXTURE" : "CONTINUE WEEK") + " [ENTER]", button)) ContinueWeek();
            if (week.Phase == CareerWeekPhase.Complete && GUI.Button(new Rect(65, 695, 430, 55), "MATCH / WEEK REPORTS [P]", button)) OpenReports(true);
            if (week.Phase == CareerWeekPhase.FixtureReady && GUI.Button(new Rect(530, 695, 380, 55), shortFixture ? "4-MINUTE MATCH [L]" : "90-MINUTE MATCH [L]", button)) shortFixture = !shortFixture;
            if (GUI.Button(new Rect(965, 695, 380, 55), "NEW FOOTBALLER [C]", button)) BeginCreation();
            if (week.Phase == CareerWeekPhase.ClubWeek && GUI.Button(new Rect(65, 775, 360, 50), "EXPLORE CLUB [O]", button)) OpenClub();
            if (GUI.Button(new Rect(450, 775, 360, 50), "CONVERSATIONS [N]", button)) OpenMeetings();
            if (GUI.Button(new Rect(840, 775, 360, 50), "COACH [T]", button)) OpenCoach();
            GUI.Label(new Rect(65, 850, 1260, 30), "Returning from the pitch pauses and keeps the same fixture. Use Space to resume. Save / Load is available at the top right (F5 / F9).", small);
        }
        private void VerifyCareerWeek()
        {
            var original = Career;
            try
            {
                BeginCreation(); Balance(creationContent.footballAttributes, creationDraft.attributes, creationRules.footballBudget);
                Balance(creationContent.personalityTraits, creationDraft.traits, creationRules.personalityBudget);
                creationDraft.name = "Week Smoke Prospect"; JoinCreatedCareer();
                if (!calendarOpen || Career.Meetings.Meetings.Count != 3) throw new InvalidOperationException("Creation did not enter the authored week.");
                while (Career.Week.Phase == CareerWeekPhase.ClubWeek)
                {
                    var commitment = Career.Calendar.CurrentCommitments.FirstOrDefault();
                    PreviewCalendar(commitment == null ? CalendarChoiceKind.Replace : CalendarChoiceKind.Attend, commitment?.Id ?? "rest"); ConfirmCalendar();
                    if (Career.Meetings.Pending != null) { PreviewMeeting(0); ConfirmMeeting(); meetingsOpen = false; }
                }
                if (calendarOpen || Career.Week.SelectionAnnouncement == null) throw new InvalidOperationException("Fixture handoff failed.");
                Career.Week.StartFixture(new CompetitionRules(2, 0, .1), new MatchFitnessRules(injuryRatePerSecond: 0));
                ContinueWeek(); preview.GetComponent<MatchDriver>().enabled = false;
                var world = preview.World; var match = preview.Match;
                if (world.UserParticipation != ParticipationState.Playing || !world.CoachEnabled) throw new InvalidOperationException("Disciplined prospect should start.");
                preview.Close(); ContinueWeek(); preview.GetComponent<MatchDriver>().enabled = false;
                if (preview.World != world || preview.Match != match) throw new InvalidOperationException("Returning to the pitch recreated the fixture.");
                match.Resume();
                for (int i = 0; i < 20000 && Career.Week.Phase != CareerWeekPhase.Complete; i++)
                { if (world.HasPendingDecision || preview.UserRestart || world.RefereeNoticePending || world.OffsideNoticePending || world.FixturePhase == FixturePhase.HalfTime) preview.CommitChosenPass(); match.Advance(TimeSpan.FromMilliseconds(50)); }
                if (Career.Week.Phase != CareerWeekPhase.Complete) throw new InvalidOperationException("Career week did not finish.");
                preview.Close(); Career.Week.World.Dispose(); Debug.Log("FOOTBALL_RPG_CAREER_WEEK_PASSED");
            }
            finally { if (preview != null) preview.Close(); Career = original; creationDraft = null; reportsOpen = calendarOpen = meetingsOpen = false; }
        }
    }
}
