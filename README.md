# Football Career RPG

A single-player Unity football career RPG: one footballer, meaningful match decisions, training and relationships.

**Current milestone:** overhead pitch and player inspection (SME-110). Explore a scaled 3D pitch with 22 player placeholders, inspect either team, and control the camera while the match clock is running or paused. Players are stationary; football movement and career gameplay come next.

- [Prototype specification](docs/prototype-spec.md)
- [Architecture](docs/architecture.md)
- [Data schema and ownership](docs/game-data.md)
- [Match timing and replay contract](docs/match-simulation.md)
- [Pitch preview and controls](docs/pitch-preview.md)
- [Linear project](https://linear.app/smeagol-hermes/project/football-career-rpg-54f8537541e7)
- [First playable checkpoint: SME-113](https://linear.app/smeagol-hermes/issue/SME-113)

## Open and run

1. Install Unity **6000.3.23f1** through Unity Hub and activate a suitable Unity license.
2. Clone this repository and add its root directory as a project in Unity Hub. Initial import resolves pinned packages and creates local caches.
3. Open `Assets/FootballRpg/Scenes/Launch.unity` and press Play.
4. Choose **Explore the pitch** or press Enter. Click a player to inspect them; Tab cycles players. Use WASD to pan, Q/E to rotate, the mouse wheel to zoom, Home to reset, Space to pause/resume, and Escape to return.

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

Use `--pitch-smoke-test` instead of `--smoke-test` to also verify the packaged pitch, all 22 player selection rays, camera isolation, and pause/resume. Expect `FOOTBALL_RPG_PITCH_PREVIEW_PASSED`. Use `--pitch-preview` to open the interactive pitch directly.

Equivalent direct editor commands (substitute executable and project paths):

```sh
Unity -batchmode -nographics -projectPath /path/to/project -runTests -testPlatform EditMode -testResults /path/to/results.xml -logFile /path/to/tests.log
Unity -batchmode -nographics -quit -projectPath /path/to/project -buildTarget StandaloneWindows64 -executeMethod FootballRpg.Editor.PrototypeBuild.BuildWindows -logFile /path/to/build.log
```

Do not add `-quit` to tests: the runner exits on completion. Build menu equivalents are under **Football RPG** in the editor.

## Contribution rules

Work on an issue branch and submit a PR. Commit Assets with their `.meta` files, Packages and ProjectSettings. Never commit generated caches, test output or builds. Preserve the boundary between deterministic rules and presentation. Each issue's verification criteria are part of its deliverable.
