# Football verification — SME-136

This is a verification suite for the prototype's promised football scope, not certification of every IFAB law. The baseline is **IFAB 2026/27**, checked on 13 October 2026 against the [official edition catalogue](https://www.theifab.com/laws-of-the-game-documents/) and the current Laws pages below. Keep the edition explicit when changing rules; `FoulRules.LawsEdition` already identifies 2026/27.

## Reproduce the automated checks

Run `./scripts/verify-football.sh` (Unity 6000.3.23f1 and Python 3). This invokes the `FootballVerification` NUnit category and writes `TestResults/football.xml`, `football.md` and `football.json`, with `Logs/football.log` for diagnostics. The wrapper removes only its own generated reports first, returns nonzero on compile/test/report failure, and cannot reuse stale results. The reporter rejects absent scenario families, absent roles, skipped/failed tests and duplicate role evidence. `./scripts/unity.sh test` still runs the entire regression suite.

The report keeps automated success separate from manual verification. Its role list comes from shipped position content, not a hand-maintained subset. Each of the 14 role cases creates a character through the real budget/creation flow, attends the authored week, starts an 11-v-11 fixture and controls actual pauses through `PitchPreview`. The same inputs run at full speed with 100 ms frames and highlights with 233 ms frames. It compares every stop's tick/identity/options/percentages and the **entire serialized final career**, including RNG, rules, skills, readiness, relationships, rewards and reports. Opponent inspection and an hour of attempted paused playback must not alter tick, draws or options. A keeper response requires the current match keeper; an emergency keeper remains legal regardless of their original career position.

The paired role scenario uses seed 42 and 600-second halves to reach meaningful involvement. Injuries and coach changes are disabled only in that comparison so every role remains available. Dedicated deterministic participation scenarios separately force injuries, unavailable benches, substitutions, emergency keepers and dismissal cases. Existing playback scenarios add seeded injury events and 1x/8x/highlight comparisons. These are automated presentation calls, not human mouse observations or fourteen real-time 90-minute matches.

## Rule coverage and boundaries

| Area | Scenario families | Implemented contract / remaining boundary |
|---|---|---|
| Goals and boundaries | FixtureRulesTests | Whole-ball swept crossing, goal opening/height, own goals, last-touch ownership, both ends; no post/net collision physics. |
| Restarts and clock | FixtureRulesTests, SetPieceTests | Kickoff, throw, corner, goal kick, direct/indirect free kick, penalty, spacing, direct-goal restrictions, retry safety, halves, added time and finality. Invalid restart forms are prevented; not every infringement sanction is modeled. |
| Offside | OffsideRulesTests, OffsideIntegrationTests | Frozen touch positions, involvement, deliberate play versus save/deflection, exemptions, rebounds and indirect restart; capsule/body envelopes approximate playable body parts. [Law 11](https://www.theifab.com/laws/latest/offside/). |
| Fouls, advantage and cards | FoulRulesTests, RefereeIntegrationTests | Shared action outcomes, contact severity, penalty-area location, DOGSO mitigation, bounded advantage/recall, deferred cautions, second yellows, dismissals and notices. The advantage duration and distance thresholds are game tuning. [Law 12](https://www.theifab.com/laws/latest/fouls-and-misconduct/). |
| Penalties | RefereeIntegrationTests, SetPieceTests | Legal setup, shot/keeper resolution, exactly-once release and half extension. Not a complete encroachment/feinting/double-touch infringement table. [Law 14](https://www.theifab.com/laws/latest/the-penalty-kick/). |
| Keeper handling | GoalkeeperTests, RefereeIntegrationTests | Area/direction, teammate kick/throw, own release, protected control, illegal-handle choice, eight-second corner and paused-clock safety. Repeated time-limit offence cautions and deliberate trickery are not fully modeled. [Law 12](https://www.theifab.com/laws/latest/fouls-and-misconduct/). |
| Players, injuries and substitutions | ParticipationTests | Independent benches/limits/windows, halftime, no dismissed-player replacement/re-entry, injury removal/recovery, emergency keepers and abandonment below seven. No delayed entrance/treatment protocols or post-abandonment competition adjudication. [Law 3](https://www.theifab.com/laws/latest/the-players/). |
| Decisions and presentation | MatchPlaybackTests, four presentation suites, FootballVerificationTests | Role-relevant threats, user stops, keeper-only flows, inspection, committed choices, full/highlight parity and persistent consequences. Visual usability requires the manual matrix below. |

Further edition references: [Law 7](https://www.theifab.com/laws/latest/the-duration-of-the-match/), [Law 8](https://www.theifab.com/laws/latest/the-start-and-restart-of-play/), [Law 9](https://www.theifab.com/laws/latest/the-ball-in-and-out-of-play/), [Law 13](https://www.theifab.com/laws/latest/free-kicks/), [Law 15](https://www.theifab.com/laws/latest/the-throw-in/), [Law 16](https://www.theifab.com/laws/latest/the-goal-kick/), [Law 17](https://www.theifab.com/laws/latest/the-corner-kick/).

Not claimed complete: VAR/official contact/dropped-ball protocols, crowd/extra-person interference, misconduct outside modeled action evidence, time-wasting restart countdown protocols, temporary/concussion/return substitutions, equipment checks, penalty shootouts/extra time, or all infringement/re-take permutations. Illegal gestures absent from the available actions are not evidence that their sanctions have been implemented. Forfeit results after abandonment require competition administration beyond this one-week slice.

## Observations requiring follow-up

The initial long-role matrix on 13 October produced **9 abandonments in 14 role runs**, with 3–9 dismissals per fixture. All ended on the implemented below-seven rule, and both viewing modes produced identical outcomes. This is a major balancing concern for SME-137, not a reason to alter the minimum-player law or suppress red cards in the verification. The matrix checks a legal terminal state and still requires meaningful decisions and user action records for every role. Automated PASS means deterministic behavior and exercised cases; it does not mean match pacing is balanced or ready for delivery.

The existing short fixture and scripted rule scenarios complement these longer runs. New blocking flow failures must be fixed here; pacing/risk changes belong in SME-137, retaining this baseline for comparison. Corrected stale documentation/UI claims that saving and other already-shipped rule features would arrive later.

Reference run: [automated evidence and per-role counts](football-verification-evidence.md).

## Manual play matrix

Use `docs/football-manual-matrix.csv` as the durable evidence sheet. Record build commit, tester, date, observations and result for **each** role. Do not populate it from test results. An unchecked row remains pending.

For each GK, CB, LB, RB, LWB, RWB, DM, CM, AM, LM, RM, LW, RW and ST:

1. Create the role with the normal screen (F1/F2 balance the separate budgets, F3 cycles position). Check its recommendations and profile, join, attend the week and enter the fixture.
2. Inspect your player and an opponent with the mouse. Check selected names, role, tactical marker and readable probabilities. Pause/resume; thinking must not move the clock or roll an outcome.
3. Commit an appropriate possession choice with the mouse, then a defensive/off-ball choice. For GK also inspect a handling/save/distribution choice and the illegal-handling warning when applicable.
4. Toggle full/highlight viewing. Verify highlights return before relevant involvement and a pending decision cannot be skipped. Complete an action with Enter and inspect its outcome.
5. Reach a stoppage/interval, acknowledge the notice or take the role's set-piece duty. Return and resume; verify the same fixture remains. Check the resulting report and record any clipping, unreachable control or dead-end.

Scripted referee, injury and set-piece scenarios prove deterministic outcomes; do not claim a rare event was observed manually unless it occurred in that run. A complete manual matrix is required before closing SME-136. Windows remains a separate unverified platform; the current build target is macOS Mono.
