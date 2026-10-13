# Football verification evidence

Run: 2026-10-13 10:22:58Z — 2026-10-13 10:23:43Z
Automated status: PASS; 330/330 tests.
Manual status: 14/14 role playtests PASS on macOS; see the separately recorded UI evidence below and `football-manual-matrix.csv`. The generated automated report does not attest manual completion.

| Area | Passed/executed | Suite |
|---|---:|---|
| Boundaries, goals, restarts and match phases | 37/37 | `FixtureRulesTests` |
| Offside position and exemptions | 27/27 | `OffsideRulesTests` |
| Offside involvement and live restarts | 20/20 | `OffsideIntegrationTests` |
| Foul and sanction tables | 21/21 | `FoulRulesTests` |
| Advantage, cards, keeper offences and penalties | 19/19 | `RefereeIntegrationTests` |
| Injuries, substitutions and minimum players | 22/22 | `ParticipationTests` |
| Keeper handling and response choices | 33/33 | `GoalkeeperTests` |
| Set-piece roles and deliveries | 29/29 | `SetPieceTests` |
| Full/highlight pacing and protected stops | 52/52 | `MatchPlaybackTests` |
| Created-career role and consequence matrix | 14/14 | `FootballVerificationTests` |
| All selectable roles and starting budgets | 31/31 | `CharacterCreationTests` |
| Career fixture lifecycle | 14/14 | `CareerWeekTests` |
| Possession decision presentation | 3/3 | `DecisionPresentationTests` |
| Defensive decision presentation | 2/2 | `DefensePresentationTests` |
| Keeper decision presentation | 3/3 | `GoalkeeperPresentationTests` |
| Football action presentation | 3/3 | `FootballActionPresentationTests` |

| Role | Decisions | Keeper decisions | User actions | Dismissals | End | Full/highlight state |
|---|---:|---:|---:|---:|---|---|
| GK | 46 | 3 | 11 | 6 | Abandoned (<7) | `01e8bedaf8e0c36703f667c1233b59a2242d7d76ebafd37df91e4ccd43e4f463` |
| CB | 85 | 0 | 10 | 6 | Abandoned (<7) | `cc4954e1138609f853f0c23714a3d423c078306d25d20fc0869b8634505cbcd5` |
| LB | 129 | 0 | 13 | 5 | Full time | `e7fccbf77f3cd1239b0d99219363ffb5d1fbe6db70bd06786cc3fd4ec0ecfe1c` |
| RB | 128 | 0 | 26 | 6 | Full time | `ef7779cc2b2cd703522a94a43b37ccd1e787bf5dca691de78b5a25635c2bfda6` |
| LWB | 147 | 0 | 21 | 7 | Full time | `79f6fc1e5c92e0bac17be79e1ebc0ecfdab861cc16eb2ff6a4200f0ad2503518` |
| RWB | 110 | 0 | 9 | 8 | Abandoned (<7) | `b7b2ceaf9ba3bc3e9dc2a5a26e51e0fefc3ce3999640f4da8b63688a56a58634` |
| DM | 153 | 0 | 35 | 7 | Abandoned (<7) | `7518159f4900f9e596790aadb6660534636ee597b5e689472f66cb26e9e5f3bf` |
| CM | 179 | 0 | 67 | 6 | Abandoned (<7) | `b0552f6e7fecef122e5dd605558a2313d11b24e3e0f5eec9a695eb2ebf1b6f05` |
| AM | 118 | 0 | 23 | 9 | Abandoned (<7) | `07e6e6b8304670cc8823dbad6c6a8f01f179661be8a376ba1aff91ed9f2559a4` |
| LM | 132 | 0 | 14 | 3 | Full time | `6ac68e0bed2687bab2002ddb074d9ff1a1a9bf4e539b4c5a2a96a8e7bbe8d61f` |
| RM | 114 | 0 | 13 | 6 | Abandoned (<7) | `0cc84b5424a1982042921799c5c616d8711fe14f0cf5a3f96978016b2ba6cbf2` |
| LW | 138 | 0 | 18 | 4 | Full time | `f913d6172a31c1b31d6dbafa32810ce3f9c6385538e23e57aa61543dacc8bc45` |
| RW | 102 | 0 | 22 | 6 | Abandoned (<7) | `93f26446ac5111d43c0722cfb18bc8a1f1db14b89ffd35052a9aa8b85ba87378` |
| ST | 123 | 0 | 57 | 7 | Abandoned (<7) | `f212805a08b82926a04922de84560e7a1897ab8d6cb2876a4c122286477c6689` |

Balance observation: 9/14 role scenarios ended early after dismissals. This is visible tuning evidence, not proof of balanced match pacing.
Each role uses a balanced created character, the attended authored week, seed 42 and two 600-second halves. Random injuries and coach substitutions are disabled only in this paired role replay; separate named scenarios exercise those systems.
Both modes must have identical stops/probabilities and complete serialized career state. These automated presentation calls are not mouse or visual verification.

## Verification context

