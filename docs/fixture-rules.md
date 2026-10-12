# Fixture rules — SME-117

The launch screen now starts a regulation fixture with a kickoff, two equal halves, halftime, added time and full time. Press L to choose a standard 90-minute match or a four-minute test match before entering as an outfielder (Enter) or keeper (G). At halftime, Enter or Space starts the second half. Teams change ends and the other team takes the kickoff. Draws finish at full time without extra time or penalties.

## Simulation contract

`OpenPlayWorld.StartFixture(CompetitionRules)` starts a fixture once, before any simulation ticks or action draws. `CompetitionRules` configures half duration, minimum added seconds, restart preparation duration and the opening kickoff team. Durations are simulation seconds; playback speed does not change them. The default is 45-minute halves, zero minimum added time and two seconds of restart preparation. Invalid non-finite or non-positive durations are rejected (zero minimum added time is valid).

Constructing `OpenPlayWorld` or `PitchPreview.Initialize` alone retains the practice harness used by action scenario tests. The production launch calls `StartFixture`; practice is deliberately independent of the fixture lifecycle.

The match clock starts when the opening kickoff is released. Thinking, inspection, manual pauses and the halftime screen cost no match time. The prototype referee counts subsequent restart preparation as lost time, adding it to the current half. The announced allowance may increase, never decrease. Second-half time starts at the configured half duration, without carrying first-half added time into the second half. Full time freezes gameplay and emits its event once. A committed open-play flight may be stopped by the whistle; penalty extensions belong to the penalties ticket.

`FixturePhase`, `RestartState` and immutable `FixtureEvent` facts are separate from the simulation run/pause state and the ball's flight state. Event IDs have a match-specific monotonic sequence. Releasing the same current/latest restart twice returns its original result; changing the recipient is rejected. Older or stale restart IDs cannot start another flight. The list exposes goals, half transitions and restart award/release facts without changing career attributes.

## Boundaries and goals

The pitch is 105 × 68 metres, with a 0.11-metre ball radius. `PitchBoundary.Cross` sweeps a flight segment to the first line the whole ball crosses, interpolating height at that point. Merely touching or overlapping the line does not stop play. A goal requires the whole ball between the posts and below the crossbar at the goal-line crossing. Crossings outside that opening produce a restart rather than a goal; post/crossbar rebound physics are not simulated.

Shots still resolve on-target probability and, when reachable, an explicit keeper response before crossing the line. An unsaved shot continues beyond the goal line and is counted once. The conceding side receives the kickoff. Last-touch provenance assigns a throw to the other team; a last defender touch over the goal line awards a corner, and a last attacker touch awards a goal kick. The assignment follows current attacking directions after halftime and supports own goals.

## Restarts

Players are arranged legally before release. Kickoffs put everyone except the taker in their own half and opponents outside the centre circle. Throws use the crossing location and keep opponents two metres away. Corners place the ball in the nearest corner area and keep opponents outside the required distance. Goal kicks use the goal area and keep opponents outside the penalty area. The keeper normally takes goal kicks; a nearby outfielder takes throws/corners and an advanced player takes kickoffs.

The controlled taker receives a paused recipient choice with the exact probability of the teammate receiving the delivery. Select a teammate or press R, then Enter to release. Opponent inspection preserves the target. AI takers use the same previews and resolver after preparation. For this foundational rules ticket, deliveries use the existing passing model; authored set-piece routines and specialist delivery choices remain separate work.

The release creates one frozen flight and one random draw. The kick moves on release; a throw enters the pitch before normal flight advances. The taker cannot recover their own untouched restart. Direct throw-in goals are disallowed, and direct own goals from these restarts award a corner. Throw touch provenance also prevents a keeper handling a direct teammate throw. Illegal restart forms are not selectable; infringement sanctions, delay countdowns, offside exemptions and authored restart animations belong to subsequent rules/presentation work.

## Verification

208 EditMode tests pass, including 37 new cases for whole-ball/goal-opening boundaries, swept paths, last-touch allocation, both teams, every restart, legal positioning, direct throw restrictions, real shot flights, pause/retry behavior, halftime direction changes, added time, drawn full time and presentation selection/commit.

The packaged `--pitch-smoke-test` includes a short two-half fixture through the presentation layer and requires `FOOTBALL_RPG_FIXTURE_PASSED`. Existing action, defense and keeper smoke checks remain in place. Mac builds are verified; Windows remains unverified.

Rules consulted on 2026-10-12: [IFAB Law 7](https://www.theifab.com/laws/latest/the-duration-of-the-match/), [Law 8](https://www.theifab.com/laws/latest/the-start-and-restart-of-play/), [Law 9](https://www.theifab.com/laws/latest/the-ball-in-and-out-of-play/), [Law 15](https://www.theifab.com/laws/latest/the-throw-in/), [Law 16](https://www.theifab.com/laws/latest/the-goal-kick/) and [Law 17](https://www.theifab.com/laws/latest/the-corner-kick/). This ticket implements the core geometry, ownership and lifecycle described above, not all offences or competition protocols in those laws.
