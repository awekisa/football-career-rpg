using System;
using System.Collections.Generic;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Simulation;
using UnityEngine;

namespace FootballRpg.Presentation
{
    public sealed partial class PitchPreview : MonoBehaviour
    {
        private readonly List<PitchPlayer> players = new List<PitchPlayer>();
        private readonly Dictionary<string, PositionDefinition> positions = new Dictionary<string, PositionDefinition>();
        private readonly Dictionary<string, ValueDefinition> attributes = new Dictionary<string, ValueDefinition>();
        private PitchGeometry geometry;
        private Camera launchCamera;
        private bool launchCameraEnabled;
        private Vector3 focus;
        private float yaw;
        private float zoom = 50;
        private Action onClose;
        public Camera ViewCamera { get; private set; }
        public CareerState Career { get; private set; }
        public MatchSession Match { get; private set; }
        public IReadOnlyList<PitchPlayer> Players => players.AsReadOnly();
        public PlayerState SelectedPlayer { get; private set; }
        public float Zoom => zoom;
        public float Yaw => yaw;
        private float UiScale => Mathf.Max(.5f, Screen.height / 900f);
        private float SidebarPixels => 360 * UiScale;

        public void Initialize(CareerState career, Action close = null)
        {
            if (Career != null) throw new InvalidOperationException("Preview already initialized.");
            Career = career ?? throw new ArgumentNullException(nameof(career));
            onClose = close;
            var content = PrototypeContent.Load();
            foreach (var position in content.positions) positions.Add(position.id, position);
            foreach (var attribute in content.footballAttributes) attributes.Add(attribute.id, attribute);
            Match = new MatchSession(career.Fixture.Id + "-preview", 42);
            Match.Pause();
            gameObject.AddComponent<MatchDriver>().Initialize(Match);
            launchCamera = Camera.main;
            if (launchCamera != null)
            {
                launchCameraEnabled = launchCamera.enabled;
                launchCamera.enabled = false;
            }
            var cameraObject = new GameObject("Pitch Camera", typeof(Camera));
            cameraObject.transform.SetParent(transform, false);
            ViewCamera = cameraObject.GetComponent<Camera>();
            ViewCamera.tag = "MainCamera";
            ViewCamera.orthographic = true;
            ViewCamera.clearFlags = CameraClearFlags.SolidColor;
            ViewCamera.backgroundColor = new Color(.035f, .07f, .08f);
            ViewCamera.nearClipPlane = .1f;
            ViewCamera.farClipPlane = 350;

            var light = new GameObject("Pitch Light").AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.transform.rotation = Quaternion.Euler(55, -35, 0);
            geometry = new PitchGeometry(transform);
            geometry.BuildField();
            BuildPlayers();
            SelectPlayer(Career.UserPlayerId);
            ResetCamera();
        }

        private void BuildPlayers()
        {
            var homeKit = geometry.Material(new Color(.22f, .65f, .78f));
            var awayKit = geometry.Material(new Color(.96f, .40f, .28f));
            var keeperKit = geometry.Material(new Color(.86f, .68f, .22f));
            var shorts = geometry.Material(new Color(.055f, .09f, .13f));
            var skin = geometry.Material(new Color(.75f, .55f, .38f));
            var userColor = geometry.Material(new Color(.72f, .96f, .35f), true);
            var selectedColor = geometry.Material(Color.white, true);
            var anchors = new[] { new Vector2(-49, 0), new Vector2(-33, -25), new Vector2(-36, -9),
                new Vector2(-36, 9), new Vector2(-33, 25), new Vector2(-23, -14), new Vector2(-22, 0),
                new Vector2(-20, 15), new Vector2(-8, -24), new Vector2(-8, 24), new Vector2(-6, 0) };
            foreach (bool home in new[] { true, false })
            {
                string clubId = home ? Career.Fixture.HomeClubId : Career.Fixture.AwayClubId;
                var squad = Career.Clubs[clubId].SquadPlayerIds.Take(11).ToArray();
                for (int i = 0; i < squad.Length; i++)
                {
                    var state = Career.Players[squad[i]];
                    var root = new GameObject(state.Name).transform;
                    root.SetParent(transform, false);
                    int direction = home ? 1 : -1;
                    root.localPosition = new Vector3(anchors[i].x * direction, 0, anchors[i].y * direction);
                    var kit = state.PrimaryPositionId == "gk" ? keeperKit : home ? homeKit : awayKit;
                    geometry.Shape("Shirt", PrimitiveType.Capsule, new Vector3(0, 1.2f, 0), new Vector3(1.05f, .65f, 1.05f), kit, root);
                    geometry.Shape("Shorts", PrimitiveType.Cube, new Vector3(0, .4f, 0), new Vector3(.8f, .65f, .65f), shorts, root);
                    geometry.Shape("Head", PrimitiveType.Sphere, new Vector3(0, 2.05f, 0), Vector3.one * .68f, skin, root);
                    var collider = root.gameObject.AddComponent<CapsuleCollider>();
                    collider.center = new Vector3(0, 1.1f, 0);
                    collider.height = 3.2f;
                    collider.radius = 1.35f;
                    bool user = state.Id == Career.UserPlayerId;
                    if (user) geometry.Circle("Your Player", 1.8f, new Vector3(0, .12f, 0), userColor, .32f, root);
                    var ring = geometry.Circle("Selection", 2.2f, new Vector3(0, .12f, 0), selectedColor, .14f, root);
                    var player = root.gameObject.AddComponent<PitchPlayer>();
                    player.Initialize(state.Id, i + 1, home, user, ring.gameObject);
                    players.Add(player);
                }
            }
        }

