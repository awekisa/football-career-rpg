# Pitch view and decisions (SME-110–113)

Open Launch.unity and press Play, then choose Explore the pitch or press Enter. The standalone app also accepts --pitch-preview. The opening scenario starts with your player in possession and a frozen pass decision. Select a teammate and commit a pass to start play. A lime area marks the user’s current tactical target.

## Controls

| Action | Input |
| --- | --- |
| Inspect a player | Left-click their placeholder |
| Cycle selection | Tab / Shift+Tab, or previous/next buttons |
| Switch team | Home team / Away team buttons |
| Pan | WASD or right mouse drag |
| Rotate | Q / E |
| Zoom | Mouse wheel over pitch, or +/- buttons |
| Reset camera | Home or Reset view |
| Choose pass recipient | Click a teammate, or R / Next teammate |
| Choose pass type | 1 for feet, 2 for through ball, or action buttons |
| Other contextual actions / variants | Lower row, keys 3–7; repeat to cycle variant |
| Commit the chosen action | Enter or Play action |
| Pause/resume clock between decisions | Space or clock button |
| Return to menu | Escape or Back to menu |

Blue shirts identify the home team, coral shirts the visitors, and yellow shirts the goalkeepers. Number labels distinguish individual players and retain their team colour. A permanent lime ring and YOU label identify the user; a separate white ring follows inspection selection. The sidebar shows club, role, biographical details, four role-relevant football attributes, confidence and fatigue, from canonical CareerState data. When selecting a different teammate of the current ball carrier, its footer compares exact whole-percent pass-to-feet and through-ball estimates. The decision panel uses frozen options and shows the selected recipient. A cyan line and target ring preview the chosen pass. Inspecting an opponent leaves that selection intact. Attribute bars use configured definition ranges.

## Ownership and limits

One Unity unit is one metre. PitchGeometry draws a 105 by 68 metre field, markings, 7.32 by 2.44 metre goals and a ball. OpenPlayWorld places the first eleven fixture squad entries using role anchors and updates movement on fixed ticks. This is a simple role-based formation, not a coach lineup selection system. Passing, pressure and loose-ball recovery sustain open play; possession decisions let the user choose passes; SME-114 adds shots, dribbles, crosses, receiving and headers; SME-115 adds defensive pauses and off-ball instructions; full match rules are later tickets. See [open play](open-play.md).

The preview reuses LaunchScreen.Career and reads player records without editing them. Its separate MatchSession uses the SME-109 fixed clock; pausing stops clock ticks, while camera and inspection continue using presentation input. Neither selection nor camera movement consumes match randomness. Closing releases the preview and restores the launch camera.

Material templates live in Content/Resources so Standard and Unlit/Color shaders survive standalone packaging. The explicit Football RPG material creation editor command creates missing templates only; normal imports do not overwrite authored assets.

## Verification

The EditMode suite includes pitch dimensions and lineup identity, mouse-ray selection of all 22 players in running and paused states, unchanged career/clock/random state during inspection, camera bounds/reset, invalid selection and closing the preview. The macOS build is also checked using --pitch-smoke-test; this verifies packaged content, all selection rays and clock pause/resume AI movement/passing and the player-decision commit/pause/flight path without graphics. Visual checks cover field layout, the initial user inspector and keyboard camera/selection controls. Automated native mouse input did not register with the Unity window, so direct manual mouse interaction remains a check for a human tester; ray selection is covered by automated tests.

Off the ball, open **Off-ball instruction [I]** to set movement intent. For marking/tracking, inspect an opposing player first. Defensive prompts use **1 / 2 / 3** for tackle / contain / block and **Enter** to commit. See [defense and intentions](defense-and-intentions.md).

Use **Play as goalkeeper [G]** on the launch screen for the keeper. Numbered options cover goalkeeper responses and distribution. **R** cycles a recipient and **F** switches to ordinary football actions while the ball is at feet. Hand-held balls must be distributed or dropped first.

## Fixture controls (SME-117)

The launch starts a full fixture. L switches between 90-minute and four-minute matches before entry. A user restart pauses for teammate selection (click or R) and release (Enter). Halftime waits for Enter/Space and switches ends. Full time stops play; Escape returns to the launch. The header shows the half, score and added time. See [fixture rules](fixture-rules.md).

## Offside explanation (SME-118)

A fixture pauses when offside is called. The panel names the offender, explains the active involvement and identifies IFAB 2026/27. Player inspection remains available. Enter/Continue acknowledges the notice; the awarded taker then uses the normal restart recipient controls. Space cannot skip the explanation.
