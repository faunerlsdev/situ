#!/usr/bin/env bash
# Runs on the server after the release directory is in place.
# Usage: remote-release.sh <stamp>
set -euo pipefail

if [[ "$(id -u)" -ne 0 ]]; then
  printf 'remote-release.sh se ejecuta como root.\n' >&2
  exit 1
fi

stamp="${1:-}"
if [[ ! "$stamp" =~ ^[0-9]{14}$ ]]; then
  printf 'stamp inválido\n' >&2
  exit 1
fi

release="/opt/situ/releases/${stamp}"
if [[ ! -d "${release}/api" ]]; then
  printf 'No está %s\n' "$release" >&2
  exit 1
fi

previous=""
if [[ -L /opt/situ/current ]]; then
  previous=$(readlink -f /opt/situ/current || true)
fi

if [[ ! -d "${release}/landing" ]]; then
  if [[ -n "$previous" && -d "${previous}/landing" ]]; then
    cp -a "${previous}/landing" "${release}/landing"
    printf '\033[31m%s\033[0m\n' "ADVERTENCIA: este release no trae landing. Se reutilizó la de ${previous}." >&2
  else
    printf '\033[31m%s\033[0m\n' "ADVERTENCIA: no hay landing en este release. situ-landing no se reinicia." >&2
  fi
fi

chown -R root:situ "$release"
find "$release" -type d -exec chmod 750 {} +
find "$release" -type f -exec chmod 640 {} +

set -a
# shellcheck disable=SC1091
source /etc/situ/api.env
# shellcheck disable=SC1091
source /etc/situ/migrate.env
set +a

cd "${release}/api"
if ! runuser -u situ --preserve-environment -- /usr/bin/dotnet Situ.Api.dll migrate; then
  printf 'La migración falló. current no cambió.\n' >&2
  exit 1
fi

ln -sfn "$release" /opt/situ/current
services=(situ-api situ-web situ-admin situ-support)
if [[ -d "${release}/landing" ]]; then
  services+=(situ-landing)
fi
systemctl enable "${services[@]}"
systemctl restart "${services[@]}"

set -a
# shellcheck disable=SC1091
source /opt/situ/ports.env
set +a

ready=0
for _ in 1 2 3 4 5 6 7 8 9 10 11 12; do
  if curl -fsS --max-time 5 "http://127.0.0.1:${SITU_API_PORT}/health/ready" >/dev/null; then
    ready=1
    break
  fi
  sleep 3
done

if [[ "$ready" -ne 1 ]]; then
  printf 'health/ready no respondió. Vuelvo al release anterior.\n' >&2
  if [[ -n "$previous" && "$previous" != "$release" ]]; then
    ln -sfn "$previous" /opt/situ/current
    systemctl restart "${services[@]}" || true
  else
    systemctl stop "${services[@]}" || true
    rm -f /opt/situ/current
  fi
  exit 1
fi

mapfile -t names < <(find /opt/situ/releases -mindepth 1 -maxdepth 1 -type d -printf '%f\n' | sort)
current_name=$(basename "$(readlink -f /opt/situ/current)")
if [[ "${#names[@]}" -gt 3 ]]; then
  extra=$((${#names[@]} - 3))
  index=0
  for name in "${names[@]}"; do
    if [[ "$index" -ge "$extra" ]]; then
      break
    fi
    index=$((index + 1))
    if [[ "$name" == "$current_name" || "$name" == "$(basename "$previous")" ]]; then
      continue
    fi
    rm -rf "/opt/situ/releases/${name}"
  done
fi

printf 'Release %s activo.\n' "$stamp"