        public bool SelectPlayer(string id)
        {
            var player = players.Find(p => p.PlayerId == id);
            if (player == null) return false;
            SelectedPlayer = Career.Players[id];
            foreach (var marker in players) marker.SetSelected(marker == player);
            return true;
        }

        public bool SelectAtScreenPoint(Vector2 screenPoint)
        {
            if (ViewCamera == null || !ViewCamera.pixelRect.Contains(screenPoint)) return false;
            if (!Physics.Raycast(ViewCamera.ScreenPointToRay(screenPoint), out var hit, 350)) return false;
            var player = hit.collider.GetComponent<PitchPlayer>();
            return player != null && players.Contains(player) && SelectPlayer(player.PlayerId);
        }

        public void TogglePause()
        {
            if (Match.State == MatchRunState.Paused) Match.Resume();
            else Match.Pause();
        }

        public void AdjustCamera(float panX, float panZ, float rotate, float zoomDelta)
        {
            focus.x = Mathf.Clamp(focus.x + panX, -55, 55);
            focus.z = Mathf.Clamp(focus.z + panZ, -38, 38);
            yaw = Mathf.Repeat(yaw + rotate, 360);
            zoom = Mathf.Clamp(zoom + zoomDelta, 25, 75);
            UpdateCamera();
        }

        public void ResetCamera()
        {
            focus = Vector3.zero;
            yaw = 0;
            zoom = 50;
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            if (ViewCamera == null) return;
            ViewCamera.rect = new Rect(0, 0, Mathf.Max(.45f, 1 - SidebarPixels / Mathf.Max(1, Screen.width)), 1);
            ViewCamera.orthographicSize = zoom * Mathf.Max(1, 1.2f / ViewCamera.aspect);
            var rotation = Quaternion.Euler(55, yaw, 0);
            ViewCamera.transform.SetPositionAndRotation(focus + rotation * new Vector3(0, 0, -130), rotation);
        }

        private void Update()
        {
            if (Career == null) return;
            UpdateCamera();
            if (Input.GetKeyDown(KeyCode.Space)) TogglePause();
            if (Input.GetKeyDown(KeyCode.Home)) ResetCamera();
            if (Input.GetKeyDown(KeyCode.Tab)) Browse(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1);
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            float x = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
            float z = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
            float rotation = (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0);
            var mouse = Input.mousePosition;
            bool overPitch = mouse.x < Screen.width - SidebarPixels && mouse.y > 65 * UiScale && mouse.y < Screen.height - 105 * UiScale;
            float scroll = overPitch ? Input.mouseScrollDelta.y : 0;
            if (overPitch && Input.GetMouseButton(1))
            {
                x -= Input.GetAxis("Mouse X") * 3;
                z -= Input.GetAxis("Mouse Y") * 3;
            }
            AdjustCamera(x * 35 * dt, z * 35 * dt, rotation * 50 * dt, -scroll * 3);
            if (overPitch && Input.GetMouseButtonDown(0)) SelectAtScreenPoint(mouse);
        }

        public void Close()
        {
            if (launchCamera != null) launchCamera.enabled = launchCameraEnabled;
            onClose?.Invoke();
            PitchGeometry.Release(gameObject);
        }

        private void OnDestroy()
        {
            if (launchCamera != null) launchCamera.enabled = launchCameraEnabled;
            geometry?.Dispose();
        }
    }
}
