using System;
using System.Linq;
using FootballRpg.Simulation;
using UnityEngine;

namespace FootballRpg.Presentation
{
    public sealed partial class PitchPreview
    {
        private static string DutyLabel(SetPieceDuty duty) => duty == SetPieceDuty.FollowZone ? "follow zone" : duty == SetPieceDuty.MarkPlayer ? "mark player" : duty == SetPieceDuty.AttackSpace ? "attack space" : "await counter";
        private string setPieceUiId;
        private SetPieceDuty selectedDuty;
        private string selectedMark;
        private int restartOption;
        private string shotVariant = "placed";
        private void EnsureSetPieceSelection()
        {
            var restart = World?.Restart;
            if (restart == null || restart.Id == setPieceUiId) return;
            setPieceUiId = restart.Id; restartOption = restart.Kind == RestartKind.Penalty ? 3 : 0; shotVariant = "placed";
            var duty = World.PendingSetPiece;
            if (duty != null) { selectedDuty = duty.Duty; selectedMark = duty.MarkId; }
        }
        public void SelectSetPieceDuty(SetPieceDuty duty)
        {
            EnsureSetPieceSelection(); if (World.PendingSetPiece == null) return;
            selectedDuty = duty;
            if (duty == SetPieceDuty.MarkPlayer && SelectedPlayer.ClubId != Career.UserPlayer.ClubId && World.CanParticipate(SelectedPlayer.Id)) selectedMark = SelectedPlayer.Id;
        }
        public void SelectRestartOption(int option)
        {
            EnsureSetPieceSelection(); if (!UserRestart || option < 0 || option > 3) return;
            if (World.Restart.Kind == RestartKind.Penalty) { if (option == 3) shotVariant = shotVariant == "placed" ? "power" : "placed"; return; }
            if (option == 3 && World.RestartShotPreview() == null || (option == 1 || option == 2) && World.RestartDeliveries().Count == 0) return;
            if (option == 3 && restartOption == 3) shotVariant = shotVariant == "placed" ? "power" : "placed";
            restartOption = option;
        }
        private FootballActionPreview ChosenRestartDelivery => World.RestartDeliveries().FirstOrDefault(p => p.Receiver.Id == ChosenRestart?.Receiver.Id && p.Variant == (restartOption == 1 ? "low" : "lofted"));
        private bool CommitSetPieceChoice()
        {
            EnsureSetPieceSelection();
            if (World.PendingSetPiece != null)
            { World.CommitSetPieceDuty(World.Restart.Id, selectedDuty, selectedDuty == SetPieceDuty.MarkPlayer ? selectedMark : null); return true; }
            if (!UserRestart) return false;
            if (restartOption == 3) World.ShootRestart(World.Restart.Id, shotVariant);
            else if (restartOption == 1 || restartOption == 2) World.DeliverRestart(World.Restart.Id, ChosenRestartDelivery.OptionId);
            else World.ReleaseRestart(World.Restart.Id, ChosenRestart.Receiver.Id);
            return true;
        }
        private void CycleSetPieceMark()
        {
            EnsureSetPieceSelection();
            var opponents = World.Players.Where(p => p.Player.ClubId != Career.UserPlayer.ClubId).ToArray();
            int index = Array.FindIndex(opponents, p => p.Player.Id == selectedMark);
            selectedMark = opponents[(index + 1) % opponents.Length].Player.Id; SelectPlayer(selectedMark);
        }
        private void SetPieceKeys()
        {
            EnsureSetPieceSelection();
            if (World.RefereeNoticePending || World.OffsideNoticePending) return;
            for (int i = 0; i < 4; i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                { if (World.PendingSetPiece != null) SelectSetPieceDuty((SetPieceDuty)i); else SelectRestartOption(i); }
        }
        private bool DrawSetPieceChoice(float left)
        {
            EnsureSetPieceSelection();
            var assignment = World.PendingSetPiece;
            if (assignment == null && !UserRestart) return false;
            Panel(new Rect(0, 106, left, 188), Ink);
            Text(new Rect(24, 114, left - 48, 30), OpenPlayWorld.RestartLabel(World.Restart.Kind).ToUpperInvariant() + " / " + Career.Players[World.Restart.TakerId].Name, label, Lime);
            if (assignment != null)
            {
                string mark = selectedDuty == SetPieceDuty.MarkPlayer ? " · " + Career.Players[selectedMark].Name : "";
                Text(new Rect(24, 151, left - 48, 28), "Coach: " + DutyLabel(assignment.Duty) + "   |   You: " + DutyLabel(selectedDuty) + mark, muted, Color.white);
                string[] labels = { "Follow zone [1]", "Mark player [2]", "Attack space [3]", "Await counter [4]" };
                for (int i = 0; i < 4; i++)
                    if (Button(new Rect(24 + i * (left - 48) / 4, 191, (left - 48) / 4 - 6, 36), ((int)selectedDuty == i ? "● " : "") + labels[i])) SelectSetPieceDuty((SetPieceDuty)i);
                if (selectedDuty == SetPieceDuty.MarkPlayer && Button(new Rect(24, 243, 245, 32), "NEXT OPPONENT [R]")) CycleSetPieceMark();
                if (Button(new Rect(left - 255, 243, 230, 32), "CONFIRM DUTY [ENTER]")) CommitChosenPass();
            }
            else
            {
                var shot = restartOption == 3 ? World.RestartShotPreview(shotVariant) : null;
                var delivery = restartOption == 1 || restartOption == 2 ? ChosenRestartDelivery : null;
                string description = shot != null ? shotVariant + " · " + shot.Percent + "% on target; keeper save separate"
                    : delivery != null ? delivery.Variant + " to " + Career.Players[delivery.Receiver.Id].Name + " · " + delivery.Percent + "% reaches target; contest separate"
                    : "Pass to " + Career.Players[ChosenRestart.Receiver.Id].Name + " · " + ChosenRestart.Percent + "% received";
                Text(new Rect(24, 151, left - 48, 28), description, muted, Color.white);
                string[] labels = { "Pass [1]", "Low delivery [2]", "Lofted delivery [3]", "Shot / variant [4]" };
                for (int i = 0; i < 4; i++)
                {
                    GUI.enabled = World.Restart.Kind == RestartKind.Penalty ? i == 3 : i == 0 || i == 3 && World.RestartShotPreview() != null || (i == 1 || i == 2) && World.RestartDeliveries().Count > 0;
                    if (Button(new Rect(24 + i * (left - 48) / 4, 191, (left - 48) / 4 - 6, 36), (restartOption == i ? "● " : "") + labels[i])) SelectRestartOption(i);
                }
                GUI.enabled = true;
                if (World.Restart.Kind != RestartKind.Penalty && Button(new Rect(24, 243, 230, 32), "NEXT TEAMMATE [R]")) CycleRestartTarget();
                if (Button(new Rect(left - 255, 243, 230, 32), "TAKE RESTART [ENTER]")) CommitChosenPass();
            }
            return true;
        }
    }
}
