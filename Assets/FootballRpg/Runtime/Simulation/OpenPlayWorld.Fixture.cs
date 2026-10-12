using System;
using System.Collections.Generic;
using System.Linq;

namespace FootballRpg.Simulation
{
    public sealed partial class OpenPlayWorld
    {
        public CompetitionRules Rules { get; private set; }
        public FixturePhase FixturePhase { get; private set; } = FixturePhase.Practice;
        public RestartState Restart { get; private set; }
        public double HalfElapsedSeconds { get; private set; }
        public double AddedSeconds { get; private set; }
        public double AnnouncedAddedSeconds { get; private set; }
        public double MatchSeconds => Rules == null ? session.Elapsed.TotalSeconds :
            (FixturePhase == FixturePhase.SecondHalf || FixturePhase == FixturePhase.FullTime ? Rules.HalfSeconds : 0) + HalfElapsedSeconds;
        private readonly List<FixtureEvent> fixtureEvents = new List<FixtureEvent>();
        public IReadOnlyList<FixtureEvent> FixtureEvents => fixtureEvents.AsReadOnly();
        public event Action<FixtureEvent> FixtureChanged;
        private long fixtureSequence;
        private double setupElapsed;
        private bool periodStarted;
        private string lastReleasedRestart, lastRestartReceiver;
        private PassResolved lastRestartResult;
        private RestartKind? directRestart;
        private bool directRestartHome;

