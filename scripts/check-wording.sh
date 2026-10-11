#!/usr/bin/env sh
# Guardrail 4: a submitted request is a place in the draw, never a booking. Fails if "จองสำเร็จ" appears in source.
# CLAUDE.md states the rule (and so contains the phrase); it is not source and is not scanned.
set -eu
cd "$(dirname "$0")/.."
if grep -rn --exclude-dir=node_modules --exclude-dir=bin --exclude-dir=obj --exclude-dir=dist --exclude-dir=.angular \
    "จองสำเร็จ" api/src web/src docs; then
  echo 'Found "จองสำเร็จ" — use "ยื่นคำขอแล้ว" (CLAUDE.md › Guardrail 4).' >&2
  exit 1
fi
echo 'OK: no "จองสำเร็จ" in api/src, web/src or docs.'
