#!/usr/bin/env bash
# Run on the Ubuntu server. Prints capacity; it does not change anything.
set -euo pipefail
ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
if [[ ! -r /proc/meminfo ]]; then
  printf 'check-capacity.sh se ejecuta en el VPS Linux.\n' >&2
  exit 1
fi
exec node "${ROOT}/capacity.mjs"
