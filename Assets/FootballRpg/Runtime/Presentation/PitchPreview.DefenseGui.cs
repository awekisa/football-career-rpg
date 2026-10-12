using System;
using System.Linq;
using FootballRpg.Simulation;
using UnityEngine;

namespace FootballRpg.Presentation
{
    public sealed partial class PitchPreview
    {
        private bool showIntentMenu;
        private static string IntentLabel(OffBallIntent intent)
        {
            switch (intent)
            {
                case OffBallIntent.PersonalMark: return "Mark selected opponent";
                case OffBallIntent.HoldZone: return "Hold current zone";
                case OffBallIntent.Track: return "Track selected opponent";
                case OffBallIntent.FindSpace: return "Find space";
                case OffBallIntent.ForwardRun: return "Forward run";
                default: return intent.ToString();
            }
        }
        private void DrawDefenseAndIntent(float left)
        {
            if (World.PendingKeeper != null) return;
            var decision = World.PendingDefense;
            if (decision != null)
            {
                Panel(new Rect(0, 106, left, 135), Ink);
                Text(new Rect(24, 114, left - 48, 28), "DEFEND  /  " + Career.Players[decision.Options[0].Attacker.Id].Name, label, Coral);
                Text(new Rect(24, 145, left - 48, 25), "Tackle: win ball  ·  Contain: delay attack  ·  Block: deflect ball from shooting lane", muted, Color.white);
                float w = (left - 60) / 4;
                for (int i = 0; i < 3; i++)
                {
                    var kind = (DefensiveAction)i;
                    var option = decision.Options.FirstOrDefault(p => p.Kind == kind);
                    GUI.enabled = option != null;
                    string caption = kind + (option == null ? " / out of reach" : " " + option.Percent + "%") + " [" + (i + 1) + "]";
                    if (option == ChosenDefense) caption = "● " + caption;
                    if (Button(new Rect(24 + w * i, 183, w - 6, 39), caption)) ChooseDefense(kind);
                }
                GUI.enabled = true;
                if (Button(new Rect(24 + w * 3, 183, w - 6, 39), "DEFEND [ENTER]")) CommitChosenPass();
            }
            if (World.PendingDecision != null) return;
            Panel(new Rect(0, 714, left, 80), Ink);
            string status = World.ActiveIntent == null ? World.IntentStatus : IntentLabel(World.ActiveIntent.Kind) +
                (World.ActiveIntent.TargetId == null ? "" : " / " + Career.Players[World.ActiveIntent.TargetId].Name) +
                "  ·  " + World.IntentSecondsRemaining.ToString("0.0") + "s remaining";
            Text(new Rect(24, 720, left - 48, 28), status, muted, Lime);
            if (Button(new Rect(24, 754, 210, 30), "OFF-BALL INSTRUCTION [I]")) showIntentMenu = !showIntentMenu;
            Text(new Rect(246, 756, left - 270, 26), "Select an opponent to mark or track", small, Color.white);
            if (!showIntentMenu) return;
            Panel(new Rect(0, 602, left, 110), Ink);
            var intents = (OffBallIntent[])Enum.GetValues(typeof(OffBallIntent));
            float cell = (left - 48) / 5;
            for (int i = 0; i < intents.Length; i++)
            {
                GUI.enabled = World.CanSetIntent(intents[i], SelectedPlayer?.Id);
                if (Button(new Rect(24 + (i % 5) * cell, 612 + (i / 5) * 47, cell - 8, 38), IntentLabel(intents[i]))) ChooseIntent(intents[i]);
            }
            GUI.enabled = true;
        }
    }
}
