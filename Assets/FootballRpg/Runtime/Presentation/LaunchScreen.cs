using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Simulation;
using UnityEngine;

namespace FootballRpg.Presentation
{
    /// <summary>Honest foundation screen; career entry arrives with the playable-week milestone.</summary>
    public sealed partial class LaunchScreen : MonoBehaviour
    {
        public CareerState Career { get; private set; }
        private PitchPreview preview;
        private GUIStyle title;
        private GUIStyle eyebrow;
        private GUIStyle body;
        private GUIStyle small;
        private GUIStyle button;

        private IEnumerator Start()
        {
            bool pitchSmoke = Array.IndexOf(Environment.GetCommandLineArgs(), "--pitch-smoke-test") >= 0;
            bool smokeTest = pitchSmoke || Array.IndexOf(Environment.GetCommandLineArgs(), "--smoke-test") >= 0;
            try
            {
                Career = PrototypeContent.CreateCareer();
                Debug.Log($"FOOTBALL_RPG_DATA_READY: {Career.Players.Count} players, {Career.Clubs.Count} clubs");
                if (smokeTest) VerifyMatchFoundation();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (smokeTest) Application.Quit(1);
                throw;
            }
            Debug.Log("FOOTBALL_RPG_LAUNCH_READY");
            if (!smokeTest)
            {
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "--pitch-preview") >= 0) OpenPitch();
                yield break;
            }

