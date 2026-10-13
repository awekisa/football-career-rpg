using System;
using System.Linq;
using FootballRpg.Simulation;
using UnityEngine;

namespace FootballRpg.Presentation
{
    public sealed class ClubVenueView : MonoBehaviour
    {
        private PitchGeometry geometry;
        private Camera priorCamera;
        private bool priorEnabled, closed, showSquad;
        private Action onClose;
        private Action<ClubInteraction> onInteraction;
        private Transform player, marker;
        private readonly System.Collections.Generic.Dictionary<string, GameObject> people = new System.Collections.Generic.Dictionary<string, GameObject>();
        private LineRenderer route;
        private GUIStyle heading, text, button, caption;
        private Vector2 detailsScroll;
        private int readyFrame;
        private string feedback;
        public ClubExploration Exploration { get; private set; }
        public Camera ViewCamera { get; private set; }
        public void Initialize(ClubExploration exploration, Action close = null, Action<ClubInteraction> interact = null)
        {
            if (Exploration != null) throw new InvalidOperationException("Club view is already initialized.");
            Exploration = exploration ?? throw new ArgumentNullException(nameof(exploration)); onClose = close; onInteraction = interact;
            readyFrame = Time.frameCount + 1;
            priorCamera = Camera.main; priorEnabled = priorCamera != null && priorCamera.enabled;
            if (priorCamera != null) priorCamera.enabled = false;
            ViewCamera = new GameObject("Club Camera").AddComponent<Camera>(); ViewCamera.transform.SetParent(transform, false);
            ViewCamera.tag = "MainCamera"; ViewCamera.orthographic = true; ViewCamera.orthographicSize = 28;
            ViewCamera.clearFlags = CameraClearFlags.SolidColor; ViewCamera.backgroundColor = new Color(.055f, .11f, .12f);
            ViewCamera.nearClipPlane = .1f; ViewCamera.farClipPlane = 250; ViewCamera.rect = new Rect(0, .14f, .65f, .68f);
            ViewCamera.transform.position = new Vector3(-22, 44, -35); ViewCamera.transform.LookAt(Vector3.zero);
            var light = new GameObject("Club Light").AddComponent<Light>(); light.transform.SetParent(transform, false);
            light.type = LightType.Directional; light.intensity = 1.1f; light.transform.rotation = Quaternion.Euler(60, -40, 0);
            geometry = new PitchGeometry(transform); BuildVenue(); Sync();
        }
        private static Vector3 Point(PitchPoint p, float y = 0) => new Vector3((float)p.X, y, (float)p.Z);
        private void BuildVenue()
        {
            var map = Exploration.Venue; float cell = (float)map.CellSize;
            var floor = geometry.Material(new Color(.29f, .42f, .4f)); var wall = geometry.Material(new Color(.65f, .73f, .69f));
            var white = geometry.Material(new Color(.9f, .94f, .87f), true); var accent = geometry.Material(new Color(.6f, .9f, .3f), true);
            geometry.Shape("Club floor", PrimitiveType.Cube, new Vector3(0, -.15f, 0), new Vector3(map.Width * cell, .2f, map.Height * cell), floor);
            for (int z = 0; z < map.Height; z++) for (int x = 0; x < map.Width; x++)
                if (map.IsWall(x, z)) geometry.Shape("Wall " + x + ":" + z, PrimitiveType.Cube, Point(map.Point(x, z), .55f), new Vector3(cell, 1.1f, cell), wall);
            foreach (var d in map.Destinations)
            {
                var center = Point(map.Point(d));
                geometry.Shape(d.Name + " destination", PrimitiveType.Cylinder, center + Vector3.up * .04f, new Vector3(2.4f, .04f, 2.4f), accent);
                if (d.Id != "arrival" && d.Id != "training")
                {
                    geometry.Shape(d.Name + " desk", PrimitiveType.Cube, center + new Vector3(0, .55f, 2.2f), new Vector3(2.6f, 1.1f, 1), floor);
                    people[d.Id] = geometry.Shape(d.Name + " person", PrimitiveType.Capsule, center + new Vector3(0, 1, 3.2f), new Vector3(.65f, .9f, .65f), white);
                }
            }
            var arrival = Point(map.Point(map.Destinations.Single(d => d.Id == "arrival")));
            var car = geometry.Material(new Color(.08f, .18f, .27f));
            geometry.Shape("Parked car", PrimitiveType.Cube, arrival + new Vector3(3.6f, .55f, 0), new Vector3(1.7f, 1.1f, 3), car);
            geometry.Shape("Car cabin", PrimitiveType.Cube, arrival + new Vector3(3.6f, 1.2f, 0), new Vector3(1.5f, .55f, 1.5f), white);
            player = geometry.Shape("Your footballer", PrimitiveType.Capsule, Point(Exploration.Position, 1), new Vector3(.65f, 1, .65f), geometry.Material(new Color(.2f, .7f, 1))).transform;
            marker = geometry.Circle("Walk target", .7f, Vector3.up * .15f, accent, .15f).transform;
            route = geometry.Line("Walking route", Array.Empty<Vector3>(), accent, .10f);
        }
        public bool Navigate(string id)
        {
            feedback = null; showSquad = false; detailsScroll = Vector2.zero;
            bool result = Exploration.WalkTo(id); Sync(); return result;
        }
        public bool SelectAtScreenPoint(Vector3 point)
        {
            if (!ViewCamera.pixelRect.Contains(point)) return false;
            var ray = ViewCamera.ScreenPointToRay(point); var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float distance)) return false;
            var world = ray.GetPoint(distance); var target = new PitchPoint(world.x, world.z);
            var nearby = Exploration.Venue.Destinations.OrderBy(d => Exploration.Venue.Point(d).Distance(target)).First();
            feedback = null; showSquad = false; detailsScroll = Vector2.zero;
            bool result = Exploration.Venue.Point(nearby).Distance(target) < Exploration.Venue.CellSize * .7 ?
                Exploration.WalkTo(nearby.Id) : Exploration.WalkTo(target);
            Sync(); return result;
        }
        public void AdvanceWalk(double seconds) { Exploration.Advance(seconds); Sync(); }
        private void Sync()
        {
            player.position = Point(Exploration.Position, 1);
            foreach (var person in people) person.Value.SetActive(Exploration.Inspect(person.Key).Available);
            var points = Exploration.Moving ? Exploration.Path.Select(p => Point(p, .15f)).ToArray() : Array.Empty<Vector3>();
            route.positionCount = points.Length; if (points.Length > 0) route.SetPositions(points);
            marker.gameObject.SetActive(Exploration.Destination != null || points.Length > 0);
            if (Exploration.Destination != null) marker.position = Point(Exploration.Venue.Point(Exploration.Destination));
            else if (points.Length > 0) marker.position = points.Last();
        }
        public void Interact()
        {
            try
            {
                var action = Exploration.Interact();
                if (action.Kind == ClubInteractionKind.Squad) showSquad = true;
                else onInteraction?.Invoke(action);
            }
            catch (InvalidOperationException e) { feedback = e.Message; }
        }
        public void Close()
        {
            if (closed) return; closed = true;
            if (priorCamera != null) priorCamera.enabled = priorEnabled;
            gameObject.SetActive(false); onClose?.Invoke(); PitchGeometry.Release(gameObject);
        }
        private void OnDestroy() { geometry?.Dispose(); if (!closed && priorCamera != null) priorCamera.enabled = priorEnabled; }
        private void Update()
        {
            if (Time.frameCount <= readyFrame) return;
            AdvanceWalk(Time.unscaledDeltaTime);
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            for (int i = 0; i < Exploration.Venue.Destinations.Count; i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) Navigate(Exploration.Venue.Destinations[i].Id);
            if (!CareerSaveControls.Covers(Input.mousePosition) && Input.GetMouseButtonDown(0)) SelectAtScreenPoint(Input.mousePosition);
            if (Input.GetKeyDown(KeyCode.Return)) Interact();
        }
        private void OnGUI()
        {
            if (Exploration == null || closed) return;
            if (text == null)
            {
                text = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true };
                heading = new GUIStyle(text) { fontSize = 29, fontStyle = FontStyle.Bold };
                button = new GUIStyle(GUI.skin.button) { fontSize = 18, wordWrap = true };
                caption = new GUIStyle(text) { fontSize = 15, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            }
            var matrix = GUI.matrix; float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            var offset = new Vector2((Screen.width - 1440 * scale) / 2, (Screen.height - 900 * scale) / 2);
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one * scale);
            var color = GUI.color; GUI.color = new Color(.025f, .06f, .07f);
            GUI.DrawTexture(new Rect(0, 0, 1440, 165), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(935, 165, 505, 735), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, 775, 935, 125), Texture2D.whiteTexture); GUI.color = color;
            var calendar = Exploration.Career.Calendar;
            GUI.Label(new Rect(30, 25, 1360, 50), "YOUR CLUB  ·  " + calendar.TimeLabel(calendar.Minute), heading);
            GUI.Label(new Rect(30, 83, 1340, 70), "Click open ground or a marked destination and watch your footballer walk there.\nWalking and inspection cost no time. Activities are confirmed in the calendar.", text);
            for (int i = 0; i < Exploration.Venue.Destinations.Count; i++)
            {
                var d = Exploration.Venue.Destinations[i]; var available = Exploration.Inspect(d.Id);
                if (GUI.Button(new Rect(960, 180 + i * 60, 450, 52), (i + 1) + ". " + d.Name + "\n" + (available.Available ? "Available" : "Unavailable"), button)) Navigate(d.Id);
                var point = ViewCamera.WorldToScreenPoint(Point(Exploration.Venue.Point(d), 1.8f));
                var label = new Rect((point.x - offset.x) / scale - 78, (Screen.height - point.y - offset.y) / scale - 15, 156, 30);
                if (ViewCamera.pixelRect.Contains(point)) { GUI.Box(label, GUIContent.none); GUI.Label(label, (i + 1) + ". " + d.Name, caption); }
            }
            string details = feedback ?? Exploration.Message;
            ClubInteraction interaction = null;
            if (Exploration.Destination != null)
            {
                interaction = Exploration.Inspect(Exploration.Destination.Id);
                details += "\n\n" + interaction.Description;
                if (Exploration.Career.UserPlayer.InjuryDaysRemaining > 0) details += "\nInjury: " + Exploration.Career.UserPlayer.InjuryDaysRemaining + " day(s). Walking does not change recovery time.";
            }
            if (showSquad) details += "\n\n" + string.Join("\n", Exploration.Career.Players.Values.Where(p => p.ClubId == Exploration.Career.UserPlayer.ClubId && p.Id != Exploration.Career.UserPlayerId).Select(p => p.Name + " · " + p.PrimaryPositionId.ToUpperInvariant() + " · passing " + p.FootballAttributes["short-passing"]));
            float height = Mathf.Max(180, text.CalcHeight(new GUIContent(details), 410));
            detailsScroll = GUI.BeginScrollView(new Rect(960, 550, 450, 190), detailsScroll, new Rect(0, 0, 420, height));
            GUI.Label(new Rect(0, 0, 410, height), details, text); GUI.EndScrollView();
            GUI.enabled = Exploration.AtDestination && interaction?.Available == true;
            if (GUI.Button(new Rect(960, 760, 450, 55), (interaction?.Title ?? "Walk to a destination") + "  [ENTER]", button)) Interact();
            GUI.enabled = true;
            GUI.Label(new Rect(30, 795, 880, 85), "Blue: your footballer · Green: destinations and walking route\n" + Exploration.Message, text);
            if (GUI.Button(new Rect(960, 830, 450, 45), "BACK TO LAUNCH  [ESC]", button)) Close();
            GUI.matrix = matrix;
        }
    }
}
