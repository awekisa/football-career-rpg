# Attacking and receiving actions (SME-114)

The lower decision row shows actions valid for the current ball situation. Press the same action again to cycle variants; the header displays the selected variant and exact chance. Enter commits it. Selecting another teammate updates a targeted action’s recipient; inspecting an opponent preserves the current choice. Mouse controls have keyboard equivalents.

| Situation | Available actions and variants | Success means |
| --- | --- | --- |
| Incoming ball within control height | Control: cushion / turn | Actor controls the ball, optionally after turning |
| Low incoming ball | First-time pass | Named teammate controls at the frozen target |
| Possession with room ahead | Dribble: carry / take-on | Advance up to five metres / beat a nearby defender over an eight-metre diagonal move |
| Possession in a wide area | Cross: low / lofted | Delivery reaches the selected target; keeper intervention and reception are separate situations |
| Possession | Shoot: placed / power | Ball is on target; a reachable keeper gets a separate save decision |
| Aerial arrival within physical reach | Head to teammate: direct / flick | Selected teammate controls the redirected ball |
| Aerial arrival within physical reach | Head at goal: downward / power | Ball is on target; a reachable keeper gets a separate save decision |
| Possession or a reachable arrival | Clear: long / wide | Ball reaches the clearance destination away from the actor |

Owned-ball keys are 3 Dribble, 4 Shoot, 5 Cross, 6 Clear. Arrival keys are 3 Control, 4 First-time pass, 5 Head to teammate, 6 Head at goal, 7 Clear. Infeasible actions are disabled. There is no attribute threshold: low skills lower the chance. Header availability depends on arrival height and physical reach (standing height + .9 m), while jumping skill affects probability. To-feet/through passes remain in the upper row for controlled possession.

## Resolution and progression contracts

FootballActionContext snapshots the actor, ball height/speed, attacking direction and opponents. FootballActionPreview freezes the variant, target, success/failure definitions and progression weights. FootballActionResolver uses MatchSession’s existing single draw, stable request/event IDs and diagnostic log; no separate random generator or collision reroll is introduced. Chance is clamped to 5–98% and rounded once to the exact displayed integer percentage. Skill attribution is 80% action skill and 20% decisions; typed FootballActionResolved facts expose these weights and the actual outcome without mutating XP.

Version-one chances use the action skill (control, passing, dribbling, crossing, finishing/long shots or heading), pressure, distance, confidence, morale, fatigue, ball height and arrival speed. Dribbles additionally use agility, strength, weight and opponent tackling. Aerial actions use height, jumping and strength. Shots use lateral angle; opposing goalkeeper ability is applied in the subsequent explicit save response. Variants change targets and/or chance: placed shots aim off-centre, power shots centrally; take-ons change direction and cover more ground; lofted crosses use a higher arc than low deliveries. Selected formula inputs and the unrounded chance are logged alongside the displayed chance. These weights are initial tuning.

Failure is frozen before the roll: shots/goal headers go wide; other actions are dispossessed when closely pressured, otherwise they produce a loose ball. Saves and aerial keeper interventions are explicit SME-116 responses. Fouls and formal restart consequences remain later rules work. Flight applies the resolved fact once; pauses freeze horizontal position and height. Dribbles/control move the actor to the committed destination before possession resumes.

## Receiving and AI

Successful crosses produce an aerial keeper contest when reachable, otherwise Incoming with a designated receiver and trajectory, not an automatic control result. A controlled user gets a new frozen decision; AI receivers choose a feasible control, first-time pass or header. This does not overturn cross success: delivery and reception have explicitly different success definitions. Ordinary successful passes retain their existing guarantee that the receiver controls the ball, with no extra control roll.

AI uses the same previews/resolver. It considers shots near goal, low/lofted crosses from wide attacking positions, clearances under defensive pressure and occasional carries. Receiving choices depend on ball height and field position. All action sequences remain deterministic under the fixed clock.

An on-target shot increments provisional home/away counts once if the keeper cannot reach it or fails the save. Goals and misses use the existing sandbox reset; official kickoffs, throw-ins, corners, match phases and final scores are SME-117 work. The partial OpenPlayWorld.Actions file owns this integration. PassDecision retains its existing pass options and adds a list of generic actions, preserving old pass callers and tests.

## Verification

The original SME-114 verification passed 113 EditMode tests; the current suite also covers the subsequent goalkeeper integration. Every variant is sampled 1,500 times with an exact-chance assertion and reproducible statistical tolerance (22,500 samples total). Feasible/infeasible contexts cover all eight action kinds. Tests cover trajectory and physical effects, low-skill aerial availability, cross-to-arrival transitions, AI reception, no draws during flight, pauses, retries, target changes and presentation consistency. The packaged pitch smoke also commits a dribble and a header through real presentation methods and requires FOOTBALL_RPG_FOOTBALL_ACTIONS_PASSED.

## Goalkeeper integration (SME-116)

Shot and goal-header success now means **on target**, labelled beside the displayed percentage. A reachable keeper then makes an explicit response, using the shared resolver. A save stops the goal; a missed save or unreachable keeper allows it. Goal counts remain provisional until SME-117. Successful crosses may likewise face a reachable claim/punch attempt before the receiver acts. See [goalkeeper play](goalkeeper.md). This supersedes the earlier direct-goal success definition.
