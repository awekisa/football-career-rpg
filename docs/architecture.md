# Project boundaries

`Assets/FootballRpg` owns game assets and code. Unity-generated metadata is committed alongside every asset. Library, Temp, Logs, TestResults and Builds are local outputs.

## Assemblies

- **Simulation**: pure C# game rules and state. `noEngineReferences` prevents accidental Unity dependencies. Owns definition DTOs, validation, career state and event contracts. MatchSession owns fixed simulation time, seeded randomness and action diagnostics.
- **Content**: Unity content definitions and adapters, allowed to reference Simulation. Loads editable prototype JSON and validates it through Simulation.
- **Presentation**: scenes, UI, camera, animation and input; may reference Content and Simulation. Presentation must not own outcome randomness or apply skill XP.
- **Editor**: asset setup and reproducible build entry points, excluded from players.
- **EditModeTests**: scene and build-configuration checks, excluded from players.

Dependency direction is Presentation → Content → Simulation, with Presentation also allowed to consume Simulation directly. Simulation must never reference UI, scene objects or Unity clocks.

## Current state

Launch.unity is a committed, editable scene with a camera and a foundation screen. Its Play match button creates a temporary PitchPreview using the same canonical career state. The preview owns procedural geometry, camera, inspection UI and a paused MatchSession driven by MatchDriver. OpenPlayWorld owns role targets, positions and ball state for the active players (22 at kickoff), driven only by MatchSession ticks. PitchPreview copies that state into transforms and the user tactical marker; it does not start a playable career. Closing it restores the launch camera and releases owned objects/materials.

`--smoke-test` runs the built scene for two frames, loads and validates the fixture, checks its camera/controller, logs a result and exits. This verifies lifecycle, not visuals or gameplay. The scene generator is explicit and only runs if the scene is absent; normal imports/builds never regenerate authored scenes.

## Next components

SME-108 provides shared data/events and a validated 36-player fixture (see [data ownership](game-data.md)). SME-109 provides deterministic time and randomness (see [match simulation](match-simulation.md)). SME-110 provides the [pitch preview](pitch-preview.md). SME-111 supplies [AI movement and possession](open-play.md). SME-112 supplies [frozen pass probabilities and outcomes](pass-resolution.md). SME-113 supplies [frozen possession decisions](player-decisions.md), idempotent commits and the interactive panel. SME-114 extends that lifecycle with [football actions and incoming-ball contexts](football-actions.md). SME-115 adds [defensive choices and movement intentions](defense-and-intentions.md). SME-116 adds [goalkeeper responses, distribution and handling eligibility](goalkeeper.md), with a keeper entry on the launch screen. SME-117 adds [fixture phases, goals and boundary restarts](fixture-rules.md); the launch enters a full fixture while test scenarios can retain the practice harness. SME-118 adds [offside snapshots, involvement checks and indirect restarts](offside.md). SME-119 adds [referee decisions, advantage, cards and free/penalty kicks](referee.md). SME-120 adds [team-authored set-piece plans, user duties, deliveries and career-facing records](set-pieces.md). Later issues connect football outcomes to career consequences. Follow explicit Linear dependencies rather than treating every milestone as a serial gate.
