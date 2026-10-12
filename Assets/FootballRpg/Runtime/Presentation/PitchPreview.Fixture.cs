using System.Linq;
using FootballRpg.Simulation;
using UnityEngine;

namespace FootballRpg.Presentation
{
    public sealed partial class PitchPreview
    {
        private string restartTargetId;
        private int DisplaySeconds => (int)System.Math.Floor(World.MatchSeconds + 1e-7);
        public string MatchClockText => (DisplaySeconds / 60).ToString("00") + ":" + (DisplaySeconds % 60).ToString("00");
        private string FixturePhaseLabel => World.FixturePhase == FixturePhase.FirstHalf ? "FIRST HALF" : World.FixturePhase == FixturePhase.SecondHalf ? "SECOND HALF" : World.FixturePhase == FixturePhase.HalfTime ? "HALFTIME" : "FULL TIME";
        public bool UserRestart => World?.Restart?.TakerId == Career?.UserPlayerId && World?.Restart != null;
        public PassPreview ChosenRestart => !UserRestart ? null : World.RestartRecipients().FirstOrDefault(p => p.Receiver.Id == restartTargetId)
            ?? World.RestartRecipients().OrderByDescending(p => p.Chance).First();
        public void CycleRestartTarget()
        {
            if (!UserRestart) return;
            var options = World.RestartRecipients().ToArray();
            int index = System.Array.FindIndex(options, p => p.Receiver.Id == ChosenRestart.Receiver.Id);
            restartTargetId = options[(index + 1) % options.Length].Receiver.Id;
        }
        private void DrawFixture(float left)
        {
            if (World.Rules == null || (World.Restart == null && World.FixturePhase != FixturePhase.HalfTime && World.FixturePhase != FixturePhase.FullTime)) return;
            string phase = FixturePhaseLabel;
            if (World.AnnouncedAddedSeconds > 0) phase += "  +" + World.AnnouncedAddedSeconds.ToString("0.#") + "s added";
            if (World.Restart != null) phase += "  /  " + World.Restart.Kind + " · " + Career.Players[World.Restart.TakerId].Name;
            Panel(new Rect(0, 106, left, UserRestart ? 139 : 38), Ink);
            Text(new Rect(24, 112, left - 48, 28), phase, small, Lime);
            if (UserRestart)
            {
                Text(new Rect(24, 148, left - 48, 28), "To " + Career.Players[ChosenRestart.Receiver.Id].Name + "  ·  " + Mathf.RoundToInt(ChosenRestart.Chance * 100) + "% received", label, Color.white);
                if (Button(new Rect(24, 190, 200, 35), "NEXT TEAMMATE [R]")) CycleRestartTarget();
                if (Button(new Rect(234, 190, 210, 35), "TAKE RESTART [ENTER]")) CommitChosenPass();
            }
            if (World.FixturePhase == FixturePhase.HalfTime)
            {
                Panel(new Rect(0, 144, left, 65), Ink);
                if (Button(new Rect(24, 158, 300, 36), "SECOND HALF [ENTER / SPACE]")) World.ContinueSecondHalf();
            }
            if (World.FixturePhase == FixturePhase.FullTime)
            {
                Panel(new Rect(0, 144, left, 65), Ink);
                Text(new Rect(24, 158, left - 48, 35), World.HomeGoals == World.AwayGoals ? "Match drawn · Return to club [ESC]" : "Match finished · Return to club [ESC]", label, Color.white);
            }
        }
    }
}
