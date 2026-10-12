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
        private long lastSample;
        private bool hasSample;
        private int loggedCount;

        public void Initialize(MatchSession session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            hasSample = false;
            loggedCount = 0;
        }

        private void OnDisable() => hasSample = false;

        private void Update()
        {
            if (Session == null) return;
            // Round the absolute timestamp, not each delta, so rounding does not accumulate per frame.
            long sample = checked((long)Math.Round(Time.realtimeSinceStartupAsDouble * TimeSpan.TicksPerSecond));
            long delta = hasSample ? Math.Max(0, sample - lastSample) : 0;
            lastSample = sample;
            hasSample = true;
            Session.Advance(TimeSpan.FromTicks(delta), playbackMultiplier);
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
