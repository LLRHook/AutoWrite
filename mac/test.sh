#!/bin/bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WORK="$(mktemp -d "${TMPDIR:-/tmp}/AutoWrite-tests.XXXXXX")"
trap 'rm -rf "$WORK"' EXIT
export AUTOWRITE_DATA_DIR="$WORK/data"
mkdir -p "$AUTOWRITE_DATA_DIR"
if [[ -n "${AUTOWRITE_EVIDENCE_DIR:-}" ]]; then EVIDENCE="$AUTOWRITE_EVIDENCE_DIR"; else EVIDENCE="$(mktemp -d "${TMPDIR:-/tmp}/AutoWrite-evidence.XXXXXX")"; fi
if [[ "$EVIDENCE" != /* ]]; then echo 'FAIL AUTOWRITE_EVIDENCE_DIR must be absolute'; exit 1; fi
mkdir -p "$EVIDENCE"
printf 'Evidence: %s\n' "$EVIDENCE"
swiftc -swift-version 5 -O -module-cache-path "$WORK/modules" "$ROOT"/mac/Sources/*.swift "$ROOT"/mac/Tests/*.swift -o "$WORK/AutoWriteTests"
# Stream the output and keep a copy: a run passes only if its last line is the
# totals line with 0 FAIL, whatever the binary's exit code.
status=0
"$WORK/AutoWriteTests" "$ROOT/reference/data" "$EVIDENCE" | tee "$WORK/output" || status=${PIPESTATUS[0]}
last="$(tail -n 1 "$WORK/output")"
totals='^TOTAL: [0-9]+ PASS, [0-9]+ FAIL;'
passed='^TOTAL: [0-9]+ PASS, 0 FAIL;'
if [[ ! "$last" =~ $totals ]]; then echo "FAIL test run stopped before its TOTAL line (exit code $status)"; exit $(( status == 0 ? 1 : status ))
elif [[ ! "$last" =~ $passed ]]; then echo "FAIL test run reported failures in its TOTAL line (exit code $status)"; exit $(( status == 0 ? 1 : status ))
fi
exit "$status"
