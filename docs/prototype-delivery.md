# macOS prototype delivery (SME-138)

The first delivery platform is macOS, matching the development and play-test host. [Player guide](player-guide.md) and [known limitations](known-limitations.md) accompany the packaged application.

## Reproduce the package

1. Install the pinned Unity version in ProjectSettings/ProjectVersion.txt with Mac Build Support and a valid editor license. Close this project's editor.
2. Commit source changes so the working tree is clean.
3. Run `./scripts/package-mac.sh` from the repository root. Set `UNITY_EDITOR` if the editor executable is elsewhere.
4. The command prints a new ZIP, player folder and build log under ignored `Builds/packages/`. It creates a non-development application with a manifest containing the exact source revision, Unity version and executable architecture. SHA256SUMS covers all regular files in the player folder; a sibling checksum identifies the ZIP. No saves or editor caches are packaged.
5. Extract the ZIP into a new directory. From its player folder, run `shasum -a 256 -c SHA256SUMS`, then open the app and follow START-HERE.md. Keep extracted content together. `./scripts/unity.sh build-mac` still produces the separate development build.

## Acceptance checklist

Use an extracted package, not Editor Play mode. Preserve any existing local career slot before testing; use a clearly named test player and restore the previous slot afterward.

- Create a fresh CM with both budgets, enter the week and explore the club using the mouse. Check walking does not spend time.
- Complete guided training, simulated training and all three scheduled meetings. Inspect results and probabilities. Rest in free slots; attend selection and preparation.
- Attend and play the four-minute 11v11 fixture. Check player inspection, pause, readable action probabilities, mouse commitment and keyboard commitment. Continue through restarts and halftime to full time without deadlock.
- Open match and week reports; save the completed career, quit, relaunch and load. Confirm the same player, score and completed report without replaying rewards.
- Run the extracted app with `--pitch-smoke-test -logFile /absolute/path/smoke.log`; require exit 0 and FOOTBALL_RPG_SMOKE_PASSED. This is automated integration evidence, separate from desktop observations.

Evidence for this delivery is recorded below when checks complete. Existing rule and role coverage is documented in [football verification](football-verification.md) and [balance](balance.md).

## Delivery evidence — 13 October 2026

**Passed.** Built from clean source `49d8676cb1ed664b421175e3a4a9d50bed2e6e7d` with Unity 6000.3.23f1, non-development configuration. Subsequent changes in this PR only record this evidence. Tested on macOS 26.5, arm64; the executable contains arm64 and x86_64 slices, but Intel hardware was not tested.

Local archive: `Builds/packages/football-rpg-mac-49d8676c-j7e8ZK.zip` (approximately 40 MB). SHA-256: `c825fc93e424783e7687772037feca2e1c5ede9b9ab8cd386c6858cb18528bea`.

Extracted afresh into `Builds/packages/acceptance-49d8676/player-49d8676c-AXnaqf/`. Every regular-file checksum passed with `shasum -a 256 -c SHA256SUMS`. The app was launched from this extracted copy for both checks below. Build/checksum logs and the test save are retained locally under `TestResults/delivery-49d8676/`; binary artifacts and generated logs are intentionally excluded from Git. This is local delivery, not a published GitHub release.

### Direct desktop acceptance

Performed through the rendered standalone app with keyboard input and the user-authorized native mouse helper, verifying screenshots after actions. No Editor Play mode or injected game state was used.

| Check | Observed result |
| --- | --- |
| Creation | Created **Delivery Test CM**, default physical profile, balanced 1200 football and 300 personality budgets. Mouse allocation/Join buttons worked. Keyboard shortcuts are suppressed while the name text field has focus; use the buttons after typing. |
| Exploration | Clicked the training entrance marker; watched the player follow the route from parking and arrive. Calendar remained Monday morning. Enter opened the training preview. |
| Guided training | Completed three passing drills at displayed 57%, 57%, 56% chances: intercepted, received, received. Mouse confirmation and keyboard continuation worked. Report awarded 45.7 XP, including 41.1 short passing XP. |
| Calendar and broad training | Attended all authored commitments and rested in free slots; later training used simulated attendance. Tuesday broad passing training awarded 52.1 XP. No calendar deadlock. |
| Meetings and selection | Completed coach, teammate and agent first-option responses. Coach preview showed 51% positive response; role training plan and playing-time priority persisted. Saturday selected Starter; Sunday showed CM / Support / HoldZone, fatigue 0%, coach trust 67. |
| 11v11 and controls | Entered the four-minute fixture with the created player. Paused at 00:04, inspected a teammate with Tab and opponent Owen Stone with the mouse; clock stayed 00:04. Switched highlights/full viewing, then used 8× full-match playback. |
| Decisions | Read and mouse-committed Contain 77%; keyboard-committed a ToFeet 78% pass. Further defensive choices, halftime, referee notice, free-kick duty and goal-kick duty all continued normally. |
| Completion | Reached full time at 04:04 (four seconds added), Harbour Vale FC 0–0 Meadowbridge Athletic, still playing. Escape opened the aftermath. Match XP gained 151.94, lost 7.68, net +144.26. |
| Reports | Tab opened week completion: gained 408.18, lost 7.68, net +400.5. Skills section and a mouse-selected short passing row showed training, failed-pass and successful-pass causes; short passing net +117.55. |
| Save/relaunch/load | F5 reported saved; quit the process, relaunched the same extracted app, F9 reported loaded. Same player, CM, 0–0 score and report totals returned. Tab showed the same +400.5 week net XP, without replaying the fixture. |

The local career-save directory was empty before testing. The test-created slot was moved to `TestResults/delivery-49d8676/manual-slot.json` afterward, restoring the empty slot without discarding evidence. Save SHA-256: `35944b9a5683385309189c9e00e56870c2348a020e3fbf77f3c4cecd63797a3d`.

### Automated evidence

- `bash -n scripts/package-mac.sh` and `git diff --check`: passed.
- `./scripts/package-mac.sh`: exit 0, `FOOTBALL_RPG_BUILD_PASSED` and `FOOTBALL_RPG_PACKAGE_PASSED`.
- Extracted executable `--pitch-smoke-test -logFile <absolute log path>`: exit 0. All integration markers passed, including career week, reports, save/restore, guided practice, meetings, pitch decisions, goalkeeping, rules, participation, viewing and progression; final `FOOTBALL_RPG_SMOKE_PASSED`. Log SHA-256: `df955f2a478e10151bc012dc0918f73e35721ca342e6e1ca8235b6b58db60d56`.
- Runtime baseline inherited from merged PR #31: **712/712 EditMode tests passed**, 13 October 2026 15:33:35–15:35:38 UTC, including all-position rule/replay and balance coverage. XML SHA-256: `7921b196491fd3068700f1ded898f6e0c9b1d0a265eed993e75f30fdb5129a24`. This ticket changes packaging/editor code and documentation, not game runtime; the suite was not rerun or represented as a new run. The new non-development build and packaged checks validate the delivery configuration.

This desktop check is one complete CM route, not a fresh manual matrix across all positions or a 90-minute human playthrough. Wider positional coverage remains in the earlier football-verification evidence. No Windows, Intel hardware, public-download signing or full-season claims are made. Long-fixture balance limitations remain explicitly listed in the package.
