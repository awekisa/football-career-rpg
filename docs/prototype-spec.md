# Football Career RPG — prototype contract

This document preserves the product decisions from the design interview. **Confirmed** requirements define scope; **proposed defaults** are implementation and balancing choices to validate during playtesting. Linear contains the implementation breakdown.

Repository: https://github.com/awekisa/football-career-rpg

Project: https://linear.app/smeagol-hermes/project/football-career-rpg-54f8537541e7

## 1. Vision and first slice

**Confirmed:** A single-player offline PC game in Unity. The user plays one footballer rather than managing a team. Use mouse and keyboard, 3D placeholders initially, an angled overhead camera and click-to-move navigation. The eventual career runs from young prospect through aging and retirement.

The prototype covers character creation and one authored week at a small club, ending with an 11-versus-11 match and aftermath. Skip the guided youth assessment for now. All positions, including goalkeeper, are supported. Build reusable components for seasons without implementing a season in this slice.

No multiplayer, console, mobile, romance, family, post-retirement management or detailed financial economy. Personal life focuses on friendships/rivalries. Track basic finances, wages and contract state. Transfers, extensive media/fan systems and more explorable clubs are later work.

## 2. Character creation

**Confirmed:** Name, young starting age, basic appearance, preferred foot, height, weight and position. Physical characteristics affect relevant football performance. Separate point budgets for football attributes and character traits. Explain each attribute and recommend role-appropriate choices without forcing allocation.

Support goalkeeper, centre-back, both full-backs/wing-backs, defensive/central/attacking midfield, wide midfield/wingers and striker. Positions are assignments, not permanent action locks. A player can play elsewhere and develop relevant skills through successful actions.

**Proposed defaults:** No separate position-familiarity system initially. Attribute groups include technical, tactical, physical, set pieces and goalkeeping. Examples: control, passing, dribbling, crossing, finishing, tackling, heading; positioning, marking, anticipation, vision, off-ball movement; pace, acceleration, agility, balance, strength, stamina, jumping; penalties, free kicks, corners; reflexes, handling, diving, one-on-ones, aerial command, distribution. Every shipped attribute needs a use.

Character examples: charisma, leadership, discipline, ambition, composure and baseline confidence. Mostly stable, these change slowly with repeated behaviour, not football training. Separate temporary confidence/morale. Charisma affects dialogue, leadership affects influence and opportunities, confidence affects performance. Users may choose behaviour inconsistent with traits.

## 3. Calendar and exploration

**Confirmed:** Freely explore a pitch/training ground and a small club with arrival/parking, coach office, teammate space, training entrance, agent meeting area and media room. Walking does not advance time. Activities/decisions do.

Calendar commitments include training, games, meetings, travel and rest. Attend, skip or replace commitments with contextual consequences. Extra practice occupies the next available slot and competes with other activities. Decisions can cause lateness, such as oversleeping after a late activity; navigation cannot.

Multiple strategies should be viable. Small clubs attract less media; coaches, clubs and fanbases react differently. Do not force every room or future social system into the first week.

**Proposed default:** Morning, afternoon and evening slots, allowing multi-slot activities. Display known costs before commitment and distinguish uncertain consequences.

## 4. Training and progression

**Confirmed:** Simulated scheduled training gives broad XP. Interactive practice uses coached match situations for more focused development. Discuss direction and choose focus. Neither mode relies on reflex execution.

Extra training consumes slots and adds fatigue, competing with rest/meetings. Coach, staff and facility quality affect development. Strong clubs offer better training but fewer opportunities for unestablished players; small clubs offer more playing time but weaker resources.

Every attribute level needs more XP than the last. Successful match actions generally develop skills faster than training. Failures remove relevant skill XP and can lower levels. Reliable choices enable steady growth; risky choices offer more upside and loss. Missing a penalty can also reduce confidence and future penalty responsibility.

Separate skill, confidence and coach effects. Attribute failures to the actual action: a good pass followed by a teammate error is not automatically a failed pass. Training provides a recovery route. Ordinary training never increases personality traits.

**Proposed defaults:** Configurable XP floors/caps, significance/difficulty modifiers and bounded repetition rewards. Tune against trivial farming and hopeless downward spirals while preserving skill regression.

## 5. Match flow

**Confirmed:** All 22 players move through AI, including the user's character. Continuous play pauses for a meaningful decision, particularly on possession, facing an opponent or influencing a threat. Choose, resolve, then resume.

Manual pause allows thinking, viewing opponents/stats/probabilities and changing valid intentions. No input timing, aiming precision or reflex execution affects success.

Automatic positioning follows role, tactics, ball, teammates, opponents and phase. Show the expected position/area. Persistent overrides include pressing, personal marking, zone holding, tracking, finding space, support, attacking, staying back and waiting for a counterattack. Show active intent and expiry/invalidation. Control only the user's footballer.

Click targets or areas for contextual actions: pass to feet, through ball, lofted pass, dribble, shoot, header, control, clearance, tackle, contain, block. All choices remain available regardless of attributes when physically/legalistically valid: no distant tackles or shots without possession.

