using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FootballRpg.Simulation
{
    public enum MatchRunState { Running, Paused, Resolving }

    /// <summary>Single-threaded fixed-step clock and exactly-once action boundary; no Unity dependencies.</summary>
    public sealed class MatchSession
    {
        private readonly SeededRandom random;
        private readonly List<ActionDiagnostic> diagnostics = new List<ActionDiagnostic>();
        private readonly Dictionary<string, ActionDiagnostic> requests = new Dictionary<string, ActionDiagnostic>(StringComparer.Ordinal);
        private readonly HashSet<string> completedResolutions = new HashSet<string>(StringComparer.Ordinal);
        private MatchRunState resumeState = MatchRunState.Running;
        private string activeEventId;
        private long pendingTimeTicks;
        private long sequence;
        private bool advancing;

        public bool CanSave => !advancing && State == MatchRunState.Paused && resumeState != MatchRunState.Resolving;
        public string SessionId { get; }
        public ulong Seed { get; }
        public ulong RandomState => random.State;
        public long DrawCount { get; private set; }
        public long Tick { get; private set; }
        public TimeSpan FixedStep { get; }
        public TimeSpan Elapsed => TimeSpan.FromTicks(checked(Tick * FixedStep.Ticks));
        public MatchRunState State { get; private set; } = MatchRunState.Running;
        public IReadOnlyList<ActionDiagnostic> Diagnostics { get; }
        public int MaxStepsPerAdvance { get; }
        public event Action<long> TickAdvanced;

        public MatchSession(string sessionId, ulong seed, TimeSpan? fixedStep = null, int maxStepsPerAdvance = 512)
        {
            SessionId = ActionRequest.Required(sessionId);
            Seed = seed;
            FixedStep = fixedStep ?? TimeSpan.FromMilliseconds(50);
            if (FixedStep.Ticks <= 0) throw new ArgumentOutOfRangeException(nameof(fixedStep));
            if (maxStepsPerAdvance <= 0) throw new ArgumentOutOfRangeException(nameof(maxStepsPerAdvance));
            MaxStepsPerAdvance = maxStepsPerAdvance;
            random = new SeededRandom(seed);
            Diagnostics = new ReadOnlyCollection<ActionDiagnostic>(diagnostics);
        }

        /// <summary>Playback changes how many fixed steps run, never the duration or equations of a step.</summary>
        public int Advance(TimeSpan realElapsed, int playbackMultiplier = 1)
        {
            if (realElapsed.Ticks < 0) throw new ArgumentOutOfRangeException(nameof(realElapsed));
            if (playbackMultiplier < 1 || playbackMultiplier > 64) throw new ArgumentOutOfRangeException(nameof(playbackMultiplier));
            if (advancing) throw new InvalidOperationException("Advance cannot be called from a tick callback.");
            if (State != MatchRunState.Running) return 0;
            pendingTimeTicks = checked(pendingTimeTicks + checked(realElapsed.Ticks * playbackMultiplier));
            int steps = 0;
            advancing = true;
            try
            {
                while (State == MatchRunState.Running && pendingTimeTicks >= FixedStep.Ticks && steps < MaxStepsPerAdvance)
                {
                    long nextTick = checked(Tick + 1);
                    checked { _ = nextTick * FixedStep.Ticks; }
                    pendingTimeTicks -= FixedStep.Ticks;
                    Tick = nextTick;
                    steps++;
                    TickAdvanced?.Invoke(Tick);
                }
            }
            catch
            {
                // A world callback failure stops further ticks. Its caller must treat the world as failed.
                Pause();
                throw;
            }
            finally { advancing = false; }
            return steps;
        }

        public void Pause()
        {
            if (State == MatchRunState.Paused) return;
            resumeState = State;
            State = MatchRunState.Paused;
            // Drop unconsumed frame time: resuming must not fast-forward past the decision.
            pendingTimeTicks = 0;
        }

        public void Resume()
        {
            if (State == MatchRunState.Paused) State = resumeState;
        }

        /// <summary>Commit a frozen choice once while paused. Retries return its existing fact without a draw.</summary>
        public ActionDiagnostic BeginResolution(ActionRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (requests.TryGetValue(request.RequestId, out var previous))
            {
                if (!previous.Request.SameChoice(request))
                    throw new InvalidOperationException("Request ID reused with a different choice.");
                return previous;
            }
            if (State != MatchRunState.Paused || resumeState != MatchRunState.Running || activeEventId != null)
                throw new InvalidOperationException("A new action requires a paused, non-resolving session.");

            long nextSequence = checked(sequence + 1);
            ulong before = random.State;
            float roll = random.NextUnitFloat();
            string eventId = SessionId + ":action-" + nextSequence.ToString(CultureInfo.InvariantCulture);
            var result = new ActionResolved(eventId, request.PlayerId, nextSequence, request.ActionId,
                request.TargetId, request.Chance, roll, roll < request.Chance ? request.SuccessOutcomeId : request.FailureOutcomeId);
            var diagnostic = new ActionDiagnostic(request, result, Tick, Elapsed.Ticks, Seed, before, random.State);
            sequence = nextSequence;
            DrawCount++;
            diagnostics.Add(diagnostic);
            requests.Add(request.RequestId, diagnostic);
            activeEventId = eventId;
            pendingTimeTicks = 0;
            State = MatchRunState.Resolving;
            return diagnostic;
        }

        /// <summary>Resolve an AI choice synchronously without discarding accumulated render time.</summary>
        public ActionDiagnostic ResolveAutomatic(ActionRequest request)
        {
            if (State != MatchRunState.Running)
                throw new InvalidOperationException("Automatic choices require a running match.");
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (requests.TryGetValue(request.RequestId, out var previous))
            {
                if (!previous.Request.SameChoice(request))
                    throw new InvalidOperationException("Request ID reused with a different choice.");
                return previous;
            }
            long remainder = pendingTimeTicks;
            Pause();
            var result = BeginResolution(request);
            CompleteResolution(result.Result.EventId);
            pendingTimeTicks = remainder;
            return result;
        }

        /// <summary>Animation completion resumes the clock; it never resolves or publishes the action again.</summary>
        public bool CompleteResolution(string eventId)
        {
            if (eventId == null) throw new ArgumentNullException(nameof(eventId));
            if (completedResolutions.Contains(eventId)) return false;
            if (State != MatchRunState.Resolving || eventId != activeEventId)
                throw new InvalidOperationException("No matching active resolution, or its animation is manually paused.");
            completedResolutions.Add(eventId);
            activeEventId = null;
            resumeState = MatchRunState.Running;
            State = MatchRunState.Running;
            return true;
        }
    }
}
