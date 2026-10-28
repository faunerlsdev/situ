#!/usr/bin/env bash
# Shared helpers for the server scripts. Source this file; do not execute it.

if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
  printf 'Este archivo se importa, no se ejecuta.\n' >&2
  exit 1
fi

log() {
  printf '%s\n' "$*"
}

die() {
  printf 'error: %s\n' "$*" >&2
  exit 1
}

require_root() {
  if [[ "$(id -u)" -ne 0 ]]; then
    die "Ejecuta este script con sudo."
  fi
}

installed() {
  dpkg -s "$1" >/dev/null 2>&1
}

ensure_package() {
  local package="$1"
  if installed "$package"; then
    log "Ya está instalado: ${package}"
    return 0
  fi
  die "Falta el paquete ${package}. Este servidor es compartido: no instalo paquetes del sistema."
}

valid_hostname() {
  [[ "$1" =~ ^[A-Za-z0-9]([A-Za-z0-9.-]*[A-Za-z0-9])?$ ]]
}
