# Fatigue, injuries and substitutions — SME-121

Fixtures now maintain a named starting lineup and bench, persistent player fatigue, injury readiness and participation states. The user may start, wait on the bench, enter, be replaced or injured out, or be dismissed and watch the remaining play.

## Fitness and readiness

`StartFixture` accepts optional `MatchFitnessRules`. Practice worlds retain their existing behavior. The defaults are tuning values:

| Setting | Default |
| --- | --- |
| Fatigue gain per live match second | 0.00013 |
| Injury hazard per exposure second | 0.000006 |
| Fatigue risk multiplier | 4 |
| Match-ending severity chance | 35% |
| Match-ending injury recovery | 7 career days |
| Coach fatigue threshold | 72% |
| Substitution windows per team | 3 |

Every live fixed step increases fatigue by `rate × seconds × effort × (1.4 − 0.7 × stamina/100)`, clamped to 100%. Outfield effort is 1; keeper effort is 0.35. Thinking pauses, halftime, restart setup and bench time add no fatigue. Existing action previews read the updated readiness. Core personality and football attributes are untouched.

Injury exposure accumulates as `seconds × effort × (1 + fatigueMultiplier × fatigue)`. At a stoppage or halftime, the injury probability is `1 − exp(−rate × exposure)`. A separate seeded fitness stream makes the assessment reproducible without consuming the action-resolution random stream. An injury severity draw selects a knock (+10 percentage points of fatigue) or a match-ending injury. This prototype assesses injuries at existing stoppages; it does not interrupt live play for an animated medical incident.

`PlayerState.Fatigue` and `InjuryDaysRemaining` live on the same career-owned player object. A subsequent fixture built from that career excludes injured players and uses a reserve where available, without charging an in-match substitution. Calendar recovery and disk save/load are later work; days do not elapse in this match component.

## Roster and substitution rules

The first eleven club squad entries are starters, followed by the fixture's named bench size (seven in sample content). The existing fixture data sets the substitution limit (five). `MatchFitnessRules` configures windows separately.

- Coach requests made during live play wait for the next stoppage. Requests at a stoppage or halftime are processed there.
- Multiple changes in the same stoppage use one window. Each team has separate counters. Halftime changes count toward the player limit but consume no window.
- Only a fit, unused named substitute from the same club can enter. A substituted player cannot return, and a sent-off player's place cannot be filled.
- The incoming player inherits the outgoing player's tactical position, direction and keeper responsibility; their underlying attributes and preferred position stay their own.
- Injury replacements take precedence as each affected player is assessed. With no available substitute or allowance, the player leaves and the team plays short. An outfielder takes over if the keeper cannot be replaced. Fewer than seven ends the fixture as abandoned.
- Restart takers, recipients and duties are rebuilt against the active roster. Old restart choices cannot bring back removed players.

The baseline follows the named-substitute, stoppage, window, halftime and no-return rules in [IFAB Law 3, 2026/27](https://www.theifab.com/laws/latest/the-players/). Entry/exit and referee permission are represented by one atomic roster change at the stoppage. Extra time, concussion allowances, temporary treatment and entry/exit delay protocols are not simulated.

The prototype coach replaces players who exceed the fatigue threshold and chooses a reserve by positional fit, then freshness. A bench user whose preferred position is occupied is offered a halftime entry when the allowance permits. Richer coach personality and selection policy belongs to SME-131. `RequestSubstitution` is a coach-facing simulation API, not a player-facing manager control.

`ParticipationEvents` retain injury severity and substitution IDs/ticks; `Participation`, `Bench`, `SubstitutionsUsed` and `WindowsUsed` expose current state. The match view shows user status, substitution totals and the most recent roster message. Player inspection includes availability and remaining injury days.

## Try it

1. From the launch screen, press **L** for the four-minute fixture, then **B** to start on the bench as the reserve central midfielder.
2. Watch and inspect the match; acknowledge referee notices with **Enter**. Your player is absent from the pitch and receives no action choices while benched.
3. At halftime the coach brings you on when the normal limits permit. Press **Enter** for the second half, then play the usual contextual decisions.
4. For deterministic entry/removal verification, run the packaged `--pitch-smoke-test`: it starts a bench user, enters at halftime, removes them at a second-half stoppage, checks visible player markers and watches to full time.

Automated tests cover limits, shared/separate windows, halftime, queued requests, invalid opponents/re-entry, persistent readiness, knock/out severity, injuries with and without substitutes, emergency keepers, abandonment, red cards, user participation, presentation visibility and deterministic fitness replay. Build and manual verification results are recorded in the linked PR.

## Verification on 2026-10-12

- 346/346 EditMode tests passed, including 22 new participation tests.
- macOS player build passed; all packaged smoke markers passed, including PARTICIPATION.
- Desktop check confirmed the bench launch button, absent user marker, bench availability label, and resuming spectator play after a referee notice.
- Halftime entry, removal and watching through full time were verified by packaged integration checks, not a separate manual full-match walkthrough.
- Windows build not run.
