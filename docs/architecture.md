# Project boundaries

`Assets/FootballRpg` owns game assets and code. Unity-generated metadata is committed alongside every asset. Library, Temp, Logs, TestResults and Builds are local outputs.

## Assemblies

- **Simulation**: pure C# game rules and state. `noEngineReferences` prevents accidental Unity dependencies. SME-108 and SME-109 introduce actual data and simulation types; this issue only establishes the boundary.
- **Content**: Unity content definitions and adapters, allowed to reference Simulation. Stable content IDs and validation belong in SME-108.
- **Presentation**: scenes, UI, camera, animation and input; may reference Content and Simulation. Presentation must not own outcome randomness or apply skill XP.
- **Editor**: asset setup and reproducible build entry points, excluded from players.
- **EditModeTests**: scene and build-configuration checks, excluded from players.

Dependency direction is Presentation → Content → Simulation, with Presentation also allowed to consume Simulation directly. Simulation must never reference UI, scene objects or Unity clocks.

## Current state

Launch.unity is a committed, editable scene with a camera and a foundation screen. Its chapter button shows the first playable scope; it does not start a playable career. The pitch diagram is placeholder presentation, not a match simulation.

`--smoke-test` runs the built scene for two frames, checks its camera/controller, logs a result and exits. This verifies lifecycle, not visuals or gameplay. The scene generator is explicit and only runs if the scene is absent; normal imports/builds never regenerate authored scenes.

## Next components

SME-108 defines shared data/events. SME-109 introduces deterministic time and randomness. SME-110 through SME-113 implement the first visible pass loop. Later issues connect football outcomes to career consequences. Follow explicit Linear dependencies rather than treating every milestone as a serial gate.
