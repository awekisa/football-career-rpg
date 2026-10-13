using System;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Presentation;
using FootballRpg.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace FootballRpg.Tests
{
    public sealed class ClubExplorationTests
    {
        private CareerState career;
        private ClubExploration club;
        [SetUp] public void Setup() { career = PrototypeContent.CreateCareer(); club = new ClubExploration(career, PrototypeContent.LoadClubVenue()); }
        private void Walk(string id) { Assert.That(club.WalkTo(id), Is.True); club.Advance(120); Assert.That(club.AtDestination, Is.True); }
        private void WaitUntil(int slot) { while (career.Calendar.Minute < slot * 360) career.Calendar.Commit(career.Calendar.Preview(CalendarChoiceKind.Wait)); }
        private static ClubVenueDefinition Definition() => JsonUtility.FromJson<ClubVenueDefinition>(Resources.Load<TextAsset>("ClubVenue").text);
        [Test] public void EveryDestinationIsReachableWithoutChangingAnyCareerState()
        {
            var traits = career.UserPlayer.PersonalityTraits.ToArray(); var skills = career.UserPlayer.FootballAttributes.ToArray();
            float fatigue = career.UserPlayer.Fatigue, morale = career.UserPlayer.Morale;
            foreach (var destination in club.Venue.Destinations)
            {
                club.Inspect(destination.Id); Walk(destination.Id);
                Assert.That(club.Destination, Is.SameAs(destination));
                for (int i = 0; i < 10; i++) club.Inspect(destination.Id);
            }
            Assert.That(career.Calendar.Minute, Is.Zero); Assert.That(career.Calendar.Attendance, Is.Empty);
            Assert.That(career.Training.Results, Is.Empty); Assert.That(career.Progression.Changes, Is.Empty);
            Assert.That(career.UserPlayer.Fatigue, Is.EqualTo(fatigue)); Assert.That(career.UserPlayer.Morale, Is.EqualTo(morale));
            Assert.That(career.UserPlayer.PersonalityTraits.ToArray(), Is.EqualTo(traits)); Assert.That(career.UserPlayer.FootballAttributes.ToArray(), Is.EqualTo(skills));
        }
        [Test] public void PathsUseOpenAdjacentCellsAndNeverCutThroughWallsOrFurniture()
        {
            foreach (var destination in club.Venue.Destinations)
            {
                club.WalkTo(destination.Id); var path = club.Path;
                for (int i = 0; i < path.Count; i++)
                {
                    Assert.That(club.Venue.Cell(path[i], out _, out _), Is.True);
                    if (i > 0) Assert.That(path[i].Distance(path[i - 1]), Is.EqualTo(club.Venue.CellSize).Within(.0001));
                }
                for (int i = 0; i < 2000 && club.Moving; i++)
                { club.Advance(.025); Assert.That(club.Venue.Cell(club.Position, out _, out _), Is.True); }
                Assert.That(club.AtDestination, Is.True);
            }
        }
        [Test] public void WalkingIsContinuousAndCanBeRedirectedMidSegment()
        {
            var start = club.Position; club.WalkTo("coach");
            Assert.That(club.Position.Distance(start), Is.Zero); Assert.That(club.AtDestination, Is.False);
            Assert.Throws<InvalidOperationException>(() => club.Interact());
            club.Advance(.1); Assert.That(club.Position.Distance(start), Is.EqualTo(.4).Within(.0001));
            club.WalkTo("media"); var redirected = club.Position;
            club.Advance(.02); Assert.That(club.Position.Distance(redirected), Is.LessThanOrEqualTo(.08001));
            club.Advance(120); Assert.That(club.AtDestination, Is.True); Assert.That(club.Destination.Id, Is.EqualTo("media"));
            Assert.That(career.Calendar.Minute, Is.Zero);
        }
        [Test] public void SplitFrameDurationsArriveAtTheSamePosition()
        {
            var other = new ClubExploration(career, club.Venue); club.WalkTo("media"); other.WalkTo("media");
            club.Advance(2); for (int i = 0; i < 100; i++) other.Advance(.02);
            Assert.That(club.Position.Distance(other.Position), Is.LessThan(.0001));
        }
        [Test] public void UnreachableRequestsStopMovementAndKeepAnExplanation()
        {
            club.WalkTo("coach"); club.Advance(.1); var before = club.Position;
            foreach (var point in new[] { club.Venue.Point(0, 0), club.Venue.Point(10, 5), new PitchPoint(1e9, 0), new PitchPoint(double.NaN, 0) })
            {
                Assert.That(club.WalkTo(point), Is.False); Assert.That(club.Moving, Is.False);
                club.Advance(120); Assert.That(club.Message, Does.StartWith("Unreachable"));
                Assert.That(club.Position.Distance(before), Is.Zero);
            }
            Assert.That(club.WalkTo("missing"), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => club.Advance(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => club.Advance(double.NaN));
        }
        [Test] public void ArrivalOpensInspectionAndTrainingOnlySpendsTimeAfterConfirmation()
        {
            Assert.That(club.Interact().Kind, Is.EqualTo(ClubInteractionKind.Calendar));
            Walk("training"); var interaction = club.Interact();
            Assert.That(interaction.Kind, Is.EqualTo(ClubInteractionKind.Activity)); Assert.That(interaction.Choice.CostMinutes, Is.EqualTo(360));
            Assert.That(career.Calendar.Minute, Is.Zero);
            career.Calendar.Commit(interaction.Choice); career.Calendar.Commit(interaction.Choice);
            Assert.That(career.Calendar.Minute, Is.EqualTo(360)); Assert.That(career.Training.Results.Count, Is.EqualTo(1));
            var extra = club.Interact(); Assert.That(extra.Choice.Kind, Is.EqualTo(CalendarChoiceKind.ExtraPractice));
            career.Calendar.Commit(extra.Choice); Assert.That(career.Calendar.Minute, Is.EqualTo(720));
            Assert.That(club.Inspect("training").Available, Is.False);
        }
        [Test] public void MediaRequiresAnActualAppointmentAndCannotBeRepeated()
        {
            Walk("media"); Assert.That(club.Inspect("media").Available, Is.False);
            Assert.That(club.Inspect("media").Description, Does.Contain("small club"));
            Assert.Throws<InvalidOperationException>(() => club.Interact()); WaitUntil(2);
            var interaction = club.Interact(); Assert.That(interaction.Choice.Commitment.Id, Is.EqualTo("mon-media"));
            career.Calendar.Commit(interaction.Choice);
            Assert.That(club.Inspect("media").Available, Is.False); Assert.That(club.Inspect("media").Description, Does.Contain("No further appointment"));
        }
        [Test] public void AgentMeetingAndTravelUseExistingCalendarAppointments()
        {
            Walk("agent"); Assert.That(club.Inspect("agent").Available, Is.False);
            WaitUntil(8); Walk("arrival"); var travel = club.Interact();
            Assert.That(travel.Choice.Activity.Id, Is.EqualTo("travel")); Assert.That(travel.Choice.CostMinutes, Is.EqualTo(720));
            career.Calendar.Commit(travel.Choice); WaitUntil(17); Walk("agent");
            var agent = club.Interact(); Assert.That(agent.Choice.Commitment.Id, Is.EqualTo("fri-agent"));
            career.Calendar.Commit(agent.Choice); Assert.That(club.Inspect("agent").Available, Is.False);
        }
        [Test] public void CoachAndTeammatesFollowSquadScheduleAndOfficeHours()
        {
            Assert.That(club.Inspect("coach").Available, Is.False); Assert.That(club.Inspect("teammates").Available, Is.False);
            WaitUntil(1); Walk("coach"); Assert.That(club.Interact().Kind, Is.EqualTo(ClubInteractionKind.TrainingGuidance));
            Walk("teammates"); Assert.That(club.Interact().Kind, Is.EqualTo(ClubInteractionKind.Squad));
            Assert.That(career.Calendar.Minute, Is.EqualTo(360)); WaitUntil(3);
            Assert.That(club.Inspect("coach").Description, Does.Contain("night")); Assert.That(club.Inspect("teammates").Available, Is.False);
        }
        [Test] public void InjuryAndPendingPracticeRefreshAvailabilityWithoutHealingFromWalking()
        {
            typeof(PlayerState).GetProperty("InjuryDaysRemaining").SetValue(career.UserPlayer, 1);
            Walk("training"); Assert.That(club.Interact().Kind, Is.EqualTo(ClubInteractionKind.Calendar));
            Assert.That(club.Interact().Description, Does.Contain("injured")); Assert.That(career.UserPlayer.InjuryDaysRemaining, Is.EqualTo(1));
            typeof(PlayerState).GetProperty("InjuryDaysRemaining").SetValue(career.UserPlayer, 0);
            career.Calendar.Commit(career.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training", practice: PracticeFamily.Passing));
            Assert.That(club.Inspect("training").Available, Is.False); Assert.That(club.Inspect("arrival").Available, Is.False);
            career.Training.FinishPractice(); Assert.That(club.Inspect("training").Available, Is.True);
        }
        [Test] public void WeekEndKeepsWalkingButDisablesNewActivities()
        {
            WaitUntil(28); foreach (var d in club.Venue.Destinations) { Walk(d.Id); Assert.That(club.Inspect(d.Id).Available, Is.False); }
            Assert.That(career.Calendar.Minute, Is.EqualTo(10080));
        }
        [Test] public void MapCopiesDataAndRejectsBlockedOrDisconnectedDestinations()
        {
            var d = Definition(); var map = new ClubVenue(d); d.rows[2] = new string('#', 21); d.destinations[0].name = "Changed";
            Assert.That(map.Walkable(10, 2), Is.True); Assert.That(map.Destinations[0].Name, Is.Not.EqualTo("Changed"));
            d = Definition(); d.destinations[0].x = 0; Assert.Throws<ArgumentException>(() => new ClubVenue(d));
            d = Definition(); d.rows[7] = new string('#', 21); Assert.Throws<ArgumentException>(() => new ClubVenue(d));
            d = Definition(); d.cellSize = float.NaN; Assert.Throws<ArgumentException>(() => new ClubVenue(d));
            d = Definition(); d.destinations[0].id = "coach"; Assert.Throws<ArgumentException>(() => new ClubVenue(d));
        }
        [Test] public void DisconnectedOpenGroundIsReportedAsUnreachable()
        {
            var d = Definition(); var rows = d.rows.Select(s => s.ToCharArray()).ToArray();
            rows[1][1] = '.'; rows[1][2] = '#'; rows[2][1] = '#'; d.rows = rows.Select(r => new string(r)).ToArray();
            var isolated = new ClubExploration(career, new ClubVenue(d));
            Assert.That(isolated.WalkTo(isolated.Venue.Point(1, 1)), Is.False); Assert.That(isolated.Message, Does.Contain("no open route"));
        }
        [Test] public void LaunchHandsOffTrainingPreviewAndReturnsToSameClubPosition()
        {
            var root = new GameObject("Club launch test"); ClubVenueView view = null;
            try
            {
                var launch = root.AddComponent<LaunchScreen>();
                typeof(LaunchScreen).GetProperty("Career").SetValue(launch, career);
                launch.OpenClub();
                var field = typeof(LaunchScreen).GetField("clubView", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                view = (ClubVenueView)field.GetValue(launch);
                view.Navigate("training"); view.AdvanceWalk(120); var position = view.Exploration.Position;
                view.Interact();
                Assert.That(career.Calendar.Minute, Is.Zero);
                var choice = (CalendarChoice)typeof(LaunchScreen).GetField("calendarChoice", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(launch);
                Assert.That(choice.Commitment.Id, Is.EqualTo("mon-training"));
                typeof(LaunchScreen).GetMethod("ConfirmCalendar", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(launch, null);
                Assert.That(career.Calendar.Minute, Is.EqualTo(360));
                typeof(LaunchScreen).GetMethod("CloseCalendar", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(launch, null);
                view = (ClubVenueView)field.GetValue(launch);
                Assert.That(view.Exploration.Position.Distance(position), Is.Zero);
                Assert.That(view.Exploration.Interact().Choice.Kind, Is.EqualTo(CalendarChoiceKind.ExtraPractice));
                view.Close(); view = null;
                var nextCareer = PrototypeContent.CreateCareer(); typeof(LaunchScreen).GetProperty("Career").SetValue(launch, nextCareer);
                launch.OpenClub(); view = (ClubVenueView)field.GetValue(launch);
                Assert.That(view.Exploration.Destination.Id, Is.EqualTo("arrival")); Assert.That(view.Exploration.Career, Is.SameAs(nextCareer));
            }
            finally { if (view != null) view.Close(); UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test] public void MouseRaysReachAllDestinationsAndClosingRestoresTheCamera()
        {
            GameObject cameraRoot = null; if (Camera.main == null) { cameraRoot = new GameObject("Camera", typeof(Camera)); cameraRoot.tag = "MainCamera"; }
            var original = Camera.main; var root = new GameObject("Club view test");
            try
            {
                var view = root.AddComponent<ClubVenueView>(); view.Initialize(club);
                Assert.That(original.enabled, Is.False);
                foreach (var d in club.Venue.Destinations)
                {
                    var p = club.Venue.Point(d); var screen = view.ViewCamera.WorldToScreenPoint(new Vector3((float)p.X, 0, (float)p.Z));
                    Assert.That(view.SelectAtScreenPoint(screen), Is.True, d.Id); view.AdvanceWalk(120);
                    Assert.That(club.Destination.Id, Is.EqualTo(d.Id)); Assert.That(club.AtDestination, Is.True);
                }
                Assert.That(view.SelectAtScreenPoint(new Vector3(Screen.width - 1, 0, 0)), Is.False);
                Assert.That(career.Calendar.Minute, Is.Zero); view.Close(); Assert.That(original.enabled, Is.True);
            }
            finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); if (cameraRoot != null) UnityEngine.Object.DestroyImmediate(cameraRoot); }
        }
    }
}
