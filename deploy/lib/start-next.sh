#!/usr/bin/env bash
# Locate the Next standalone server.js. Next may nest it under apps/<name>/.
set -euo pipefail

if [[ $# -ne 1 ]]; then
  printf 'uso: start-next.sh <web|admin|landing|support>\n' >&2
  exit 1
fi

app="$1"
root="/opt/situ/current/${app}"

if [[ -f "${root}/apps/${app}/server.js" ]]; then
  cd "$root"
  exec /usr/bin/node "apps/${app}/server.js"
fi

if [[ -f "${root}/server.js" ]]; then
  cd "$root"
  exec /usr/bin/node server.js
fi

printf 'No está server.js para %s en %s\n' "$app" "$root" >&2
exit 1
