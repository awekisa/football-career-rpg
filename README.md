# Football Career RPG

A single-player Unity football career RPG: one footballer, meaningful match decisions, training and relationships.

**Current milestone:** first playable pass loop (SME-113). Your player starts on the ball. Choose a teammate, compare pass-to-feet and through-ball chances, and commit a pass. Play pauses again when you gain possession. All 22 players move automatically between decisions; full match rules and career gameplay come later.

- [Prototype specification](docs/prototype-spec.md)
- [Architecture](docs/architecture.md)
- [Data schema and ownership](docs/game-data.md)
- [Match timing and replay contract](docs/match-simulation.md)
- [Pitch preview and controls](docs/pitch-preview.md)
- [Open-play simulation](docs/open-play.md)
- [Pass probabilities and outcomes](docs/pass-resolution.md)
- [Player decisions](docs/player-decisions.md)
- [Linear project](https://linear.app/smeagol-hermes/project/football-career-rpg-54f8537541e7)
- [First playable checkpoint: SME-113](https://linear.app/smeagol-hermes/issue/SME-113)

## Open and run

1. Install Unity **6000.3.23f1** through Unity Hub and activate a suitable Unity license.
2. Clone this repository and add its root directory as a project in Unity Hub. Initial import resolves pinned packages and creates local caches.
3. Open `Assets/FootballRpg/Scenes/Launch.unity` and press Play.
4. Choose **Explore the pitch** or press Enter. Click a teammate or press R to select your pass target; choose **To feet** / **Through ball** (1 / 2), then **Play pass** (Enter). Tab inspects any player. Use WASD to pan, Q/E to rotate, the mouse wheel to zoom, Home to reset, Space to pause/resume between decisions, and Escape to return.

The project uses the built-in renderer and placeholder UI, with no paid assets or external services. Keep the editor version and `Packages/packages-lock.json` committed for consistent imports.

## Verify and build

On macOS, close this project's editor before running these commands from the repository root:

```sh
./scripts/unity.sh import
./scripts/unity.sh test
./scripts/unity.sh build-mac
```

Set `UNITY_EDITOR` to override the executable location. The helper is Bash-compatible (macOS/Linux/Git Bash); native Windows users can invoke Unity directly as below. Only macOS build support was installed on the initial development machine. Install **Windows Build Support (Mono)** via Unity Hub before using `build-windows`. Windows output is supported by the build entry point but must be verified on Windows before claiming it works there.

Output locations:

- macOS: `Builds/macOS/Football Career RPG.app`
- Windows: `Builds/Windows/Football Career RPG.exe`
- Test results: `TestResults/editmode.xml`
- Editor/build logs: `Logs/`

Run the generated macOS app by opening it, or perform a lifecycle smoke check:

```sh
'Builds/macOS/Football Career RPG.app/Contents/MacOS/Football Career RPG' \
  -batchmode -nographics --smoke-test -logFile /tmp/football-rpg-smoke.log
```

Expect exit code 0 and `FOOTBALL_RPG_SMOKE_PASSED` in the log. Open the app normally as well to inspect the UI; headless checks do not verify appearance.

Use `--pitch-smoke-test` instead of `--smoke-test` to also verify the packaged pitch, all 22 player selection rays, camera isolation, pause/resume, 30 seconds of AI movement/passing, and committing a player decision through the presentation controls. Expect `FOOTBALL_RPG_PITCH_PREVIEW_PASSED`. Use `--pitch-preview` to open the interactive pitch directly.

Equivalent direct editor commands (substitute executable and project paths):

```sh
Unity -batchmode -nographics -projectPath /path/to/project -runTests -testPlatform EditMode -testResults /path/to/results.xml -logFile /path/to/tests.log
Unity -batchmode -nographics -quit -projectPath /path/to/project -buildTarget StandaloneWindows64 -executeMethod FootballRpg.Editor.PrototypeBuild.BuildWindows -logFile /path/to/build.log
```

Do not add `-quit` to tests: the runner exits on completion. Build menu equivalents are under **Football RPG** in the editor.

## Contribution rules

Work on an issue branch and submit a PR. Commit Assets with their `.meta` files, Packages and ProjectSettings. Never commit generated caches, test output or builds. Preserve the boundary between deterministic rules and presentation. Each issue's verification criteria are part of its deliverable.
