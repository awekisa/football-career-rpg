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
