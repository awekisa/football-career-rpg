using System;
using System.Collections;
using UnityEngine;

namespace FootballRpg.Presentation
{
    /// <summary>Honest foundation screen; career entry arrives with the playable-week milestone.</summary>
    public sealed class LaunchScreen : MonoBehaviour
    {
        private bool showScope;
        private GUIStyle title;
        private GUIStyle eyebrow;
        private GUIStyle body;
        private GUIStyle small;
        private GUIStyle button;

        private IEnumerator Start()
        {
            Debug.Log("FOOTBALL_RPG_LAUNCH_READY");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--smoke-test") < 0)
                yield break;

            // Exercise the real launch scene and lifecycle in a built player before exiting.
            yield return null;
            yield return null;
            if (Camera.main == null || !isActiveAndEnabled)
            {
                Debug.LogError("FOOTBALL_RPG_SMOKE_FAILED");
                Application.Quit(1);
                yield break;
            }
            Debug.Log("FOOTBALL_RPG_SMOKE_PASSED");
            Application.Quit(0);
        }

        private void OnGUI()
        {
            EnsureStyles();
            var originalMatrix = GUI.matrix;
            var originalColor = GUI.color;
            float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.TRS(
                new Vector3((Screen.width - 1440 * scale) / 2, (Screen.height - 900 * scale) / 2, 0),
                Quaternion.identity, new Vector3(scale, scale, 1));

            Fill(new Rect(0, 0, 1440, 900), new Color(0.025f, 0.06f, 0.07f, 0.96f));
            Fill(new Rect(68, 78, 42, 4), new Color(0.66f, 0.91f, 0.43f));
            GUI.Label(new Rect(68, 108, 620, 30), "ONE PLAYER. YOUR CAREER.", eyebrow);
            GUI.Label(new Rect(64, 172, 850, 180), "FOOTBALL\nCAREER RPG", title);
            GUI.Label(new Rect(70, 398, 580, 110),
                "Read the game. Choose your moment.\nEarn your place, on and off the pitch.", body);
            GUI.Label(new Rect(70, 543, 550, 96),
                showScope
                    ? "The first chapter: create your footballer, live a week at a small club, and play an 11-versus-11 match."
                    : "An early foundation build. The playable career is still in development.", small);

            if (GUI.Button(new Rect(70, 683, 260, 58), showScope ? "BACK" : "THE FIRST CHAPTER", button))
                showScope = !showScope;
            if (GUI.Button(new Rect(346, 683, 125, 58), "QUIT", button))
                Application.Quit();

            DrawPitch(new Rect(872, 200, 460, 500));
            GUI.Label(new Rect(70, 810, 1100, 30), "PROTOTYPE  /  FOUNDATION     •     SINGLE PLAYER", eyebrow);
            GUI.matrix = originalMatrix;
            GUI.color = originalColor;
        }

        private void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 70, fontStyle = FontStyle.Bold };
            title.normal.textColor = new Color(0.95f, 0.96f, 0.91f);
            eyebrow = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            eyebrow.normal.textColor = new Color(0.66f, 0.91f, 0.43f);
            body = new GUIStyle(GUI.skin.label) { fontSize = 25, wordWrap = true };
            body.normal.textColor = new Color(0.88f, 0.9f, 0.86f);
            small = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
            small.normal.textColor = new Color(0.6f, 0.7f, 0.67f);
            button = new GUIStyle(GUI.skin.button) { fontSize = 17, fontStyle = FontStyle.Bold };
        }

        private static void DrawPitch(Rect r)
        {
            var line = new Color(0.27f, 0.45f, 0.36f);
            Fill(r, new Color(0.07f, 0.16f, 0.13f));
            Border(r, line);
            Fill(new Rect(r.x, r.center.y, r.width, 2), line);
            Border(new Rect(r.x + 120, r.y, 220, 90), line);
            Border(new Rect(r.x + 120, r.yMax - 90, 220, 90), line);
            Fill(new Rect(r.center.x - 7, r.center.y - 7, 14, 14), new Color(0.66f, 0.91f, 0.43f));
        }

        private static void Border(Rect r, Color color)
        {
            Fill(new Rect(r.x, r.y, r.width, 2), color);
            Fill(new Rect(r.x, r.yMax - 2, r.width, 2), color);
            Fill(new Rect(r.x, r.y, 2, r.height), color);
            Fill(new Rect(r.xMax - 2, r.y, 2, r.height), color);
        }

        private static void Fill(Rect r, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
