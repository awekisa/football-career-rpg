# Football Career RPG

A single-player Unity football career RPG: one footballer, meaningful match decisions, training and relationships.

**Current milestone:** full-match and position-aware highlights (SME-122). Watch at normal or faster speed, or fast-forward quiet passages and return for your involvement. Every mode runs the same simulation and preserves required decisions. Character creation, training and club-life integration come next.

- [Prototype specification](docs/prototype-spec.md)
- [Architecture](docs/architecture.md)
- [Data schema and ownership](docs/game-data.md)
- [Match timing and replay contract](docs/match-simulation.md)
- [Pitch preview and controls](docs/pitch-preview.md)
- [Open-play simulation](docs/open-play.md)
- [Pass probabilities and outcomes](docs/pass-resolution.md)
- [Player decisions](docs/player-decisions.md)
- [Football actions and receiving](docs/football-actions.md)
- [Defense and off-ball instructions](docs/defense-and-intentions.md)
- [Goalkeeper play and handling restrictions](docs/goalkeeper.md)
- [Match phases and restarts](docs/fixture-rules.md)
- [Offside decisions and exemptions](docs/offside.md)
- [Referee decisions and sanctions](docs/referee.md)
- [Set-piece duties, deliveries and records](docs/set-pieces.md)
- [Fatigue, injuries, bench entry and substitutions](docs/injuries-and-substitutions.md)
- [Full-match and highlight viewing](docs/match-viewing.md)
- [Linear project](https://linear.app/smeagol-hermes/project/football-career-rpg-54f8537541e7)
- [First playable checkpoint: SME-113](https://linear.app/smeagol-hermes/issue/SME-113)

## Open and run

1. Install Unity **6000.3.23f1** through Unity Hub and activate a suitable Unity license.
2. Clone this repository and add its root directory as a project in Unity Hub. Initial import resolves pinned packages and creates local caches.
3. Open `Assets/FootballRpg/Scenes/Launch.unity` and press Play.
4. Press L to choose a 90-minute or four-minute match, then choose **Play match** or press Enter; choose **Play as goalkeeper [G]** for the goalkeeper role. Keeper prompts use numbered actions and Enter, R cycles distribution targets, and F switches to ordinary feet actions when available. Click a teammate or press R to select your pass target; choose **To feet** / **Through ball** (1 / 2), then **Play action** (Enter). The lower action row offers contextual choices (3–7); pressing an action again cycles its variants. When defending, 1–3 select tackle, contain or block; Enter commits. Off the ball, I opens persistent movement instructions; select an opponent before marking or tracking. Tab inspects any player. Use WASD to pan, Q/E to rotate, the mouse wheel to zoom, Home to reset, Space to pause/resume between decisions, and Escape to return. For your restarts, R selects a teammate and Enter releases the ball. Enter or Space continues after halftime. Offside and referee explanations pause the fixture; inspect players and press Enter to continue. Set-piece panels use 1–4 for duties or delivery types, R for targets, and Enter to confirm. Shot selection cycles placed/power variants.

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

Use `--pitch-smoke-test` instead of `--smoke-test` to also verify the packaged pitch, all 22 player selection rays, camera isolation, pause/resume, 30 seconds of AI movement/passing, and committing pass, dribble, aerial, defensive and goalkeeper decisions through the presentation controls. Expect `FOOTBALL_RPG_PITCH_PREVIEW_PASSED`. Use `--pitch-preview` to open the interactive pitch directly.

Equivalent direct editor commands (substitute executable and project paths):

```sh
Unity -batchmode -nographics -projectPath /path/to/project -runTests -testPlatform EditMode -testResults /path/to/results.xml -logFile /path/to/tests.log
Unity -batchmode -nographics -quit -projectPath /path/to/project -buildTarget StandaloneWindows64 -executeMethod FootballRpg.Editor.PrototypeBuild.BuildWindows -logFile /path/to/build.log
```

Do not add `-quit` to tests: the runner exits on completion. Build menu equivalents are under **Football RPG** in the editor.

## Contribution rules

Work on an issue branch and submit a PR. Commit Assets with their `.meta` files, Packages and ProjectSettings. Never commit generated caches, test output or builds. Preserve the boundary between deterministic rules and presentation. Each issue's verification criteria are part of its deliverable.

Character creation is available with **C** on the launch screen: separate football/personality budgets, every starting position, physical profile and four placeholder appearance presets. See [character creation](docs/character-creation.md).

Match actions now award or remove football skill XP, with level changes, specialist set-piece attribution and repetition limits. Pitch inspection shows progress toward the next level. See [skill progression](docs/skill-progression.md).

Open the **Career Calendar [K]** from launch to preview and confirm attendance, skips, replacements, waiting and extra practice. See [career calendar](docs/career-calendar.md) for timing and current integration limits.
