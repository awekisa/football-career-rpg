# Guided practice (SME-128)

## Playing a session

Open the calendar with **K**. **D / Coach** opens a free training-direction discussion: the coach recommends a family for your position and explains the choices. **I** switches simulated attendance to guided practice; **F** cycles Passing, Defending, Attacking and Goalkeeping. All families are available to every position. The family initially follows your primary role. Switching attendance mode or direction clears the old preview.

Choose **Attend [1]**, or **Extra practice [4]** when the next slot is free, inspect the forecast, then **Enter** to reserve the slot and enter the practice pitch. Injured players must recover before interactive practice. Simulated attendance retains the existing injury/zero-XP behavior.

The angled overhead 3D scene shows you in blue, teammates in green and the opponent in red. The yellow line shows the selected action's target. Choose an action with the mouse or **1–3**; inspect its actual success percentage, primary attribute and coaching explanation. **Enter** confirms; the outcome is shown until you choose **Next drill [Enter]**. No reflex input or time limit applies. The static scene illustrates the decision; passing success also places the ball at its destination. There is no full-match continuation between these isolated drills.

- Passing: a nearby pass to feet or a through ball leading a runner, with a nearby opponent applying pressure.
- Defending: tackle, contain or block a goal-side shooting lane.
- Attacking: placed shot, power shot or dribble into space. Shot success means on target, not a guaranteed goal.
- Goalkeeping: shot (hold/parry), cross (claim/punch), then one-on-one (set/rush). A training-only goalkeeper role override lets outfield players develop these skills without changing their career position.

After three drills, return to the calendar and read the training report. **Escape / End session** ends early, retaining only completed drill XP; it never refunds the slot. The slot is spent when practice begins, with one workload update. Calendar actions remain locked until practice completes or ends.

## Rewards and ownership

The session shares the same 120-XP-per-slot budget, pre-workload readiness, club/coach/staff/facility modifiers, level penalty and cap as simulated training. Each completed drill contributes one third of that budget: 90% to the action's dominant skill and 10% to decisions. A failed drill contributes 65% of its share; practice failure is learning, separate from competitive match failure losses. Unattempted drills contribute zero. These are prototype tuning defaults.

For example, three short passes concentrate the reward in short passing and decisions rather than splitting it across the broad passing group and role attributes. Goalkeeper drills can target handling/diving, aerial command and positioning/one-on-ones. The pre-session estimate assumes successful first-option actions; actual choices, failures and early exit determine the final report. Inspection never draws randomness, advances time or grants XP.

`GuidedPracticeSession` freezes actors and calls the existing `PassResolver`, `DefenseResolver`, `FootballActionResolver` and `GoalkeeperResolver` previews and commits. It owns a seeded `MatchSession` independent of the career fixture, with exactly one random draw per committed choice. Repeating the identical decision returns its existing result; switching an already committed choice or submitting a stale one is rejected. `Next` requires a completed drill and cannot extend the three-attempt cap.

`CareerCalendar` binds the attendance mode and family to the immutable preview, reserves the slot and rejects competing/stale attendance. `CareerReadiness` captures readiness and applies workload once. `CareerTraining` suppresses the general reward for guided attendance and applies the final focused award once through the existing attendance ledger. A zero-attempt exit records a zero-XP report. Extra practice is a new attendance in its own subsequent slot. Finish or end an active guided session before saving. Completed training results are included in career saves.

## Verification

Edit-mode coverage includes all four families for all fourteen starting positions, every offered action, displayed versus resolved probabilities, deterministic replay, inert inspection, duplicate/stale decisions, mutually exclusive attendance modes, partial/zero-attempt exits, injury gating, focused versus general rewards and view/camera teardown. Packaged smoke runs all four families through the actual practice view controls, verifies one reward, one slot and three draws each, and logs `FOOTBALL_RPG_GUIDED_PRACTICE_PASSED`.
