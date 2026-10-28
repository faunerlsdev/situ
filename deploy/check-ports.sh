#!/usr/bin/env bash
# Fail when a SITU bind port is already taken. Does not check PostgreSQL, 80 or 443.
set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/lib/ports.sh"
situ_load_ports

if ! command -v ss >/dev/null 2>&1; then
  printf 'Falta ss (iproute2). Este chequeo se corre en el servidor.\n' >&2
  exit 1
fi

fail=0
occupied="22 25 80 443 465 587 993 1935 18888 19997 3000 3001 3003 3306 5000 5002 6379 11332 11333 11334"
ports=(
  "api ${SITU_API_PORT}"
  "admin ${SITU_ADMIN_PORT}"
  "web ${SITU_WEB_PORT}"
  "landing ${SITU_LANDING_PORT}"
  "ayuda ${SITU_SUPPORT_PORT}"
  "rtmps ${SITU_RTMPS_PORT}"
  "hls ${SITU_HLS_PORT}"
  "mediamtx-api ${SITU_MTX_API_PORT}"
  "live-auth ${SITU_LIVE_AUTH_PORT}"
)

for item in "${ports[@]}"; do
  name=${item%% *}
  port=${item##* }
  if [[ " ${occupied} " == *" ${port} "* ]]; then
    printf 'El puerto %s (%s) pertenece a otro servicio de este servidor.\n' "$port" "$name" >&2
    fail=1
    continue
  fi
  if situ_port_listening "$port"; then
    printf 'El puerto %s (%s) ya está ocupado.\n' "$port" "$name" >&2
    fail=1
  fi
done

if [[ "$fail" -ne 0 ]]; then
  exit 1
fi

printf 'Los puertos de SITU están libres.\n'
