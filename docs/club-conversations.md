# Authored club conversations

SME-132 adds three appointment conversations to the existing week. Open **Conversations [N]** to inspect appointments, saved outcomes, training focus, career priority, weekly wage and balance. Attend or skip through the calendar, or walk to the contact during their appointment to open the same calendar preview. Walking and inspection never consume time.

| Appointment | Slot | Purpose |
| --- | --- | --- |
| Coach development meeting | Tuesday evening, slot 6 | Explain role/expectations and agree a training direction |
| Teammate conversation | Thursday evening, slot 14 | Build support, rivalry or professional cooperation |
| Existing agent meeting | Friday afternoon, slot 17 | Record playing-time, development or competition priority |

Existing training, travel, media and match commitments remain in their original slots. The coach appointment is required; teammate and agent meetings are optional commitments with explicit social consequences when skipped. Optional does not mean consequence-free.

## Calendar and conversation state

Confirming attendance pays the calendar slot once and applies its configured attendance effect. A late arrival pays only the remaining appointment time and applies the late effect instead. The meeting then pauses further calendar choices until a response is committed or the player explicitly leaves without agreement. Leaving has a visible relationship cost, preserves existing plans, uses no random draw and does not refund time. Skipping, waiting through, or replacing the appointment records a missed-meeting result without inventing a conversation or preferences.

The conversation's three choices show an exact integer response probability from the relevant personality trait. One independent seeded draw uses that probability. High and low traits can produce different responses with the same roll. The response changes affinity, professional trust and standing independently; the preview shows both outcomes. A stated focus or agent priority is recorded even if the contact responds negatively. This is the player's decision, not a skill unlock. Ordinary conversation choices here do not award football XP or directly change personality levels.

Immutable previews carry their owner and context. Foreign/stale previews and alternate replays are rejected before mutation; replaying the committed choice returns its original result. Calendar attendance has its own stable fact ID, so repeated confirmation cannot spend time or apply an attendance effect twice. Pending conversations also prevent new brief social exchanges or coach persuasion requests.

## Persistent choices

The coach explains the currently assigned role, selection expectations, personality preferences and recommended guided-practice family. The player can accept role-focused training, choose any available training focus, or reject a plan. An agreed focus becomes the default for subsequent training previews and rewards. Role-focused plans follow the coach's assigned position, including a goalkeeper assignment even when the player's original position was outfield. Explicit later training-focus selections override the default for that activity; guided practice still follows the action family chosen by the player.

The teammate is a stable senior squad contact selected from the user's existing relationship contacts. Support and rivalry branches can cross friendship/rivalry thresholds; professional trust can rise despite rivalry. Existing relationship values still determine the final label.

The agent records one of **PlayingTime**, **Development** or **Challenge**. That preference and the agent relationship are saved in career memory for later opportunity systems. No offers, transfers, negotiation, wage payments or purchases are simulated. Contract currency, wage and balance are read from the existing player state and displayed without mutation.

## Content and integration

Authored dialogue and attendance/branch effects live in `GameDefinitions.meetings` in `PrototypeFixture.json`. Definitions reference non-overlapping calendar appointments, supported social roles, trait IDs and training focuses; invalid references, duplicate choices, invalid effects and overlapping appointments fail validation. Runtime meetings copy the content and resolve the appropriate coach, teammate and agent for the current player.

`CareerMeetings` owns pending/resolved conversation state and the separate seeded stream (default 7132, injectable through career creation). `CareerCalendar` owns paid time and the preferred training focus. `CareerSocial` remains the single relationship owner; authored outcomes add causal before/after records there. A completed meeting remains readable on reopening. Persistence is in memory for this ticket; disk save/restore remains SME-135. The integrated week and final fixture routing remain SME-133.

## Verification

`CareerMeetingsTests` verifies contrasting coach/teammate/agent branches, high/low traits with the exact displayed probability, default and overridden training focus, assigned-role training, skipped/late/abandoned meetings, pending-state protection, duplicate/foreign/stale choices, venue/calendar handoff, content validation, contract immutability and presentation reopening. The packaged smoke check completes all three conversations through their presentation preview/confirm methods and checks saved focus and priority (`FOOTBALL_RPG_MEETINGS_PASSED`).

Manual walkthrough: use N to inspect the appointments; advance to Tuesday evening and attend the coach meeting; select a focus with F, choose a response with 1–3 and confirm with Enter. Inspect the next training preview. Attend Thursday's teammate and Friday's agent meetings to compare branches. Restart the career to compare skipped meetings. Check contract values and saved outcomes when reopening N. Rendered layout and physical mouse flow require an unlocked desktop.
