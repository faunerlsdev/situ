#!/usr/bin/env bash
# Render SITU nginx sites, request the certificate if needed, and reload.
# --render-only <dir> writes the files and does not touch the live server.
# Does not edit existing nginx files, does not remove the default site,
# and never restarts nginx. If nginx -t fails, only this run's symlinks go away.
set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/../lib/common.sh"
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/../lib/ports.sh"
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/../lib/nginx.sh"
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/../lib/layout.sh"
situ_load_ports
situ_apply_domain

if [[ -f /etc/situ/nginx.env ]]; then
  set -a
  # shellcheck disable=SC1091
  source /etc/situ/nginx.env
  set +a
  situ_apply_domain
fi

RENDER_ONLY=""
if [[ "${1:-}" == "--render-only" ]]; then
  RENDER_ONLY="${2:-}"
  if [[ -z "${RENDER_ONLY}" ]]; then
    die "uso: install.sh --render-only <directorio>"
  fi
  mkdir -p "${RENDER_ONLY}/snippets"
fi

clean_var() {
  local name="$1"
  local value="${!name-}"
  value="${value//$'\r'/}"
  value="${value#"${value%%[![:space:]]*}"}"
  value="${value%"${value##*[![:space:]]}"}"
  if [[ "$value" == \"*\" && "$value" == *\" ]]; then
    value="${value:1:${#value}-2}"
  fi
  printf -v "$name" '%s' "$value"
}

for name in DOMAIN_ROOT LETSENCRYPT_EMAIL APPLE_TEAM_ID APPLE_BUNDLE_ID ANDROID_PACKAGE ANDROID_SHA256; do
  clean_var "$name"
done

: "${DOMAIN_ROOT:?Define SITU_DOMAIN_ROOT en ports.env}"
if [[ -z "${APPLE_TEAM_ID:-}" || "${APPLE_TEAM_ID}" == *CHANGE_ME* ]]; then
  APPLE_TEAM_ID=Y27698UG63
  log "APPLE_TEAM_ID vacío o en CHANGE_ME. Uso Y27698UG63, el Team ID de app.json."
fi
if [[ -z "${APPLE_BUNDLE_ID:-}" || "${APPLE_BUNDLE_ID}" == *CHANGE_ME* ]]; then
  APPLE_BUNDLE_ID=com.somossitu.app
fi
if [[ -z "${ANDROID_PACKAGE:-}" || "${ANDROID_PACKAGE}" == *CHANGE_ME* ]]; then
  ANDROID_PACKAGE=com.somossitu.app
fi
if [[ -z "${ANDROID_SHA256:-}" || "${ANDROID_SHA256}" == *CHANGE_ME* || ! "${ANDROID_SHA256}" =~ ^[A-Fa-f0-9:]+$ ]]; then
  ANDROID_FINGERPRINTS=""
  log "ANDROID_SHA256 no es una huella. assetlinks.json queda vacío. Completa /etc/situ/nginx.env y vuelve a correr este script para los App Links."
else
  ANDROID_FINGERPRINTS="\"${ANDROID_SHA256}\""
fi

valid_hostname "${DOMAIN_ROOT}" || die "DOMAIN_ROOT no es un nombre de host."
[[ "${APPLE_TEAM_ID}" =~ ^[A-Za-z0-9]{10}$ ]] || die "APPLE_TEAM_ID inválido (${APPLE_TEAM_ID}). En /etc/situ/nginx.env pon los 10 caracteres, por ejemplo Y27698UG63."
[[ "${APPLE_BUNDLE_ID}" =~ ^[A-Za-z0-9.]+$ ]] || die "APPLE_BUNDLE_ID inválido (${APPLE_BUNDLE_ID})."
[[ "${ANDROID_PACKAGE}" =~ ^[A-Za-z0-9._]+$ ]] || die "ANDROID_PACKAGE inválido (${ANDROID_PACKAGE})."
if [[ -z "${LETSENCRYPT_EMAIL:-}" || "${LETSENCRYPT_EMAIL}" == *CHANGE_ME* || "${LETSENCRYPT_EMAIL}" != *@* ]]; then
  die "Falta LETSENCRYPT_EMAIL. Edita /etc/situ/nginx.env y pon un correo real."
fi

CERT_DIR="${CERT_DIR:-/etc/letsencrypt/live/${DOMAIN_ROOT}}"
SITES=(situ-limits situ-http situ-www situ-landing situ-web situ-admin situ-ayuda situ-api)

render() {
  local source="$1"
  local target="$2"
  sed \
    -e "s|__DOMAIN_ROOT__|${DOMAIN_ROOT}|g" \
    -e "s|__CERT_DIR__|${CERT_DIR}|g" \
    -e "s|__APPLE_TEAM_ID__|${APPLE_TEAM_ID}|g" \
    -e "s|__APPLE_BUNDLE_ID__|${APPLE_BUNDLE_ID}|g" \
    -e "s|__ANDROID_PACKAGE__|${ANDROID_PACKAGE}|g" \
    -e "s|__ANDROID_FINGERPRINTS__|${ANDROID_FINGERPRINTS}|g" \
    -e "s|__SITU_API_PORT__|${SITU_API_PORT}|g" \
    -e "s|__SITU_ADMIN_PORT__|${SITU_ADMIN_PORT}|g" \
    -e "s|__SITU_WEB_PORT__|${SITU_WEB_PORT}|g" \
    -e "s|__SITU_LANDING_PORT__|${SITU_LANDING_PORT}|g" \
    -e "s|__SITU_SUPPORT_PORT__|${SITU_SUPPORT_PORT}|g" \
    "$source" > "$target"
}

write_sites() {
  local dir="$1"
  local name
  for name in "${SITES[@]}"; do
    render "${SCRIPT_DIR}/${name}.conf.template" "${dir}/${name}"
  done
}

if [[ -n "${RENDER_ONLY}" ]]; then
  write_sites "${RENDER_ONLY}"
  render "${SCRIPT_DIR}/apple-app-site-association.template" "${RENDER_ONLY}/apple-app-site-association"
  render "${SCRIPT_DIR}/assetlinks.json.template" "${RENDER_ONLY}/assetlinks.json"
  cp "${SCRIPT_DIR}/situ-proxy.conf" "${RENDER_ONLY}/snippets/situ-proxy.conf"
  log "Configuración escrita en ${RENDER_ONLY}"
  exit 0
fi

require_root
situ_install_layout
install -d /etc/nginx/snippets /etc/nginx/sites-available /var/lib/situ/well-known /var/lib/situ/acme
install -m 644 "${SCRIPT_DIR}/situ-proxy.conf" /etc/nginx/snippets/situ-proxy.conf
render "${SCRIPT_DIR}/apple-app-site-association.template" /var/lib/situ/well-known/apple-app-site-association
render "${SCRIPT_DIR}/assetlinks.json.template" /var/lib/situ/well-known/assetlinks.json
chmod 644 /var/lib/situ/well-known/apple-app-site-association /var/lib/situ/well-known/assetlinks.json

legacy=/etc/nginx/conf.d/situ-live-limit.conf
removed_legacy=0
restore_legacy() {
  if [[ "$removed_legacy" == 1 && ! -e "$legacy" && ! -L /etc/nginx/sites-enabled/situ-limits ]]; then
    printf 'limit_conn_zone $binary_remote_addr zone=situ_live:10m;\n' > "$legacy"
    log "Devolví ${legacy}."
  fi
}
trap restore_legacy EXIT
if [[ -f "$legacy" ]] && grep -qx 'limit_conn_zone $binary_remote_addr zone=situ_live:10m;' "$legacy"; then
  rm -f "$legacy"
  removed_legacy=1
  log "Aparté ${legacy} para esta prueba. Si no queda sites-enabled/situ-limits, lo devuelvo."
fi

install_available() {
  local name
  for name in "$@"; do
    render "${SCRIPT_DIR}/${name}.conf.template" "/etc/nginx/sites-available/${name}"
  done
}

enable_sites() {
  local name
  for name in "$@"; do
    situ_nginx_enable "$name"
  done
}

if [[ ! -f "${CERT_DIR}/fullchain.pem" || ! -f "${CERT_DIR}/privkey.pem" ]]; then
  install_available situ-http
  enable_sites situ-http
  situ_nginx_reload || die "No recargué nginx. El sitio HTTP de SITU quedó desactivado."
  SITU_NGINX_LINKS=()
  certbot certonly --webroot -w /var/lib/situ/acme \
    --non-interactive --agree-tos -m "${LETSENCRYPT_EMAIL}" --keep-until-expiring \
    -d "${DOMAIN_ROOT}" \
    -d "www.${DOMAIN_ROOT}" \
    -d "app.${DOMAIN_ROOT}" \
    -d "admin.${DOMAIN_ROOT}" \
    -d "ayuda.${DOMAIN_ROOT}" \
    -d "api.${DOMAIN_ROOT}"
fi

install_available "${SITES[@]}"
enable_sites "${SITES[@]}"

if [[ ! -f /etc/letsencrypt/renewal-hooks/deploy/situ-reload.sh ]]; then
  install -d /etc/letsencrypt/renewal-hooks/deploy
  printf '#!/bin/sh\nsystemctl reload nginx\n' > /etc/letsencrypt/renewal-hooks/deploy/situ-reload.sh
  chmod 755 /etc/letsencrypt/renewal-hooks/deploy/situ-reload.sh
fi

if ! situ_nginx_reload; then
  die "nginx -t falló. Quité solo los enlaces de SITU de esta ejecución."
fi
systemctl enable certbot.timer || true
log "nginx recargado para ${DOMAIN_ROOT}. API en 127.0.0.1:${SITU_API_PORT}."