Reference run for SME-136 on macOS, Unity 6000.3.23f1, 13 October 2026. The complete regression run passed 684/684; the macOS build and packaged `--pitch-smoke-test` exited successfully. The initial manual attempt was blocked by a locked Mac; subsequent UI testing completed all 14 roles as recorded below. Windows was not verified.

Reproduce rather than treating this historical snapshot as current results. The report generator was also checked against skipped-case and missing-role XML fixtures; both correctly failed.

Source fingerprints:

- `Assets/FootballRpg/Tests/EditMode/FootballVerificationTests.cs`: `56c4ca9c4558b8f5d5b2d4850b672ee7a33243543a8dbe58ced5090040524308`
- `scripts/football-evidence.py`: `ada8fd06da8a08cead17fe2496ed4fc6c35ff9dd7e4da440c9a1d90ef0428c8a`
- `scripts/verify-football.sh`: `9f66ccb6b600256044df97d72a86359216bb79f879fee69961875e947814462e`

## Initial manual follow-up — 13 October 2026 (superseded by completion below)

On build `c81da0fb61a331aa25242bad5cb38b25e3c31693`, created a balanced CM through the normal screen, attended the full week, saw training XP/recovery and coach/teammate/agent meeting consequences, and entered the four-minute fixture as a starter. Observed full/highlight switching at a frozen pause, keyboard player inspection, a referee pause, a 73% free-kick pass preview, the failed pass consequence (-22.1 skill XP), and return/reopening of the same fixture at 00:42. The user directly confirmed that clicking Take restart works. Automated mouse clicks did not reliably activate the Unity controls, so mouse inspection/off-ball coverage and the final report are not attested. The all-position matrix remains incomplete; automated results are not substituted for it.

Found a separate visible defect: the Save/Load control was drawn under the opaque launch/creation/calendar layer. `CareerSaveControls` now retains its own foreground GUI depth instead of resetting it to zero at the end of every event. Rebuilt macOS and directly verified that both the control and the expanded message render above creation; F5 correctly displayed “Start a career before saving” without writing a save.

After the overlay fix: full EditMode regression **684/684 passed** (10:48:35–10:49:40 UTC), macOS build passed, packaged smoke exited 0 with `SAVE_RESTORE_READY` and `SMOKE_PASSED` in `/tmp/football-sme136-overlay-smoke.log`. The focused scenario implementation and its source fingerprints are unchanged.


## Completed UI play matrix — 13 October 2026

**14/14 PASS** in `football-manual-matrix.csv`, on macOS build commit `333d5b6cbd15a5c23c01e518747d5120863906e5`, Unity 6000.3.23f1, 1440×900 game client. Tester: Codex operating the actual packaged app. These are direct UI observations, separate from the NUnit results; they are not a human attestation or a claim of 14 full-length 90-minute games.

For every role, used the normal creation screen and separate balanced budgets, followed the authored training/meeting/rest week, entered the four-minute fixture as a starter, inspected own player and opponent with mouse, checked frozen decisions and full/highlight viewing, committed a possession choice and a defensive/off-ball choice by mouse, returned to the week and reopened the same fixture, reached full time and inspected the actual match report. Keyboard default choices advanced intervening decisions; only specifically recorded panels/events are claimed as visually inspected. All 14 short fixtures reached full time. The CSV records actual score, time, XP and role-specific observations.

Goalkeeper follow-up directly inspected the 03:03 Save and hold (40%) / Parry (57%) choices; changing viewing mode preserved the choice and clock. A mouse-committed held save succeeded (+112.0 skill XP), opening Roll (85%), Throw, Short kick, Long kick and Drop to feet. Mouse Roll to Jonas Marlow succeeded (+21.7 XP), increasing the Distribution bar. Goal-kick distribution and final report also passed. No illegal-handling opportunity occurred in the UI run; its warning and sanctions remain covered by dedicated deterministic tests, not a claimed manual observation.

Standard computer-tool clicks delivered stale pointer coordinates to Unity. The user explicitly authorized a native macOS mouse helper; it moves the physical pointer before down/up and verifies the expected app/window. This local testing aid lives outside the repository and changes no game code. Native mouse controls worked throughout the matrix. No OS permissions or security settings were changed.

No blocking flow failures remain in the tested short fixtures. Minor selection-status clipping and capsule-label overlap remain presentation polish; the stale report footer claiming disk saves were absent was corrected after these runs. The existing save overlay fix was already in the tested build. Longer seeded fixture dismissal frequency remains the separate SME-137 balance concern above. Rare laws, substitutions and injuries are covered by named automated cases unless specifically observed in the CSV; Windows remains unverified.

Final follow-up changed only the report footer string (plus evidence files). Rebuilt macOS successfully and reran the packaged `--pitch-smoke-test`: exit 0, `REPORTS_PASSED`, `SAVE_RESTORE_READY` and `SMOKE_PASSED` in `/tmp/football-sme136-final-smoke.log`. Opened the rebuilt report UI directly and verified the new Save / Load guidance is readable. The 684/684 full-suite and 330/330 focused results above precede this text-only change; no simulation or test logic changed afterward.
