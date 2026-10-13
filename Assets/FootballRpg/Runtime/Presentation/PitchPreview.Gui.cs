using System.Linq;
using UnityEngine;

namespace FootballRpg.Presentation
{
    public sealed partial class PitchPreview
    {
        private GUIStyle heading, label, muted, small, uiButton, numberLabel;
        private static readonly Color Lime = new Color(.72f, .96f, .35f);
        private static readonly Color Blue = new Color(.34f, .77f, .92f);
        private static readonly Color Coral = new Color(1f, .52f, .38f);
        private static readonly Color Ink = new Color(.035f, .07f, .08f);

        private void OnGUI()
        {
            if (SelectedPlayer == null || ViewCamera == null) return;
            Styles();
            EnsureDecisionSelection();
            Matrix4x4 original = GUI.matrix;
            Color originalColor = GUI.color;
            Color originalBackground = GUI.backgroundColor;
            GUI.matrix = Matrix4x4.identity;
            GUI.backgroundColor = Ink;
            foreach (var player in players.Where(p => p.gameObject.activeSelf))
            {
                Vector3 point = ViewCamera.WorldToScreenPoint(player.transform.position + Vector3.up * 3.1f);
                if (point.z <= 0 || !ViewCamera.pixelRect.Contains(point)) continue;
                var rectangle = new Rect(point.x - 17 * UiScale, Screen.height - point.y - 9 * UiScale, 34 * UiScale, 21 * UiScale);
                numberLabel.fontSize = Mathf.RoundToInt(13 * UiScale);
                numberLabel.normal.textColor = player.IsUser ? Lime : player.IsHome ? Blue : Coral;
                GUI.Label(rectangle, player.IsUser ? "YOU" : player.ShirtNumber.ToString("00"), numberLabel);
            }
            GUI.matrix = Matrix4x4.Scale(new Vector3(UiScale, UiScale, 1));
            float width = Screen.width / UiScale;
            if (coachPanel) { DrawMatchCoach(width); GUI.matrix = original; GUI.color = originalColor; GUI.backgroundColor = originalBackground; return; }
            float left = width - 360;
            Panel(new Rect(left, 0, 360, 900), Ink);
            Panel(new Rect(0, 0, left, 106), new Color(.03f, .065f, .075f, .97f));
            Panel(new Rect(0, 832, left, 68), new Color(.03f, .065f, .075f, .97f));
            Text(new Rect(28, 22, 300, 25), (World.Rules == null ? "PRACTICE" : FixturePhaseLabel + (World.AnnouncedAddedSeconds > 0 ? " +" + World.AnnouncedAddedSeconds.ToString("0.#") + "s" : "")) + "  /  " + World.HomeGoals + " – " + World.AwayGoals, small, Lime);
            Text(new Rect(28, 58, World.CoachEnabled ? left - 530 : left - 220, 35), (Career.Clubs[Career.Fixture.HomeClubId].Name + "  /  " + Career.Clubs[Career.Fixture.AwayClubId].Name).ToUpperInvariant(), label, Color.white);
            Text(new Rect(left - 192, 18, 155, 27), MatchClockText, label, Color.white);
            GUI.enabled = !World.HasPendingDecision && !World.OffsideNoticePending && !World.RefereeNoticePending && !UserRestart && World.FixturePhase != Simulation.FixturePhase.FullTime;
            if (Button(new Rect(left - 192, 54, 160, 34), World.FixturePhase == Simulation.FixturePhase.FullTime ? "MATCH FINISHED" : World.FixturePhase == Simulation.FixturePhase.HalfTime ? "2ND HALF [SPACE]" : World.HasPendingDecision || UserRestart ? "CHOOSE ACTION" : Match.State == Simulation.MatchRunState.Paused ? "RESUME  [SPACE]" : "PAUSE  [SPACE]"))
                TogglePause();
            GUI.enabled = true;
            if (World.CoachEnabled && Button(new Rect(left - 492, 54, 280, 34), "COACH: " + World.CoachInstruction + " [T]")) OpenMatchCoach();
            DrawDecisionPanel(left);
            DrawFootballActions(left);
            DrawKeeperPanel(left);
            DrawDefenseAndIntent(left);
            DrawFixture(left);
            DrawViewingControls(left);
            Text(new Rect(24, 796, left - 390, 30), "Your status: " + World.UserParticipation + " · Subs " + World.SubstitutionsUsed(true) + " / " + World.SubstitutionsUsed(false) + " · " +
                (World.ParticipationMessage ?? "Coach selects substitutions at stoppages"), small, Lime);

            Text(new Rect(left + 24, 26, 310, 25), "PLAYER INSPECTION", small, Lime);
            var selectedMarker = players.First(p => p.PlayerId == SelectedPlayer.Id);
            Color teamColor = selectedMarker.IsHome ? Blue : Coral;
            Text(new Rect(left + 24, 69, 312, 35), Career.Clubs[SelectedPlayer.ClubId].Name, label, teamColor);
            Text(new Rect(left + 22, 112, 316, 80), SelectedPlayer.Name, heading, Color.white);
            Text(new Rect(left + 24, 200, 310, 35), positions[SelectedPlayer.PrimaryPositionId].name, label, Color.white);
            Text(new Rect(left + 24, 241, 315, 44),
                SelectedPlayer.Age + " years  ·  " + SelectedPlayer.HeightCm + " cm  ·  " + SelectedPlayer.PreferredFoot + " foot", muted, new Color(.63f, .73f, .73f));
            Panel(new Rect(left + 24, 290, 312, 1), new Color(.17f, .25f, .26f));
            Text(new Rect(left + 24, 310, 312, 25), "ROLE ATTRIBUTES", small, new Color(.63f, .73f, .73f));
            var relevant = positions[SelectedPlayer.PrimaryPositionId].recommendedAttributeIds;
            for (int i = 0; i < relevant.Length && i < 4; i++)
            {
                string id = relevant[i];
                Bar(left + 24, 336 + i * 50, attributes[id].name, (SelectedPlayer.FootballAttributes[id] - attributes[id].minimum) / (float)(attributes[id].maximum - attributes[id].minimum),
                    SelectedPlayer.FootballAttributes[id].ToString(), teamColor);
                var xp = Career.Progression.ForPlayer(SelectedPlayer.Id)[id];
                Text(new Rect(left + 24, 370 + i * 50, 300, 22), xp.NextLevelXp == 0 ? "Maximum level" : $"XP {xp.WithinLevelXp:0.0} / {xp.NextLevelXp:0}", small, new Color(.63f, .73f, .73f));
            }
            var development = Career.Progression.Changes.LastOrDefault(c => c.PlayerId == SelectedPlayer.Id);
            if (development != null)
                Text(new Rect(left + 24, 544, 312, 20), $"Last action: {development.Deltas.Values.Sum():+0.0;-0.0;0} skill XP", small, Lime);
            Text(new Rect(left + 24, 565, 312, 25), (World.Participation(SelectedPlayer.Id).ToString().ToUpperInvariant() + (World.Discipline.Yellows(SelectedPlayer.Id) > 0 && World.CanParticipate(SelectedPlayer.Id) ? " · YELLOW CARD" : "") + (SelectedPlayer.InjuryDaysRemaining > 0 ? " · " + SelectedPlayer.InjuryDaysRemaining + " DAYS OUT" : "")), small, new Color(.63f, .73f, .73f));
            Bar(left + 24, 604, $"Confidence · Morale {SelectedPlayer.Morale:P0}", SelectedPlayer.Confidence, Mathf.RoundToInt(SelectedPlayer.Confidence * 100) + "%", Lime);
            Bar(left + 24, 651, "Fatigue", SelectedPlayer.Fatigue, Mathf.RoundToInt(SelectedPlayer.Fatigue * 100) + "%", Coral);
            var condition = Career.Readiness.Changes.LastOrDefault(c => c.PlayerId == SelectedPlayer.Id);
            if (condition != null) Text(new Rect(left + 24, 694, 312, 25), condition.Reason, small, Lime);
            if (Button(new Rect(left + 24, 724, 150, 37), "HOME TEAM")) SelectPlayer(players.First(p => p.IsHome).PlayerId);
            if (Button(new Rect(left + 184, 724, 150, 37), "AWAY TEAM")) SelectPlayer(players.First(p => !p.IsHome).PlayerId);
            if (Button(new Rect(left + 24, 774, 150, 37), "PREVIOUS")) Browse(-1);
            if (Button(new Rect(left + 184, 774, 150, 37), "NEXT")) Browse(1);
            string passInfo = "AI open play  ·  " + World.BallState + "\nLime area: current movement target.";
            if (World.CanParticipate(SelectedPlayer.Id) && World.OwnerId != null && World.OwnerId != SelectedPlayer.Id &&
                Career.Players[World.OwnerId].ClubId == SelectedPlayer.ClubId &&
                World.Players.First(p => p.Player.Id == World.OwnerId).Position.Distance(World.Players.First(p => p.Player.Id == SelectedPlayer.Id).Position) >= .1)
                passInfo = "From ball carrier to selected player\nFeet " + World.PreviewPass(SelectedPlayer.Id, Simulation.PassKind.ToFeet).Percent +
                    "%  ·  Through " + World.PreviewPass(SelectedPlayer.Id, Simulation.PassKind.ThroughBall).Percent + "%";
            Text(new Rect(left + 24, 836, 312, 55), passInfo, muted, new Color(.63f, .73f, .73f));

            Text(new Rect(28, 847, left - 400, 43), "Click / Tab to inspect  ·  Scroll to zoom\nWASD / right-drag to pan  ·  Q / E to rotate", muted, new Color(.7f, .78f, .76f));
            if (Button(new Rect(left - 342, 848, 80, 32), "−")) AdjustCamera(0, 0, 0, 5);
            if (Button(new Rect(left - 252, 848, 80, 32), "+")) AdjustCamera(0, 0, 0, -5);
            if (Button(new Rect(left - 162, 848, 130, 32), "RESET VIEW")) ResetCamera();
            if (Button(new Rect(left - 342, 796, 310, 30), "BACK TO MENU  [ESC]")) Close();

            GUI.matrix = original;
            GUI.color = originalColor;
            GUI.backgroundColor = originalBackground;
        }

