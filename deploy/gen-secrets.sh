#!/usr/bin/env bash
# Fill /etc/situ/*.env from the examples. Generated values stay in the files.
# This script prints key names, never the values.
set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/lib/common.sh"

require_root
umask 077

FORCE=0
if [[ "${1:-}" == "--force" ]]; then
  FORCE=1
fi

: "${DOMAIN_ROOT:?Define DOMAIN_ROOT}"
valid_hostname "${DOMAIN_ROOT}" || die "DOMAIN_ROOT no es un nombre de host."

install -d -m 700 /etc/situ

new_secret() {
  openssl rand -base64 48 | tr -d '\n'
}

set_key() {
  local file="$1"
  local key="$2"
  local value="$3"
  local tmp
  tmp=$(mktemp)
  if [[ -f "$file" ]]; then
    grep -v "^${key}=" "$file" > "$tmp" || true
  fi
  printf '%s=%s\n' "$key" "$value" >> "$tmp"
  chmod 600 "$tmp"
  mv "$tmp" "$file"
}

current_value() {
  local file="$1"
  local key="$2"
  if [[ ! -f "$file" ]]; then
    printf ''
    return
  fi
  grep "^${key}=" "$file" | head -n 1 | cut -d= -f2- || true
}

write_from_example() {
  local name="$1"
  local target="/etc/situ/${name}.env"
  if [[ -f "$target" && "$FORCE" -ne 1 ]]; then
    log "Conservo ${target}"
    return
  fi
  sed "s|__DOMAIN__|${DOMAIN_ROOT}|g" "${SCRIPT_DIR}/env/${name}.env.example" > "$target"
  chmod 600 "$target"
  log "Escrito ${target}"
}

for name in postgres api migrate web admin landing support backup monitor nginx; do
  write_from_example "$name"
done

jwt=$(new_secret)
guardian=$(new_secret)
media=$(new_secret)
app_password=$(new_secret)
migrator_password=$(new_secret)

fill_if_placeholder() {
  local file="$1"
  local key="$2"
  local value="$3"
  local current
  current=$(current_value "$file" "$key")
  if [[ "$FORCE" -eq 1 || -z "$current" || "$current" == *CHANGE_ME* ]]; then
    set_key "$file" "$key" "$value"
    log "Generado ${key} en ${file}"
  fi
}

fill_if_placeholder /etc/situ/postgres.env APP_PASSWORD "$app_password"
fill_if_placeholder /etc/situ/postgres.env MIGRATOR_PASSWORD "$migrator_password"
fill_if_placeholder /etc/situ/api.env Jwt__Secret "$jwt"
fill_if_placeholder /etc/situ/api.env GuardianConsent__SigningKey "$guardian"
fill_if_placeholder /etc/situ/api.env FileStorage__SigningKey "$media"

app_password=$(current_value /etc/situ/postgres.env APP_PASSWORD)
migrator_password=$(current_value /etc/situ/postgres.env MIGRATOR_PASSWORD)
fill_if_placeholder /etc/situ/api.env ConnectionStrings__Default \
  "Host=127.0.0.1;Port=5432;Database=situ;Username=situ_app;Password=${app_password}"
fill_if_placeholder /etc/situ/migrate.env ConnectionStrings__Default \
  "Host=127.0.0.1;Port=5432;Database=situ;Username=situ_migrator;Password=${migrator_password}"

if [[ ! -f /etc/situ/age.key ]]; then
  public_key=$(age-keygen -o /etc/situ/age.key | sed -n 's/^# public key: //p')
  chmod 600 /etc/situ/age.key
  if [[ -z "$public_key" ]]; then
    die "age-keygen no devolvió la clave pública."
  fi
  set_key /etc/situ/backup.env AGE_RECIPIENT "$public_key"
  log "Clave age creada en /etc/situ/age.key"
fi

unset jwt guardian media app_password migrator_password public_key

pending=0
while IFS= read -r key; do
  [[ -n "$key" ]] || continue
  printf 'pendiente: %s\n' "$key"
  pending=1
done < <(grep -h 'CHANGE_ME' /etc/situ/*.env | cut -d= -f1 | sort -u)

if [[ "$pending" -eq 1 ]]; then
  log "Esos valores los escribes tú en /etc/situ. El script no los imprime."
  exit 2
fi

log "Secretos generados. No quedó ningún CHANGE_ME."
