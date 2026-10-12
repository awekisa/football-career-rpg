# Playable set pieces — SME-120

Every active player can take part in restart decisions. Penalties, free kicks, corners, goal kicks and throws use the existing match clock, probabilities, offside and referee rules. Kickoffs retain their simple release flow.

## Assignments and choices

Each club optionally authors a `setPieces` plan containing ordered penalty, free-kick and corner takers, plus attacking/defending duties by position. Plans are validated against the club squad and position/duty catalog and copied into immutable career data. The two prototype clubs include plans. Missing role entries use defaults for every supported position. Unavailable takers are skipped, then the existing context/attribute fallback applies. An explicitly assigned goalkeeper may take a kick; taking is not locked by attributes or primary position.

At setup, each active player receives a coach-linked assignment. Keepers normally protect their goal, attacking centre backs and forwards attack the delivery, defending centre backs mark, and forwards may wait for a counter. The non-taking user pauses to select **follow zone**, **mark player**, **attack space** or **await counter**. The default is the coach's assignment. Marking requires an active opponent. Departures from the duty or assigned opponent are recorded rather than forbidden.

Setup positions are constrained by restart distance and area rules; even a keeper choosing to run forward must remain on the line until a defending penalty is kicked. Once released, duties influence movement until first receipt/contest/outcome, a new stoppage, or eight match seconds. Targeted recipients still move to the delivery, and keepers following their zone retain responsive keeper movement. No wall-clock time or random draw is spent on choosing a duty.

## Taking and contests

Takers can select a teammate and pass. Corners and free kicks additionally offer low and lofted deliveries. Direct free kicks and penalties offer placed/power shots. The displayed probability comes directly from the shared pass/cross/shot preview and is used by the committed action. Accuracy or reaching the target is distinct from a subsequent save or aerial contest.

A restart context permits the shared crossing model to deliver from a central free kick; it does not introduce a second probability formula. AI uses the same release paths. A reachable defending marker can contest an aerial delivery through the ordinary incoming-ball header/clearance choices. The usual keeper claim/punch/save path takes precedence where reachable. Direct corner/throw/goal-kick offside exemptions and indirect-kick goal restrictions remain enforced.

The current model uses capsule positions and ordinary shot variants. It does not add animated routines, new directional penalty aiming, encroachment animations, or attribute-based unlocks.

## Career-facing records

`SetPieceRecords` is a read-only stream of immutable records with restart, coach and player IDs, tick, choice, deviation flag and optional action event ID/outcome:

- `Assigned`: the planned duty or taker.
- `DutyChosen`: the user's actual duty/opponent, including whether it departs from the plan.
- `DutyOverridden`: a later off-ball command replaces the temporary set-piece duty.
- `TakingResult`: the committed pass/delivery/shot result, linked to its probability diagnostic.
- `DeliveryOutcome`: first receipt, contest, keeper intervention, offside or boundary consequence. A contested delivery has an unresolved (`null`) success flag, not an automatic failure. Boundary outcomes respect direct-goal restrictions.

Repeated identical commits return the original result without rerolling or duplicating records; conflicting commits are rejected. Records survive later restarts. Actual relationship changes and taker-responsibility progression remain for their career tickets.

## Controls and verification

Non-takers: 1–4 select zone, mark, attack or counter; R cycles the marked opponent; Enter confirms. Takers: 1 pass, 2 low delivery, 3 lofted delivery, 4 shot/cycle shot variant; R cycles recipients; Enter releases. Inapplicable choices are disabled. Referee/offside notices are acknowledged first. Inspection remains available during every pause.

324 EditMode tests pass (29 new cases). The test suite covers assignments, data copying/validation, dismissed-taker fallback, all position defaults, attacking/defending goalkeeper/defender/forward choices, legal setup, actual header commits, displayed/committed probabilities, offside exemptions, penalty variants, direct-goal outcome records, and presentation choices. The Mac build and packaged verification pass, exercising live restart choices as goalkeeper, defender and forward alongside all previous gameplay smoke checks. Manual Mac verification covered the duty panel, changing to marking, cycling an opponent without advancing the clock, confirmation and resumed restart setup. Taker delivery/shot panels and aerial choices are covered by integration tests; they were not manually walked through. Windows was not built.

Sources: [IFAB Law 13](https://www.theifab.com/laws/latest/free-kicks/), [Law 14](https://www.theifab.com/laws/latest/the-penalty-kick/) and [Law 17](https://www.theifab.com/laws/latest/the-corner-kick/), implemented alongside the existing 2026/27 fixture/offside rules. The eight-second duty lifetime is a gameplay tuning value, not a law.