        private void DrawDecisionPanel(float left)
        {
            var decision = World.PendingDecision;
            var chosen = ChosenPass;
            var action = ChosenFootballAction;
            if (decision == null || (World.PendingKeeper != null && !showKeeperFeet)) return;
            Panel(new Rect(0, 106, left, 105), new Color(.04f, .12f, .14f, .98f));
            string title = action != null ? ActionLabel(action.Kind) + " / " + action.Variant + "  " + action.Percent + (action.Success == Simulation.FootballOutcome.ShotOnTarget ? "% on target" : "%")
                : "Pass to " + (chosen == null ? "teammate" : Career.Players[chosen.Receiver.Id].Name);
            if (action?.Receiver != null) title += " → " + Career.Players[action.Receiver.Id].Name;
            Text(new Rect(24, 115, left - 230, 30), title, label, Lime);
            if (Button(new Rect(left - 192, 114, 168, 29), "NEXT TEAMMATE  [R]")) CyclePassTarget();
            float w = (left - 72) / 3;
            var feet = decision.Options.FirstOrDefault(p => p.Receiver.Id == selectedReceiverId && p.Kind == Simulation.PassKind.ToFeet);
            var through = decision.Options.FirstOrDefault(p => p.Receiver.Id == selectedReceiverId && p.Kind == Simulation.PassKind.ThroughBall);
            GUI.enabled = feet != null;
            if (Button(new Rect(24, 154, w, 40), feet == null ? "RECEIVING BALL" : (action == null && chosen?.Kind == Simulation.PassKind.ToFeet ? "● " : "") + "TO FEET  " + feet.Percent + "%  [1]"))
                ChoosePassKind(Simulation.PassKind.ToFeet);
            GUI.enabled = through != null;
            if (Button(new Rect(36 + w, 154, w, 40), through == null ? "CHOOSE AN ACTION BELOW" : (action == null && chosen?.Kind == Simulation.PassKind.ThroughBall ? "● " : "") + "THROUGH BALL  " + through.Percent + "%  [2]"))
                ChoosePassKind(Simulation.PassKind.ThroughBall);
            GUI.enabled = action != null || chosen != null;
            if (Button(new Rect(48 + w * 2, 154, w, 40), "PLAY ACTION  [ENTER]")) CommitChosenPass();
            GUI.enabled = true;
        }

