#!/usr/bin/env bash
# Check the public hosts. Does not log in and does not print response bodies
# beyond status codes and header names.
set -euo pipefail

: "${DOMAIN_ROOT:?Define DOMAIN_ROOT}"

fail=0
note() { printf '%s\n' "$*"; }
bad() { printf 'FALLO: %s\n' "$*" >&2; fail=1; }

check_status() {
  local url="$1"
  local expected="$2"
  local code
  code=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 20 "$url" || printf '000')
  if [[ "$code" != "$expected" ]]; then
    bad "${url} respondió ${code}, esperaba ${expected}"
  else
    note "ok ${code} ${url}"
  fi
}

header_has() {
  local url="$1"
  local name="$2"
  if ! curl -sSI --max-time 20 "$url" | grep -iq "^${name}:"; then
    bad "${url} no envía ${name}"
  else
    note "ok ${name} ${url}"
  fi
}

check_status "https://api.${DOMAIN_ROOT}/health/live" 200
check_status "https://api.${DOMAIN_ROOT}/health/ready" 200
check_status "https://api.${DOMAIN_ROOT}/api/v1/app-config" 200
check_status "https://app.${DOMAIN_ROOT}/login" 200

negotiate_code=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 20 -X POST "https://api.${DOMAIN_ROOT}/hubs/map/negotiate?negotiateVersion=1" || printf '000')
if [[ "$negotiate_code" == "404" || "$negotiate_code" == "000" ]]; then
  bad "el hub respondió ${negotiate_code}"
else
  note "ok ${negotiate_code} hub negotiate"
fi

admin_code=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 20 \
  -H 'content-type: application/json' \
  -d '{"email":"nobody@example.com","password":"not-a-real-password"}' \
  "https://api.${DOMAIN_ROOT}/api/v1/admin/auth/login" || printf '000')
case "$admin_code" in
  400 | 401 | 429) note "ok ${admin_code} admin login" ;;
  *) bad "admin login respondió ${admin_code}" ;;
esac

check_legal() {
  local path="$1"
  local url="https://${DOMAIN_ROOT}${path}"
  local body
  body=$(mktemp)
  local code
  code=$(curl -sS -o "$body" -w '%{http_code}' --max-time 20 "$url" || printf '000')
  if [[ "$code" != "200" ]]; then
    bad "${url} respondió ${code}, esperaba 200"
  elif grep -q 'Texto en revisión' "$body"; then
    printf '\033[31mFALLO: %s todavía dice "Texto en revisión". No sirve para la versión de las tiendas.\033[0m\n' "$url" >&2
    fail=1
  else
    note "ok legal ${url}"
  fi
  rm -f "$body"
}

check_legal /terminos
check_legal /privacidad
check_legal /politica-de-tratamiento-de-datos
check_legal /contenido-y-moderacion

for host in "$DOMAIN_ROOT" "app.${DOMAIN_ROOT}" "admin.${DOMAIN_ROOT}" "ayuda.${DOMAIN_ROOT}" "api.${DOMAIN_ROOT}"; do
  header_has "https://${host}/" "strict-transport-security"
  header_has "https://${host}/" "x-content-type-options"
done

if [[ "$fail" -ne 0 ]]; then
  exit 1
fi
note "Smoke en verde."
