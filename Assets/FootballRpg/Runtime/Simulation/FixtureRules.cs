using System;

namespace FootballRpg.Simulation
{
    public enum FixturePhase { Practice, FirstHalf, HalfTime, SecondHalf, FullTime }
    public enum RestartKind { Kickoff, ThrowIn, Corner, GoalKick, IndirectFreeKick }

    /// <summary>Regulation-only competition. Durations are match seconds, independent of playback speed.</summary>
    public sealed class CompetitionRules
    {
        public double HalfSeconds { get; }
        public double MinimumAddedSeconds { get; }
        public double RestartSetupSeconds { get; }
        public bool HomeKicksOff { get; }
        public bool AllowsDraw => true;
        public CompetitionRules(double halfSeconds = 2700, double minimumAddedSeconds = 0, double restartSetupSeconds = 2, bool homeKicksOff = true)
        {
            if (!Finite(halfSeconds) || halfSeconds <= 0 || !Finite(minimumAddedSeconds) || minimumAddedSeconds < 0 ||
                !Finite(restartSetupSeconds) || restartSetupSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(halfSeconds));
            HalfSeconds = halfSeconds; MinimumAddedSeconds = minimumAddedSeconds; RestartSetupSeconds = restartSetupSeconds; HomeKicksOff = homeKicksOff;
        }
        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    }

    public sealed class RestartState
    {
        public string Id { get; }
        public RestartKind Kind { get; }
        public bool IsHome { get; }
        public string TakerId { get; }
        public PitchPoint Spot { get; }
        internal RestartState(string id, RestartKind kind, bool home, string taker, PitchPoint spot)
        { Id = id; Kind = kind; IsHome = home; TakerId = taker; Spot = spot; }
    }
    public sealed class FixtureEvent
    {
        public string Id { get; }
        public string Kind { get; }
        public long Tick { get; }
        public bool? IsHome { get; }
        public RestartKind? Restart { get; }
        internal FixtureEvent(string id, string kind, long tick, bool? home, RestartKind? restart)
        { Id = id; Kind = kind; Tick = tick; IsHome = home; Restart = restart; }
    }

    public readonly struct BoundaryCrossing
    {
        public PitchPoint Point { get; }
        public double Height { get; }
        public bool GoalLine { get; }
        public bool IsGoal => GoalLine && Math.Abs(Point.Z) + PitchBoundary.BallRadius < 3.66 && Height + PitchBoundary.BallRadius < 2.44;
        internal BoundaryCrossing(PitchPoint point, double height, bool goalLine) { Point = point; Height = height; GoalLine = goalLine; }
    }
    public static class PitchBoundary
    {
        public const double BallRadius = .11;
        // Sweep to the first wholly crossed line, rather than judging the endpoint of a fast flight.
        public static BoundaryCrossing? Cross(PitchPoint from, double fromHeight, PitchPoint to, double toHeight)
        {
            double best = double.PositiveInfinity; bool goal = false;
            foreach (bool x in new[] { true, false })
            {
                double a = x ? from.X : from.Z, b = x ? to.X : to.Z;
                double limit = (x ? 52.5 : 34) + BallRadius;
                if (Math.Abs(b) <= limit || b == a) continue;
                double t = (Math.Sign(b) * limit - a) / (b - a);
                if (t >= 0 && t <= 1 && t < best) { best = t; goal = x; }
            }
            if (double.IsInfinity(best)) return null;
            return new BoundaryCrossing(new PitchPoint(from.X + (to.X - from.X) * best, from.Z + (to.Z - from.Z) * best),
                fromHeight + (toHeight - fromHeight) * best, goal);
        }
    }
}
