# Match clock and action boundary (SME-109)

## Scope

This is the simulation foundation, not a moving 22-player match. `MatchSession` owns fixed time, its random stream and committed action diagnostics. SME-111 adds world movement; SME-112 supplies real football chance calculations; SME-113 supplies contextual decision prompts. Existing action requests use a supplied frozen probability, not a newly invented passing formula.

## Time and playback

The default fixed step is **50 ms (20 Hz)**, a configurable engineering default. Simulation time is an integer tick count multiplied by that fixed duration. `Advance(TimeSpan, multiplier)` accumulates elapsed time in 100 ns units, executes fixed steps and retains ordinary fractional remainders. World systems subscribe to `TickAdvanced` and must update only there, using deterministic iteration order. Do not update game state from render-frame delta time.

Playback uses an integer multiplier from 1 to 64. It changes the number of steps due, never their duration or the random draws. Normal viewing, fast playback and future highlights therefore share the same equations. The per-call budget defaults to 512 steps; excess time stays queued until subsequent advances, including `Advance(TimeSpan.Zero)`. No running time is silently dropped because of a slow frame.

On a pause, unconsumed time from the current frame is intentionally discarded. A large fast-forward frame cannot accumulate behind a decision and jump past it on resume. This also discards any partial step at that boundary. Pause/resume inputs must be recorded against simulation ticks for replay; identical wall-clock timestamps at different frame rates are not equivalent simulation inputs.

`MatchDriver` is an optional Unity scene adapter, not automatically attached to the launch screen. Future pitch scenes initialize it with their session. It rounds absolute unscaled timestamps and subtracts them, avoiding accumulated per-frame conversion error; it samples even while paused and resets its sample after disable. Unity animation and `Time.timeScale` do not own match time. Animation code must respect manual pause itself.

## States and actions

| State | Clock | Permitted transitions |
|---|---|---|
| Running | Fixed steps execute | Pause before a choice or manual inspection |
| Paused | No ticks or random draws from elapsed time | Resume prior state, or explicitly commit one new choice if no resolution is pending |
| Resolving | No world steps while the resolved action is presented | Complete the matching animation to resume, or manually pause the resolution |

An explicit choice commitment is the only operation that consumes randomness while leaving Paused. Waiting, inspecting, pausing/resuming and animation completion never consume it. Every accepted action, even 0% or 100%, draws once so replay consumption is unambiguous.

`ActionRequest` copies its context and contains a stable request ID, actor/target/action IDs, the displayed chance, and success/failure outcome IDs. Context should include chance-calculation inputs (skills, pressure, distance, etc.), encoded invariantly by the future resolver. Action availability and valid targets belong to that resolver, not to this clock layer.

`BeginResolution` creates one immutable `ActionResolved` fact and diagnostic. Repeating the same request ID and identical contents returns the original result in any state. Reusing an ID with different contents fails without a draw. The caller uses the returned event ID to apply world consequences **once**; idempotent commitment does not automatically deduplicate arbitrary downstream systems.

`CompleteResolution(eventId)` never resolves the action again. Duplicate completion returns false, including a delayed callback from an older action; it cannot end a newer resolution. A manual pause during Resolving must resume back to Resolving before completion. No pending frame time survives the choice boundary.

For now, `Resume` can leave a normal inspection pause without making a choice. Mandatory decision prompts and decision-generation tokens belong in SME-113. This issue does not yet distinguish inspection from a required football decision.

## Randomness and diagnostic logs

The private generator uses explicitly specified **SplitMix64** unsigned arithmetic, with the top 24 bits mapped to a float in [0,1). Seed zero is valid. Fixed algorithm vectors are tested so runtime upgrades cannot silently change sequences. This is simulation randomness, not security randomness. No Unity random, wall clock or GUID enters the stream.

Event IDs are `<session-id>:action-<sequence>`, starting at 1. Use a unique stable match/session ID within a career and preserve it for replay. Diagnostics retain seed, before/after generator state, tick/time, action context, chance, draw, outcome and event/request IDs.

`ToJsonLine()` emits stable, culture-independent, escaped JSON. Seed/RNG values are decimal strings to avoid losing 64-bit precision in log consumers. Context keys sort ordinally. `MatchDriver.FlushDiagnostics()` logs new records; applications can also consume the read-only diagnostic list without Unity logging. Retention is currently per match, not an unbounded career history.

## Failure boundaries and future persistence

Bad time/speed/chance inputs and conflicting action retries fail before state changes. Recursive advancement from a tick callback is rejected. A failed world callback pauses further ticks and propagates its exception; abort that session because a partially applied external world update is not rolled back. The API is single-threaded.

Exposed RNG state is diagnostic only. Full snapshot/restore, queued time, pending resolution, event sequence and deduplication history must be persisted together in SME-135; this ticket does not claim save/load support.

## Verification

The tests replay a simple tick-driven world and the same choices at 30/60/144 FPS, 1x/4x/16x speed and varying pause/animation delays. They compare complete tick traces and JSON action histories, not only final scores. Additional tests cover known RNG vectors, invalid inputs, fractional carry, bounded catch-up, duplicate/conflicting commitments, delayed animation callbacks, paused resolutions and log escaping under a non-English culture.

The packaged startup smoke includes a clearly marked synthetic choice, checks one draw despite a duplicate submission, and logs `FOOTBALL_RPG_MATCH_FOUNDATION_PASSED`. It runs only with `--smoke-test`; normal launch does not secretly play a match.
