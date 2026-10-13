using System;
using System.Linq;
using FootballRpg.Simulation;
using UnityEngine;

namespace FootballRpg.Presentation
{
    public sealed class GuidedPracticeView : MonoBehaviour
    {
        private CareerState career;
        private Action onClose;
        private PitchGeometry geometry;
        private Camera priorCamera;
        private bool priorEnabled, closed;
        private int readyFrame, selected;
        private GUIStyle text, heading, button;
        private Vector2 scroll;
        private Transform ball;
        private LineRenderer targetLine;
        public Camera ViewCamera { get; private set; }
        public GuidedPracticeSession Session => career.Training.ActivePractice;
        public void Initialize(CareerState state, Action close)
        {
            if (career != null || state?.Training.ActivePractice == null) throw new InvalidOperationException("Start practice before opening its view.");
            career = state; onClose = close; readyFrame = Time.frameCount + 1;
            priorCamera = Camera.main; priorEnabled = priorCamera != null && priorCamera.enabled;
            if (priorCamera != null) priorCamera.enabled = false;
            ViewCamera = new GameObject("Practice Camera").AddComponent<Camera>();
            ViewCamera.transform.SetParent(transform, false); ViewCamera.tag = "MainCamera";
            ViewCamera.orthographic = true; ViewCamera.orthographicSize = 17; ViewCamera.farClipPlane = 200;
            ViewCamera.clearFlags = CameraClearFlags.SolidColor; ViewCamera.backgroundColor = new Color(.025f, .06f, .07f);
            ViewCamera.rect = new Rect(0, .16f, .60f, .63f);
            var light = new GameObject("Practice Light").AddComponent<Light>(); light.transform.SetParent(transform, false);
            light.type = LightType.Directional; light.intensity = 1.3f; light.transform.rotation = Quaternion.Euler(55, -35, 0);
            BuildScene();
        }
        private static Vector3 Point(PitchPoint p, float height = 0) => new Vector3((float)p.X, height, (float)p.Z);
        private void BuildScene()
        {
            if (geometry != null) { geometry.Root.gameObject.SetActive(false); geometry.Dispose(); }
            geometry = new PitchGeometry(transform); geometry.BuildField();
            var decision = Session.Decision;
            foreach (var actor in decision.Actors)
                geometry.Shape(actor.Id, PrimitiveType.Capsule, Point(actor.Position, 1), new Vector3(.8f, 1, .8f), geometry.Material(
                    actor.Id == career.UserPlayerId ? new Color(.2f, .7f, 1) : actor.ClubId == career.UserPlayer.ClubId ? new Color(.6f, .95f, .3f) : new Color(1, .3f, .2f)));
            ball = geometry.Root.Find("Ball"); ball.position = Point(decision.Ball, .23f);
            targetLine = geometry.Line("Selected action target", new[] { Point(decision.Ball, .1f), Point(decision.Options[selected].Target, .1f) }, geometry.Material(Color.yellow, true), .14f);
            var center = Point(decision.Actors[0].Position) + new Vector3(Session.Family == PracticeFamily.Passing ? 8 : 3, 0, 0);
            ViewCamera.transform.position = center + new Vector3(-15, 25, -23); ViewCamera.transform.LookAt(center);
        }
        public void Select(int index)
        {
            if (Session.Finished || Session.LastResult != null || index < 0 || index >= Session.Decision.Options.Count) return;
            selected = index; targetLine.SetPosition(1, Point(Session.Decision.Options[selected].Target, .1f));
        }
        public void Continue()
        {
            if (closed) return;
            if (Session.Finished) { Close(); return; }
            if (Session.LastResult != null) { Session.Next(); selected = 0; BuildScene(); return; }
            var choice = Session.Decision.Options[selected];
            career.Training.ChoosePractice(Session.Decision.Id, choice.Id);
            // The static aftermath is illustrative; there is no animation timing or reflex input.
            if (Session.LastResult.Succeeded && Session.Family == PracticeFamily.Passing) ball.position = Point(choice.Target, .23f);
        }
        public void Close()
        {
            if (closed) return;
            closed = true; career.Training.FinishPractice();
            if (priorCamera != null) priorCamera.enabled = priorEnabled;
            gameObject.SetActive(false); onClose?.Invoke(); PitchGeometry.Release(gameObject);
        }
        private void OnDestroy()
        {
            geometry?.Dispose();
            if (!closed && career != null) { career.Training.FinishPractice(); if (priorCamera != null) priorCamera.enabled = priorEnabled; }
        }
        private void Update()
        {
            if (Time.frameCount <= readyFrame) return;
            for (int i = 0; i < Session.Decision.Options.Count; i++) if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) Select(i);
            if (Input.GetKeyDown(KeyCode.Return)) Continue();
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }
        private void OnGUI()
        {
            if (career == null || closed) return;
            if (text == null)
            {
                text = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true };
                heading = new GUIStyle(text) { fontSize = 28, fontStyle = FontStyle.Bold };
                button = new GUIStyle(GUI.skin.button) { fontSize = 19, wordWrap = true };
            }
            var matrix = GUI.matrix; float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1440 * scale) / 2, (Screen.height - 900 * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
            var color = GUI.color; GUI.color = new Color(.025f, .06f, .07f);
            GUI.DrawTexture(new Rect(0, 0, 1440, 185), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(865, 185, 575, 715), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, 755, 865, 145), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.Label(new Rect(35, 30, 1360, 45), "GUIDED PRACTICE  ·  " + Session.Family + "  ·  Drill " + Math.Min(Session.Results.Count + (Session.LastResult == null ? 1 : 0), 3) + "/3", heading);
            GUI.Label(new Rect(35, 85, 1300, 85), GuidedPracticeSession.Guidance(Session.Family), text);
            GUI.Label(new Rect(35, 770, 790, 90), "Blue: you · Green: teammate · Red: opponent\nYellow: selected target · Scene paused for your decision\nThe slot is already spent. Ending early keeps only completed drill XP.", text);
            var option = Session.Decision.Options[selected];
            GUI.Label(new Rect(890, 185, 515, 50), Session.LastResult == null ? "Choose an action; inspect freely" : "Coach's review", heading);
            for (int i = 0; i < Session.Decision.Options.Count; i++)
            {
                var o = Session.Decision.Options[i]; GUI.enabled = !Session.Finished && Session.LastResult == null;
                if (GUI.Button(new Rect(890, 250 + 60 * i, 515, 52), (selected == i ? "• " : "") + (i + 1) + ". " + o.Id + "  ·  " + o.Percent + "%", button)) Select(i);
            }
            GUI.enabled = true;
            string explanation = option.Guidance + "\n\nPrimary skill: " + option.PrimarySkill.Replace('-', ' ') + " (" + career.UserPlayer.FootballAttributes[option.PrimarySkill] + ")" +
                "\n90% of each drill's reward targets that skill; 10% develops decisions. Failed drills earn 65% of that reward. Three drills share one slot's XP budget.";
            if (Session.LastResult != null) explanation = "Outcome: " + Session.LastResult.OutcomeId + "\n" + (Session.LastResult.Succeeded ? "Successful execution." : "The attempt failed. Review the risk and try a different approach next time.") + "\n\n" + explanation;
            explanation += "\n\nOpponent: " + string.Join(" · ", Session.Decision.Actors.Last().Attributes.Where(p => new[] { "dribbling", "tackling", "positioning" }.Contains(p.Key)).Select(p => p.Key + " " + p.Value));
            if (Session.Finished)
                explanation += "\n\nSESSION REWARD\n" + string.Join("\n", career.Training.Results.Last().Gains.Select(g => g.SkillId + ": +" + g.Xp.ToString("0.0") + " XP"));
            float height = Mathf.Max(280, text.CalcHeight(new GUIContent(explanation), 475));
            scroll = GUI.BeginScrollView(new Rect(890, 445, 515, 285), scroll, new Rect(0, 0, 485, height));
            GUI.Label(new Rect(0, 0, 475, height), explanation, text); GUI.EndScrollView();
            if (GUI.Button(new Rect(890, 750, 515, 55), (Session.Finished ? "RETURN TO CALENDAR" : Session.LastResult != null ? "NEXT DRILL" : "CONFIRM ACTION") + "  [ENTER]", button)) Continue();
            if (GUI.Button(new Rect(890, 820, 515, 45), "END SESSION / KEEP EARNED XP  [ESC]", button)) Close();
            GUI.matrix = matrix;
        }
    }
}
