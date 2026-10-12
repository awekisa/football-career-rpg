# Pass resolution (SME-112)

## Success and targets

A pass succeeds when the selected teammate controls the ball at the frozen target. A pass to feet targets the teammate’s position at commit time. A through ball targets selected space; the current AI selects a point eight metres ahead of its receiver, bounded to the sandbox pitch. The pure resolver accepts an explicit valid target, ready for the decision UI in SME-113. Select a teammate of the ball carrier in the inspector to compare both estimates. This ticket does not add human decision prompts or pass buttons.

PassActor snapshots copy skills, condition, position and facing. PassPreview is immutable: it contains the target, chance, modifier breakdown, failure context and weighted skill attribution. Commit uses that same chance and immutable context through MatchSession. Future decision callers must freeze the world while choosing and use the same preview when committing; passing a refreshed preview represents a new choice. Preview alone consumes no randomness.

## Formula v1

Sum the following probability contributions, clamp to 5–98%, then round once to a whole percentage (midpoints away from zero). The displayed integer divided by 100 is the exact float sent to the resolver; there is no extra hidden precision. Values are tunable defaults on the current 0–100 attribute scale.

| Contribution | Formula |
| --- | --- |
| Base | .92 to feet; .82 through ball |
| Passer skills | (weighted skill − 50) × .003; 65% short/long passing, 20% vision, 15% decisions |
| Receiver skill | (ball control/off-ball movement − 50) × .0015 |
| Distance | −.0035 per metre |
| Angle | Up to −.12 for turning 180 degrees from facing |
| Pressure | Up to −.16 for nearest opponent within 8 metres of passer |
| Receiver coverage | Up to −.10 for nearest opponent within 8 metres of target |
| Lane | Up to −.12 for an opponent within 4 metres of the pass segment |
| Opponent skill | (50 − average anticipation/positioning) × .0015, scaled by lane proximity within 8 metres |
| Foot | −.10 when using the non-preferred foot; none for either-footed players |
| Confidence / morale | (confidence − .5) × .08 plus (morale − .5) × .04 |
| Fatigue | −.15 × passer fatigue, −.06 × receiver fatigue |
| Through-ball run | −.004 per metre the receiver must cover |

## One roll, one outcome

The seeded draw is compared only once against the frozen chance. Success always becomes Received. Failure type is selected from the frozen geometry without another random draw: an opponent within three metres of the pass segment produces Intercepted; otherwise a target near the boundary produces OutOfPlay; otherwise the pass becomes an under-hit LooseBall. This is a deterministic v1 failure policy, not a complete taxonomy of football mistakes.

PassResolved is a typed GameEvent sharing the action’s stable ID and sequence. It returns the actual outcome, destination, receiving player (including an interceptor), frozen context and passer skill weights. To-feet attribution is 70% short passing / 30% decisions; through-ball attribution is 50% long passing / 35% vision / 15% decisions. It does not change XP or condition. The shared request ID prevents rerolls on retry. OpenPlayWorld publishes PassCommitted once when it starts a pass; flight only applies that fact, with enough travel time for the receiving player to reach the target. No incidental collision or late receiver check can overturn the committed result.

OutOfPlay has no owner. The current sandbox resets after one second to keep AI play running; official restart selection and execution remain in the rules ticket. Tackles still use their earlier provisional formula.

## Verification

73 EditMode tests pass. Controlled cases cover passing skill, receiver skill, defender skill, distance, angle, foot, pressure, coverage and temporary condition. Immutable preview/retry tests verify exact chance reuse, stable IDs, attribution and one draw. All three failure types have explicit outcome checks. World tests compare committed pass outcomes with the final animated state and verify no random draws occur during flight. Existing deterministic world replay and pause tests remain green.

For 8,000 seeded attempts per type, a displayed 87% to-feet chance produced 87.03% successes; a displayed 71% through-ball chance produced 70.63%. Tests use a reproducible seed and a six-standard-error binomial tolerance, and assert each result uses the exact displayed chance.
