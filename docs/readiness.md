# Fatigue, confidence and recovery

The career owns one `PlayerState` per person. Calendar workload and rest, live match exertion, injuries, action confidence and final-result morale all update that same state. Starting another match uses it immediately; no copy can silently reset readiness. Core personality values remain unchanged.

## Calendar effects

`CalendarActivityDefinition.fatiguePerSlot` and `moralePerSlot` are copied, validated signed values in [-1, 1], applied in proportion to actual participation. Prototype tuning:

| Activity | Fatigue per slot | Morale per slot |
|---|---:|---:|
| Training | +14 points | +1 point |
| Extra practice | +18 points | +1 point |
| Rest | -22 points | +2.5 points |
| Party | +15 points | +3 points |
| Travel | +4 points | -1 point |

Wait/skip provide 4 points of unscheduled fatigue recovery per slot. Missed required commitments cost 4 morale points; late attendance costs 2, separately from the chosen activity. Optional misses carry no readiness penalty. The party's existing 60-minute delay consumes career time but supplies no additional recovery. It can still make the player late. All values clamp to 0–100%; logged deltas are the actual changes after clamping.

Rest and extra practice compete for the same next slot. Late attendance uses remaining minutes (300/360 of a slot in the Tuesday example). Previewing does nothing; duplicate confirmation applies no second consequence. The calendar header shows current fatigue, confidence, morale, injury days and training-yield multiplier. Its scrollable history explains the most recent 100 changes for the user, including match changes. Previews show workload and current training yield before confirmation.

## Training yield contract

Before workload or mood effects, each attended training fact snapshots:

`multiplier = clamp(1 - 0.7 × fatigue + 0.1 × (confidence - 0.5) + 0.1 × (morale - 0.5), 0, 1.2)`

`effectiveSlots = actualParticipatedMinutes / minutesPerSlot × multiplier`

`TrainingReadiness` records the stable attendance ID, multiplier, actual slots and effective slots. Training while injured records zero yield and no physical workload or attendance morale boost; passing time still heals the injury. Skipped training never creates a yield record. `CareerTraining` now consumes these snapshots and awards XP using coach/staff/facility and current-level modifiers; see [scheduled training](training.md).

## Match effects

Existing match fatigue/stamina/GK effort rates and the independent seeded injury stream are preserved. Live exertion immediately changes shared fatigue, with reason entries aggregated per 60 live seconds and flushed at halftime/fulltime/abandonment or when closing the match. Benched or removed players receive no further exertion. Injury assessment sets shared injury days and costs 4 morale points; a knock additionally adds 10 fatigue points. Match fitness rates remain configurable through `MatchFitnessRules`.

Meaningful actions (significance at least 1.5: shots, longer forward passes, danger-area defense, major keeper responses and specialist set pieces) update confidence from the actual resolver outcome. Success adds `0.008 × significance × (1.5 - chance)`; failure subtracts `0.012 × significance × (0.5 + chance)`. A per-player, per-match absolute budget of 0.12 limits farming and spirals. Low-impact actions and practice-mode actions do not alter match confidence. Action IDs scoped to each career match make repeated delivery idempotent. Underlying skill XP remains governed by its separate progression rules.

A finished fixture adds/subtracts 5 morale points for a win/loss to players who spent live time on the pitch; a draw records zero change. Substituted and injured participants remain eligible, unused bench players do not. An abandoned fixture awards no result morale. Pausing consumes neither fitness nor career recovery time. Full-match and highlight viewing produce identical condition values and reason records.

Passing, football actions, defense and goalkeeper probability formulas already use confidence/fatigue (and morale where relevant). New snapshots use current readiness; already displayed decision snapshots stay frozen through confirmation. No extra outcome RNG is introduced.

## Recovery and configuration

Injury days decrease after a full elapsed calendar day from injury onset, including time carried between choices. One calendar day is `slots.Count × minutesPerSlot`. All injured players recover together as career time advances, and new injuries reset that player's partial-day counter. A match or time spent inspecting screens cannot heal an injury. Recovery is deterministic; rehabilitation quality and new training-injury simulation are outside this ticket.

`ReadinessRules`, passed optionally to `CareerState.Create`, configures idle recovery, attendance penalties, confidence gain/loss/budget, result/injury morale, knock fatigue and training readiness weights. Rules are immutable and validated. Activity-specific rates are authored in the existing fixture JSON. In-memory ledgers and injury recovery counters will need inclusion in the future save/load component.

Calendar match attendance itself has zero workload: live match simulation supplies match exertion. Launch matches remain separate from scheduled match-day attendance until the integrated-week ticket, avoiding a second calendar fatigue charge.

## Verification

Tests compare rested/overworked schedules and subsequent pass chances, partial late training, injured training, elapsed-day recovery and next-fixture selection, duplicate confirmations, match result eligibility, live exertion, paused time, confidence limits, frozen snapshots, stable traits and configurable rates. All fourteen positional full/highlight replays also compare condition and the reason ledger. Packaged calendar smoke validates workload, one-time yield recording and rest via the launch preview/confirm adapter (`FOOTBALL_RPG_READINESS_PASSED`).
