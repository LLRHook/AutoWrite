#!/bin/bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
if [[ "$#" -ne 3 || "$1" != --live-typing || "$2" != --require-idle-seconds || ! "$3" =~ ^[0-9]+$ ]] || (( 10#$3 < 240 || 10#$3 > 86400 )); then
  echo 'exit=4; usage: mac/smoke.sh --live-typing --require-idle-seconds N (240..86400)'
  exit 4
fi
if [[ ! -x "$ROOT/dist/AutoWrite.app/Contents/MacOS/AutoWrite" ]]; then
  echo 'exit=2; run mac/build.sh first'
  exit 2
fi
WORK="$(mktemp -d "${TMPDIR:-/tmp}/M1-AutoWrite-smoke.XXXXXX")"
export AUTOWRITE_DATA_DIR="$WORK/data"
mkdir -p "$AUTOWRITE_DATA_DIR"
child=''
watchdog=''
cleanup() {
  if [[ -n "$watchdog" ]]; then kill "$watchdog" 2>/dev/null || true; wait "$watchdog" 2>/dev/null || true; fi
  if [[ -n "$child" ]]; then kill "$child" 2>/dev/null || true; wait "$child" 2>/dev/null || true; fi
  rm -rf "$WORK"
}
trap cleanup EXIT INT TERM
"$ROOT/dist/AutoWrite.app/Contents/MacOS/AutoWrite" "$@" &
child=$!
( sleep 15 & timer=$!; trap 'kill "$timer" 2>/dev/null || true; wait "$timer" 2>/dev/null || true; exit' TERM EXIT; wait "$timer"; kill -KILL "$child" 2>/dev/null || true ) &
watchdog=$!
set +e
wait "$child"
status=$?
set -e
child=''
if ((status > 4)); then echo 'exit=2; external 15-second watchdog or process failure'; exit 2; fi
exit "$status"
