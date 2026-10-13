#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "$0")/.." && pwd)"
# Discard only this runner's generated output so a compile failure cannot reuse old evidence.
rm -f "$project_dir/TestResults/football.xml" "$project_dir/TestResults/football.md" "$project_dir/TestResults/football.json"
result=0
"$project_dir/scripts/unity.sh" verify-football || result=$?
if [[ ! -f "$project_dir/TestResults/football.xml" ]]; then
  echo "Football verification produced no test results; inspect Logs/football.log." >&2
  exit 1
fi
python3 "$project_dir/scripts/football-evidence.py" || result=1
exit "$result"
