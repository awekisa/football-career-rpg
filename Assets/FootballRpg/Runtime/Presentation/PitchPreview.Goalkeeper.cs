using System;
using System.Linq;
using FootballRpg.Simulation;
using UnityEngine;

namespace FootballRpg.Presentation
{
    public sealed partial class PitchPreview
    {
        private bool showKeeperFeet;
        public void ToggleKeeperFeet() { _ = ChosenKeeperAction; if (World.PendingKeeper != null && World.PendingDecision != null) showKeeperFeet = !showKeeperFeet; }
        private string selectedKeeperDecisionId, selectedKeeperOptionId;
        public KeeperPreview ChosenKeeperAction
        {
            get
            {
                var d = World?.PendingKeeper;
                if (d == null) return null;
                if (selectedKeeperDecisionId != d.Id)
                { selectedKeeperDecisionId = d.Id; selectedKeeperOptionId = d.Options[0].OptionId; showKeeperFeet = false; }
                if (showKeeperFeet && World.PendingDecision != null) return null;
                return d.Options.FirstOrDefault(p => p.OptionId == selectedKeeperOptionId) ?? d.Options[0];
            }
        }
        public void ChooseKeeperAction(KeeperAction kind)
        {
            var d = World.PendingKeeper;
            if (d == null) return;
            var choice = d.Options.FirstOrDefault(p => p.Kind == kind && p.Receiver?.Id == SelectedPlayer?.Id)
                ?? d.Options.FirstOrDefault(p => p.Kind == kind);
            if (choice == null) return;
            selectedKeeperDecisionId = d.Id; selectedKeeperOptionId = choice.OptionId; showKeeperFeet = false;
        }
        public void CycleKeeperTarget()
        {
            var action = ChosenKeeperAction;
            if (action == null) return;
            var options = World.PendingKeeper.Options.Where(p => p.Kind == action.Kind && p.Receiver != null).ToArray();
            if (options.Length == 0) return;
            var next = options[(Array.IndexOf(options, action) + 1) % options.Length];
            SelectPlayer(next.Receiver.Id);
        }
        private static string KeeperLabel(KeeperAction kind)
        {
            switch (kind)
            {
                case KeeperAction.ReleaseToFeet: return "Drop to feet";
                case KeeperAction.SetPosition: return "Set position";
                case KeeperAction.RushOut: return "Rush out";
                case KeeperAction.HoldSave: return "Save and hold";
                case KeeperAction.ShortKick: return "Short kick";
                case KeeperAction.LongKick: return "Long kick";
                default: return kind.ToString();
            }
        }
        private void DrawKeeperPanel(float left)
        {
            var d = World.PendingKeeper; var chosen = ChosenKeeperAction;
            if (d == null) return;
            if (World.PendingDecision != null && Button(new Rect(24, 796, 260, 30), showKeeperFeet ? "KEEPER ACTIONS [F]" : "FEET ACTIONS [F]")) ToggleKeeperFeet();
            if (chosen == null) return;
            Panel(new Rect(0, 106, left, 142), Ink);
            string caption = "GOALKEEPER  /  " + KeeperLabel(chosen.Kind) + "  " + chosen.Percent + "%";
            if (chosen.Receiver != null) caption += " → " + Career.Players[chosen.Receiver.Id].Name;
            Text(new Rect(24, 112, left - 220, 31), caption, label, Lime);
            if (chosen.Receiver != null && Button(new Rect(left - 192, 114, 168, 29), "NEXT TEAMMATE [R]")) CycleKeeperTarget();
            string hint = d.Context.Restriction ?? (d.Context.Holding ? "Ball in your hands — choose your distribution" : d.Context.Situation == KeeperSituation.Shot
                ? "Hold: keep possession  ·  Parry: safer save, loose rebound" : d.Context.Situation == KeeperSituation.Cross
                ? "Claim: catch the delivery  ·  Punch: clear into loose play" : d.Context.Situation == KeeperSituation.Distribution
                ? "Ball at your feet — gather, distribute or switch to feet actions [F]" : "Set your position or commit to the ball — no timing required");
            Text(new Rect(24, 149, left - 48, 28), hint, muted, Color.white);
            var kinds = d.Options.Select(p => p.Kind).Distinct().ToArray();
            float w = (left - 48) / (kinds.Length + 1);
            for (int i = 0; i < kinds.Length; i++)
                if (Button(new Rect(24 + i * w, 190, w - 6, 40), (chosen.Kind == kinds[i] ? "● " : "") + KeeperLabel(kinds[i]) + " [" + (i + 1) + "]")) ChooseKeeperAction(kinds[i]);
            if (Button(new Rect(24 + kinds.Length * w, 190, w - 6, 40), "PLAY [ENTER]")) CommitChosenPass();
        }
    }
}
