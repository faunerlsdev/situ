#!/usr/bin/env bash
# Locate the Next standalone server.js and bind the port from /opt/situ/ports.env.
set -euo pipefail

if [[ $# -ne 1 ]]; then
  printf 'uso: start-next.sh <web|admin|landing|support>\n' >&2
  exit 1
fi

app="$1"
root="/opt/situ/current/${app}"
ports=/opt/situ/ports.env
if [[ ! -f "$ports" ]]; then
  printf 'Falta %s\n' "$ports" >&2
  exit 1
fi

set -a
# shellcheck disable=SC1090
source "$ports"
set +a

case "$app" in
  web) PORT=$SITU_WEB_PORT ;;
  admin) PORT=$SITU_ADMIN_PORT ;;
  landing) PORT=$SITU_LANDING_PORT ;;
  support) PORT=$SITU_SUPPORT_PORT ;;
  *)
    printf 'App desconocida: %s\n' "$app" >&2
    exit 1
    ;;
esac

export PORT
export HOSTNAME=127.0.0.1

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
