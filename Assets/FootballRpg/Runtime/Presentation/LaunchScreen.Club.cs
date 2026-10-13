using System;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Simulation;
using UnityEngine;

namespace FootballRpg.Presentation
{
    public sealed partial class LaunchScreen
    {
        private ClubExploration clubExploration;
        private ClubVenueView clubView;
        private bool returnToClub;
        public void OpenClub()
        {
            if (Career?.Week.CalendarBlock != null) return;
            if (Career == null || preview != null || practiceView != null || clubView != null || (Career.Calendar.AwaitingPractice || Career.Calendar.AwaitingMeeting)) return;
            if (clubExploration == null || clubExploration.Career != Career) clubExploration = new ClubExploration(Career, PrototypeContent.LoadClubVenue());
            clubView = new GameObject("Club Exploration").AddComponent<ClubVenueView>();
            clubView.Initialize(clubExploration, () => clubView = null, OpenClubInteraction);
        }
        private void OpenClubInteraction(ClubInteraction interaction)
        {
            clubView.Close(); OpenCalendar(); returnToClub = true;
            if (interaction.Kind == ClubInteractionKind.TrainingGuidance) coachDiscussion = true;
            if (interaction.Choice != null)
            {
                calendarSelection = interaction.Choice.Commitment?.Id;
                PreviewCalendar(interaction.Choice.Kind, interaction.Choice.Commitment?.Id ?? interaction.Choice.Activity?.Id);
            }
        }
        private void CloseCalendar()
        {
            calendarOpen = false; calendarChoice = null;
            if (returnToClub) { returnToClub = false; OpenClub(); }
        }
        private void VerifyClub()
        {
            var c = PrototypeContent.CreateCareer(); var model = new ClubExploration(c, PrototypeContent.LoadClubVenue());
            var root = new GameObject("Club Smoke"); var view = root.AddComponent<ClubVenueView>(); view.Initialize(model);
            foreach (var d in model.Venue.Destinations)
            {
                var point = view.ViewCamera.WorldToScreenPoint(new Vector3((float)model.Venue.Point(d).X, 0, (float)model.Venue.Point(d).Z));
                if (!view.SelectAtScreenPoint(point)) throw new InvalidOperationException("Club destination picking failed: " + d.Id);
                view.AdvanceWalk(120);
                if (!model.AtDestination || model.Destination.Id != d.Id || c.Calendar.Minute != 0 || c.Calendar.Attendance.Count != 0)
                    throw new InvalidOperationException("Club navigation changed career state or failed to arrive.");
            }
            if (model.Inspect("media").Available || model.Inspect("coach").Available) throw new InvalidOperationException("Unavailable club appointments were offered.");
            var interaction = model.Interact();
            if (interaction.Choice?.Activity.Id != "training") throw new InvalidOperationException("Missing training entrance interaction.");
            c.Calendar.Commit(interaction.Choice);
            if (c.Calendar.Minute != 360 || c.Training.Results.Count != 1) throw new InvalidOperationException("Club attendance did not use the calendar.");
            view.Close();
            Debug.Log("FOOTBALL_RPG_CLUB_EXPLORATION_PASSED");
        }
    }
}
