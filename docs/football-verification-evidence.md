# Football verification evidence

Run: 2026-10-13 10:22:58Z — 2026-10-13 10:23:43Z
Automated status: PASS; 330/330 tests.
Manual status: NOT ATTESTED by this report. See docs/football-verification.md and the manual matrix.

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

Reference run for SME-136 on macOS, Unity 6000.3.23f1, 13 October 2026. The complete regression run passed 684/684; the macOS build and packaged `--pitch-smoke-test` exited successfully. Manual interaction was attempted but the Mac remained locked, so all manual matrix rows remain pending. Windows was not verified.

Reproduce rather than treating this historical snapshot as current results. The report generator was also checked against skipped-case and missing-role XML fixtures; both correctly failed.

Source fingerprints:

- `Assets/FootballRpg/Tests/EditMode/FootballVerificationTests.cs`: `56c4ca9c4558b8f5d5b2d4850b672ee7a33243543a8dbe58ced5090040524308`
- `scripts/football-evidence.py`: `ada8fd06da8a08cead17fe2496ed4fc6c35ff9dd7e4da440c9a1d90ef0428c8a`
- `scripts/verify-football.sh`: `9f66ccb6b600256044df97d72a86359216bb79f879fee69961875e947814462e`
