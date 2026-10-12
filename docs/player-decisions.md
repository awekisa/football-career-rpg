# Player decisions (SME-113 / SME-114)

The interactive pitch opens with the user already holding the ball so the first choice is immediately accessible. Choose a teammate by clicking them or pressing R, choose a pass to feet (1), through ball (2), or a contextual action in the lower row (3–7), then Play action / Enter. Repeating an action button cycles its variants. The cyan line and ring preview the destination. The percentage beside each action is its frozen, exact resolution chance.

Inspect teammates or opponents with mouse selection or Tab. Opponent inspection preserves the selected pass; the panel names the recipient explicitly. Camera controls and the lime expected-position area remain available. Space pauses/resumes ordinary play and pass flight. During a pending possession decision, play stays paused until a valid pass is committed; Space does not delegate the choice to AI. Escape returns to the launch screen.

## State and ownership

OpenPlayWorld accepts an optional controlled player ID. AI-only scenarios retain their existing behavior. Each controlled possession opens one immutable PassDecision with a stable possession ID, tick and frozen pass previews for every eligible teammate and both supported pass types. A recipient must be another starting teammate at a distinct position; no football or personality skill threshold hides either action. Automatic AI movement, world time and random state stop while choosing.

CommitDecision validates the active decision and selected option, commits that exact preview through the shared resolver, starts its already-resolved flight, clears the decision and resumes play. A cache keyed by possession ID returns the same fact on identical retries, including retries after later possessions; different choices under an already-used ID are rejected. Old retries cannot restart the animation, emit another event, consume randomness or dismiss a newer prompt. The next receipt or recovery of the ball opens a new possession ID.

The Resolving state covers the atomic commit boundary. Simulation resumes into flight so players can move to the frozen destination. Animation represents the committed result, and can itself be manually paused; it cannot reroll success. A newly controlled reception stops the clock at that tick, before the next AI choice. Presentation selects options and displays state; it does not mutate possession or resolve probabilities independently.

## Verification

82 EditMode tests pass. New tests cover 20 frozen starting options, low-skill availability, invalid targets and stale IDs, duplicate/conflicting commits, success and failure for both pass types, manual pauses in flight, subsequent user possession, harmless retries from older possessions, opponent inspection, target cycling, destination marker consistency and matching the displayed preview to the committed result.

The packaged --pitch-smoke-test checks both AI-only play and the real presentation commit methods. It verifies one draw, exact chance, duplicate-click prevention, manual pause and completed flight, and logs FOOTBALL_RPG_DECISION_LOOP_PASSED.

SME-114 adds [attacking and receiving actions](football-actions.md) to the same decision lifecycle. SME-115 adds [defensive choices and persistent off-ball instructions](defense-and-intentions.md). Full laws, match highlights, creation and career systems remain later work. Native mouse injection has been unreliable in desktop automation; picking is covered by ray tests, and the keyboard path provides a complete playable verification route.

SME-116 adds [goalkeeper decisions](goalkeeper.md). With the ball at feet, keeper distribution and ordinary football menus represent the same opportunity: committing either invalidates the other. Holding the ball offers hand distribution or a drop to feet; handling restrictions persist through ordinary control and dribbling.