            // Exercise the real launch scene and lifecycle in a built player before exiting.
            yield return null;
            yield return null;
            if (Camera.main == null || !isActiveAndEnabled)
            {
                Debug.LogError("FOOTBALL_RPG_SMOKE_FAILED");
                Application.Quit(1);
                yield break;
            }
            if (pitchSmoke)
            {
                try { VerifyPitchPreview(); VerifyDecisionLoop(); VerifyFootballActions(); VerifyDefense(); VerifyGoalkeeper(); VerifyFixture(); VerifyOffside(); VerifyReferee(); VerifySetPieces(); VerifyParticipation(); VerifyViewing(); }
                catch (Exception error) { Debug.LogException(error); Application.Quit(1); throw; }
            }
            Debug.Log("FOOTBALL_RPG_SMOKE_PASSED");
            Application.Quit(0);
        }

        private void VerifyMatchFoundation()
        {
            var match = new MatchSession("startup-check", 42);
            match.Advance(TimeSpan.FromMilliseconds(100));
            match.Pause();
            match.Advance(TimeSpan.FromHours(1));
            var request = new ActionRequest("startup-choice", Career.UserPlayerId, "pass-to-feet",
                "harbour-player-07", .9f, "completed-pass", "interception",
                new Dictionary<string, string> { ["mode"] = "synthetic-startup-check" });
            var diagnostic = match.BeginResolution(request);
            match.BeginResolution(request);
            match.CompleteResolution(diagnostic.Result.EventId);
            if (match.Tick != 2 || match.DrawCount != 1 || match.Diagnostics.Count != 1)
                throw new InvalidOperationException("Match clock/action smoke check failed.");
            Debug.Log("FOOTBALL_RPG_ACTION " + diagnostic.ToJsonLine());
            Debug.Log("FOOTBALL_RPG_MATCH_FOUNDATION_PASSED");
        }

        private void VerifyPitchPreview()
        {
            OpenPitch(false);
            preview.GetComponent<MatchDriver>().enabled = false;
            Physics.SyncTransforms();
            if (preview.Players.Count != 22) throw new InvalidOperationException("Expected 22 player views.");
            foreach (var player in preview.Players)
            {
                var point = preview.ViewCamera.WorldToScreenPoint(player.transform.position + Vector3.up * 1.1f);
                if (!preview.SelectAtScreenPoint(point) || preview.SelectedPlayer.Id != player.PlayerId)
                    throw new InvalidOperationException("Pitch picking failed: " + player.PlayerId);
            }
            preview.AdjustCamera(4, 4, 15, -5);
            if (preview.Match.Tick != 0 || preview.Match.DrawCount != 0)
                throw new InvalidOperationException("Inspection changed the match.");
            preview.ResetCamera();
            preview.TogglePause();
            preview.Match.Advance(TimeSpan.FromMilliseconds(100));
            preview.TogglePause();
            preview.Match.Advance(TimeSpan.FromSeconds(5));
            if (preview.Match.Tick != 2) throw new InvalidOperationException("Pitch pause failed.");
            var initial = preview.World.Players[7].Position;
            preview.TogglePause();
            for (int i = 0; i < 300; i++) preview.Match.Advance(TimeSpan.FromMilliseconds(100));
            preview.TogglePause();
            preview.SyncWorldView();
            if (preview.World.Players[7].Position.Distance(initial) < .1 || preview.World.CompletedPasses == 0)
                throw new InvalidOperationException("Open play did not advance.");
            Debug.Log("FOOTBALL_RPG_OPEN_PLAY_PASSED");
            Debug.Log("FOOTBALL_RPG_PITCH_PREVIEW_PASSED");
        }

        private void VerifyDecisionLoop()
        {
            var decisionRoot = new GameObject("Decision Smoke");
            var view = decisionRoot.AddComponent<PitchPreview>();
            view.Initialize(Career);
            view.GetComponent<MatchDriver>().enabled = false;
            var choice = view.ChosenPass;
            if (choice == null || view.World.PendingDecision.Options.Count != 20)
                throw new InvalidOperationException("Missing player decision options.");
            view.SelectPlayer(choice.Receiver.Id);
            view.ChoosePassKind(PassKind.ThroughBall);
            choice = view.ChosenPass;
            if (!view.CommitChosenPass() || view.CommitChosenPass() || view.Match.DrawCount != 1 ||
                view.World.LastPass.Action.SuccessProbability != choice.Chance)
                throw new InvalidOperationException("Decision was not committed exactly once.");
            view.Match.Advance(TimeSpan.FromMilliseconds(100));
            view.TogglePause();
            var ball = view.World.BallPosition;
            view.Match.Advance(TimeSpan.FromSeconds(10));
            if (view.World.BallPosition.Distance(ball) > .001)
                throw new InvalidOperationException("Manual pause failed during pass flight.");
            view.TogglePause();
            for (int i = 0; i < 500 && view.World.BallState == BallPhase.Travelling; i++)
                view.Match.Advance(TimeSpan.FromMilliseconds(50));
            if (view.World.BallState == BallPhase.Travelling || view.Match.DrawCount != 1)
                throw new InvalidOperationException("Committed pass flight stalled or rerolled.");
            view.Close();
            Debug.Log("FOOTBALL_RPG_DECISION_LOOP_PASSED");
        }

        private void VerifyFootballActions()
        {
            foreach (bool incoming in new[] { false, true })
            {
                var root = new GameObject("Football Action Smoke");
                var view = root.AddComponent<PitchPreview>();
                view.Initialize(Career, openingArrivalHeight: incoming ? (double?)1.8 : null);
                view.GetComponent<MatchDriver>().enabled = false;
                view.ChooseFootballAction(incoming ? FootballActionKind.HeaderPass : FootballActionKind.Dribble);
                var choice = view.ChosenFootballAction;
                if (choice == null || !view.CommitChosenPass() || view.CommitChosenPass())
                    throw new InvalidOperationException("Football action selection/commit failed.");
                for (int i = 0; i < 500 && view.World.BallState == BallPhase.Travelling; i++)
                    view.Match.Advance(TimeSpan.FromMilliseconds(50));
                if (view.World.BallState == BallPhase.Travelling || view.Match.DrawCount != 1 ||
                    view.World.LastFootballAction.Action.SuccessProbability != choice.Chance)
                    throw new InvalidOperationException("Football action flight/chance failed.");
                view.Close();
            }
            Debug.Log("FOOTBALL_RPG_FOOTBALL_ACTIONS_PASSED");
        }

        private void VerifyDefense()
        {
            var root = new GameObject("Defense Smoke");
            var view = root.AddComponent<PitchPreview>(); view.Initialize(Career);
            view.GetComponent<MatchDriver>().enabled = false;
            view.CommitChosenPass();
            if (!view.ChooseIntent(OffBallIntent.HoldZone)) throw new InvalidOperationException("Off-ball instruction failed.");
            view.ChooseIntent(OffBallIntent.Automatic);
            for (int i = 0; i < 24000 && view.World.PendingDefense == null; i++)
            {
                if (view.World.PendingDecision != null) view.CommitChosenPass();
                view.Match.Advance(TimeSpan.FromMilliseconds(50));
            }
            if (view.World.PendingDefense == null) throw new InvalidOperationException("No defensive decision reached.");
            view.ChooseDefense(DefensiveAction.Contain);
            var chosen = view.ChosenDefense; long draws = view.Match.DrawCount;
            view.TogglePause(); view.Match.Advance(TimeSpan.FromSeconds(2));
            if (view.World.PendingDefense == null || !view.CommitChosenPass() || view.Match.DrawCount != draws + 1 ||
                view.World.LastDefense.Action.SuccessProbability != chosen.Chance)
                throw new InvalidOperationException("Defensive pause/resolution failed.");
            view.Close(); Debug.Log("FOOTBALL_RPG_DEFENSE_PASSED");
        }

        private void VerifyGoalkeeper()
        {
            var definitions = PrototypeContent.Load(); definitions.userPlayerId = "harbour-player-01";
            var root = new GameObject("Goalkeeper Smoke"); var view = root.AddComponent<PitchPreview>();
            view.Initialize(CareerState.Create(definitions)); view.GetComponent<MatchDriver>().enabled = false;
            if (view.World.PendingKeeper == null) throw new InvalidOperationException("Missing goalkeeper distribution prompt.");
            view.ChooseKeeperAction(KeeperAction.ShortKick); var choice = view.ChosenKeeperAction;
            if (!view.CommitChosenPass() || view.World.LastKeeperAction.Action.SuccessProbability != choice.Chance)
                throw new InvalidOperationException("Goalkeeper distribution failed.");
            bool defended = false;
            for (int i = 0; i < 40000 && !defended; i++)
            {
                var d = view.World.PendingKeeper;
                if (d != null)
                {
                    if (d.Context.Situation != KeeperSituation.Distribution)
                    {
                        choice = view.ChosenKeeperAction; long draws = view.Match.DrawCount;
                        view.TogglePause(); view.Match.Advance(TimeSpan.FromSeconds(10));
                        if (!view.CommitChosenPass() || view.Match.DrawCount != draws + 1 || view.World.LastKeeperAction.Action.SuccessProbability != choice.Chance)
                            throw new InvalidOperationException("Goalkeeper defense failed.");
                        defended = true;
                    }
                    else { view.ChooseKeeperAction(KeeperAction.ShortKick); view.CommitChosenPass(); }
                }
                else if (view.World.HasPendingDecision) view.CommitChosenPass();
                view.Match.Advance(TimeSpan.FromMilliseconds(50));
            }
            if (!defended) throw new InvalidOperationException("No relevant goalkeeper threat reached.");
            view.Close(); Debug.Log("FOOTBALL_RPG_GOALKEEPER_PASSED");
        }

        private void VerifyFixture()
        {
            var root = new GameObject("Fixture Smoke");
            var view = root.AddComponent<PitchPreview>(); view.Initialize(Career, interactive: false);
            view.GetComponent<MatchDriver>().enabled = false;
            view.World.StartFixture(new CompetitionRules(2, .5, .1));
            for (int i = 0; i < 400 && view.World.FixturePhase != FixturePhase.FullTime; i++)
            {
                if (view.World.FixturePhase == FixturePhase.HalfTime) view.CommitChosenPass();
                view.Match.Advance(TimeSpan.FromMilliseconds(50)); view.SyncWorldView();
            }
            if (view.World.FixturePhase != FixturePhase.FullTime || view.World.FixtureEvents.Count(e => e.Kind == "RestartReleased") < 2)
                throw new InvalidOperationException("Fixture did not complete both halves and kickoffs.");
            view.Close(); Debug.Log("FOOTBALL_RPG_FIXTURE_PASSED");
        }

        private void VerifyOffside()
        {
            var root = new GameObject("Offside Smoke");
            var definitions = PrototypeContent.Load(); definitions.userPlayerId = "harbour-player-11";
            var view = root.AddComponent<PitchPreview>(); view.Initialize(CareerState.Create(definitions));
            view.GetComponent<MatchDriver>().enabled = false; view.World.StartFixture(new CompetitionRules(600));
            for (int i = 0; i < 24000 && view.World.LastOffside == null; i++)
            {
                if (view.World.RefereeNoticePending || view.World.FixturePhase == FixturePhase.HalfTime || view.World.HasPendingDecision || view.UserRestart) view.CommitChosenPass();
                var intent = view.World.CanSetIntent(OffBallIntent.ForwardRun) ? OffBallIntent.ForwardRun : OffBallIntent.Counterattack;
                if (view.World.CanSetIntent(intent) && view.World.ActiveIntent?.Kind != intent) view.ChooseIntent(intent);
                view.Match.Advance(TimeSpan.FromMilliseconds(50));
            }
            view.SyncWorldView();
            if (view.World.LastOffside == null || view.World.Restart?.Kind != RestartKind.IndirectFreeKick || !view.World.OffsideNoticePending)
                throw new InvalidOperationException("No offside restart reached in the seeded forward-run fixture.");
            if (string.IsNullOrWhiteSpace(view.World.LastOffside.Explanation)) throw new InvalidOperationException("Missing offside explanation.");
            view.CommitChosenPass(); if (view.World.OffsideNoticePending) throw new InvalidOperationException("Notice did not acknowledge.");
            view.Close(); Debug.Log("FOOTBALL_RPG_OFFSIDE_PASSED");
        }

        private void VerifyReferee()
        {
            var root = new GameObject("Referee Smoke");
            var view = root.AddComponent<PitchPreview>(); view.Initialize(Career, interactive: false);
            view.GetComponent<MatchDriver>().enabled = false; view.World.StartFixture(new CompetitionRules(180));
            for (int i = 0; i < 20000 && view.World.FixturePhase != FixturePhase.FullTime; i++)
            {
                if (view.World.FixturePhase == FixturePhase.HalfTime) view.World.ContinueSecondHalf();
                view.Match.Advance(TimeSpan.FromMilliseconds(50));
            }
            view.SyncWorldView();
            if (view.World.LastFoul == null || view.World.FixturePhase != FixturePhase.FullTime ||
                !view.World.FixtureEvents.Any(e => e.Kind == "AdvantageRecalled" || e.Restart == RestartKind.Penalty || e.Restart == RestartKind.DirectFreeKick))
                throw new InvalidOperationException("Seeded fixture did not exercise fouls and referee restarts through full time.");
            view.Close(); Debug.Log("FOOTBALL_RPG_REFEREE_PASSED");
        }

        private void VerifySetPieces()
        {
            foreach (var user in new[] { "harbour-player-01", "harbour-player-04", "harbour-player-11" })
            {
                var root = new GameObject("Set Piece Smoke");
                var definitions = PrototypeContent.Load(); definitions.userPlayerId = user;
                var view = root.AddComponent<PitchPreview>(); view.Initialize(CareerState.Create(definitions));
                view.GetComponent<MatchDriver>().enabled = false; view.World.StartFixture(new CompetitionRules(180));
                string chosenRestart = null;
                for (int i = 0; i < 16000; i++)
                {
                    if (view.World.RefereeNoticePending || view.World.OffsideNoticePending || view.World.FixturePhase == FixturePhase.HalfTime) view.CommitChosenPass();
                    else if (view.World.PendingSetPiece != null)
                    {
                        chosenRestart = view.World.Restart.Id;
                        view.SelectSetPieceDuty(SetPieceDuty.Counterattack); view.CommitChosenPass();
                    }
                    else if (view.World.HasPendingDecision || view.UserRestart) view.CommitChosenPass();
                    view.Match.Advance(TimeSpan.FromMilliseconds(50));
                    if (chosenRestart != null && view.World.SetPieceRecords.Any(r => r.RestartId == chosenRestart && r.Kind == "TakingResult")) break;
                }
                view.SyncWorldView();
                if (chosenRestart == null || !view.World.SetPieceRecords.Any(r => r.RestartId == chosenRestart && r.Kind == "DutyChosen") ||
                    !view.World.SetPieceRecords.Any(r => r.RestartId == chosenRestart && r.Kind == "TakingResult"))
                    throw new InvalidOperationException("Set-piece role and delivery were not reached for " + user);
                view.Close();
            }
            Debug.Log("FOOTBALL_RPG_SET_PIECES_PASSED");
        }

        private void VerifyParticipation()
        {
            var definitions = PrototypeContent.Load(); definitions.userPlayerId = "harbour-player-17";
            var root = new GameObject("Participation Smoke"); var view = root.AddComponent<PitchPreview>();
            view.Initialize(CareerState.Create(definitions)); view.GetComponent<MatchDriver>().enabled = false;
            view.World.StartFixture(new CompetitionRules(8, 0, .1), new MatchFitnessRules(injuryRatePerSecond: 0));
            if (view.World.UserParticipation != ParticipationState.Bench || view.Players.Any(p => p.IsUser))
                throw new InvalidOperationException("Bench player appeared on pitch before entering.");
            bool entered = false, removed = false;
            for (int i = 0; i < 16000 && view.World.FixturePhase != FixturePhase.FullTime; i++)
            {
                if (view.World.FixturePhase == FixturePhase.HalfTime && !entered)
                {
                    entered = view.World.UserParticipation == ParticipationState.Playing;
                    view.SyncWorldView();
                    if (!entered || !view.Players.Any(p => p.IsUser)) throw new InvalidOperationException("Coach did not bring bench player on at halftime.");
                }
                if (entered && !removed && view.World.FixturePhase == FixturePhase.SecondHalf && view.World.Restart != null)
                {
                    view.World.RequestSubstitution(definitions.userPlayerId, "harbour-player-18"); removed = true; view.SyncWorldView();
                    if (view.World.UserParticipation != ParticipationState.Replaced || view.Players.Any(p => p.IsUser))
                        throw new InvalidOperationException("Removed user still participates.");
                }
                if (view.World.RefereeNoticePending || view.World.OffsideNoticePending || view.World.HasPendingDecision || view.UserRestart || view.World.FixturePhase == FixturePhase.HalfTime)
                    view.CommitChosenPass();
                view.Match.Advance(TimeSpan.FromMilliseconds(50));
            }
            if (!entered || !removed || view.World.FixturePhase != FixturePhase.FullTime) throw new InvalidOperationException("Spectator fixture failed to finish.");
            view.Close(); Debug.Log("FOOTBALL_RPG_PARTICIPATION_PASSED");
        }

        private void VerifyViewing()
        {
            foreach (string user in new[] { "harbour-player-01", "harbour-player-04", "harbour-player-08", "harbour-player-11", "harbour-player-17" })
            {
                string[] expected = null;
                foreach (var mode in new[] { MatchViewingMode.FullMatch, MatchViewingMode.Highlights })
                {
                    var definitions = PrototypeContent.Load(); definitions.userPlayerId = user;
                    var root = new GameObject("Viewing Smoke"); var view = root.AddComponent<PitchPreview>();
                    view.Initialize(CareerState.Create(definitions)); view.GetComponent<MatchDriver>().enabled = false;
                    view.World.StartFixture(new CompetitionRules(12, 0, .1)); view.Playback.SetMode(mode); view.Playback.SetFullMatchSpeed(4);
                    bool fast = false, focused = false;
                    for (int frame = 0; frame < 20000 && view.World.FixturePhase != FixturePhase.FullTime; frame++)
                    {
                        if (view.World.RefereeNoticePending || view.World.OffsideNoticePending || view.World.HasPendingDecision || view.UserRestart || view.World.FixturePhase == FixturePhase.HalfTime)
                            view.CommitChosenPass();
                        fast |= view.Playback.EffectiveSpeed > 1; focused |= view.Playback.Focus != HighlightFocus.Quiet;
                        view.Playback.Advance(TimeSpan.FromMilliseconds(17));
                    }
                    view.SyncWorldView();
                    if (view.World.FixturePhase != FixturePhase.FullTime || !fast || !focused) throw new InvalidOperationException("Viewing mode failed for " + user);
                    var facts = view.Match.Diagnostics.Select(d => d.ToJsonLine()).Concat(view.World.FixtureEvents.Select(e => e.Id + e.Kind + e.Tick)).Concat(new[] {
                        view.Match.Tick + ":" + view.Match.RandomState + ":" + view.World.HomeGoals + ":" + view.World.AwayGoals,
                        string.Join("|", view.Career.Players.Values.Select(p => p.Id + ":" + p.Fatigue.ToString("R") + ":" + p.InjuryDaysRemaining)) }).ToArray();
                    if (expected == null) expected = facts;
                    else if (!expected.SequenceEqual(facts)) throw new InvalidOperationException("Viewing changed the seeded match for " + user);
                    view.Close();
                }
            }
            Debug.Log("FOOTBALL_RPG_VIEWING_PASSED");
        }

        private bool shortFixture;

        public void OpenPitch(bool interactive = true, bool goalkeeper = false, bool bench = false)
        {
            if (preview != null || Career == null) return;
            preview = new GameObject("Pitch Preview").AddComponent<PitchPreview>();
            var selectedCareer = Career;
            if (goalkeeper || bench)
            {
                var definitions = PrototypeContent.Load(); definitions.userPlayerId = bench ? "harbour-player-17" : "harbour-player-01";
                selectedCareer = CareerState.Create(definitions);
            }
            preview.Initialize(selectedCareer, () => preview = null, interactive);
            if (interactive) preview.World.StartFixture(new CompetitionRules(shortFixture ? 120 : 2700));
        }

        private void Update()
        {
            if (creationDraft != null) { UpdateCreation(); return; }
            if (preview == null && Input.GetKeyDown(KeyCode.C)) BeginCreation();
            if (preview == null && Input.GetKeyDown(KeyCode.Return)) OpenPitch();
            if (preview == null && Input.GetKeyDown(KeyCode.L)) shortFixture = !shortFixture;
            if (preview == null && Input.GetKeyDown(KeyCode.B)) OpenPitch(bench: true);
            if (preview == null && Input.GetKeyDown(KeyCode.G)) OpenPitch(goalkeeper: true);
        }

        private void OnGUI()
        {
            if (preview != null) return;
            EnsureStyles();
            var originalMatrix = GUI.matrix;
            var originalColor = GUI.color;
            float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.TRS(
                new Vector3((Screen.width - 1440 * scale) / 2, (Screen.height - 900 * scale) / 2, 0),
                Quaternion.identity, new Vector3(scale, scale, 1));

            Fill(new Rect(0, 0, 1440, 900), new Color(0.025f, 0.06f, 0.07f, 0.96f));
            if (creationDraft != null) { DrawCreation(); GUI.matrix = originalMatrix; GUI.color = originalColor; return; }
            Fill(new Rect(68, 78, 42, 4), new Color(0.66f, 0.91f, 0.43f));
            GUI.Label(new Rect(68, 108, 620, 30), "ONE PLAYER. YOUR CAREER.", eyebrow);
            GUI.Label(new Rect(64, 172, 850, 180), "FOOTBALL\nCAREER RPG", title);
            GUI.Label(new Rect(70, 398, 580, 110),
                "Read the game. Choose your moment.\nEarn your place, on and off the pitch.", body);
            GUI.Label(new Rect(70, 543, 550, 96),
                Career.UserPlayer.Name + " · " + Career.UserPlayer.PrimaryPositionId.ToUpperInvariant() + "\nCreate your prospect, then play the fixture.", small);

            if (GUI.Button(new Rect(70, 683, 260, 58), "PLAY MATCH  [ENTER]", button))
                OpenPitch();
            if (GUI.Button(new Rect(346, 683, 125, 58), "QUIT", button))
                Application.Quit();

            if (GUI.Button(new Rect(70, 754, 330, 45), "PLAY AS GOALKEEPER  [G]", button)) OpenPitch(goalkeeper: true);

            if (GUI.Button(new Rect(420, 754, 380, 45), "START ON BENCH  [B]", button)) OpenPitch(bench: true);

            if (GUI.Button(new Rect(490, 683, 310, 58), shortFixture ? "4-MINUTE MATCH  [L]" : "90-MINUTE MATCH  [L]", button)) shortFixture = !shortFixture;
            if (GUI.Button(new Rect(872, 730, 460, 58), "CREATE FOOTBALLER  [C]", button)) BeginCreation();
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
