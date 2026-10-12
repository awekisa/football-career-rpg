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
        public PassPreview ChosenRestart => !UserRestart || World.Restart.Kind == RestartKind.Penalty ? null : World.RestartRecipients().FirstOrDefault(p => p.Receiver.Id == restartTargetId)
            ?? World.RestartRecipients().OrderByDescending(p => p.Chance).First();
        public void CycleRestartTarget()
        {
            if (!UserRestart || World.Restart.Kind == RestartKind.Penalty) return;
            var options = World.RestartRecipients().ToArray();
            int index = System.Array.FindIndex(options, p => p.Receiver.Id == ChosenRestart.Receiver.Id);
            restartTargetId = options[(index + 1) % options.Length].Receiver.Id;
        }
        private void DrawFixture(float left)
        {
            if (World.RefereeNoticePending)
            {
                Panel(new Rect(0, 106, left, 185), Ink);
                Text(new Rect(24, 114, left - 48, 28), "REFEREE DECISION", label, Coral);
                Text(new Rect(24, 150, left - 48, 65), World.RefereeMessage, new GUIStyle(muted) { wordWrap = true }, Color.white);
                Text(new Rect(24, 222, left - 260, 38), FoulRules.LawsEdition, small, Lime);
                if (Button(new Rect(left - 228, 236, 204, 36), "CONTINUE [ENTER]")) World.AcknowledgeReferee();
                return;
            }
            if (World.OffsideNoticePending)
            {
                Panel(new Rect(0, 106, left, 185), Ink);
                Text(new Rect(24, 114, left - 48, 28), "OFFSIDE  /  " + Career.Players[World.LastOffside.PlayerId].Name, label, Coral);
                var explanation = new GUIStyle(muted) { wordWrap = true };
                Text(new Rect(24, 150, left - 48, 60), World.LastOffside.Explanation, explanation, Color.white);
                Text(new Rect(24, 218, left - 260, 45), "Indirect free kick · " + OffsideOffence.LawsEdition, small, Lime);
                if (Button(new Rect(left - 228, 236, 204, 36), "CONTINUE [ENTER]")) World.AcknowledgeOffside();
                return;
            }
            if (World.Advantage != null)
                Text(new Rect(24, 108, left - 48, 32), "ADVANTAGE · play continues", small, Lime);
            if (World.KeeperHoldSeconds >= 3)
                Text(new Rect(24, 140, left - 48, 28), "Keeper release countdown: " + System.Math.Max(0, 8 - World.KeeperHoldSeconds).ToString("0.0") + "s", small, Coral);
            if (World.Rules == null || (World.Restart == null && World.FixturePhase != FixturePhase.HalfTime && World.FixturePhase != FixturePhase.FullTime)) return;
            string phase = FixturePhaseLabel;
            if (World.AnnouncedAddedSeconds > 0) phase += "  +" + World.AnnouncedAddedSeconds.ToString("0.#") + "s added";
            if (World.Restart != null) phase += "  /  " + OpenPlayWorld.RestartLabel(World.Restart.Kind) + " · " + Career.Players[World.Restart.TakerId].Name;
            Panel(new Rect(0, 106, left, UserRestart ? 139 : 38), Ink);
            Text(new Rect(24, 112, left - 48, 28), phase, small, Lime);
            if (UserRestart && World.Restart.Kind == RestartKind.Penalty)
            {
                Text(new Rect(24, 148, left - 48, 28), "Penalty · " + World.RestartShotPreview().Percent + "% on target (keeper save resolved separately)", label, Color.white);
                if (Button(new Rect(24, 190, 250, 35), "TAKE PENALTY [ENTER]")) CommitChosenPass();
            }
            else if (UserRestart)
            {
                Text(new Rect(24, 148, left - 48, 28), "To " + Career.Players[ChosenRestart.Receiver.Id].Name + "  ·  " + Mathf.RoundToInt(ChosenRestart.Chance * 100) + "% received", label, Color.white);
                if (Button(new Rect(24, 190, 200, 35), "NEXT TEAMMATE [R]")) CycleRestartTarget();
                if (Button(new Rect(234, 190, 210, 35), "TAKE RESTART [ENTER]")) CommitChosenPass();
                if (World.Restart?.Kind == RestartKind.DirectFreeKick && Button(new Rect(454, 190, 260, 35), "SHOOT · " + World.RestartShotPreview().Percent + "% ON TARGET")) World.ShootRestart(World.Restart.Id);
            }
            if (World.FixturePhase == FixturePhase.HalfTime)
            {
                Panel(new Rect(0, 144, left, 65), Ink);
                if (Button(new Rect(24, 158, 300, 36), "SECOND HALF [ENTER / SPACE]")) World.ContinueSecondHalf();
            }
            if (World.FixturePhase == FixturePhase.FullTime)
            {
                Panel(new Rect(0, 144, left, 65), Ink);
                Text(new Rect(24, 158, left - 48, 35), World.Abandoned ? "Match abandoned — fewer than seven players · [ESC]" : World.HomeGoals == World.AwayGoals ? "Match drawn · Return to club [ESC]" : "Match finished · Return to club [ESC]", label, Color.white);
            }
        }
    }
}