        public void StartFixture(CompetitionRules rules = null)
        {
            if (Rules != null || session.Tick != 0 || session.DrawCount != 0) throw new InvalidOperationException("Start a fixture once, before advancing the practice clock.");
            Rules = rules ?? new CompetitionRules();
            ClearPlay(); DecisionCount = 0; session.Resume();
            FixturePhase = FixturePhase.FirstHalf;
            AddedSeconds = Rules.MinimumAddedSeconds;
            RecordFixture("FirstHalf");
            AwardRestart(RestartKind.Kickoff, Rules.HomeKicksOff, new PitchPoint(0, 0));
        }
        private void RecordFixture(string kind, bool? home = null, RestartKind? restart = null)
        {
            var fact = new FixtureEvent(session.SessionId + ":fixture-" + (++fixtureSequence), kind, session.Tick, home, restart);
            fixtureEvents.Add(fact); FixtureChanged?.Invoke(fact);
        }
        private void ClearPlay()
        {
            PendingDecision = null; PendingDefense = null; PendingKeeper = null;
            activeFootballAction = null; keeperThreat = null; keeperFlight = null;
            OwnerId = IncomingPlayerId = HoldingKeeperId = PossessionClubId = null;
            receiver = null; BallState = BallPhase.OutOfPlay;
            RefreshIntent();
        }
        private bool StepFixture()
        {
            if (Rules == null) return false;
            if (FixturePhase == FixturePhase.FullTime || FixturePhase == FixturePhase.HalfTime) { session.Pause(); return true; }
            if (Restart != null && Restart.TakerId == controlledPlayerId) { session.Pause(); return true; }
            double dt = session.FixedStep.TotalSeconds;
            // Thinking and inspection pauses never consume match time. Setup delays do, and are restored as added time.
            if (periodStarted)
            {
                HalfElapsedSeconds += dt;
                if (Restart != null) AddedSeconds += dt;
            }
            if (HalfElapsedSeconds >= Rules.HalfSeconds)
                AnnouncedAddedSeconds = Math.Max(AnnouncedAddedSeconds, AddedSeconds);
            if (HalfElapsedSeconds + 1e-8 >= Rules.HalfSeconds + AddedSeconds)
            {
                ClearPlay(); Restart = null;
                FixturePhase = FixturePhase == FixturePhase.FirstHalf ? FixturePhase.HalfTime : FixturePhase.FullTime;
                RecordFixture(FixturePhase.ToString()); session.Pause(); return true;
            }
            if (Restart == null) return false;
            setupElapsed += dt;
            if (setupElapsed >= Rules.RestartSetupSeconds)
            {
                var target = RestartRecipients().OrderByDescending(p => p.Chance).First();
                ReleaseRestart(Restart.Id, target.Receiver.Id);
            }
            return true;
        }
        public void ContinueSecondHalf()
        {
            if (FixturePhase != FixturePhase.HalfTime) return;
            foreach (var p in players)
            {
                p.Direction *= -1; p.Anchor = new PitchPoint(-p.Anchor.X, -p.Anchor.Z);
                p.Position = p.Anchor; p.Target = p.Anchor; p.Facing = new PitchPoint(p.Direction, 0);
            }
            periodStarted = false; HalfElapsedSeconds = 0; AddedSeconds = Rules.MinimumAddedSeconds; AnnouncedAddedSeconds = 0;
            keeperCooldowns.Clear(); containedUntilTick = beatenUntilTick = 0;
            FixturePhase = FixturePhase.SecondHalf; RecordFixture("SecondHalf");
            session.Resume(); AwardRestart(RestartKind.Kickoff, !Rules.HomeKicksOff, new PitchPoint(0, 0));
        }
        private void AwardRestart(RestartKind kind, bool home, PitchPoint spot)
        {
            ClearPlay(); directRestart = null;
            var team = players.Where(p => p.IsHome == home);
            var taker = kind == RestartKind.GoalKick ? team.First(p => p.Player.PrimaryPositionId == "gk")
                : kind == RestartKind.Kickoff ? team.OrderByDescending(p => p.Anchor.X * p.Direction).First()
                : team.Where(p => p.Player.PrimaryPositionId != "gk").OrderBy(p => p.Position.Distance(spot)).First();
            Restart = new RestartState(session.SessionId + ":restart-" + (fixtureSequence + 1), kind, home, taker.Player.Id, spot);
            setupElapsed = 0;
            foreach (var p in players)
            {
                var pos = kind == RestartKind.Kickoff ? p.Anchor : p.Position.Bounded();
                if (kind == RestartKind.Kickoff)
                    pos = new PitchPoint(-Math.Max(1, Math.Abs(pos.X)) * p.Direction, pos.Z);
                if (p.IsHome != home)
                {
                    if (kind == RestartKind.GoalKick && HandlingRules.InOwnArea(pos, taker.Direction))
                        pos = new PitchPoint(-35 * taker.Direction, pos.Z);
                    double distance = kind == RestartKind.Corner ? 10.2 : kind == RestartKind.Kickoff ? 9.15 : kind == RestartKind.ThrowIn ? 2 : 0;
                    if (pos.Distance(spot) < distance)
                    {
                        double dx = pos.X - spot.X, dz = pos.Z - spot.Z;
                        if (Math.Abs(dx) + Math.Abs(dz) < .001) { dx = -p.Direction; dz = 0; }
                        double length = Math.Sqrt(dx * dx + dz * dz);
                        pos = new PitchPoint(spot.X + dx / length * distance, spot.Z + dz / length * distance).Bounded();
                    }
                }
                p.Position = p.Target = pos;
            }
            taker.Position = taker.Target = spot;
            BallPosition = spot; BallHeight = kind == RestartKind.ThrowIn ? 1.8 : PitchBoundary.BallRadius;
            RecordFixture("RestartAwarded", home, kind);
            if (taker.Player.Id == controlledPlayerId) session.Pause();
        }
        public IReadOnlyList<PassPreview> RestartRecipients()
        {
            if (Restart == null) return Array.Empty<PassPreview>();
            var taker = players.Single(p => p.Player.Id == Restart.TakerId);
            return players.Where(p => p.IsHome == taker.IsHome && p != taker && p.Position.Distance(taker.Position) >= .1)
                .Select(p => PassResolver.Preview(Snapshot(taker), Snapshot(p), PassKind.ToFeet, p.Position, taker.Player.PreferredFoot == "left" ? "left" : "right",
                    players.Where(o => o.IsHome != taker.IsHome).Select(Snapshot))).ToArray();
        }
        public PassResolved ReleaseRestart(string id, string receiverId)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A restart ID is required.", nameof(id));
            if (id == lastReleasedRestart)
            {
                if (receiverId != lastRestartReceiver) throw new InvalidOperationException("Restart already released to another player.");
                return lastRestartResult;
            }
            var restart = Restart;
            if (restart == null || restart.Id != id) throw new InvalidOperationException("Stale restart.");
            var preview = RestartRecipients().SingleOrDefault(p => p.Receiver.Id == receiverId);
            if (preview == null) throw new ArgumentException("Choose a teammate.");
            var taker = players.Single(p => p.Player.Id == restart.TakerId);
            // Resolve once at release. The taker cannot touch again before the committed recipient/other player.
            session.Resume();
            var result = PassResolver.Commit(session, id, preview, true);
            periodStarted = true; Restart = null; PossessionClubId = taker.Player.ClubId; BeginFlight(result, taker);
            directRestart = restart.Kind; directRestartHome = restart.IsHome;
            if (restart.Kind == RestartKind.ThrowIn)
                Handling.RecordTouch(taker.Player.Id, taker.Player.ClubId, BallTouchKind.ThrowIn, receiverId);
            lastReleasedRestart = id; lastRestartReceiver = receiverId; lastRestartResult = result;
            // A kick clearly moves on release; a throw enters at the line before normal flight begins.
            BallPosition = restart.Kind == RestartKind.ThrowIn ? new PitchPoint(restart.Spot.X, Math.Sign(restart.Spot.Z) * 33.99)
                : restart.Spot.MoveTowards(destination, .02);
            flightOrigin = BallPosition;
            RecordFixture("RestartReleased", restart.IsHome, restart.Kind);
            PassCommitted?.Invoke(result); return result;
        }
        private bool CheckBoundary(PitchPoint previous, double previousHeight)
        {
            if (Rules == null) return false;
            var crossing = PitchBoundary.Cross(previous, previousHeight, BallPosition, BallHeight);
            if (!crossing.HasValue) return false;
            ApplyBoundary(crossing.Value); return true;
        }
        private void ApplyBoundary(BoundaryCrossing crossing)
        {
            var point = crossing.Point;
            var defending = players.First(p => p.Direction == -(point.X >= 0 ? 1 : -1));
            bool lastHome = players.FirstOrDefault(p => p.Player.Id == Handling.LastPlayerId)?.IsHome ?? !defending.IsHome;
            if (!crossing.GoalLine)
                AwardRestart(RestartKind.ThrowIn, !lastHome, new PitchPoint(Math.Max(-52.5, Math.Min(52.5, point.X)), Math.Sign(point.Z) * 34));
            else if (crossing.IsGoal && directRestart.HasValue && directRestartHome == defending.IsHome)
                AwardRestart(RestartKind.Corner, !defending.IsHome, new PitchPoint(Math.Sign(point.X) * 52, (point.Z >= 0 ? 1 : -1) * 33.5));
            else if (crossing.IsGoal && directRestart == RestartKind.ThrowIn)
                AwardRestart(RestartKind.GoalKick, defending.IsHome, new PitchPoint(Math.Sign(point.X) * 49, 5));
            else if (crossing.IsGoal)
            {
                bool scoringHome = !defending.IsHome;
                if (scoringHome) HomeGoals++; else AwayGoals++;
                RecordFixture("Goal", scoringHome);
                AwardRestart(RestartKind.Kickoff, defending.IsHome, new PitchPoint(0, 0));
            }
            else if (lastHome == defending.IsHome)
                AwardRestart(RestartKind.Corner, !defending.IsHome, new PitchPoint(Math.Sign(point.X) * 52, (point.Z >= 0 ? 1 : -1) * 33.5));
            else AwardRestart(RestartKind.GoalKick, defending.IsHome, new PitchPoint(Math.Sign(point.X) * 49, point.Z >= 0 ? 5 : -5));
        }
    }
}
