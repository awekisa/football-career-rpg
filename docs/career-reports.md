# Match aftermath and week reports (SME-134)

After completing or abandoning the career fixture, return from the pitch to open **Match aftermath**. Tab switches to **Week completion**. Keys 1–4 or the tabs select readiness, skills, relationships, and actions/statistics. Click a metric to inspect its contributing causes; click an action to see its chance, resolved outcome, target and associated career changes. Escape returns to the week overview; P or the reports button reopens it. Before completion, the screen explains that no report is available.

## Scope and ownership

`CareerReports` belongs to the same `CareerState` as the week and is independent of the pitch view. It captures two baselines:

- Week: when the new career week begins, before any calendar decisions.
- Match: immediately before the career match is constructed, after fixture attendance consequences.

Once full time or abandonment has applied readiness, match result and coach consequences, it freezes both reports. Reports remain available throughout that career session, even after closing the pitch, and reopening them never recaptures state or applies rewards. Starting another career creates a separate report owner. Disk serialization and restoration remain SME-135; exiting the app still loses the session.

## Evidence shown

The score and selection/participation are captured alongside the created player's name and assigned role. User-only match diagnostics supply action attempts, successful/failed checks and individual outcomes. All 14 positions use the same ledger; goalkeeper defaults include saves, claims and distribution, while outfield defaults include passing, shooting, headers, crosses, dribbles, defensive checks and clearances. Any additional performed action is included regardless of position. Absent/unused players have explicit empty action records and zero attempts. Relevant substitution/injury events are listed.

Statistics count the actual resolver checks. A successful shooting check is not presented as a personal goal or assist: score comes from the fixture result. Individual action timestamps are elapsed simulation time, including stoppages, rather than an invented official clock. Variant and outcome distinguish different actions within a family.

The report displays separate gross XP gained, gross XP lost and net XP. Per-skill totals and levels show their baseline/final values and exact changes. Training rows reference attended sessions, focus and participation; match rows use actual applied XP after repetition limits and skill bounds. `SkillXpChange` retains its per-skill before/after levels so each gain or loss can be traced even when later actions offset it.

Readiness uses the existing fatigue, confidence, morale and injury ledgers. Relationship rows show each contact's affinity, professional trust and standing, including coach trust. Cause descriptions come from attendance, dialogue, authored meetings and coach reaction records. Zero totals can contain offsetting consequences, and the detail view retains them. No probabilities are rerolled.

Balance and weekly wage are copied in minor currency units and formatted as money. Both remain unchanged because this prototype has no payment/spending engine. The screen explicitly says that a contract wage is not automatically credited; reports do not introduce financial transactions.

## Verification

- Report deltas are reconciled to source cause sums for XP, levels, readiness and relationships across all positions.
- Training/meeting/attendance effects appear in the whole-week scope but not the match scope.
- Controlled successful/failed facts verify exact XP, level losses and goalkeeper/outfield outcome records, including duplicate application protection.
- Real twenty-minute seeded fixtures verify goalkeeper and outfield resolver statistics against diagnostics.
- Absent players retain zero actions and unchanged finances. Reports are unavailable before completion.
- Repeated reads, screen reopening and duplicate fixture-start requests do not change XP, readiness, random draws or report identity. Completed snapshots do not change if live career values change afterward.
- The packaged `FOOTBALL_RPG_REPORTS_PASSED` check runs the real creation/calendar/fixture presentation path, verifies automatic aftermath opening and repeatedly switches report scopes without rewards.

Headless tests cover behavior, not rendered layout or physical mouse input.