        private void DrawFootballActions(float left)
        {
            if (World.PendingDecision == null || (World.PendingKeeper != null && !showKeeperFeet)) return;
            Panel(new Rect(0, 714, left, 80), Ink);
            Text(new Rect(24, 719, left - 48, 22), "ACTION VARIANTS  ·  Press the same action again to change its variant", small, Lime);
            var kinds = ContextualActionKinds;
            float w = (left - 48) / kinds.Length;
            for (int i = 0; i < kinds.Length; i++)
            {
                GUI.enabled = World.PendingDecision.Actions.Any(p => p.Kind == kinds[i]);
                if (Button(new Rect(24 + i * w, 746, w - 8, 35), ActionLabel(kinds[i]) + "  [" + (i + 3) + "]")) ChooseFootballAction(kinds[i]);
            }
            GUI.enabled = true;
        }

        private static string ActionLabel(Simulation.FootballActionKind kind)
        {
            switch (kind)
            {
                case Simulation.FootballActionKind.FirstTimePass: return "First-time pass";
                case Simulation.FootballActionKind.HeaderPass: return "Head to teammate";
                case Simulation.FootballActionKind.HeaderShot: return "Head at goal";
                case Simulation.FootballActionKind.Shot: return "Shoot";
                case Simulation.FootballActionKind.Clearance: return "Clear";
                default: return kind.ToString();
            }
        }

        private void Browse(int direction)
        {
            int index = players.FindIndex(p => p.PlayerId == SelectedPlayer.Id);
            SelectPlayer(players[(index + direction + players.Count) % players.Count].PlayerId);
        }

        private void Bar(float x, float y, string caption, float value, string displayedValue, Color color)
        {
            Text(new Rect(x, y, 255, 25), caption, muted, Color.white);
            Text(new Rect(x + 260, y, 50, 25), displayedValue, label, color);
            Panel(new Rect(x, y + 29, 312, 4), new Color(.14f, .23f, .24f));
            Panel(new Rect(x, y + 29, 312 * Mathf.Clamp01(value), 4), color);
        }

        private void Styles()
        {
            if (heading != null) return;
            heading = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, wordWrap = true };
            label = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true };
            muted = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            small = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            uiButton = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold };
            numberLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            numberLabel.normal.background = Texture2D.whiteTexture;
        }

        private static void Text(Rect rect, string value, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(rect, value, style);
        }

        private bool Button(Rect rect, string value)
        {
            GUI.backgroundColor = new Color(.28f, .43f, .42f);
            return GUI.Button(rect, value, uiButton);
        }

        private static void Panel(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
