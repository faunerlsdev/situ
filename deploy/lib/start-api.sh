#!/usr/bin/env bash
# Bind the API to the port in /opt/situ/ports.env. Does not use 5080.
set -euo pipefail

ports=/opt/situ/ports.env
if [[ ! -f "$ports" ]]; then
  printf 'Falta %s\n' "$ports" >&2
  exit 1
fi

set -a
# shellcheck disable=SC1090
source "$ports"
set +a

export ASPNETCORE_URLS="http://127.0.0.1:${SITU_API_PORT}"
cd /opt/situ/current/api
exec /usr/bin/dotnet Situ.Api.dll
