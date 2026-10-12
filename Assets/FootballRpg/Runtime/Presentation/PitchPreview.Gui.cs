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
            Matrix4x4 original = GUI.matrix;
            Color originalColor = GUI.color;
            Color originalBackground = GUI.backgroundColor;
            GUI.matrix = Matrix4x4.identity;
            GUI.backgroundColor = Ink;
            foreach (var player in players)
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
            float left = width - 360;
            Panel(new Rect(left, 0, 360, 900), Ink);
            Panel(new Rect(0, 0, left, 106), new Color(.03f, .065f, .075f, .97f));
            Panel(new Rect(0, 832, left, 68), new Color(.03f, .065f, .075f, .97f));
            Text(new Rect(28, 22, 300, 25), "FIRST WEEK  /  PITCH PREVIEW", small, Lime);
            Text(new Rect(28, 58, left - 220, 35), (Career.Clubs[Career.Fixture.HomeClubId].Name + "  /  " + Career.Clubs[Career.Fixture.AwayClubId].Name).ToUpperInvariant(), label, Color.white);
            Text(new Rect(left - 192, 18, 155, 27), Match.Elapsed.ToString(@"mm\:ss"), label, Color.white);
            if (Button(new Rect(left - 192, 54, 160, 34), Match.State == Simulation.MatchRunState.Paused ? "RESUME  [SPACE]" : "PAUSE  [SPACE]"))
                TogglePause();

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
                Bar(left + 24, 352 + i * 48, attributes[id].name, (SelectedPlayer.FootballAttributes[id] - attributes[id].minimum) / (float)(attributes[id].maximum - attributes[id].minimum),
                    SelectedPlayer.FootballAttributes[id].ToString(), teamColor);
            }
            Text(new Rect(left + 24, 565, 312, 25), "MATCH READINESS", small, new Color(.63f, .73f, .73f));
            Bar(left + 24, 604, "Confidence", SelectedPlayer.Confidence, Mathf.RoundToInt(SelectedPlayer.Confidence * 100) + "%", Lime);
            Bar(left + 24, 651, "Fatigue", SelectedPlayer.Fatigue, Mathf.RoundToInt(SelectedPlayer.Fatigue * 100) + "%", Coral);
            if (Button(new Rect(left + 24, 724, 150, 37), "HOME TEAM")) SelectPlayer(players.First(p => p.IsHome).PlayerId);
            if (Button(new Rect(left + 184, 724, 150, 37), "AWAY TEAM")) SelectPlayer(players.First(p => !p.IsHome).PlayerId);
            if (Button(new Rect(left + 24, 774, 150, 37), "PREVIOUS")) Browse(-1);
            if (Button(new Rect(left + 184, 774, 150, 37), "NEXT")) Browse(1);
            Text(new Rect(left + 24, 836, 312, 55), "AI open play  ·  " + World.BallState + "\nLime area: expected position.", muted, new Color(.63f, .73f, .73f));

            Text(new Rect(28, 847, left - 400, 43), "Click / Tab to inspect  ·  Scroll to zoom\nWASD / right-drag to pan  ·  Q / E to rotate", muted, new Color(.7f, .78f, .76f));
            if (Button(new Rect(left - 342, 848, 80, 32), "−")) AdjustCamera(0, 0, 0, 5);
            if (Button(new Rect(left - 252, 848, 80, 32), "+")) AdjustCamera(0, 0, 0, -5);
            if (Button(new Rect(left - 162, 848, 130, 32), "RESET VIEW")) ResetCamera();
            if (Button(new Rect(left - 342, 796, 310, 30), "BACK TO MENU  [ESC]")) Close();

            GUI.matrix = original;
            GUI.color = originalColor;
            GUI.backgroundColor = originalBackground;
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