Goalkeeper is a fully supported role with positioning, saves, rushing out, claims/punches and distribution, plus dedicated training and relevant defensive decisions.

## 6. Probabilities

**Confirmed:** Display the actual chance used by the resolver. Relevant factors include attributes, opponents, teammates, distance/angle, pressure, positioning, preferred foot, fatigue, confidence, morale, physical profile and ball circumstances.

A 90% chance is per attempt, not a guarantee of nine successes in ten. Define success per action: reaching a teammate, scoring, winning the ball, etc. A completed pass and a subsequent goal are separate. Freeze context while paused. Avoid hidden second rolls invalidating displayed chances. Failed passes may be intercepted, deflected, out of play or loose; animations represent the resolved outcome.

Later difficulty settings may hide percentages. The prototype shows them.

## 7. Viewing and football rules

**Confirmed:** Full-match normal-speed viewing and highlights share the same simulation, preserving fatigue, progression, rules and consequences. Highlights accelerate quiet periods and return before meaningful involvement or a dangerous attack the player can affect.

Relevance follows position and intent: keepers/centre-backs mainly defend, full-backs/midfielders often play both phases, forwards primarily attack. Set pieces and late coach instructions can change responsibilities.

Include goals, clock, halves, halftime, stoppage time, kickoffs, throw-ins, corners, goal kicks, free kicks, penalties, fouls, advantage, offside, yellow/second-yellow/red cards, substitutions, injuries and keeper handling restrictions. Offside considers ball, second-last opponent, timing, involvement and restart exemptions. Competition limits are configurable. Document the laws edition when implementing rules.

**Proposed default:** A league-style match permits a draw; defer extra time/shootouts. Playback speed controls may accelerate viewing without altering simulation logic.

## 8. Coach and relationships

**Confirmed:** Coach controls selection, roles, substitutions and set-piece duties; dialogue can influence them. The introductory weak club normally starts the young player. This favourable context does not override injury or severe conduct consequences. Stronger competitions later require earning opportunities.

Coaches have personalities and tactical preferences. Deviation is generally unwelcome; reactions consider context, result, relationship, trust and standing. Stars receive more freedom. Success may still anger a strict coach; trusted players may be forgiven creative failures. Coaches issue sideline instructions and expected positioning updates.

Separate affinity, professional trust and team standing. Include coach, teammate and agent relationships, friendship and rivalry. Dialogue has persistent attribute-dependent outcomes. Agents primarily deliver eventual opportunities; optional micromanagement later includes approaches, trials, changing agents, requests, negotiation and rejection. For now, the agent meeting records preferences and relationship effects.

## 9. Week and reports

**Confirmed:** Coach meeting, role/focus discussion, training, extra work/rest, teammate/agent interactions, attendance choices, selection, match and aftermath. Effects persist in skills, fatigue, confidence, relationships, standing and basic finances. Skipping or non-selection must not dead-end the week.

**Proposed schedule:** Monday arrival/coach/training; Tuesday practice/teammate; Wednesday team training/agent; Thursday tactics/extra work or recovery; Friday preparation/selection; Saturday fixture; Sunday recovery/review.

Reports explain causes and totals. Opening a report must not reapply rewards.

## 10. Technical contract

Separate character/career data, calendar, venues, training, relationships/dialogue, match simulation, tactics/AI, rules, resolution, progression, presentation and persistence. Use configurable content/balance data. Simulation time and randomness are independent of rendering/playback. Publish explicit activity/action/progression/instruction/relationship/selection/match events.

Seed randomness and log enough context for replay/debugging. Save between activities and at stable match decisions including clock, actors, ball, rules, intents and RNG state. Loading restores decisions without duplicated rewards or rerolling resolved events. Version saves and handle corruption safely.

## 11. Identities and assets

The desired future world uses recognizable football equivalents with altered identities. This is not established rights clearance. Use original replaceable placeholder clubs, players, badges and kits during development. Simulation must not depend on particular real clubs.

## 12. Acceptance and delivery

Milestones: foundation; first playable pass loop; complete football; development/training; career week; integrated verification. Internal increments do not remove requirements from the final prototype.

The integrated slice demonstrates creation for every position, meaningful physical/skill differences, free exploration, finite calendar activities, both training modes, 22-player football with the listed laws, role-sensitive decisions, overrides, exact probabilities, XP gains/losses, coach/relationship consequences, both viewing modes, normal introductory starting opportunities, reports and save/load.

Validate deterministic rule scenarios, statistical chance accuracy, all-position playthroughs and complete weeks following contrasting strategies. Playtesting assesses whether decisions are enjoyable; successful compilation alone is not finished gameplay.

Remaining tuning values: attribute scale, age range, point budgets, XP curves/penalties, fatigue/recovery, relationship thresholds, decision frequency, body modifiers, training quality and coach tolerance. Configure these rather than treating them as blockers.
