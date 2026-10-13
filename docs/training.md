# Scheduled training

Training and extra practice now award football skill XP when confirmed in the calendar. Choose simulated attendance for broad development, or [guided practice](guided-practice.md) for coached match decisions and focused development. No personality trait receives training XP.

## Player flow

Open the calendar with **K**. **F** cycles between Scheduled focus, Current position, Passing and possession, Attacking, Defending, Fitness, Goalkeeping and Set pieces. The same control is clickable. Selecting a focus costs no time and clears the current preview, so an old preview cannot accidentally confirm a different focus. The chosen focus stays active until changed; choose Scheduled focus to return to the timetable's recommendation.

Preview Attend [1] or Extra practice [4], then inspect the scrollable training forecast before confirming with Enter. A completed session opens **Training results [T]**, showing total and individual XP, each skill's share and level multiplier, levels before/after, participation, readiness and club modifiers. **Readiness changes [R]** shows the fatigue/morale consequences. The result history shows the most recent 20 sessions; the career retains every report in memory.

Focus groups are available to every position, including GK. By default, 70% of the XP allocation follows the chosen broad group and 30% follows the player's primary-position recommended attributes. Overlapping skills combine their shares. Current position allocates all XP to that position's recommended skills. This allows broad development toward another position while retaining general training; targeted single-skill interactive drills remain separate.

## Authored schedule

The existing `calendar` content now includes `trainingFocuses`. Each has a stable ID and name, with either `usePosition: true` or at least two distinct football `attributeIds`. Personality references are rejected. Training activities have a default `trainingFocusId`; individual commitments can override it. User selection overrides that scheduled default for the confirmed choice.

The prototype uses position training on Monday and Friday, passing on Tuesday, fitness on Thursday and set pieces on Saturday. Extra practice defaults to position work unless the user chooses another group. All references are validated before a career is created, and runtime data is copied from content. Optional empty focus IDs survive Unity JSON round trips as unset values. Older calendar definitions without a focus catalog remain attendance/readiness-only.

## XP calculation

For each skill:

`XP = baseXpPerSlot × participatedSlots × readiness × coachFactor × staffFactor × facilityFactor × allocationShare × levelFactor`

Default tuning:

- Base XP pool: **120 per slot**, spread across the skill allocation.
- Each quality factor: **0.5 + quality**, using the user's current club and that club's coach; quality is in [0, 1]. Each source independently affects development.
- Readiness: the existing fatigue/confidence/morale multiplier captured before training adds workload. Injury gives zero physical training yield.
- Level factor: **1 - 0.75 × (currentLevel - minimumLevel) / (maximumLevel - minimumLevel)**. Advanced skills gain less XP, in addition to the existing increasing XP threshold per level.
- Participation: actual minutes divided by slot length. Tuesday's 60-minute late arrival yields 300/360 of a slot.

XP is capped at each skill's maximum and updates the shared football level used by future match snapshots. Capped XP is not redistributed to other skills. Rates are immutable `TrainingRules` passed optionally to `CareerState.Create`. Base XP is validated within (0, 1,000,000]; quality floor is (0, 1], quality weight/level penalty/focus share are [0, 1]. Values are tuning defaults.

## Ownership and exactly-once behavior

`CareerCalendar.Preview` binds an immutable focus to the choice and remains inert. Confirmation advances time and publishes attendance once. `CareerReadiness` captures training readiness and applies workload first; `CareerTraining` then consumes that snapshot, builds its report and asks `SkillProgression.ApplyTraining` to award football XP. It does not repeat readiness changes or spend more time. Awards and reports use stable attendance IDs to reject reapplication.

Skip/wait/rest/non-training attendance award no training XP. Extra practice uses its real subsequent slot and the same reward calculation, without a second workload charge. Late training is proportional. Training while injured records zero XP. Existing match XP repetition/cap rules remain match-only; training is not represented as a fabricated match action. Changing focus or opening reports cannot affect a frozen match decision or award XP.

Preview reports reflect current readiness, club and levels when inspected; the confirmed report records actual values at execution. The calendar UI cannot run a match concurrently. In-memory training reports and award IDs must be included in the future save/load component. Transfers and their playing-time tradeoff are not implemented here; the calculation already resolves the player's club rather than hard-coding the home fixture club.

## Verification

Tests cover each quality source, strong/small-club comparisons, fatigue and current-level comparisons, scheduled/user focus selection, all fourteen position groups, cross-position training, late and extra practice, injury, skipped/replaced attendance, duplicate confirmation, inert previews, clamping/level changes, stable traits, configuration copying/validation and JSON round trips. Character creation verifies role training for every position. Packaged smoke exercises the real calendar adapter, checks preview against awarded XP and logs `FOOTBALL_RPG_TRAINING_PASSED`.
