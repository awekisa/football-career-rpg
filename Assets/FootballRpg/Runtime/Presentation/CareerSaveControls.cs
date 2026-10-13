using UnityEngine;

namespace FootballRpg.Presentation
{
    // Separate GUI layer keeps save controls available over pitch and club views.
    public sealed class CareerSaveControls : MonoBehaviour
    {
        public LaunchScreen ScreenOwner;
        private bool open;
        private static CareerSaveControls instance;
        private void OnEnable() { instance = this; }
        private void OnDisable() { if (instance == this) instance = null; }
        public static bool Covers(Vector3 point)
        {
            if (instance == null) return false;
            var gui = new Vector2(point.x, Screen.height - point.y);
            return new Rect(Screen.width - 145, 2, 140, 25).Contains(gui) || instance.open && new Rect(Screen.width - 405, 32, 400, 165).Contains(gui);
        }
        private string message;
        private GUIStyle messageStyle;
        private void Update()
        {
            if (ScreenOwner == null) return;
            if (Input.GetKeyDown(KeyCode.F5)) { message = ScreenOwner.SaveCareer().Message; open = true; }
            if (Input.GetKeyDown(KeyCode.F9)) { message = ScreenOwner.LoadCareer().Message; open = true; }
        }
        private void OnGUI()
        {
            if (ScreenOwner == null) return;
            int depth = GUI.depth; GUI.depth = -100;
            if (messageStyle == null) messageStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            if (GUI.Button(new Rect(Screen.width - 145, 2, 140, 25), "SAVE / LOAD")) open = !open;
            if (open)
            {
                GUI.Box(new Rect(Screen.width - 405, 32, 400, 165), "Career save");
                if (GUI.Button(new Rect(Screen.width - 390, 62, 180, 30), "Save [F5]")) message = ScreenOwner.SaveCareer().Message;
                if (GUI.Button(new Rect(Screen.width - 200, 62, 180, 30), "Load [F9]")) message = ScreenOwner.LoadCareer().Message;
                GUI.Label(new Rect(Screen.width - 390, 100, 370, 85), message ?? "One career slot with a previous-save backup.\nPause the match before saving. Loading replaces\nthe current session with your saved career.", messageStyle);
            }
            GUI.depth = depth;
        }
    }
}
