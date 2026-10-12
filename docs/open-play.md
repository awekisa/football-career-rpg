# AI open play (SME-111)

Open the pitch and press Space. The user player follows the same AI as everyone else. The lime area shows their expected tactical target, distinct from the permanent user ring and white selection ring. Mouse and keyboard inspection/camera controls do not move a player directly.

## Simulation contract

OpenPlayWorld subscribes to MatchSession fixed ticks and has no Unity dependency. Each tick computes all tactical targets from one position snapshot, then moves players at attribute-dependent speed. Role anchors cover all 14 defined positions and mirror across teams; repeated roles receive lateral spacing. Targets respond to ball position and possession phase, nearest opposing runners, teammate spacing, pressure and loose-ball pursuit. Goalkeepers maintain deeper positioning but participate in distribution and recovery. The fixture’s first eleven squad entries form the lineup; coach-selected formations and substitutions are separate work.

There is one ball state: Owned (one owner ID), Travelling (no owner, intended receiver and fixed destination), or Loose (no owner or possession club). Flight is integrated at a fixed speed; the receiver moves to the destination. Failed passes are under-hit using the same action draw; nearby players recover the resulting loose ball. The nearest defending outfielder presses; close challenges can release the ball. Every recovery assigns ownership atomically. Pass selection balances space, progress and distance, with a short hold/dribble period to sustain play.

AI pass and tackle choices use MatchSession.ResolveAutomatic and its existing seeded resolver, event IDs and diagnostics. Unlike a human decision pause, automatic resolution preserves accumulated render time so frame batching does not change world history. Provisional pass/tackle probabilities use attributes and distance, clamped to tuning bounds; SME-112 owns final football formulas. A successful pass draw can still end loose if the receiver cannot reach the arrival point; it is not yet a displayed user probability. No independent random generator, career XP, scoring, fouls, out-of-play restarts or decision UI is introduced. Players and ball stay within sandbox bounds until the rules ticket adds proper restarts.

Presentation copies positions after simulation advances; it never integrates movement or resolves actions. Closing the pitch unsubscribes the world. Camera/inspection remains functional while all world state and randomness are frozen on pause.

## Verification

Three five-minute seeded scenarios check all 22 roles move, all ball phases occur, passes/tackles sustain play, possession changes, loose balls recover within a bounded interval and ownership remains consistent. Replay checks compare exact positions and complete action diagnostics across frame sizes, playback speeds and pauses. Further checks cover all role anchors, changing targets, disposal, automatic resolution idempotency, and presentation/marker synchronization.

The packaged --pitch-smoke-test also runs 30 seconds of open play and requires movement and completed passes, logging FOOTBALL_RPG_OPEN_PLAY_PASSED. Full match laws and user-directed actions remain out of scope.
