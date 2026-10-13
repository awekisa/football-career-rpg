using System;
using System.Linq;

namespace FootballRpg.Simulation
{
    public enum CareerWeekPhase { NotStarted, ClubWeek, FixtureReady, Match, Complete }

    /// <summary>Owns the single career fixture across views. Calendar choices pay time once; match ticks never advance it.</summary>
    public sealed class CareerWeek
    {
        private readonly CareerState career;
        private CalendarAttendance fixtureAttendance;
        public CareerWeekPhase Phase { get; private set; }
        public bool Active => Phase != CareerWeekPhase.NotStarted;
        public CoachSelection SelectionAnnouncement { get; private set; }
        public CoachSelection KickoffSelection => World?.CoachSelection;
        public MatchSession Match { get; private set; }
        public OpenPlayWorld World { get; private set; }
        public int HomeGoals { get; private set; }
        public int AwayGoals { get; private set; }
        public bool Abandoned { get; private set; }
        public AttendanceOutcome? FixtureAttendance => fixtureAttendance?.Fact.Attendance;
        public string CalendarBlock => Phase == CareerWeekPhase.Complete ? "The prototype week is complete." :
            Phase == CareerWeekPhase.FixtureReady || Phase == CareerWeekPhase.Match ? "Finish the scheduled fixture before advancing career time." : null;
        public string Preparation => career.Coach.Select().Reason + "\nRole: " + career.Coach.AssignedRole.ToUpperInvariant() +
            " · In possession: " + career.Coach.Instruction(true, false, career.Coach.AssignedRole) +
            " · Defending: " + career.Coach.Instruction(false, false, career.Coach.AssignedRole) +
            "\nSelection is confirmed at kickoff using your current readiness and relationship. Late arrival means the bench; missing the fixture means you cannot play.";
        internal CareerWeek(CareerState career) { this.career = career; }
        public void Begin()
        {
            if (Active) return;
            if (career.Calendar == null || career.Calendar.Minute != 0 || career.Calendar.History.Count != 0 ||
                career.Calendar.Commitments.Count(c => c.Activity.Id == "match") != 1)
                throw new InvalidOperationException("Start a new career with one scheduled fixture.");
            Phase = CareerWeekPhase.ClubWeek;
            career.Reports.BeginWeek();
        }
        internal void ApplyCalendar(CalendarDecision decision)
        {
            if (!Active) return;
            if (decision.Attendance.Any(a => a.Fact.ActivityId == "selection")) SelectionAnnouncement = career.Coach.Select();
            var fixture = decision.Attendance.SingleOrDefault(a => a.Fact.ActivityId == "match");
            if (fixture == null) return;
            fixtureAttendance = fixture; Phase = CareerWeekPhase.FixtureReady;
        }
        public OpenPlayWorld StartFixture(CompetitionRules rules = null, MatchFitnessRules fitnessRules = null)
        {
            if (World != null) return World;
            if (Phase != CareerWeekPhase.FixtureReady || career.Calendar.AwaitingMeeting || career.Calendar.AwaitingPractice)
                throw new InvalidOperationException("Resolve the scheduled fixture attendance first.");
            career.Reports.BeginMatch();
            Match = new MatchSession(career.Fixture.Id + "-career-week", 42);
            World = new OpenPlayWorld(career, Match, career.UserPlayerId);
            World.FixtureChanged += OnFixtureChanged;
            Phase = CareerWeekPhase.Match;
            World.StartFixture(rules, fitnessRules, applyCoachSelection: true,
                userAbsent: FixtureAttendance == AttendanceOutcome.Skipped, userLate: FixtureAttendance == AttendanceOutcome.Late);
            return World;
        }
        private void OnFixtureChanged(FixtureEvent fact)
        {
            if (fact.Kind != "FullTime" && fact.Kind != "Abandoned") return;
            HomeGoals = World.HomeGoals; AwayGoals = World.AwayGoals; Abandoned = World.Abandoned;
            Phase = CareerWeekPhase.Complete;
            career.Reports.Complete();
        }
    }
}
