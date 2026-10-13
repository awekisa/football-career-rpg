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

Launch.unity is a committed, editable scene with a camera and a career entry screen. Start career opens character creation and enters the calendar. CareerWeek coordinates the authored week and owns its one MatchSession/OpenPlayWorld across pitch view lifetimes. PitchPreview owns geometry, camera and inspection controls; its driver advances the shared match while visible. Returning to the week pauses the same fixture, so resuming cannot reroll or reset it. Separate launch demos still own and dispose their temporary worlds.

OpenPlayWorld owns role targets, positions and ball state for the active players (22 at normal kickoff), driven only by MatchSession ticks. PitchPreview copies that state into transforms and the tactical marker. Calendar and football clocks remain independent; finishing or abandonment completes the prototype week.

`--smoke-test` runs the built scene for two frames, loads and validates the fixture, checks its camera/controller, logs a result and exits. This verifies lifecycle, not visuals or gameplay. The scene generator is explicit and only runs if the scene is absent; normal imports/builds never regenerate authored scenes.

## Next components

SME-108 provides shared data/events and a validated 36-player fixture (see [data ownership](game-data.md)). SME-109 provides deterministic time and randomness (see [match simulation](match-simulation.md)). SME-110 provides the [pitch preview](pitch-preview.md). SME-111 supplies [AI movement and possession](open-play.md). SME-112 supplies [frozen pass probabilities and outcomes](pass-resolution.md). SME-113 supplies [frozen possession decisions](player-decisions.md), idempotent commits and the interactive panel. SME-114 extends that lifecycle with [football actions and incoming-ball contexts](football-actions.md). SME-115 adds [defensive choices and movement intentions](defense-and-intentions.md). SME-116 adds [goalkeeper responses, distribution and handling eligibility](goalkeeper.md), with a keeper entry on the launch screen. SME-117 adds [fixture phases, goals and boundary restarts](fixture-rules.md); the launch enters a full fixture while test scenarios can retain the practice harness. SME-118 adds [offside snapshots, involvement checks and indirect restarts](offside.md). SME-119 adds [referee decisions, advantage, cards and free/penalty kicks](referee.md). SME-120 adds [team-authored set-piece plans, user duties, deliveries and career-facing records](set-pieces.md). SME-121 adds [fatigue, injury readiness, a named bench, coach substitutions and user participation states](injuries-and-substitutions.md). SME-122 adds [full-match and highlight pacing](match-viewing.md) over the same fixed-step simulation, with role/intent anticipation and protected decisions. Later issues connect football outcomes to career consequences. Follow explicit Linear dependencies rather than treating every milestone as a serial gate.

`CareerCalendar` owns the career minute cursor, immutable schedule, confirmed choices and attendance facts. Only explicit choice commits advance it; Unity frames, navigation and match clocks do not. See [career calendar](career-calendar.md).

`CareerReadiness` owns temporary-condition consequences and a reason ledger. Calendar confirmation invokes it once; match physics supplies live exertion, injuries, meaningful action results and final participant results. It never changes personality or awards skill XP. Training readiness records expose effective participation for `CareerTraining`. See [readiness](readiness.md).

`CareerTraining` consumes confirmed training after readiness has captured pre-workload state. It combines attendance duration, focus, club quality and skill levels, then requests exactly-once awards from `SkillProgression`. Preview reports are read-only. See [scheduled training](training.md).

`ClubVenue` owns an immutable navigation grid. `ClubExploration` owns the current walking route and contextual availability without mutating career time. The presentation hands an arrived-at activity back to the existing calendar for explicit confirmation. See [club exploration](club-exploration.md).

`CareerSocial` owns affinity, professional trust, standing, seeded dialogue outcomes and bounded repeated-behaviour evidence. Personality changes flow through its explicit operation; training and temporary condition systems have no personality hook. See [relationships](relationships.md).

`CareerCoach` owns selection policy, persuasion and reaction history while sharing relationship dimensions with `CareerSocial`. Career fixtures opt into coach selection, role-aware movement and committed-action reactions. Lower-level authored scenarios retain their fixed line-ups. See [coach behavior](coach.md).

`CareerMeetings` owns authored appointment outcomes and agent preferences, while calendar time/default focus and social relationship values remain with their existing owners. Attendance opens a pending conversation; explicit resolution/leave releases the calendar. See [club conversations](club-conversations.md).

`CareerWeek` subscribes after readiness, training, coach and meeting consequences. It records selection briefings, routes attended/late/missed fixture attendance into one coach-selected world, blocks further calendar choices while it is pending/active/complete, and records a basic final score. Character creation copies meeting content into the new career. See [career week](career-week.md).

`CareerReports` captures baseline snapshots when the week starts and immediately before the career fixture is constructed. After the final match consequences are applied, the completion callback freezes two read-only reports. They combine source XP/training, readiness, social and action ledgers without invoking any mutation/reward operation. `SkillXpChange` now retains before/after levels so level changes have exact source attribution. Pitch view disposal does not discard reports. See [career reports](career-reports.md).

SME-135 adds a versioned, closed-type career snapshot in the Content layer. Stable paused matches preserve random state, pending choices and applied-event ledgers without replaying commands. Restore reconnects simulation observers exactly once; presentation rebuilds from the restored career. Local writes are atomic and preserve a valid backup. See [career saves](career-saves.md) for the schema, Mono limitation and compatibility policy.
