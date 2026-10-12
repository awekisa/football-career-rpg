using System;

namespace FootballRpg.Simulation
{
    public enum MatchViewingMode { FullMatch, Highlights }

    /// <summary>Paces the same fixed-step simulation; no event synthesis, action selection or extra RNG.</summary>
    public sealed class MatchPlayback
    {
        private readonly OpenPlayWorld world;
        private readonly MatchSession session;
        private double availableWallTicks;
        public MatchViewingMode Mode { get; private set; }
        public int FullMatchSpeed { get; private set; } = 1;
        public int QuietSpeed { get; private set; } = 32;
        public HighlightFocus Focus => world.ViewingFocus;
        public int EffectiveSpeed => Mode == MatchViewingMode.FullMatch ? FullMatchSpeed : Focus == HighlightFocus.Quiet ? QuietSpeed : 1;
        public MatchSession Session => session;
        public MatchPlayback(OpenPlayWorld world)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world)); session = world.PlaybackSession;
        }
        public void SetMode(MatchViewingMode mode)
        {
            if (!Enum.IsDefined(typeof(MatchViewingMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            Mode = mode; ResetTiming();
        }
        public void SetFullMatchSpeed(int speed)
        {
            if (speed != 1 && speed != 2 && speed != 4 && speed != 8) throw new ArgumentOutOfRangeException(nameof(speed));
            FullMatchSpeed = speed; ResetTiming();
        }
        public void SetQuietSpeed(int speed)
        {
            if (speed != 16 && speed != 32 && speed != 64) throw new ArgumentOutOfRangeException(nameof(speed));
            QuietSpeed = speed; ResetTiming();
        }
        public void ResetTiming() => availableWallTicks = 0;
        public int Advance(TimeSpan elapsed)
        {
            if (elapsed.Ticks < 0) throw new ArgumentOutOfRangeException(nameof(elapsed));
            if (world.ViewingMustWait) session.Pause();
            if (session.State != MatchRunState.Running) { ResetTiming(); return 0; }
            availableWallTicks += elapsed.Ticks;
            int steps = 0;
            while (steps < session.MaxStepsPerAdvance && session.State == MatchRunState.Running)
            {
                var before = Focus;
                double cost = session.FixedStep.Ticks / (double)EffectiveSpeed;
                if (availableWallTicks < cost) break;
                availableWallTicks -= cost;
                steps += session.Advance(session.FixedStep);
                if (world.ViewingMustWait) session.Pause();
                // Yield this frame at the beginning of a highlight, before spending fast-forward time on it.
                if (session.State != MatchRunState.Running || (Mode == MatchViewingMode.Highlights && before == HighlightFocus.Quiet && Focus != HighlightFocus.Quiet))
                { ResetTiming(); break; }
            }
            // A slow render frame must not leave a backlog that races past future highlights or inspection.
            if (steps == session.MaxStepsPerAdvance) ResetTiming();
            return steps;
        }
    }
}
