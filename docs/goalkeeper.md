# Playable goalkeeper (SME-116)

Choose **Play as goalkeeper [G]** on the launch screen to control Harbour Vale's starting keeper. This uses the same match simulation as the outfield entry. Character creation and training still belong to later tickets.

Goalkeeper prompts pause the clock. Use the numbered buttons to select an action and Enter to commit. Inspect any player without changing the frozen chance. For distribution, click a teammate or use R to cycle eligible recipients. Space cannot skip a decision, but manual pause still works between decisions. F switches between goalkeeper and ordinary football actions when the ball is at your feet. No input timing changes any outcome.

## Decisions and outcomes

| Situation | Choices | Consequences |
| --- | --- | --- |
| One-on-one / nearby loose ball | Set position, rush out | Position goal-side or attempt to gather; a missed rush leaves the keeper advanced |
| Reachable on-target shot | Save and hold, parry | Keep possession or deflect into loose play; a failed save concedes |
| Reachable aerial cross | Claim, punch | Catch or send the ball away; a missed contest lets the intended attacker receive |
| Ball at feet | Gather when lawful; short/long kick; ordinary passes, dribbles, shots and other contextual football actions | Choose safer distribution or deviate from the goalkeeper role |
| Ball in hands | Roll, throw, short/long kick, drop to feet | Distribute to a teammate, or release to use ordinary football actions; a failure leaves a loose ball |

The keeper's role movement follows the ball laterally and keeps a deeper starting position. Set position adjusts to a goal-side point; rush-out success takes possession at the ball, while failure leaves the keeper exposed. The user can also select off-ball movement intentions. A nearby attacker triggers a one-on-one prompt only when another defender is not already covering them. After responding, a three-second simulated cooldown lets play continue.

Initial reach defaults are 8 m for saves, 7 m for aerial contests and 12 m for rushing out. Height reach is the player's height plus 1.1 m; no skill threshold hides otherwise feasible choices. Ground crosses and unreachable/out-of-area aerial balls do not create hand-action prompts. Outside the penalty area, the keeper retains ordinary tackle/contain/block choices. Protected hand possession cannot be tackled.

## Probability contract

Every displayed integer percentage is exactly the chance passed to `MatchSession`. `KeeperPreview` freezes skill, condition, pressure, location, ball height/speed and physical profile. Each committed response uses one seeded draw and produces `KeeperResolved` with stable IDs and weighted skill attribution. Identical retries return the same fact without reapplying possession, scores or animation. Inspection and pause consume no draws.

The primary attributes are positioning, one-on-ones, handling, diving, aerial command, distribution and ball control. Reflexes, positioning, jumping, height, weight, confidence, fatigue, opponent proximity and trajectory modify relevant chances. Holding a save is harder than parrying; claiming is harder than punching. AI uses the same previews, resolver and outcome application.

**Shot percentages now mean “on target,” explicitly labelled in the UI.** A successful shot/goal header emits `ShotOnTarget`. Once that frozen flight reaches its destination, an influential keeper receives a separate save choice; a missed save scores once, a successful save prevents the goal. An unreachable on-target shot scores without a redundant keeper roll. Off-target shots never ask for a save. The earlier embedded goalkeeper penalty was removed from the shot formula so keeper ability is applied in the explicit response.

Successful distribution guarantees teammate control at the frozen destination. The ball flight visualizes the committed result; it cannot be intercepted or rerolled in transit. A failed distribution ends loose. Keeper hand possession is shown with a raised ball and transfers immediately into a distribution choice, rather than letting a decision timer penalize the user.

## Handling rules boundary

`HandlingRules` is the pure simulation eligibility component for the upcoming laws system. It records the last player, team, deliberate kick target and touch type, plus whether the keeper has released the ball without another player touching it. A teammate's deliberate pass remains restricted through the keeper's first touch or dribble. An ordinary header is distinguished from a deliberate kick. Distribution kicks count as attempts to release the ball; merely dribbling does not.

The current action gate rejects handling outside the keeper's own penalty area, after a teammate's deliberate kick to the keeper or direct throw-in, and after releasing the ball before another player touches it. Ordinary feet actions remain available. This follows the handling provisions of [IFAB Law 12](https://www.theifab.com/laws/latest/fouls-and-misconduct/) (accessed 12 October 2026). The ball's position determines the area check; both attacking directions and boundary lines are tested.

SME-119 now offers an explicit illegal-handling choice and applies the modeled sanctions, referee notices and eight-second hold rule; see [referee decisions](referee.md). Current hand possession immediately prompts distribution (AI releases promptly), so paused decision time cannot cause a time-limit offence. Deliberate trickery and all repeated-offence sanctions remain outside the implemented model; see the [verification limits](football-verification.md).

## Verification

Goalkeeper tests cover every action, sampled probability, successful/missed saves and aerial contests, one-on-ones, loose-ball rushes, distribution flights, pause/retry safety, unreachable shots, outside-area foot defending, back-pass reception, protected hand possession and dropping/dribbling without gaining unlawful handling permission. View tests cover recipient cycling, opponent inspection, save selection and switching to normal football actions.

The packaged `--pitch-smoke-test` starts a goalkeeper session, distributes through the presentation controls, reaches a relevant threat through ordinary match simulation and commits a paused response. Expect `FOOTBALL_RPG_GOALKEEPER_PASSED` alongside the existing smoke markers. Windows and mouse event injection remain outside verified coverage; deterministic picking and view controls have automated tests.

SME-117 now supplies ordinary goal kicks, corners, throws and kickoffs. Direct teammate throws preserve the handling restriction. The launch starts a full fixture, and keeper goal kicks use the restart recipient panel rather than the open-play distribution menu. See [fixture rules](fixture-rules.md).
