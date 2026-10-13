# Prototype balance — SME-137

These are tuning defaults, not new restrictions on player choices. This pass keeps failed match actions reducing skill XP, exact displayed outcome probabilities, all positions, football sanctions and the stronger-club/playing-time tradeoff.

## Changes and baseline

The SME-136 baseline (seed 42, fourteen created roles, two 600-second halves) had 9/14 matches abandoned below seven players and 3–9 dismissals per fixture. AI repeatedly attempted tackles every 1.2 seconds whenever within two metres, without considering success chance or an existing yellow card.

AI now considers a tackle at most every eight seconds, requiring 45% success or 55% when booked. Otherwise tactical movement continues to pressure the carrier. The committed tackle still uses its frozen, displayed probability and the same failure severity/sanction rules. These thresholds apply to AI choices only. User defensive prompts have a five-second minimum interval instead of 2.5 seconds; nearby threats and positional reach still control whether a prompt is available. Possession, keeper and restart choices retain their original triggers. Off-ball instructions remain available between prompts.

Repeated match actions previously earned multipliers 1, .8, .6, .4, .2, then zero on success, while failures always charged full XP. This made even reliable play lose XP over long sequences. Both signs now share `(5/6)^priorAttempts`. Failure still lowers the skill; changing the target or variant does not reset the family. The total repeated exposure is bounded to six first-attempt magnitudes; the existing 200 positive-XP-per-skill match cap remains and losses do not refill it. This is diminishing learning and loss, not a guarantee that every match produces improvement.

Unchanged progression defaults: success `20 × (1 + 3 × (1 − p)) × significance`, failure `−12 × (.5 + p) × significance`, threshold `100 + 10 × (level − floor)`. Training remains 120 base XP per slot, multiplied by pre-workload readiness, each of coach/staff/facility quality (`.5 + quality`), and a level multiplier declining to .25 at the cap. Guided practice concentrates 90% on the selected skill, 10% on decisions; failed drills retain 65% learning. Rest recovers .22 fatigue per slot, general training adds .14 and extra practice adds .18.

Coach penalty weights and the 20-second reaction window are retained after comparing failure/success, defensive danger, justified late attacks, standing, trust and personality. Reactions now explicitly explain danger, star latitude and coach personality. More serious unsuccessful departures still cost more than successful or justified ones. Missing training still affects trust; recovery is not a free way to bypass commitments.

The changed trajectories exposed a Mono save precision issue: one double coordinate changed by one representable step after serialization. Saving doubles with 17 significant digits fixes the exact replay check while continuing to read existing numeric scalar strings. This is a prerequisite regression fix, not a change to outcome calculations.

## Reproduction and limits

Run `./scripts/unity.sh test` for the complete suite, or `./scripts/unity.sh verify-balance` for the Balance category. NUnit output contains `BALANCE_WEEK`, `BALANCE_RISK`, `BALANCE_TRAINING`, `BALANCE_COACH` and `BALANCE_LONG` records. `./scripts/verify-football.sh` reproduces the all-position/full-highlight comparison.

The fifteen week scenarios use normal character creation, separate budgets, an initial .35 fatigue, calendar attendance, meetings, optional three-choice guided drills, coach selection, presentation decision APIs, the fixture and final reports. Reliable attends the schedule and rests in free slots. Ambitious adds two early focused sessions, then recovers before the fixture; on the pitch it attempts useful shots at 35%+, through balls at 45%+ and tackles at 50%+. Recovery substitutes rest for training when fatigue is at least .1, accepting the attendance cost. All use the authored career seed 42 and 20-minute fixtures. GK, CB, CM and ST cover distinct role families; the separate fourteen-role suite covers every role. The stronger-club CM scenarios set staff/facility/coach quality to .9; staff quality also raises the existing selection threshold.

An initial ambitious policy filled four free slots and reached kickoff at .50 fatigue, putting every young role on the bench. Limiting extra work to two early slots is a measured strategy adjustment; the coach's selection threshold was not lowered to force starts. Resting unnecessarily also loses training/trust without improving an already recovered kickoff. The authored sparse week does not make every possible rest choice optimal.

Three autonomous 90-minute fixtures use seeds 1, 42 and 987. They retain fatigue but disable random injuries and coach substitutions to isolate action pacing. The statistical XP comparison uses 100 deterministic seeds with 80 repetitions per seed, changing targets to exercise anti-farming. The old-formula comparison is an upper bound before the old positive cap, so it cannot exaggerate the old losses. These are automated whole-week and match playtests, not new physical mouse attestations; the previous 14-role UI matrix remains dated evidence for the pre-balance build. No claim of professional statistical calibration, full-season balance or Windows verification is made.

## Recorded results — 13 October 2026

Full EditMode suite: **712/712 passed**, including 28 balance tests, from 2026-10-13 15:33:35Z to 2026-10-13 15:35:38Z. The 330 football-verification cases are included in this run, with exact full/highlight parity in all fourteen roles. Save/load continuation and the final completed-state round trip pass for both outfield and goalkeeper.

