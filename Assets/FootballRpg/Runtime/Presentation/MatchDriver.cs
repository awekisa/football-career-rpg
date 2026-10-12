using System;
using FootballRpg.Simulation;
using UnityEngine;

namespace FootballRpg.Presentation
{
    /// <summary>Optional scene adapter. Pitch gameplay will initialize it with its owned match session.</summary>
    public sealed class MatchDriver : MonoBehaviour
    {
        [Range(1, 64)] public int playbackMultiplier = 1;
        public MatchSession Session { get; private set; }
        public MatchPlayback Playback { get; private set; }
        private long lastSample;
        private bool hasSample;
        private int loggedCount;

        public void Initialize(MatchSession session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Playback = null;
            hasSample = false;
            loggedCount = 0;
        }

        public void AttachWorld(OpenPlayWorld world)
        {
            var playback = new MatchPlayback(world);
            if (!ReferenceEquals(playback.Session, Session)) throw new ArgumentException("World and driver must share a match session.");
            Playback = playback; hasSample = false;
        }

        private void OnDisable() { hasSample = false; Playback?.ResetTiming(); }

        private void Update()
        {
            if (Session == null) return;
            // Round the absolute timestamp, not each delta, so rounding does not accumulate per frame.
            long sample = checked((long)Math.Round(Time.realtimeSinceStartupAsDouble * TimeSpan.TicksPerSecond));
            long delta = hasSample ? Math.Max(0, sample - lastSample) : 0;
            lastSample = sample;
            hasSample = true;
            if (Playback == null) Session.Advance(TimeSpan.FromTicks(delta), playbackMultiplier);
            else Playback.Advance(TimeSpan.FromTicks(delta));
            FlushDiagnostics();
        }

        public void FlushDiagnostics()
        {
            if (Session == null) return;
            while (loggedCount < Session.Diagnostics.Count)
                Debug.Log("FOOTBALL_RPG_ACTION " + Session.Diagnostics[loggedCount++].ToJsonLine());
        }
    }
}
