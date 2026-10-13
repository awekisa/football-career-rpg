#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "$0")/.." && pwd)"
editor_version="$(awk '/^m_EditorVersion:/{print $2}' "$project_dir/ProjectSettings/ProjectVersion.txt")"
unity_editor="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/$editor_version/Unity.app/Contents/MacOS/Unity}"
if [[ ! -x "$unity_editor" ]]; then
  echo "Unity $editor_version not found. Set UNITY_EDITOR to the editor executable." >&2
  exit 1
fi
mkdir -p "$project_dir/Logs" "$project_dir/TestResults"
args=(-batchmode -nographics -projectPath "$project_dir")
case "${1:-}" in
  import) args+=(-quit -logFile "$project_dir/Logs/import.log") ;;
  bootstrap) args+=(-quit -executeMethod FootballRpg.Editor.PrototypeBuild.CreateLaunchScene -logFile "$project_dir/Logs/bootstrap.log") ;;
  test) args+=(-runTests -testPlatform EditMode -testResults "$project_dir/TestResults/editmode.xml" -logFile "$project_dir/Logs/tests.log") ;;
  verify-football) args+=(-runTests -testPlatform EditMode -testCategory FootballVerification -testResults "$project_dir/TestResults/football.xml" -logFile "$project_dir/Logs/football.log") ;;
  verify-balance) args+=(-runTests -testPlatform EditMode -testCategory Balance -testResults "$project_dir/TestResults/balance.xml" -logFile "$project_dir/Logs/balance.log") ;;
  build-mac) args+=(-quit -buildTarget StandaloneOSX -executeMethod FootballRpg.Editor.PrototypeBuild.BuildMac -logFile "$project_dir/Logs/build-mac.log") ;;
  build-windows) args+=(-quit -buildTarget StandaloneWindows64 -executeMethod FootballRpg.Editor.PrototypeBuild.BuildWindows -logFile "$project_dir/Logs/build-windows.log") ;;
  *) echo "Usage: $0 {import|bootstrap|test|verify-football|verify-balance|build-mac|build-windows}" >&2; exit 2 ;;
esac
exec "$unity_editor" "${args[@]}"
