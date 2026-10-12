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
        private Transform ballView, tacticalMarker, passTargetMarker;
        private LineRenderer passPath;
        private string selectedDecisionId, selectedReceiverId;
        private PassKind selectedPassKind;
        private string selectedFootballOptionId;
        public FootballActionPreview ChosenFootballAction => World?.PendingDecision?.Actions.FirstOrDefault(p => p.OptionId == selectedFootballOptionId);
        private int inputReadyFrame;
        private DefensiveAction selectedDefense = DefensiveAction.Contain;
        public DefensePreview ChosenDefense => World?.PendingDefense?.Options.FirstOrDefault(p => p.Kind == selectedDefense) ?? World?.PendingDefense?.Options.FirstOrDefault();
        public void ChooseDefense(DefensiveAction kind) { if (World.PendingDefense?.Options.Any(p => p.Kind == kind) == true) selectedDefense = kind; }
        public bool ChooseIntent(OffBallIntent kind)
        {
            string target = SelectedPlayer?.Id;
            if (!World.CanSetIntent(kind, target)) return false;
            World.SetIntent(kind, target); return true;
        }
        public PassPreview ChosenPass => World?.PendingDecision?.Options.FirstOrDefault(p => p.Receiver.Id == selectedReceiverId && p.Kind == selectedPassKind);
        public OpenPlayWorld World { get; private set; }
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

        public void Initialize(CareerState career, Action close = null, bool interactive = true, double? openingArrivalHeight = null)
        {
            if (Career != null) throw new InvalidOperationException("Preview already initialized.");
            Career = career ?? throw new ArgumentNullException(nameof(career));
            onClose = close;
            inputReadyFrame = Time.frameCount + 1;
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
            World = new OpenPlayWorld(career, Match, interactive ? career.UserPlayerId : null, interactive, openingArrivalHeight);
            BuildPlayers();
            ballView = geometry.Root.Find("Ball");
            tacticalMarker = geometry.Circle("Expected Tactical Position", 3,
                new Vector3(0, .1f, 0), geometry.Material(new Color(.72f, .96f, .35f), true), .12f).transform;
            var passColor = geometry.Material(new Color(.25f, .85f, 1), true);
            passTargetMarker = geometry.Circle("Chosen Pass Target", 1.4f, new Vector3(0, .14f, 0), passColor, .2f).transform;
            passPath = geometry.Line("Chosen Pass Path", new Vector3[2], passColor, .10f);
            SyncWorldView();
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
            foreach (bool home in new[] { true, false })
            {
                string clubId = home ? Career.Fixture.HomeClubId : Career.Fixture.AwayClubId;
                var squad = Career.Clubs[clubId].SquadPlayerIds.Take(11).ToArray();
                for (int i = 0; i < squad.Length; i++)
                {
                    var state = Career.Players[squad[i]];
                    var root = new GameObject(state.Name).transform;
                    root.SetParent(transform, false);
                    var motion = World.Players.First(p => p.Player.Id == state.Id);
                    root.localPosition = ToVector(motion.Position);
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

        private static Vector3 ToVector(PitchPoint point) => new Vector3((float)point.X, 0, (float)point.Z);

        public void SyncWorldView()
        {
            EnsureDecisionSelection();
            foreach (var marker in players)
            {
                var motion = World.Players.FirstOrDefault(p => p.Player.Id == marker.PlayerId);
                marker.gameObject.SetActive(motion != null);
                if (motion != null) marker.transform.localPosition = ToVector(motion.Position);
            }
            ballView.localPosition = ToVector(World.BallPosition) + Vector3.up * (float)World.BallHeight;
            var userMotion = World.Players.FirstOrDefault(p => p.Player.Id == Career.UserPlayerId);
            tacticalMarker.gameObject.SetActive(userMotion != null);
            if (userMotion != null) tacticalMarker.localPosition = ToVector(userMotion.Target);
            var target = ChosenRestart?.Target ?? ChosenKeeperAction?.Target ?? ChosenDefense?.Target ?? ChosenFootballAction?.Target ?? ChosenPass?.Target;
            passTargetMarker.gameObject.SetActive(target.HasValue);
            passPath.gameObject.SetActive(target.HasValue);
            if (target.HasValue)
            {
                passTargetMarker.localPosition = ToVector(target.Value);
                passPath.SetPosition(0, ToVector(World.BallPosition) + Vector3.up * .14f);
                passPath.SetPosition(1, ToVector(target.Value) + Vector3.up * .14f);
            }
        }

        private void LateUpdate() { if (World != null) SyncWorldView(); }

        public bool SelectPlayer(string id)
        {
            var player = players.Find(p => p.PlayerId == id);
            if (player == null) return false;
            SelectedPlayer = Career.Players[id];
            if (UserRestart && World.RestartRecipients().Any(p => p.Receiver.Id == id)) restartTargetId = id;
            var keeperAction = ChosenKeeperAction;
            if (keeperAction?.Receiver != null)
            {
                var option = World.PendingKeeper.Options.FirstOrDefault(p => p.Kind == keeperAction.Kind && p.Receiver?.Id == id);
                if (option != null) selectedKeeperOptionId = option.OptionId;
            }
            EnsureDecisionSelection();
            if (World.PendingDecision?.Options.Any(p => p.Receiver.Id == id) == true || World.PendingDecision?.Actions.Any(p => p.Receiver?.Id == id) == true)
            {
                var action = ChosenFootballAction;
                selectedReceiverId = id;
                if (action?.Receiver != null)
                    selectedFootballOptionId = World.PendingDecision.Actions.FirstOrDefault(p => p.Kind == action.Kind && p.Variant == action.Variant && p.Receiver?.Id == id)?.OptionId;
            }
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
            World.TogglePause();
        }

        private void EnsureDecisionSelection()
        {
            var decision = World.PendingDecision;
            if (decision == null) { selectedDecisionId = null; return; }
            if (selectedDecisionId == decision.Id) return;
            selectedDecisionId = decision.Id;
            selectedReceiverId = decision.Options.OrderByDescending(p => p.Chance).FirstOrDefault()?.Receiver.Id
                ?? decision.Actions.FirstOrDefault(p => p.Receiver != null)?.Receiver.Id;
            selectedPassKind = PassKind.ToFeet;
            selectedFootballOptionId = decision.Options.Count == 0 ? decision.Actions.FirstOrDefault()?.OptionId : null;
        }

        public void ChoosePassKind(PassKind kind)
        {
            EnsureDecisionSelection();
            if (World.PendingDecision?.Options.Any(p => p.Receiver.Id == selectedReceiverId && p.Kind == kind) == true)
            { selectedPassKind = kind; selectedFootballOptionId = null; }
        }

        public void CyclePassTarget()
        {
            if (UserRestart) { CycleRestartTarget(); return; }
            EnsureDecisionSelection();
            if (World.PendingDecision == null) return;
            var targets = World.PendingDecision.Options.Select(p => p.Receiver.Id)
                .Concat(World.PendingDecision.Actions.Where(p => p.Receiver != null).Select(p => p.Receiver.Id)).Distinct().ToArray();
            if (targets.Length == 0) return;
            SelectPlayer(targets[(Array.IndexOf(targets, selectedReceiverId) + 1) % targets.Length]);
        }

        public void ChooseFootballAction(FootballActionKind kind)
        {
            EnsureDecisionSelection();
            if (World.PendingDecision == null) return;
            var choices = World.PendingDecision.Actions.Where(p => p.Kind == kind && (p.Receiver == null || p.Receiver.Id == selectedReceiverId)).ToArray();
            if (choices.Length == 0) choices = World.PendingDecision.Actions.Where(p => p.Kind == kind).ToArray();
            if (choices.Length == 0) return;
            int current = Array.FindIndex(choices, p => p.OptionId == selectedFootballOptionId);
            var chosen = choices[(current + 1) % choices.Length];
            selectedFootballOptionId = chosen.OptionId;
            if (chosen.Receiver != null) selectedReceiverId = chosen.Receiver.Id;
        }

        public FootballActionKind[] ContextualActionKinds => World.BallState == BallPhase.Incoming
            ? new[] { FootballActionKind.Control, FootballActionKind.FirstTimePass, FootballActionKind.HeaderPass, FootballActionKind.HeaderShot, FootballActionKind.Clearance }
            : new[] { FootballActionKind.Dribble, FootballActionKind.Shot, FootballActionKind.Cross, FootballActionKind.Clearance };

        public bool CommitChosenPass()
        {
            if (World.RefereeNoticePending) { World.AcknowledgeReferee(); return true; }
            if (World.OffsideNoticePending) { World.AcknowledgeOffside(); return true; }
            if (World.FixturePhase == FixturePhase.HalfTime) { World.ContinueSecondHalf(); return true; }
            if (World.PendingSetPiece != null || UserRestart) return CommitSetPieceChoice();
            EnsureDecisionSelection();
            if (ChosenKeeperAction != null)
            { World.CommitKeeperDecision(World.PendingKeeper.Id, ChosenKeeperAction.OptionId); return true; }
            if (ChosenDefense != null)
            { World.CommitDefense(World.PendingDefense.Id, ChosenDefense.Kind); return true; }
            if (ChosenFootballAction != null)
            {
                World.CommitFootballDecision(selectedDecisionId, ChosenFootballAction.OptionId);
                selectedDecisionId = null; selectedFootballOptionId = null;
                return true;
            }
            var chosen = ChosenPass;
            if (chosen == null) return false;
            World.CommitDecision(selectedDecisionId, chosen.Receiver.Id, chosen.Kind);
            selectedDecisionId = null;
            return true;
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
            if (Career == null || Time.frameCount <= inputReadyFrame) return;
            EnsureDecisionSelection();
            SetPieceKeys();
            if (Input.GetKeyDown(KeyCode.F)) ToggleKeeperFeet();
            if (World.PendingKeeper != null && !showKeeperFeet)
            {
                var keeperKinds = World.PendingKeeper.Options.Select(p => p.Kind).Distinct().ToArray();
                for (int i = 0; i < keeperKinds.Length; i++)
                    if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) ChooseKeeperAction(keeperKinds[i]);
            }
            if (World.PendingDefense != null)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) ChooseDefense(DefensiveAction.Tackle);
                if (Input.GetKeyDown(KeyCode.Alpha2)) ChooseDefense(DefensiveAction.Contain);
                if (Input.GetKeyDown(KeyCode.Alpha3)) ChooseDefense(DefensiveAction.Block);
            }
            if (Input.GetKeyDown(KeyCode.I)) showIntentMenu = !showIntentMenu;
            if (World.PendingKeeper == null || showKeeperFeet)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) ChoosePassKind(PassKind.ToFeet);
                if (Input.GetKeyDown(KeyCode.Alpha2)) ChoosePassKind(PassKind.ThroughBall);
                var kinds = ContextualActionKinds;
                for (int i = 0; i < kinds.Length; i++) if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha3 + i))) ChooseFootballAction(kinds[i]);
            }
            if (Input.GetKeyDown(KeyCode.R)) { if (World.PendingSetPiece != null) CycleSetPieceMark(); else if (World.PendingKeeper != null && !showKeeperFeet) CycleKeeperTarget(); else CyclePassTarget(); }
            if (Input.GetKeyDown(KeyCode.Return)) CommitChosenPass();
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
            bool overPitch = mouse.x < Screen.width - SidebarPixels && mouse.y > (showIntentMenu && World.PendingDecision == null ? 300 : 190) * UiScale && mouse.y < Screen.height - (World.OffsideNoticePending || World.RefereeNoticePending || World.PendingSetPiece != null || UserRestart ? 300 : World.HasPendingDecision || UserRestart ? 250 : 105) * UiScale;
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
            World?.Dispose();
            geometry?.Dispose();
        }
    }
}
