#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "$0")/.." && pwd)"
cd "$project_dir"
if [[ -n "$(git status --porcelain)" ]]; then
  echo 'Commit source changes before packaging so the manifest identifies the build exactly.' >&2
  exit 1
fi
editor_version="$(awk '/^m_EditorVersion:/{print $2}' ProjectSettings/ProjectVersion.txt)"
unity_editor="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/$editor_version/Unity.app/Contents/MacOS/Unity}"
[[ -x "$unity_editor" ]] || { echo "Unity $editor_version not found." >&2; exit 1; }
mkdir -p Builds/packages Logs
revision="$(git rev-parse HEAD)"
package_dir="$(mktemp -d "$project_dir/Builds/packages/football-rpg-mac-${revision:0:8}-XXXXXX")"
export FOOTBALL_RPG_BUILD_OUTPUT="$package_dir/Football Career RPG.app"
"$unity_editor" -batchmode -nographics -quit -projectPath "$project_dir" \
  -buildTarget StandaloneOSX -executeMethod FootballRpg.Editor.PrototypeBuild.BuildMacDelivery \
  -logFile "$package_dir/build.log"
[[ -x "$FOOTBALL_RPG_BUILD_OUTPUT/Contents/MacOS/Football Career RPG" ]] || { echo 'App executable missing.' >&2; exit 1; }
# Build diagnostics and symbols are retained beside the archive, outside the player package.
staging_dir="$(mktemp -d "$project_dir/Builds/packages/player-${revision:0:8}-XXXXXX")"
ditto "$FOOTBALL_RPG_BUILD_OUTPUT" "$staging_dir/Football Career RPG.app"
cp docs/player-guide.md "$staging_dir/START-HERE.md"
cp docs/known-limitations.md "$staging_dir/KNOWN-LIMITATIONS.md"
{
  echo 'Football Career RPG — one-week macOS prototype'
  echo "Source revision: $revision"
  echo "Unity: $editor_version"
  echo 'Configuration: non-development; local unsigned distribution (not notarized)'
  echo "Built UTC: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  file "$staging_dir/Football Career RPG.app/Contents/MacOS/Football Career RPG"
} > "$staging_dir/BUILD.txt"
(cd "$staging_dir" && find . -type f ! -name SHA256SUMS -exec shasum -a 256 {} \; > SHA256SUMS)
archive="$package_dir.zip"
ditto -c -k --sequesterRsrc --keepParent "$staging_dir" "$archive"
shasum -a 256 "$archive" > "$archive.sha256"
echo "FOOTBALL_RPG_PACKAGE_PASSED: $archive"
echo "Player folder: $staging_dir"
echo "Build diagnostics: $package_dir/build.log"
