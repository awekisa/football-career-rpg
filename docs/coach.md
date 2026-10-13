# Coach selection and reactions

SME-131 connects the career's coach and relationship ledger to the playable match. Open **Coach [T]** at the launch screen to inspect selection and request a start, another role, or penalty duties. In a career match, **T** or the coach button opens instructions and reaction history, pausing play. Closing the panel leaves play paused.

## Ownership and tuning

`CareerCoach` belongs to `CareerState`. Its profile uses the existing preferred formation, discipline and creativity preferences. Affinity, professional trust and standing remain in `CareerSocial`; attendance and match reactions write causal before/after records there. Coach reactions change trust/standing, not personal liking or personality traits. State is retained in career memory; disk persistence remains SME-135.

`CoachRules` configures club strength, established-star threshold (80), failed specialist-action priority penalty (18), and the separate persuasion seed. Default club strength uses staff quality as the prototype's existing club-level proxy; callers may override it independently. These are tuning defaults, not restrictions on future content.

## Selection and assignment

Selection compares role-attribute mean plus readiness, trust, standing, a small affinity contribution and any agreed start request against `25 + 30 × club strength`. Fatigue reduces the score. Injury excludes the player; fatigue at 85% or trust below 20 normally means the bench. The default healthy 17-year-old at Harbour starts. Stronger competition or weak role skills can move the same player to the bench. A healthy player may have to start if no fit named reserve exists.

The primary position is the initial assignment; a successful role request changes it without changing the character's primary position or skills. Selection then evaluates the requested role. Moving into or out of goalkeeper swaps duties with a teammate to retain exactly one keeper. Pre-match selection does not consume substitution windows. In-match changes continue through the existing legal stoppage, limit and window logic. The coach can remove the user for low trust or fatigue, and introduce a sufficiently fit, trusted bench player at halftime. Stars tolerate more fatigue before automatic removal (82% versus 72%). Manual scenario substitution requests remain available.

Normal **Play Match** and **Play as Goalkeeper** launches enable career coaching. The explicit **Start on Bench** scenario and lower-level match tests retain their authored line-ups through the default `StartFixture(..., applyCoachSelection: false)`. This keeps isolated football scenarios reproducible. The career launch passes `true`.

## Requests

Requests preview the exact integer success probability, clamped to 5–95%. Charisma influences start/role requests; leadership influences penalty requests. Trust, standing, affinity and coach strictness also contribute. One independent seeded roll commits each request. Success changes the requested assignment; failure leaves it unchanged. Neither result advances calendar time. One request per calendar slot prevents retrying for a favorable roll; duplicates of the committed preview return the original result. Foreign or stale previews are rejected before drawing. Requests are unavailable during a match, unfinished practice or after the week ends. Agreements never override injury/readiness or substitution rules.

## Tactical instructions and outcomes

The formation preference is exposed as context; this ticket does not rebuild the whole team's formation. The user's automatic movement follows role-aware instructions. Strict coaches prefer holding the defensive zone; creative coaches prefer pressing. In possession, defenders hold, other roles support or find space, and a late second-half deficit encourages forward runs. Keepers retain the existing keeper positioning. Explicit user intentions still override movement.

A departure from the current instruction or set-piece duty opens one bounded reaction window. Repeated clicks cannot create additional reactions during that 20-second window. The next user action or team goal supplies its outcome; otherwise expiry uses whether the user's team has possession, while removal/abandonment is unsuccessful. This is a prototype attribution heuristic, not full counterfactual tactical analysis. A deviation's penalty considers strictness, creativity, established standing, professional trust, personal affinity, outcome, defensive danger and a late-goal chase. Trust scales the penalty from 75–125% and liking adds a smaller 90–110% modifier, independently of standing. Successful deviations are less costly and can improve standing; stars receive more latitude. Reactions have stable cause IDs and apply once. The reaction text identifies defensive danger, developing-player responsibility versus established-star latitude, and whether the coach prioritizes discipline or creativity; the report records the actual trust/standing changes.

Set-piece duties retain the authored role plan and existing user choices. Specialist takers now rank by relevant skill, configured team priority, standing and prior responsibility penalties among active players. A successful penalty-duty request promotes the player. Committed failed penalties/corners/free kicks reduce subsequent priority; success gradually restores it. A miss also cancels the penalty-duty promise. Restart replay does not repeat the penalty.

Required training, travel and match attendance also affects professional trust. Attending gives a small increase; lateness and skipping incur larger penalties with stricter coaches. Inspecting screens or walking through the club has no effect.

## Verification

`CareerCoachTests` covers weak/strong clubs, ability/readiness/conduct, strict/creative and prospect/star comparisons, causal idempotent reactions, exact persuasion odds, stale/foreign requests, requested positions including goalkeeper, legal substitutions, actual restart outcomes, automatic/overridden movement and shared presentation state. The social friendship/trust test uses a teammate so independent coach attendance consequences do not obscure its original assertion.

The packaged smoke check includes the coach screen's preview/confirm path and actual career fixture selection (`FOOTBALL_RPG_COACH_PASSED`). Rendered layout and physical mouse verification must be performed on an unlocked desktop.
