# Implemented scope and known limitations

Implemented: character creation with separate football/personality budgets; all 14 positions including goalkeeper; one authored club week; click-to-move placeholder club venue; calendar consequences; general and guided training; coach, teammate and agent conversations; relationships and coach selection; a simulated 11v11 fixture with contextual actions, off-ball choices, probabilities, pause and highlights; reports; one manual local save with backup.

This is a prototype, not a full season or production release:

- One small club week and fixture. No youth assessment, season progression, playable transfers/contracts, career aging or retirement yet. Agent preferences are recorded for future opportunities. Finances are informational.
- Placeholder 3D players, venues and interface; no finished animation, audio or licensed clubs/players. Fictional content is not an official representation of real teams.
- Balance is provisional. Long seeded AI fixtures in the current baseline produced 12–17 total goals and 9–11 yellow cards. Those rates need further tuning. A four-minute demonstration fixture is available.
- Guided training illustrates isolated decisions rather than a full moving practice match. Success probabilities describe the selected action’s success condition, not necessarily a goal.
- One save slot and previous-valid backup; no autosave, cloud saves or guaranteed compatibility with later builds. Active guided drills must end before saving. Venue position/camera/viewing preferences may reset on load.
- macOS is the first delivery target. The package architecture is recorded in BUILD.txt; testing on Apple Silicon does not establish Intel hardware compatibility. Windows has not been built or tested. No mobile, console or multiplayer support.
- The application is not Developer ID signed or notarized for public distribution. Do not disable operating-system protections to run an unknown copy.
- Automated coverage cannot establish realistic football, polished usability or exhaustive play paths. The delivery evidence distinguishes automated checks from direct desktop observations.
