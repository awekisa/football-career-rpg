# Relationships and personality checks (SME-130)

Open **Relationships [R]** from the launch screen. Choose the coach, a teammate or your agent from the contact list (**Tab** cycles contacts). Select an exchange with **1–4** or the mouse, inspect the actual success chance and both sets of consequences, then confirm with **Enter**. Inspection and reopening the screen never apply an outcome. The last exchange and its relationship/behaviour changes remain visible after returning to the screen.

This component supplies brief prototype conversations/messages and the shared social state. Each contact has one exchange per calendar slot; these brief exchanges do not themselves consume a slot. The separate authored coach/teammate/agent meeting and dialogue flows remain SME-132. Venue availability and attendance commitments are unchanged. Finishing guided practice is required before another exchange; no new exchanges are allowed after the prototype week ends.

## Independent dimensions

Each relationship has personal affinity (-100 to 100), professional trust (0 to 100) and standing (0 to 100). Initial values are 0/50/50. Affinity at least 30 is labelled Friendship and at most -30 is Rivalry; these labels do not alter trust or standing. The threshold is configurable. Contacts include the user's club coach, every other squad player and a stable user-specific placeholder agent. A newly created career owns fresh independent relationships.

Four brief choices demonstrate the mechanisms:

| Choice | Check | Successful effect: affinity / trust / standing | Failed effect | Repeated behaviour |
|---|---|---|---|---|
| Show personal interest | Charisma, difficulty 45 | +12 / 0 / 0 | -3 / 0 / 0 | Charisma + |
| Offer practical help | Discipline, difficulty 50 | +1 / +10 / +3 | 0 / -4 / -2 | Discipline + |
| Challenge their view openly | Leadership, difficulty 55 | -10 / +2 / +8 | -12 / -5 / -4 | Leadership + |
| Make an excuse to avoid responsibility | Charisma, difficulty 55 | +4 / -10 / -4 | -4 / -12 / -8 | Discipline - |

All choices remain available regardless of trait values. A highly disciplined player can make excuses; a low-charisma player can attempt a warm conversation. Success can improve liking while reducing trust, or improve standing while reducing liking. These outcomes do not automatically grant selection, duties, transfers or tactical freedom; the coach component will consume the shared relationship state.

## Check calculation and commitment

`chance = clamp(0.5 + (trait - difficulty) × 0.006, 0.05, 0.95)`

The displayed integer percentage is rounded away from zero at midpoints; that exact percentage divided by 100 is used for one seeded random roll. `SocialRules` configures attribute weight and seed. Social randomness is independent of the fixture and practice streams. Preview freezes the choice, trait value, slot and social revision. Any intervening social outcome or slot change invalidates uncommitted previews. Identical repeated commits return the original result without a reroll; a different choice for a completed exchange is rejected. Contacts and choices are validated before mutation.

`CareerSocial` belongs to `CareerState`. It owns read-only contact views, the dialogue result ledger and `RelationshipChanged` events, each with the exact cause, dimension and before/after value. Effects clamp at the relevant bounds; zero actual deltas do not create misleading change events. Reports and UI are consumers of the ledger.

## Slow personality development

The action selected supplies signed behaviour evidence whether its dialogue check succeeds or fails. The first observation for a given trait in a calendar slot counts; talking to additional contacts in that slot cannot farm that trait. Eight net observations in separate slots change the trait by one point, then reset the evidence. Opposing behaviour cancels pending evidence. Threshold is configurable through `SocialRules` (minimum two); trait bounds come from the character definitions. Evidence resets on reaching the threshold even at a trait cap, preventing stored progress from bypassing bounds.

Every result records whether its behaviour counted, remaining evidence and the trait value before/after. Only this behaviour owner changes the underlying personality dictionary; callers retain read-only views. Ordinary simulated/guided training and competitive match XP have no personality hooks. Confidence, morale and fatigue remain separate temporary state.

All social state currently persists for the lifetime of the career, including across UI reopening. Disk persistence, richer authored exchanges and long-term career integration remain subsequent tickets. Tuning values are prototype defaults.

## Verification

Tests cover all contact roles, independent dimensions, high/low charisma with the same seeded roll, friendship/rivalry, contradictory choices, clamping and causal records, slow positive/negative development, cancellation, evidence limits across multiple contacts, trait caps, training isolation, stale/foreign/duplicate commits, pending practice, week end, invalid configuration and reopening the presentation. Packaged smoke exercises preview/confirmation through the real screen adapter for coach, teammate and agent, and emits `FOOTBALL_RPG_SOCIAL_PASSED`.
