#!/bin/bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
owner=false
if [[ "$#" -eq 1 && "$1" == --live-typing ]]; then
  owner=true
elif [[ "$#" -ne 3 || "$1" != --live-typing || "$2" != --require-idle-seconds || ! "$3" =~ ^[0-9]{3,5}$ ]] || (( 10#$3 < 240 || 10#$3 > 86400 )); then
  echo 'exit=4; usage: mac/smoke.sh --live-typing [--require-idle-seconds N (240..86400)]'
  exit 4
fi
APP="$ROOT/dist/AutoWrite.app"
if [[ ! -x "$APP/Contents/MacOS/AutoWrite" ]]; then echo 'exit=2; run mac/build.sh first'; exit 2; fi
WORK="$(mktemp -d "${TMPDIR:-/tmp}/AutoWrite-smoke.XXXXXX")"
export AUTOWRITE_DATA_DIR="$WORK/data"
mkdir -p "$AUTOWRITE_DATA_DIR"
child=''
watchdog=''
cleanup() {
  if [[ -n "$watchdog" ]]; then kill "$watchdog" 2>/dev/null || true; wait "$watchdog" 2>/dev/null || true; fi
  if $owner && [[ -f "$WORK/proof.pid" ]]; then
    proof_pid="$(cat "$WORK/proof.pid")"
    if kill -0 "$proof_pid" 2>/dev/null; then
      kill -TERM "$proof_pid" 2>/dev/null || true
      for ((i=0; i<20; i++)); do
        if ! kill -0 "$proof_pid" 2>/dev/null; then break; fi
        sleep 0.05
      done
      kill -KILL "$proof_pid" 2>/dev/null || true
    fi
  fi
  if [[ -n "$child" ]]; then kill "$child" 2>/dev/null || true; wait "$child" 2>/dev/null || true; fi
  rm -rf "$WORK"
}
trap cleanup EXIT INT TERM
if $owner; then
  printf '%s\n' "$(( $(date +%s) + 30 ))" > "$WORK/deadline"
  /usr/bin/open -W -n "$APP" --args --live-typing --owner-proof-directory "$WORK" &
else
  "$APP/Contents/MacOS/AutoWrite" "$@" &
fi
child=$!
# The app has at most 15 seconds from launch. Allow 30 seconds for Launch
# Services plus its full lifetime and a 5-second cleanup margin. A late launch
# rejects the expired/missing deadline before creating a window or using data.
if $owner; then watchdog_seconds=50; else watchdog_seconds=20; fi
( sleep "$watchdog_seconds" & timer=$!; trap 'kill "$timer" 2>/dev/null || true; wait "$timer" 2>/dev/null || true; exit' TERM EXIT; wait "$timer"; if $owner && [[ -f "$WORK/proof.pid" ]]; then kill -KILL "$(cat "$WORK/proof.pid")" 2>/dev/null || true; fi; kill -KILL "$child" 2>/dev/null || true ) &
watchdog=$!
set +e
wait "$child"
status=$?
set -e
child=''
if $owner && [[ -f "$AUTOWRITE_DATA_DIR/live-probe.txt" ]]; then
  cat "$AUTOWRITE_DATA_DIR/live-probe.txt"; echo
  status="$(sed -n 's/^exit=\([0-4]\);.*/\1/p' "$AUTOWRITE_DATA_DIR/live-probe.txt")"
fi
if [[ ! "$status" =~ ^[0-4]$ ]]; then echo 'exit=2; external watchdog or process failure'; exit 2; fi
if $owner && [[ ! -f "$AUTOWRITE_DATA_DIR/live-probe.txt" ]]; then echo 'exit=2; owner proof did not report a result'; exit 2; fi
exit "$status"
