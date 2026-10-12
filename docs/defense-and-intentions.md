# Defensive decisions and off-ball intentions (SME-115)

The controlled footballer pauses the match when an opposing ball carrier is within six metres. Options are frozen before anyone moves or the AI acts. Inspect any player, choose an available action with 1–3, and commit with Enter. Space cannot skip the decision. Neither a player's role nor an attribute threshold hides a physically feasible option.

| Choice | Availability | Success | Failure |
| --- | --- | --- | --- |
| Tackle | Ball carrier within 2 m | Win possession and open a new on-ball decision | Attacker retains ball; defender recovers at quarter speed for 1.5 seconds |
| Contain | Ball carrier within 6 m | Follow a goal-side position, slow the attacker to quarter speed and delay their next action for 2 seconds | Same beaten/recovery result |
| Block | Within 6 m, goal-side, within 2.5 m of the first 6 m of the shooting lane; attacker within 30 m of goal | Deflect the ball sideways into loose play | Same beaten/recovery result |

Blocking is a pre-shot intervention against a nearby shooting threat. It does not reroll an already committed shot. Goalkeeper saves, fouls and formal restarts remain later tickets. Defensive movement currently uses placeholder repositioning rather than character animations. A 2.5-second response cooldown allows the result to play out before another defensive pause.

The displayed integer percentage is the exact probability passed to `MatchSession`. A single seeded draw incorporates tackling or positioning, anticipation, the opponent's dribbling, distance, fatigue and confidence. Containment is easier than winning the ball. Frozen previews, typed `DefenseResolved` events and skill attribution support later progression. Retries return the same result without applying its effects again. AI tackles now use this same preview/resolver and success means actual possession.

## Movement instructions

Open **Off-ball instruction [I]** while off the ball, during play or manual pause. Marking and tracking use the inspected opponent; inspecting a teammate or reserve cannot retarget the instruction. Instructions control only the user's footballer. The lime marker shows their current movement target. Selecting **Automatic** restores team tactics.

| Instruction | Movement | Phase |
| --- | --- | --- |
| Press | Chase the ball | Opposing possession |
| Mark selected opponent | Stay 1.5 m goal-side of that opponent | Opposing possession |
| Hold current zone | Stay at the position where the instruction was issued | Any live off-ball phase |
| Track selected opponent | Follow 4 m deeper with a narrower lateral position | Opposing possession |
| Find space | Choose the most open of three supporting lanes | Own-team possession |
| Support | Offer a passing option behind and beside the ball | Own-team possession |
| Forward run | Run 18 m forward from the instruction's starting point | Own-team possession |
| Counterattack | Wait high upfield | Any live off-ball phase |

Instructions last 20 seconds of match time. Manual pause consumes none of this duration. The panel displays the active instruction, target and remaining time, or why it ended. Receiving possession, an incompatible possession phase, a missing target or an out-of-play phase invalidates the instruction. A committed reception temporarily takes movement priority; containment's two-second response also takes priority. These durations, ranges and probabilities are initial tuning defaults. Coach reactions to deviating from tactics belong to SME-131.

## Verification

`DefenseTests` compares all three choices against the same attack, verifies success/failure consequences, frozen decisions, retry safety, reach, lane and target rejection, every intent, marking differences, expiry and pause timing. Probability tests sample each defensive action 2,000 times (6,000 total). Presentation tests select defense, inspect opponents, commit the shown probability and set/cancel marking through the view API. Existing pass replay checks continue through defensive interruptions.

The Mac `--pitch-smoke-test` reaches a defensive decision through normal simulated play, verifies it cannot be skipped, then commits containment through the actual presentation controls. Its success marker is `FOOTBALL_RPG_DEFENSE_PASSED`.
