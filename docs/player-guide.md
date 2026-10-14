# Football Career RPG — start here

This is a single-player, one-week macOS prototype. You control one footballer’s choices; the simulation moves all 22 players and resolves actions from attributes and probability. There are no reflex challenges.

## Launch and create your player

Open **Football Career RPG.app** in this folder. Unity Editor is not required. This local build is not notarized for public distribution. Use a 1440 × 900 window or larger for the current placeholder interface. Some Mac keyboards require Fn with function keys; all essential choices also have on-screen buttons.

Choose **Start career** (Enter). Enter your name, choose any of the 14 positions (including goalkeeper), and spend the separate football and personality budgets. F1 and F2 distribute those pools evenly; F3 cycles position. Role recommendations guide your choices without locking actions. Choose **Join club** (Enter).

## Play your week

The calendar starts on Monday. Select a current appointment, choose Attend (1), then confirm the preview (Enter). Rest (5) followed by Enter advances a free slot. Read previews: skips, parties and extra practice can displace commitments or cause lateness. Walking and inspection spend no calendar time.

For exploration, Escape closes the calendar; **Explore club** (O) opens the venue. Click the ground or destination markers to walk. Interact after arriving; appointment availability depends on your calendar. Return to the week with Escape and continue the calendar with Enter.

Training can be simulated for broad gains or guided for focused drills. In the calendar, I changes training mode, F changes focus and D opens coaching guidance. Preview attendance, then confirm. In guided practice choose an action (1–3), inspect its percentage and commit with Enter. Enter advances to the next drill; finish three drills or end early with Escape. T shows training results.

Attend the Tuesday coach, Thursday teammate and Friday agent meetings. Choose a response (1–3) and confirm with Enter; dialogue percentages use personality attributes. Resolve or explicitly leave an attended meeting to continue time. Attend Saturday’s selection briefing and Sunday’s preparation to see your role and instructions. Attend Sunday’s fixture to open match day.

## Play the match

On match day, L toggles the 90-minute and four-minute demonstration fixture. Choose the four-minute option for a first walkthrough, then **Play / watch fixture** (Enter). Selection depends on readiness and coach trust; a balanced, rested prospect normally starts.

| Control | Action |
| --- | --- |
| Mouse / numbered action buttons | Choose an available football action |
| R / click teammate | Select a passing or distribution target |
| Enter | Commit the displayed action; continue notices, restarts and halftime |
| Space | Pause/resume between decisions |
| Tab / click player | Inspect player attributes and condition |
| I | Choose persistent off-ball instructions |
| H | Full-match / highlights viewing |
| M | Change playback speed |
| WASD, Q/E, wheel, Home | Pan, rotate, zoom, reset camera |
| Escape | Return to the week, retaining the fixture |

Read each contextual panel: goalkeeping, defending and set pieces offer different choices. The displayed success chance is used by the resolver, but success may mean an on-target shot or completed delivery rather than a goal. Failures have contextual outcomes. You may inspect and think while a decision is paused. Returning to and resuming the fixture preserves its state; Space resumes after returning.

At full time, return to the week to open the aftermath. Tab switches match/week scope; 1–4 switch readiness, skills, relationships and action sections. Click a row to inspect its causes. P reopens reports from the week overview. The prototype ends here.

## Save and load

Use the top-right **Save / Load** button, F5 to save, or F9 to load. There is **one manual slot**, plus a previous-valid-save backup. A new career does not automatically save. Saving again replaces the previous slot. Loading replaces the currently open career; quitting loses changes since your last save.

Pause the match or wait for a decision before saving. Finish or end guided practice before saving. A loaded fixture resumes paused; a completed week loads its report. Saves live in your macOS user Library under `Application Support/Football Career RPG/Football Career RPG/`. Keep the whole folder when backing up. Saves are local; compatibility with future prototype builds is not guaranteed.

Read **KNOWN-LIMITATIONS.md** for the implemented scope and unfinished systems. **BUILD.txt** identifies this build; **SHA256SUMS** lists its file checksums.
