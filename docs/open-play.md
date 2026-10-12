# AI open play (SME-111)

Open the pitch and choose a pass. The user follows AI positioning between possession decisions; gaining possession pauses the simulation for a choice. The simulation can also run without a controlled player for AI-only scenarios. The lime area shows their expected tactical target, distinct from the permanent user ring and white selection ring. Mouse and keyboard inspection/camera controls do not move a player directly.

## Simulation contract

OpenPlayWorld subscribes to MatchSession fixed ticks and has no Unity dependency. Each tick computes all tactical targets from one position snapshot, then moves players at attribute-dependent speed. Role anchors cover all 14 defined positions and mirror across teams; repeated roles receive lateral spacing. Targets respond to ball position and possession phase, nearest opposing runners, teammate spacing, pressure and loose-ball pursuit. Goalkeepers maintain deeper positioning but participate in distribution and recovery. The fixture’s first eleven squad entries form the lineup; coach-selected formations and substitutions are separate work.

There is one ball state: Owned (one owner ID), Travelling (no owner, a frozen destination), Loose (no owner or possession club), or OutOfPlay. Pass outcomes are committed before flight. The receiver or interceptor moves to the frozen destination, and flight timing allows them to arrive; animation never adds a reception or interception roll. Loose failures release the ball for recovery. Out-of-play failures finish beyond the boundary, then a one-second sandbox reset gives the opposition the ball; this is deliberately not a throw-in/corner implementation. Proper restarts belong to the match-laws ticket.

AI pass choices use [PassResolver](pass-resolution.md), with passes to feet and through balls. Close challenges still use provisional tackle tuning. All actions share MatchSession’s seeded resolver, event IDs and diagnostics. Automatic resolution preserves accumulated render time so frame batching does not change world history. No independent random generator, career XP, scoring, fouls is introduced by the open-play subsystem. The interactive pitch adds the [player decision panel](player-decisions.md). Players stay within sandbox bounds; an out-of-play pass can carry the ball outside them.

Presentation copies positions after simulation advances; it never integrates movement or resolves actions. Closing the pitch unsubscribes the world. Camera/inspection remains functional while all world state and randomness are frozen on pause.

## Verification

Three five-minute seeded scenarios check all 22 roles move, all ball phases occur, passes/tackles sustain play, possession changes, loose balls recover within a bounded interval and ownership remains consistent. Replay checks compare exact positions and complete action diagnostics across frame sizes, playback speeds and pauses. Further checks cover all role anchors, changing targets, disposal, automatic resolution idempotency, and presentation/marker synchronization.

The packaged --pitch-smoke-test also runs 30 seconds of open play and requires movement and completed passes, logging FOOTBALL_RPG_OPEN_PLAY_PASSED. Full match laws and user-directed actions remain out of scope.
