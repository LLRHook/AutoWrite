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
"$WORK/AutoWriteTests" "$ROOT/reference/data" "$EVIDENCE"
