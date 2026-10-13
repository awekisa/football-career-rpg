using System;
using System.Collections.Generic;
using System.Linq;

namespace FootballRpg.Simulation
{
    public sealed class ReportCause
    {
        public string MetricId { get; }
        public string EventId { get; }
        public string Explanation { get; }
        public double Delta { get; }
        internal ReportCause(string metric, string eventId, string explanation, double delta)
        { MetricId = metric; EventId = eventId; Explanation = explanation; Delta = delta; }
    }
    public sealed class ReportMetric
    {
        public string Id { get; }
        public string Label { get; }
        public string Unit { get; }
        public double Before { get; }
        public double After { get; }
        public double Delta => After - Before;
        public IReadOnlyList<ReportCause> Causes { get; }
        internal ReportMetric(string id, string label, string unit, double before, double after, IEnumerable<ReportCause> causes)
        { Id = id; Label = label; Unit = unit; Before = before; After = after; Causes = Array.AsReadOnly(causes.ToArray()); }
    }
    public sealed class ReportAction
    {
        public string EventId { get; }
        public string ActionId { get; }
        public string Variant { get; }
        public string Outcome { get; }
        public string Target { get; }
        public float Chance { get; }
        public bool Succeeded { get; }
        public double ElapsedSeconds { get; }
        internal ReportAction(ActionDiagnostic d, CareerState career)
        {
            EventId = d.Result.EventId; ActionId = d.Result.ActionId; Outcome = d.Result.OutcomeId;
            Target = career.Players.TryGetValue(d.Result.TargetId, out var p) ? p.Name : d.Result.TargetId;
            Variant = d.Request.Context.TryGetValue("variant", out var variant) ? variant : "";
            Chance = d.Result.SuccessProbability; Succeeded = d.Result.Succeeded;
            ElapsedSeconds = TimeSpan.FromTicks(d.SimulationTimeTicks).TotalSeconds;
        }
    }
    public sealed class ReportStatistic
    {
        public string ActionId { get; }
        public int Attempts { get; }
        public int Successful { get; }
        public int Failed => Attempts - Successful;
        internal ReportStatistic(string id, IEnumerable<ReportAction> actions)
        { ActionId = id; var matching = actions.Where(a => a.ActionId == id).ToArray(); Attempts = matching.Length; Successful = matching.Count(a => a.Succeeded); }
    }
    /// <summary>Frozen evidence, not a reward command. Kept by the career after pitch views close.</summary>
    public sealed class CareerReport
    {
        public string Title { get; }
        public string FixtureId { get; }
        public string PlayerName { get; }
        public string Role { get; }
        public string Score { get; }
        public string Selection { get; }
        public ParticipationState Participation { get; }
        public bool Abandoned { get; }
        public string Currency { get; }
        public long BalanceBefore { get; }
        public long BalanceAfter { get; }
        public long WageBefore { get; }
        public long WageAfter { get; }
        public IReadOnlyList<ReportMetric> Metrics { get; }
        public IReadOnlyList<ReportCause> Causes { get; }
        public IReadOnlyList<ReportAction> Actions { get; }
        public IReadOnlyList<ReportStatistic> Statistics { get; }
        public IReadOnlyList<string> ParticipationEvents { get; }
        public double XpGained => Causes.Where(c => c.MetricId.StartsWith("xp:") && c.Delta > 0).Sum(c => c.Delta);
        public double XpLost => -Causes.Where(c => c.MetricId.StartsWith("xp:") && c.Delta < 0).Sum(c => c.Delta);
        internal CareerReport(string title, CareerState c, ReportSnapshot start, ReportSnapshot end, List<ReportCause> causes)
        {
            Title = title; FixtureId = c.Fixture.Id; PlayerName = c.UserPlayer.Name; Role = c.Week.KickoffSelection.Role;
            Score = c.Clubs[c.Fixture.HomeClubId].Name + " " + c.Week.HomeGoals + " – " + c.Week.AwayGoals + " " + c.Clubs[c.Fixture.AwayClubId].Name;
            Selection = c.Week.KickoffSelection.Outcome + " · " + c.Week.KickoffSelection.Reason;
            Participation = c.Week.World.UserParticipation; Abandoned = c.Week.Abandoned;
            Currency = c.UserPlayer.CurrencyCode; BalanceBefore = start.Balance; BalanceAfter = end.Balance; WageBefore = start.Wage; WageAfter = end.Wage;
            Causes = causes.AsReadOnly();
            Metrics = Array.AsReadOnly(start.Values.Select(k => new ReportMetric(k.Key, k.Value.label, k.Value.unit, k.Value.value,
                end.Values[k.Key].value, causes.Where(x => x.MetricId == k.Key))).ToArray());
            Actions = Array.AsReadOnly(c.Week.Match.Diagnostics.Where(d => d.Result.PlayerId == c.UserPlayerId).Select(d => new ReportAction(d, c)).ToArray());
            var expected = Role == "gk" ? new[] { "save", "claim", "distribute", "control", "tackle", "contain", "pass-to-feet" } :
                new[] { "pass-to-feet", "through-ball", "shoot", "header", "cross", "dribble", "control", "tackle", "contain", "block", "clear" };
            Statistics = Array.AsReadOnly(expected.Concat(Actions.Select(a => a.ActionId)).Distinct().Select(id => new ReportStatistic(id, Actions)).ToArray());
            ParticipationEvents = Array.AsReadOnly(c.Week.World.ParticipationEvents.Where(e => e.PlayerId == c.UserPlayerId || e.ReplacementId == c.UserPlayerId)
                .Select(e => e.Kind + ": " + c.Players[e.PlayerId].Name + (e.ReplacementId == null ? "" : " → " + c.Players[e.ReplacementId].Name) + (e.Severity.HasValue ? " · " + e.Severity : "")).ToArray());
        }
    }
    internal sealed class ReportSnapshot
    {
        internal readonly Dictionary<string, (string label, string unit, double value)> Values = new Dictionary<string, (string, string, double)>();
        internal int Training, Progression, Readiness, Social;
        internal long Balance, Wage;
        internal ReportSnapshot(CareerState c)
        {
            Training = c.Training.Results.Count; Progression = c.Progression.Changes.Count; Readiness = c.Readiness.Changes.Count; Social = c.Social.Changes.Count;
            Balance = c.UserPlayer.BalanceMinor; Wage = c.UserPlayer.WeeklyWageMinor;
            foreach (var s in c.Progression.ForPlayer(c.UserPlayerId))
            { Values.Add("xp:" + s.Key, (s.Key.Replace('-', ' '), "XP", s.Value.TotalXp)); Values.Add("level:" + s.Key, (s.Key.Replace('-', ' ') + " level", "levels", s.Value.Level)); }
            Values.Add("fatigue", ("Fatigue", "points", c.UserPlayer.Fatigue * 100.0));
            Values.Add("confidence", ("Confidence", "points", c.UserPlayer.Confidence * 100.0));
            Values.Add("morale", ("Morale", "points", c.UserPlayer.Morale * 100.0));
            Values.Add("injury", ("Injury recovery remaining", "days", c.UserPlayer.InjuryDaysRemaining));
            foreach (var r in c.Social.Relationships.Values)
            {
                string label = r.Name + " (" + r.Role + ") · ";
                Values.Add("social:" + r.PersonId + ":affinity", (label + "affinity", "points", r.Affinity));
                Values.Add("social:" + r.PersonId + ":professional-trust", (label + "trust", "points", r.ProfessionalTrust));
                Values.Add("social:" + r.PersonId + ":standing", (label + "standing", "points", r.Standing));
            }
        }
    }
    public sealed class CareerReports
    {
        private readonly CareerState career;
        private ReportSnapshot weekStart, matchStart;
        public CareerReport Match { get; private set; }
        public CareerReport Week { get; private set; }
        internal CareerReports(CareerState career) { this.career = career; }
        internal void BeginWeek() { if (weekStart == null) weekStart = new ReportSnapshot(career); }
        internal void BeginMatch() { if (matchStart == null) matchStart = new ReportSnapshot(career); }
        internal void Complete()
        {
            if (Week != null || weekStart == null || matchStart == null || career.Week.Phase != CareerWeekPhase.Complete) return;
            var end = new ReportSnapshot(career);
            Match = new CareerReport("MATCH AFTERMATH", career, matchStart, end, Changes(matchStart));
            Week = new CareerReport("WEEK COMPLETION", career, weekStart, end, Changes(weekStart));
        }
        private List<ReportCause> Changes(ReportSnapshot start)
        {
            var rows = new List<ReportCause>();
            foreach (var report in career.Training.Results.Skip(start.Training))
            foreach (var gain in report.Gains)
            {
                string reason = report.FocusName + " · " + report.ParticipatedSlots.ToString("0.##") + " training slot(s)";
                rows.Add(new ReportCause("xp:" + gain.SkillId, report.AttendanceId, reason, gain.Xp));
                if (gain.NewLevel != gain.PreviousLevel) rows.Add(new ReportCause("level:" + gain.SkillId, report.AttendanceId, reason, gain.NewLevel - gain.PreviousLevel));
            }
            foreach (var change in career.Progression.Changes.Skip(start.Progression).Where(c => c.PlayerId == career.UserPlayerId))
            foreach (var delta in change.Deltas)
            {
                string reason = change.ActionId.Replace('-', ' ') + (change.Succeeded ? " succeeded" : " failed") + " · actual XP after repetition limits and skill bounds";
                rows.Add(new ReportCause("xp:" + delta.Key, change.EventId, reason, delta.Value));
                int levels = change.NewLevels[delta.Key] - change.PreviousLevels[delta.Key];
                if (levels != 0) rows.Add(new ReportCause("level:" + delta.Key, change.EventId, reason, levels));
            }
            foreach (var c in career.Readiness.Changes.Skip(start.Readiness).Where(c => c.PlayerId == career.UserPlayerId))
            {
                if (c.FatigueDelta != 0) rows.Add(new ReportCause("fatigue", c.CauseId, c.Reason, c.FatigueDelta * 100));
                if (c.ConfidenceDelta != 0) rows.Add(new ReportCause("confidence", c.CauseId, c.Reason, c.ConfidenceDelta * 100));
                if (c.MoraleDelta != 0) rows.Add(new ReportCause("morale", c.CauseId, c.Reason, c.MoraleDelta * 100));
                if (c.InjuryDaysDelta != 0) rows.Add(new ReportCause("injury", c.CauseId, c.Reason, c.InjuryDaysDelta));
            }
            foreach (var c in career.Social.Changes.Skip(start.Social))
                rows.Add(new ReportCause("social:" + c.OtherPersonId + ":" + c.DimensionId, c.CauseEventId, ExplainSocial(c.CauseEventId), c.CurrentValue - c.PreviousValue));
            return rows;
        }
        private string ExplainSocial(string cause)
        {
            var coach = career.Coach.Reactions.FirstOrDefault(r => r.CauseId == cause); if (coach != null) return "Coach: " + coach.Reason;
            var conversation = career.Social.Results.FirstOrDefault(r => r.Preview.Id == cause); if (conversation != null) return conversation.Preview.Option.Text + " · " + conversation.Outcome;
            var meeting = career.Meetings.Results.FirstOrDefault(r => r.Changes.Any(c => c.CauseEventId == cause)); if (meeting != null) return meeting.Meeting.Role + " meeting: " + meeting.Text;
            var attendance = career.Calendar.Attendance.FirstOrDefault(a => cause.StartsWith(a.Fact.EventId + ":") || cause == a.Fact.EventId);
            if (attendance != null) return career.Calendar.Activities.Single(a => a.Id == attendance.Fact.ActivityId).Name + ": " + attendance.Fact.Attendance;
            return "Relationship consequence";
        }
    }
}
