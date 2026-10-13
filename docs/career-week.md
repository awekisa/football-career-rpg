# Playable career week (SME-133)

Start career on the launch screen, allocate the separate football/personality budgets, choose a position and join the club. This begins a fresh week at the small club. F1/F2 distribute points evenly; every supported position is available, including goalkeeper. The sample calendar and positional match demos on the original launch screen remain inspection tools independent of this route.

## Authored route

- Monday: team training, optional extra practice/rest, optional local media.
- Tuesday: passing training, free time, coach development meeting.
- Wednesday: team travel, free time.
- Thursday: fitness training, free time, optional teammate conversation.
- Friday: training and optional agent meeting.
- Saturday: set-piece training, free time, required squad selection briefing in the night slot.
- Sunday: required match preparation in the morning, fixture in the afternoon/evening. Full time ends this prototype week; the remaining night slot is not another progression opportunity.

Calendar inspection and walking remain free. Confirmed attendance/skip/replacement choices spend slots and expose conflicts before committing. Extra practice takes the next free slot immediately after training; guided practice can be completed or ended. Meetings can be resolved or explicitly left without refund. The existing workload, morale, training XP and relationship consequences remain authoritative. Selection and preparation attendance now also affect coach trust like other required football commitments.

The selection briefing records the coach's current projection, visible in the calendar and week overview. Match preparation explains the assigned role and attacking/defending instructions. Skipping either still permits the fixture. Selection is recalculated at kickoff using the player's latest readiness and relationships. A balanced prospect following the normal route starts at this club; low ability, poor trust or fatigue can mean a bench place, and injury can prevent selection.

## Fixture handoff

Confirm attendance at Sunday's fixture to reach the match-day screen. Choose the full 90-minute fixture or the existing four-minute tuning/demo duration, then play. The pitch also retains full-match/highlight viewing controls (H), playback speed (M), and pause/inspection (Space). Decisions use the player's canonical attributes and condition; coach instructions, role requests, action progression and tactical reactions continue through the existing match systems.

Late arrival starts an otherwise-selected player on the bench. Skipping the fixture, or replacing its whole window with other activities, records the usual attendance consequences and lets the club play without that player. The match-day screen still opens so the user can watch the result and complete the week. Missed players cannot be introduced from the bench. Injury and bench routes use normal legal replacement/substitution rules.

Only one fixture exists per active career week. Calendar progression and new social/coach requests stop once its attendance is resolved. Back to week/Escape pauses and detaches the pitch view, retaining the same world, clock, pending decision, randomness, score and progression. Resume fixture reattaches a view; Space resumes play. It cannot start another match or re-award outcomes. Full time or abandonment records a basic score/selection/participation summary and completes the week.

Use F5 to save and F9 to load the career, including the active paused fixture or completed reports. Quitting discards changes made since the last manual save. See [career saves](career-saves.md). No season progression, transfers or financial economy is introduced.

## Verification routes

1. Start career, distribute both budgets, choose CM (repeat with GK), join. Attend every appointment, choose the first meeting option, and rest in free slots. Check Saturday selection, Sunday's tactical plan, then play to full time. Confirm the created name/role, coach instructions and readiness on the pitch and the week-complete summary afterward.
2. Start another career. Attend Monday training, take extra practice, then skip later training and rest elsewhere. Compare training XP and coach trust with route 1. Attend and finish the fixture.
3. Party in Sunday's morning slot: preview preparation being missed and a 60-minute late arrival to the fixture. Attend late and verify bench selection. Alternatively skip the whole fixture or rest through both slots; watch the match without being selected.
4. Return from an unfinished match. Inspect the week and resume; verify the same score, time, pending choice and player state. Calendar actions cannot advance the week during this match. Finish it and confirm no replay is available.
5. In automated scenarios, injure or exhaust the player after the selection briefing; confirm kickoff updates selection and the week still completes. Guided-practice early exit and leaving a meeting also retain a path to full time.

EditMode coverage includes the complete presentation handoff and resumed pitch ownership. Two contrasting routes run four-minute fixtures; short fixtures cover CM/GK/ST/CB, missed/late/injured/fatigued routes and guards. The packaged smoke check exercises real creation, calendar/meeting presentation commands, starting selection, leaving/resuming the pitch and full time (`FOOTBALL_RPG_CAREER_WEEK_PASSED`). These checks do not substitute for rendered layout or physical mouse testing.

After returning from the completed fixture, the match aftermath opens automatically. Tab switches between match and whole-week scopes; P or the reports button reopens them from the week overview. See [reports](career-reports.md).
