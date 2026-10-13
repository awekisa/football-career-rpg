# Career save and restore

The **Save / Load** button remains available on the launch, club, calendar and pitch screens. **F5** saves; **F9** loads. Loading replaces the current session only after the complete save has been decoded and validated. Failed loads leave the current career intact. Start a career before saving; finish or end guided practice first. During a match, pause manually or wait for a decision. In-progress action resolution cannot be saved, including when its animation is manually paused.

One slot is stored at `Application.persistentDataPath/career-v1.json`; the previous valid save is `career-v1.json.bak`. No autosave or cloud sync is included. Navigation position, camera, playback speed and temporary UI selections reset when loading; career time does not advance. The loaded match remains paused. A pending conversation reopens, and a finished week opens its report.

## State contract

`CareerSaveStore` writes a version-1 JSON envelope with a SHA-256 content fingerprint, an integrity checksum and a graph snapshot. The fingerprint covers PrototypeFixture, CharacterCreation and ClubVenue resources. Different content/version is rejected; migrations are not part of this prototype.

The snapshot retains canonical object identity and read-only collection backing stores, including:

- Character identity, football/personality attributes, progression, long-integer finances, readiness, injury days and partial recovery time.
- Calendar choices, attendance, training results, relationships, coach and agent conversations, random state, applied-event ledgers and week/report baselines.
- Teams and bench, player/ball positions and trajectories, match clock, RNG states, referee/offside/restart/participation state, tactical intentions, frozen pending options and action diagnostics.
- Immutable match/week reports and all already-applied reward records. Loading never replays past activity or action commands.

`CareerSnapshotGraph` is a closed-type graph codec. It accepts only known simulation model types, primitive values and explicitly supported collection/tuple shapes. It does not use BinaryFormatter, external assembly/type lookup, constructor callbacks, Unity objects or delegate serialization. Model fields use strict names and counts. Private/readonly fields are hydrated using reflection; this currently targets the existing PC Mono player. IL2CPP, trimming and schema migrations are unverified. Changes to persisted model fields or rule semantics require a save-version bump or an explicit migration before releasing compatible saves.

After validation, `CareerState.ReconnectAfterLoad` reconnects calendar observers, match stepping and fixture completion exactly once. Active guided-practice resolver delegates are deliberately outside the stable save boundary; completed practice records remain available and subsequent practices create fresh resolvers.

## Disk failure handling

Save serializes and verifies a separate copy without changing the live session. It writes a unique temporary file beside the destination, flushes it, and atomically replaces the destination while backing up an existing valid save. A corrupt destination never replaces a valid backup. A first save uses an atomic move. Temporary files are cleaned after failure. These are single-process atomic replacements, not a guarantee against every OS/filesystem or power failure.

Load distinguishes missing, corrupt, incompatible and inaccessible saves. If the primary cannot be loaded, it tries the compatible backup and explicitly reports recovery. Size, reference, type, field-schema, content and selected career-state validation reject malformed snapshots; the checksum detects accidental corruption, not deliberate modification by an attacker.

## Verification

`CareerSaveTests` covers calendar/conversation continuation, injury recovery and exact money, completed guided practice, fresh practice after loading, actual outfield and keeper decisions followed through full time, unchanged reports, no duplicate action draws/XP, corrupt/incompatible saves, backup preservation, failed replacement and live UI load routing. Whole serialized state is compared after the same future inputs, including shared object references and ledgers.

`--pitch-smoke-test` also runs `FOOTBALL_RPG_SAVE_RESTORE_READY`: packaged Mono round-trips calendar and paused-match state, advances both copies identically and recovers a damaged primary from its backup. Smoke saves use an isolated temporary directory and never overwrite the user's slot.
