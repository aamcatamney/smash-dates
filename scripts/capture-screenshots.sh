#!/usr/bin/env bash
# Regenerate the docs/screenshots/ gallery from a seeded instance. Thin wrapper around
# scripts/capture-screenshots.mjs — runs from ClientApp/ so its playwright-core resolves.
#
# Prereqs:
#   1. A FRESH, seeded instance running (see docs/screenshots/README.md / scripts/seed-demo.sh).
#   2. ClientApp deps installed (npm ci) — provides playwright-core.
#   3. A Chromium/Chrome on PATH or via CHROME_BIN.
#
# Usage:
#   scripts/capture-screenshots.sh                  # every image
#   scripts/capture-screenshots.sh players profile  # only the named images
#   BASE_URL=http://localhost:5081 scripts/capture-screenshots.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [ ! -d "$ROOT/ClientApp/node_modules/playwright-core" ]; then
  echo "playwright-core is missing — run 'npm ci' (or 'npm install') in ClientApp first." >&2
  exit 1
fi

cd "$ROOT/ClientApp"
exec node "$ROOT/scripts/capture-screenshots.mjs" "$@"
