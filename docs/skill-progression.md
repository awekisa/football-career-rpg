# Match skill progression

A career owns football skill levels and fractional XP. Each real fixture creates a new progression scope; practice previews do not award match XP. Career values persist when returning to launch and playing another fixture. Disk saves and calendar scheduling are separate tickets.

## Levels and configuration

Football attribute definitions provide each skill's floor and cap. `ProgressionRules`, optionally passed to `CareerState.Create`, provides tuning defaults:

- Cost to advance from level L: `100 + 10 × (L − floor)` XP.
- Cumulative threshold: `100 × n + 10 × n × (n − 1) / 2`, where `n = L − floor`.
- Successful action: `20 × (1 + 3 × (1 − displayed chance)) × significance` XP.
- Failed action: `−12 × (0.5 + displayed chance) × significance` XP.
- Repetition scale 5 per player/action family per fixture: both gains and losses multiply by `(5 / 6)^priorAttempts`. Attempts count even if they fail. Each failure still lowers XP (unless already at the skill floor), while repeated exposure gradually diminishes for both outcomes. Changing targets cannot reset it. The infinite sum is bounded to six first-attempt magnitudes before the existing skill caps.
- At most 200 positive XP per player/skill per fixture across all action families. Losing XP does not restore this allowance.

Existing and newly created levels begin at their cumulative threshold, with zero progress toward the next level. A failure can therefore immediately lower a starting level. Signed fractional XP crosses any number of levels and clamps at the configured floor/cap without hidden excess credit or debt. Future action snapshots read updated levels; already offered decisions retain their original probabilities.

## Attribution and significance

Pass, football action, defensive and goalkeeper resolvers supply weighted skill attribution. Only the actor receives this development; a later receiver failure affects the receiver's control skill, not the passer. Penalties, corners and free kicks override ordinary action attribution with 80% specialist skill and 20% decisions.

Forward passes advancing more than eight metres use 1.5 significance; other passes .5. Shots and headed shots use 2; other football actions use 1. Defensive actions in the player's defensive end (longitudinal position below −25 metres in their attack direction) use 1.5, otherwise 1. Keeper saves/interventions use 2, distribution .75, release to feet and setting position .5. Penalties use 3, corners/free kicks 1.5, other restarts .5. Deliberately illegal keeper handling earns no skill XP.

Success means the resolver's displayed action success criterion, not the team's eventual outcome: a shot on target earns shooting XP even when saved. Later goals, deflections, receiver errors, animation completion and referee notices do not reward or punish that action again. Confidence, morale, personality and relationships are untouched by this subsystem.

For the first attempt before caps, a 90% short safe pass at significance .5 has about +10.86 expected XP. A 40% high-impact action at significance 2 has about +31.84 expected XP, with a larger loss when it fails. These are tuning examples, not an equilibrium guarantee; repeated farming rapidly loses its reward and still carries a proportionately reduced failure cost. See [balance evidence](balance.md) for measured whole-match outcomes.

## Integration and inspection

Committed world actions apply development exactly once using a career match scope and action event ID. This avoids collisions when the presentation reuses its match session name. Changing targets or variants cannot reset action-family repetition limits, and alternating families cannot exceed the per-skill gain cap. A genuinely new fixture has fresh limits; the prototype allows replaying fixtures until the calendar is integrated.

Pitch inspection shows XP within each recommended skill level and the latest action's total actual XP change, including zero at a floor/cap or repetition limit. Levels remain visible beside their bars. The career ledger exposes changes and per-skill totals for future reports and saving.

Verification includes weighted gains, multi-level gains/losses, configured floors/caps, immutable pre-action snapshots, duplicate events, receiver-only failure, target-switch and action-family farming, fixture scope isolation, expected risk/reward, invalid input atomicity, specialist set-piece attribution and no match XP in practice. Full/fast/highlight replays compare all player levels, XP totals and attributed deltas across all 14 positions. The packaged smoke suite checks match-action coverage and development equivalence in multiple position families.
