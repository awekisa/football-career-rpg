using System;
using System.Collections.Generic;
using System.Linq;

namespace FootballRpg.Simulation
{
    public sealed partial class OpenPlayWorld
    {
        public DisciplineRecord Discipline { get; } = new DisciplineRecord();
        public AdvantageState Advantage { get; private set; }
        public FoulVerdict LastFoul { get; private set; }
        public string RefereeMessage { get; private set; }
        public bool RefereeNoticePending { get; private set; }
        public bool Abandoned { get; private set; }
        public double KeeperHoldSeconds { get; private set; }
        private readonly HashSet<string> foulIds = new HashSet<string>();
        private readonly List<FoulVerdict> deferredCards = new List<FoulVerdict>();
        private string timedKeeperId;
        private bool penaltyInProgress;
        private readonly Dictionary<string, FootballActionResolved> restartShots = new Dictionary<string, FootballActionResolved>();

        public bool CanParticipate(string playerId) => !Discipline.IsDismissed(playerId) && players.Any(p => p.Player.Id == playerId);
        private void NotifyReferee(string message, bool pause = true)
        {
            RefereeMessage = message;
            if (pause && controlledPlayerId != null) { RefereeNoticePending = true; session.Pause(); }
        }
        public void AcknowledgeReferee()
        {
            if (!RefereeNoticePending) return;
            RefereeNoticePending = false;
            if (!HasPendingDecision && !OffsideNoticePending && Restart?.TakerId != controlledPlayerId && FixturePhase != FixturePhase.HalfTime && FixturePhase != FixturePhase.FullTime) session.Resume();
        }
        private bool JudgeChallenge(DefenseResolved result, FootballerMotion defender, FootballerMotion attacker)
        {
            if (Rules == null) return false;
            var severity = FoulRules.ChallengeSeverity(result);
            if (severity == ContactSeverity.Fair) return false;
            double distance = attacker.Position.Distance(new PitchPoint(52.5 * attacker.Direction, 0));
            int covering = players.Count(p => p.IsHome == defender.IsHome && p != defender &&
                (p.Position.X - attacker.Position.X) * attacker.Direction > 0 && Math.Abs(p.Position.Z - attacker.Position.Z) < 16);
            bool towardGoal = attacker.Facing.X * attacker.Direction > 0;
            var incident = new FoulIncident(result.EventId + ":foul", defender.Player.Id, attacker.Player.Id, attacker.Position, defender.Direction,
                FoulKind.Challenge, severity, true, distance < 25 && towardGoal && covering <= 1, distance < 40 && towardGoal && covering <= 2);
            return HandleFoul(incident);
        }
        // Evidence enters here from committed actions, never from wall-clock time or an extra referee roll.
        private bool HandleFoul(FoulIncident incident)
        {
            if (Rules == null || Restart != null || !CanParticipate(incident.OffenderId) || !foulIds.Add(incident.Id)) return false;
            var verdict = FoulRules.Evaluate(incident, BallState != BallPhase.OutOfPlay);
            if (verdict == null) return false;
            LastFoul = verdict;
            // An unresolved earlier advantage takes precedence; no second award for the same passage.
            if (Advantage != null) { RecallAdvantage(); return true; }
            var offender = players.Single(p => p.Player.Id == incident.OffenderId);
            bool safeAdvantage = verdict.Card != CardKind.Red && !(verdict.Card == CardKind.Yellow && Discipline.Yellows(incident.OffenderId) > 0);
            bool retained = OwnerId == incident.VictimId && PossessionClubId != offender.Player.ClubId;
            if (safeAdvantage && retained && verdict.Restart != RestartKind.Penalty && incident.Kind == FoulKind.Challenge && incident.Severity != ContactSeverity.Excessive)
            {
                var victim = players.Single(p => p.Player.Id == incident.VictimId);
                Advantage = new AdvantageState(verdict, session.Tick + Ticks(3), victim.Player.ClubId, victim.Direction);
                RecordFixture("Advantage", victim.IsHome);
                NotifyReferee("ADVANTAGE — " + verdict.Explanation + " Callback within 3 match seconds.", false);
                return false;
            }
            StopForFoul(verdict); return true;
        }
        private void StopForFoul(FoulVerdict verdict)
        {
            LastFoul = verdict;
            var offender = players.Single(p => p.Player.Id == verdict.Incident.OffenderId);
            bool awardedHome = !offender.IsHome;
            string name = offender.Player.Name;
            string sanction = ApplyCard(verdict, verdict.Card);
            if (!Abandoned) AwardRestart(verdict.Restart, awardedHome, verdict.Spot);
            NotifyReferee(name + ": " + verdict.Explanation + " " + sanction + " " + (Abandoned ? "Match abandoned: fewer than seven players." : RestartLabel(verdict.Restart) + " awarded."));
        }
        public static string RestartLabel(RestartKind kind) => kind == RestartKind.IndirectFreeKick ? "Indirect free kick" : kind == RestartKind.DirectFreeKick ? "Direct free kick" : kind == RestartKind.Penalty ? "Penalty kick" : kind.ToString();
        private void RealizeAdvantage()
        {
            if (Advantage == null) return;
            deferredCards.Add(Advantage.Verdict); Advantage = null;
            RecordFixture("AdvantageRealised"); NotifyReferee("Advantage realised — play continues; any required card follows at the next stoppage.", false);
        }
        private void RecallAdvantage()
        {
            if (Advantage == null) return;
            var verdict = Advantage.Verdict; Advantage = null;
            RecordFixture("AdvantageRecalled"); StopForFoul(verdict);
        }
        private bool StepReferee()
        {
            if (Rules == null) return false;
            if (Advantage != null)
            {
                if (PossessionClubId != Advantage.ClubId || BallState == BallPhase.Loose || BallState == BallPhase.OutOfPlay)
                { RecallAdvantage(); return true; }
                if (OwnerId != null && (BallPosition.X - Advantage.Verdict.Incident.Spot.X) * Advantage.Direction >= 6) RealizeAdvantage();
                else if (session.Tick >= Advantage.ExpiresAtTick) { RecallAdvantage(); return true; }
            }
            if (HoldingKeeperId == null) { timedKeeperId = null; KeeperHoldSeconds = 0; }
            else
            {
                if (timedKeeperId != HoldingKeeperId) { timedKeeperId = HoldingKeeperId; KeeperHoldSeconds = 0; }
                KeeperHoldSeconds += session.FixedStep.TotalSeconds;
                if (KeeperHoldSeconds > 8)
                {
                    var keeper = players.Single(p => p.Player.Id == HoldingKeeperId);
                    return HandleFoul(new FoulIncident(session.SessionId + ":hold-" + session.Tick, keeper.Player.Id, null, BallPosition, keeper.Direction, FoulKind.KeeperTimeLimit));
                }
            }
            return false;
        }
        private void SettleDeferredCards()
        {
            if (deferredCards.Count == 0) return;
            string message = "";
            foreach (var verdict in deferredCards) message += ApplyCard(verdict, FoulRules.AdvantageCard(verdict)) + " ";
            deferredCards.Clear();
            if (!string.IsNullOrWhiteSpace(message)) NotifyReferee(message.Trim());
        }
        private string ApplyCard(FoulVerdict verdict, CardKind requested)
        {
            var id = verdict.Incident.OffenderId;
            var card = Discipline.Apply(verdict.Incident.Id, id, requested);
            if (card == CardKind.None) return "";
            var player = players.Single(p => p.Player.Id == id);
            RecordFixture(card.ToString(), player.IsHome);
            string message = player.Player.Name + ": " + (card == CardKind.SecondYellow ? "second yellow, sent off." : card == CardKind.Red ? "red card, sent off." : "yellow card.");
            if (card == CardKind.Yellow) return message;
            players.Remove(player); // The active roster has no reinsertion/substitution route; the ledger persists across halves.
            if (id == controlledPlayerId) { ActiveIntent = null; IntentStatus = "Sent off — watching the remainder"; PendingDecision = null; PendingDefense = null; PendingKeeper = null; }
            if (players.Count(p => p.IsHome == player.IsHome) < 7)
            {
                Abandoned = true; ClearPlay(); Restart = null; FixturePhase = FixturePhase.FullTime; RecordFixture("Abandoned"); session.Pause(); return message;
            }
            if (player.IsGoalkeeper)
            {
                var replacement = players.Where(p => p.IsHome == player.IsHome).OrderByDescending(p => p.Player.FootballAttributes["handling"]).First();
                replacement.IsGoalkeeper = true; replacement.Anchor = new PitchPoint(-48 * replacement.Direction, 0);
                replacement.Position = replacement.Target = replacement.Anchor;
                message += " " + replacement.Player.Name + " takes over in goal.";
            }
            return message;
        }
        public FootballActionPreview RestartShotPreview()
        {
            if (Restart == null || (Restart.Kind != RestartKind.Penalty && Restart.Kind != RestartKind.DirectFreeKick)) return null;
            var taker = players.Single(p => p.Player.Id == Restart.TakerId);
            var context = new FootballActionContext(Snapshot(taker), BallSituation.Controlled, 0, 0, taker.Direction, players.Where(p => p.IsHome != taker.IsHome).Select(Snapshot));
            return FootballActionResolver.Preview(context, FootballActionKind.Shot, "placed");
        }
        public FootballActionResolved ShootRestart(string id)
        {
            if (id != null && restartShots.TryGetValue(id, out var cached)) return cached;
            if (Restart == null || Restart.Id != id || RefereeNoticePending || OffsideNoticePending) throw new InvalidOperationException("Restart is not ready.");
            var preview = RestartShotPreview();
            if (preview == null) throw new InvalidOperationException("This restart cannot score directly.");
            var restart = Restart; var taker = players.Single(p => p.Player.Id == restart.TakerId);
            session.Resume();
            var result = FootballActionResolver.Commit(session, id, preview, true);
            restartShots.Add(id, result); periodStarted = true; Restart = null; penaltyInProgress = restart.Kind == RestartKind.Penalty;
            PossessionClubId = taker.Player.ClubId; BeginFootballFlight(taker, result);
            directRestart = restart.Kind; directRestartHome = restart.IsHome;
            RecordFixture("RestartReleased", restart.IsHome, restart.Kind); FootballActionCommitted?.Invoke(result); return result;
        }
    }
}