| Role | Strategy | Club quality | Selection | Training XP | Match net XP | Decisions | Dismissals | Final trust |
|---|---|---|---|---:|---:|---:|---:|---:|
| CM | reliable | Starter club | Starter | 247.79 | 253.44 | 57 | 0 | 70 |
| CM | ambitious | Starter club | Starter | 291.67 | 383.59 | 82 | 0 | 65 |
| CM | recovery | Starter club | Starter | 210.02 | 253.54 | 57 | 0 | 60 |
| GK | reliable | Starter club | Starter | 249.51 | 93.54 | 27 | 0 | 66 |
| GK | ambitious | Starter club | Starter | 293.66 | 44.14 | 28 | 1 | 66 |
| GK | recovery | Starter club | Starter | 211.42 | 93.54 | 27 | 0 | 56 |
| CB | reliable | Starter club | Starter | 247.79 | 338.59 | 66 | 2 | 66 |
| CB | ambitious | Starter club | Starter | 295.65 | 268.82 | 50 | 1 | 66 |
| CB | recovery | Starter club | Starter | 210.02 | 338.59 | 66 | 2 | 56 |
| ST | reliable | Starter club | Starter | 247.79 | 308.90 | 102 | 0 | 65 |
| ST | ambitious | Starter club | Starter | 291.67 | 435.63 | 99 | 0 | 69 |
| ST | recovery | Starter club | Starter | 210.02 | 308.95 | 102 | 0 | 55 |
| CM | reliable | Strong | Substitute | 1111.01 | 0.00 | 0 | 0 | 67 |
| CM | ambitious | Strong | Substitute | 1306.92 | 0.00 | 0 | 0 | 67 |
| CM | recovery | Strong | Substitute | 941.66 | 0.00 | 0 | 0 | 57 |

All fifteen weeks reached full time. Across these scenarios, higher-quality coaching substantially increases training yield but the prospect is benched; two focused sessions improve training output without exhaustion; more ambitious match choices can improve or worsen net progress depending on the role and outcomes. No single policy wins all comparisons.

| Role | Meaningful decisions | Dismissals | Ending |
|---|---:|---:|---|
| GK | 26 | 0 | Full time |
| CB | 66 | 2 | Full time |
| LB | 26 | 0 | Full time |
| RB | 41 | 0 | Full time |
| LWB | 37 | 0 | Full time |
| RWB | 27 | 0 | Full time |
| DM | 46 | 0 | Full time |
| CM | 57 | 0 | Full time |
| AM | 64 | 1 | Full time |
| LM | 37 | 0 | Full time |
| RM | 36 | 0 | Full time |
| LW | 30 | 0 | Full time |
| RW | 37 | 1 | Full time |
| ST | 102 | 0 | Full time |

The original all-role comparison improves from 9/14 abandoned to **0/14**. Dismissals remain possible (0–2 here); laws were not suppressed. Three full-length autonomous runs complete with 144–201 AI tackles each, so the improvement does not come from disabling tackles.

```text
BALANCE_COACH risky=10 successful=3 star=3 justifiedSuccess=1
BALANCE_LONG seed=1 goals=12 reds=1 yellows=11 passes=447 tackles=180
BALANCE_LONG seed=42 goals=17 reds=1 yellows=9 passes=456 tackles=201
BALANCE_LONG seed=987 goals=12 reds=1 yellows=11 passes=491 tackles=144
BALANCE_TRAINING general=28.20 focused=24.91 strong=126.44 trainedFatigue=0.79 restFatigue=0.43
BALANCE_RISK p=0.9 significance=0.5 legacyMean=-32.34 mean=64.05 min=19.82 max=77.94 sd=13.06
BALANCE_RISK p=0.55 significance=1.5 legacyMean=-559.10 mean=136.26 min=-36.66 max=187.54 sd=38.19
BALANCE_RISK p=0.4 significance=2 legacyMean=-893.19 mean=106.90 min=-79.82 max=184.21 sd=46.53
```

The XP ensembles preserve risk: the reliable policy has a smaller standard deviation; riskier policies have higher average growth but include net-loss seeds. At .65 initial fatigue, resting gives .43 fatigue versus .79 after training and improves next-slot readiness. Focused practice gives less total XP than general attendance here but concentrates more into passing. The same failed risky deviation costs 10 trust for a developing player and 3 for a star; success costs 3, or 1 when justified.

Remaining balance limitations: the autonomous full-length runs still produce **12–17 total goals and 9–11 yellow cards**, which is too high for realistic match statistics. This pass resolves the abandonment/XP-regression blockers and establishes repeatable comparisons; it does not claim calibrated scoring or discipline distributions. Further football realism and longer career studies remain follow-up work. Random injuries/coach substitutions were isolated out of these comparisons and retain separate regression coverage.

Source evidence: `TestResults/editmode.xml` (SHA-256 `7921b196491fd3068700f1ded898f6e0c9b1d0a265eed993e75f30fdb5129a24`).

Packaged verification: macOS Unity 6000.3.23f1 build passed; the rebuilt player completed `--pitch-smoke-test` with exit 0, including report/save restore, coach, referee, all action families, progression and full/highlight checks. Log: `/tmp/football-sme137-smoke.log`. The launch screen was visually inspected; native Mac focus activation was needed for frame-based checks. No new all-position mouse matrix is claimed.
