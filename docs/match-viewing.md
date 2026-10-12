# Match viewing — SME-122

The pitch now offers full-match viewing and position-aware highlights. Both modes run the same seeded, fixed-step simulation. They change how quickly it is presented, not which actions happen or how they resolve.

## Controls

| Control | Effect |
| --- | --- |
| H / mode button | Toggle Full Match and Highlights, including while paused |
| M / speed button in Full Match | Cycle 1×, 2×, 4×, 8× |
| M / quiet-speed button in Highlights | Cycle 32×, 64×, 16× for quiet passages |
| Space / pause button | Pause/resume inspection where permitted |
| Existing inspection controls | Click or Tab to inspect; pan, rotate and zoom while paused |
| Enter | Confirm the current decision or acknowledge a required notice |

Full Match defaults to 1×. Highlights defaults to 32× quiet playback and **1× for involvement**. Full-match and quiet speeds are remembered separately when switching modes. The header shows the mode, configured speed and current effective rate; live passages also show the focus reason when no decision, restart or referee panel occupies that space.

## When highlights return

`OpenPlayWorld.ViewingFocus` is a read-only classification of the current simulation state:

- Every required user decision and restart assignment remains a mandatory stop. Referee/offside notices and halftime/full time are preserved.
- Incoming possession, the user's ball actions and their outgoing flight stay visible. An incoming pass slows before its receiving decision.
- Keepers watch opposition threats approaching their own area and their keeper intervention. Remote attacks at the other end remain quiet unless the keeper is involved.
- Centre backs, fullbacks, wingbacks and defensive midfielders anticipate nearby defensive threats within 24 metres; other midfielders use 20 metres and forwards 14 metres.
- Nearby attacking support is shown within 24 metres for strikers, wingers and attacking midfielders; other outfield roles use 18 metres.
- Pressing, marking, tracking, holding a zone, supporting, finding space, forward runs and counterattack instructions can widen relevance. The player must still be near the ball or a relevant marked player; an instruction does not force an entire match to remain a highlight.
- Goals, cards, injuries and substitutions stay visible for three match seconds. Benched, removed and dismissed users can watch events while uninvolved play fast-forwards. Entry returns them to their active tactical role.

These distances, quiet rates and event duration are prototype tuning defaults, not attribute locks or changes to action eligibility. Runtime position and keeper responsibility are used, including substitutions and emergency keepers. Final decisions still come from the ordinary action systems and their probability previews.

## Pacing and integrity

`MatchPlayback` sits between the Unity `MatchDriver` and the existing `MatchSession`. It advances one fixed step at a time and checks relevance after each step. If a quiet passage becomes a highlight, it yields the render frame immediately and drops remaining fast-forward wall time. The next frame starts at normal highlight speed. It never skips simulation ticks, invents events, consumes random draws or chooses an action.

Fractional frame time accumulates at the selected rate. Mode/speed changes, pauses and driver disable reset that remainder. Work per frame is bounded by the session step budget; excess wall time is discarded so a stalled render frame cannot create a catch-up burst through important play. This can slow presentation under load, while every simulated second retains its ordinary consequences.

Required stops are checked before stepping and after each tick, so even a direct clock resume cannot bypass a pending choice. The original session clock API remains unchanged for existing simulation and replay clients. The legacy driver multiplier remains available for adapters without an attached world; the pitch uses `MatchPlayback`.

## Verification

- Matched seeds and identical user choices are replayed through normal full-match, 8× full-match and highlights for all 14 position IDs. Tests compare action diagnostics, fixture and participation events, set-piece records, scores, ticks, random state, player/ball positions and persistent fatigue/injury state.
- Targeted tests cover anticipation for all positions, each non-automatic movement instruction, incoming possession, user action follow-through, a large frame reaching a new threat, protected decisions/restarts/intervals, paused inspection, speed validation, fractional time and bounded work.
- The packaged `--pitch-smoke-test` includes VIEWING: keeper, defender, midfielder, forward and bench users finish matched full/highlight fixtures through the actual presentation adapter and compare events and readiness.

Build and desktop verification evidence is recorded in the PR. No football rules, attribute formulas, progression or coach selection behavior are changed by this ticket.

The participation/status line sits above the footer, clear of the off-ball instruction controls and their expanded menu.

## Verified on 2026-10-12

398/398 EditMode tests passed, including 52 new viewing cases. The Mac build and every packaged smoke marker passed, including VIEWING. Desktop checks verified full/highlight switching, speed cycling, 32×/64× quiet passage playback, a paused referee notice remaining at 00:08 during mode changes and inspection, and explicit acknowledgement resuming play. The status row was visually verified clear of off-ball controls after layout correction. Windows build not run; per-position replay equivalence is automated, not a manual full-match walkthrough for each position.
