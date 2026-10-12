using FootballRpg.Simulation;
using UnityEngine;

namespace FootballRpg.Presentation
{
    public sealed partial class PitchPreview
    {
        public void ToggleViewingMode() => Playback.SetMode(Playback.Mode == MatchViewingMode.FullMatch ? MatchViewingMode.Highlights : MatchViewingMode.FullMatch);
        public void CycleViewingSpeed()
        {
            if (Playback.Mode == MatchViewingMode.FullMatch) Playback.SetFullMatchSpeed(Playback.FullMatchSpeed == 8 ? 1 : Playback.FullMatchSpeed * 2);
            else Playback.SetQuietSpeed(Playback.QuietSpeed == 64 ? 16 : Playback.QuietSpeed * 2);
        }
        public string ViewingStatus => Match.State != MatchRunState.Running ? "Paused · inspect freely" :
            Playback.Mode == MatchViewingMode.FullMatch ? "Watching every passage" :
            Playback.Focus == HighlightFocus.Quiet ? "Quiet passage · fast-forward" :
            Playback.Focus == HighlightFocus.DefensiveThreat ? "Threat within your reach" :
            Playback.Focus == HighlightFocus.AttackingSupport ? "Supporting the attack" :
            Playback.Focus == HighlightFocus.MovementIntent ? "Your off-ball instruction" :
            Playback.Focus == HighlightFocus.MatchEvent ? "Match event" : "Your involvement";
        private void DrawViewingControls(float left)
        {
            float x = 320;
            if (Button(new Rect(x, 16, 174, 30), (Playback.Mode == MatchViewingMode.FullMatch ? "FULL MATCH" : "HIGHLIGHTS") + " [H]")) ToggleViewingMode();
            int speed = Playback.Mode == MatchViewingMode.FullMatch ? Playback.FullMatchSpeed : Playback.QuietSpeed;
            if (Button(new Rect(x + 184, 16, 150, 30), (Playback.Mode == MatchViewingMode.Highlights ? "QUIET " : "SPEED ") + speed + "× [M]")) CycleViewingSpeed();
            Text(new Rect(x + 345, 18, left - x - 545, 27), Playback.EffectiveSpeed + "×", small, Lime);
            // Below the score bar, above the pitch, only when no decision or restart panel occupies it.
            if (!World.HasPendingDecision && !World.RefereeNoticePending && !World.OffsideNoticePending && World.Restart == null &&
                World.FixturePhase != FixturePhase.HalfTime && World.FixturePhase != FixturePhase.FullTime && World.Advantage == null && World.KeeperHoldSeconds < 3)
                Text(new Rect(24, 110, left - 48, 24), ViewingStatus, small, Lime);
        }
    }
}
