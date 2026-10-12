# Shared game data (SME-108)

## Loading and identity

Editable starting content lives in `Assets/FootballRpg/Runtime/Content/Resources/PrototypeFixture.json`. `PrototypeContent.Load()` deserializes it with Unity JSON and runs the engine-independent `DefinitionValidator`. `CareerState.Create()` also validates at its public boundary so non-Unity callers cannot bypass checks.

`schemaVersion: 1` describes authoring content, not a save-game format. Unsupported versions fail explicitly. Full persistence is SME-135.

IDs are stable, case-sensitive lowercase slugs. Display names can change without breaking references. Never use names, array positions or Unity instance IDs as identity. Player and coach IDs do not overlap. Squads store IDs resolving to the single canonical player in a career.

## Configurable definitions and provisional balance

- 31 football attributes across technical, tactical, physical, set-piece and goalkeeping groups.
- Six personality traits, including baseline confidence.
- 14 position choices covering all agreed roles, including goalkeeper.
- 16 contextual action definitions with success meanings and relevant football attribute IDs.
- 36 original placeholder players split between Harbour Vale FC and Meadowbridge Athletic, each with a distinct coach.
- Each squad has 18 players including two goalkeepers, enough for 11 starters and seven substitutes.
- The provisional fixture permits five substitutions; this is configurable rather than a claim about a particular competition.
- The starting user is a 17-year-old Harbour attacking midfielder. SME-123 replaces this seed through character creation.

Attribute bounds/defaults are configurable per definition. Current football defaults are 1–100/default 30; personality defaults are 1–100/default 50. These are tuning choices. Player overrides replace selected defaults; each runtime player receives every attribute/trait. Separate validation prevents football attributes being mistaken for personality. Recommendations never lock a position or gate actions.

Confidence, morale, fatigue, coaching preferences and quality factors are finite 0–1 values. Identity includes age, height in centimetres, weight in kilograms, preferred foot and a placeholder appearance preset ID. Actual appearance assets and physical outcome modifiers arrive later. Age/body validation provides broad sanity bounds, not final character-creation balance.

Money uses signed integer minor units (EUR cents) plus a currency code. Wages are non-negative; balances may represent debt. Transactions and transfers are not implemented. Formation is descriptive content until positioning/tactics work defines geometry.

## State ownership

| Data | Owner and access |
|---|---|
| Authoring DTOs | Public fields support Unity JSON; never use these as live career state. |
| Career | One `CareerState` per session, owning canonical players, clubs, coaches and fixture. No static singleton. |
| Runtime snapshots | Copy scalars and nested arrays/maps; expose read-only collections and getter-only properties. Independent sessions share no mutable player or roster state. |
| Temporary condition | Change through `CareerState.SetPlayerCondition`. Validate all values before mutation; never modify traits or other players. |
| XP and personality development | Starting values are present. Future progression/behaviour systems add explicit owner operations. Do not cast away read-only views. |
| Membership and finances | Initial records only. Future transfers/transactions must update the career owner atomically instead of maintaining separate squad copies. |
| Launch lifecycle | The launch controller currently creates and holds one career. Future orchestration should retain/pass it across scenes instead of reloading the seed at each venue. |

This ticket does not implement progression, transfers, relationships, probability calculation, saving or a general event bus ahead of their own tickets.

## Event contracts

Events are immutable facts emitted after state changes. They carry stable event/player IDs and sequence numbers; generation/dispatch belongs to simulation infrastructure. These types generate no random GUID or wall-clock timestamp.

- `ActivityResolved`: activity and slot IDs, attended/skipped/late outcome.
- `ActionResolved`: action/target/outcome IDs, exact probability and random draw in [0,1). Success derives from `roll < chance`, avoiding contradictory flags. No random numbers or probabilities are calculated here.
- `ProgressionApplied`: attribute ID, signed XP delta, previous/current levels and causing event ID. Supports XP and level loss.
- `SelectionAnnounced`: fixture, coach, selection status and assigned position. Non-selection requires no position; assigned role is independent of primary position.
- `RelationshipChanged`: other person, dimension (affinity versus professional trust, for example), previous/current values and causing event ID.

Consumers use event IDs for later deduplication and reports. Publishing a fact must not reapply its completed action. Resolvers remain responsible for choosing contextually valid outcome IDs consistent with success.

## Validation

Reject duplicate IDs, missing position choices, broken references, unknown/duplicate overrides, out-of-bounds values, NaN/infinity, conflicting memberships, insufficient benches and missing reserve goalkeepers before creating a career.

Run `./scripts/unity.sh test`. Tests load the actual JSON, cover all positions, compare sessions, mutate authoring data to detect aliasing, check read-only views and atomic condition updates, reject malformed content and exercise event boundaries.

The packaged game loads the same resource and logs `FOOTBALL_RPG_DATA_READY: 36 players, 2 clubs`. Missing/invalid content fails startup smoke mode. This checks packaged content inclusion; it does not imply playable matches yet.
