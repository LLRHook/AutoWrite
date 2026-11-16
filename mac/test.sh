#!/bin/bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WORK="$(mktemp -d "${TMPDIR:-/tmp}/M1-AutoWrite-tests.XXXXXX")"
trap 'rm -rf "$WORK"' EXIT
export AUTOWRITE_DATA_DIR="$WORK/data"
mkdir -p "$AUTOWRITE_DATA_DIR"
swiftc -swift-version 5 -O -module-cache-path "$WORK/modules" "$ROOT"/mac/Sources/*.swift "$ROOT"/mac/Tests/*.swift -o "$WORK/AutoWriteTests"
"$WORK/AutoWriteTests" "$ROOT/reference/data"
