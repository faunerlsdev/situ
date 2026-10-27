#!/usr/bin/env bash
# Render the nginx site, request the certificate if needed, and reload.
# --render-only <dir> writes the files and does not touch the live server.
set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/../lib/common.sh"

RENDER_ONLY=""
if [[ "${1:-}" == "--render-only" ]]; then
  RENDER_ONLY="${2:-}"
  if [[ -z "${RENDER_ONLY}" ]]; then
    die "uso: install.sh --render-only <directorio>"
  fi
  mkdir -p "${RENDER_ONLY}/snippets"
fi

: "${DOMAIN_ROOT:?Define DOMAIN_ROOT}"
: "${LETSENCRYPT_EMAIL:?Define LETSENCRYPT_EMAIL}"
: "${APPLE_TEAM_ID:?Define APPLE_TEAM_ID}"
: "${APPLE_BUNDLE_ID:?Define APPLE_BUNDLE_ID}"
: "${ANDROID_PACKAGE:?Define ANDROID_PACKAGE}"
: "${ANDROID_SHA256:?Define ANDROID_SHA256}"

valid_hostname "${DOMAIN_ROOT}" || die "DOMAIN_ROOT no es un nombre de host."
[[ "${APPLE_TEAM_ID}" =~ ^[A-Za-z0-9]+$ ]] || die "APPLE_TEAM_ID inválido."
[[ "${APPLE_BUNDLE_ID}" =~ ^[A-Za-z0-9.]+$ ]] || die "APPLE_BUNDLE_ID inválido."
[[ "${ANDROID_PACKAGE}" =~ ^[A-Za-z0-9._]+$ ]] || die "ANDROID_PACKAGE inválido."
[[ "${ANDROID_SHA256}" =~ ^[A-Fa-f0-9:]+$ ]] || die "ANDROID_SHA256 inválido."
[[ "${LETSENCRYPT_EMAIL}" == *@* ]] || die "LETSENCRYPT_EMAIL inválido."

CERT_DIR="${CERT_DIR:-/etc/letsencrypt/live/${DOMAIN_ROOT}}"

render() {
  local source="$1"
  local target="$2"
  sed \
    -e "s|__DOMAIN_ROOT__|${DOMAIN_ROOT}|g" \
    -e "s|__CERT_DIR__|${CERT_DIR}|g" \
    -e "s|__APPLE_TEAM_ID__|${APPLE_TEAM_ID}|g" \
    -e "s|__APPLE_BUNDLE_ID__|${APPLE_BUNDLE_ID}|g" \
    -e "s|__ANDROID_PACKAGE__|${ANDROID_PACKAGE}|g" \
    -e "s|__ANDROID_SHA256__|${ANDROID_SHA256}|g" \
    "$source" > "$target"
}

if [[ -n "${RENDER_ONLY}" ]]; then
  render "${SCRIPT_DIR}/situ.conf.template" "${RENDER_ONLY}/situ.conf"
  render "${SCRIPT_DIR}/apple-app-site-association.template" "${RENDER_ONLY}/apple-app-site-association"
  render "${SCRIPT_DIR}/assetlinks.json.template" "${RENDER_ONLY}/assetlinks.json"
  cp "${SCRIPT_DIR}/situ-proxy.conf" "${RENDER_ONLY}/snippets/situ-proxy.conf"
  log "Configuración escrita en ${RENDER_ONLY}"
  exit 0
fi

require_root
install -d /etc/nginx/snippets /var/lib/situ/well-known /var/lib/situ/acme
install -m 644 "${SCRIPT_DIR}/situ-proxy.conf" /etc/nginx/snippets/situ-proxy.conf
render "${SCRIPT_DIR}/situ.conf.template" /etc/nginx/sites-available/situ.conf
render "${SCRIPT_DIR}/apple-app-site-association.template" /var/lib/situ/well-known/apple-app-site-association
render "${SCRIPT_DIR}/assetlinks.json.template" /var/lib/situ/well-known/assetlinks.json
chmod 644 /var/lib/situ/well-known/apple-app-site-association /var/lib/situ/well-known/assetlinks.json
ln -sfn /etc/nginx/sites-available/situ.conf /etc/nginx/sites-enabled/situ.conf
rm -f /etc/nginx/sites-enabled/default

if [[ ! -f "${CERT_DIR}/fullchain.pem" || ! -f "${CERT_DIR}/privkey.pem" ]]; then
  http_only=$(mktemp)
  cat > "$http_only" <<EOF
server {
    listen 80;
    listen [::]:80;
    server_name ${DOMAIN_ROOT} www.${DOMAIN_ROOT} app.${DOMAIN_ROOT} admin.${DOMAIN_ROOT} ayuda.${DOMAIN_ROOT} api.${DOMAIN_ROOT};
    location ^~ /.well-known/acme-challenge/ { root /var/lib/situ/acme; default_type text/plain; }
    location / { return 404; }
}
EOF
  install -m 644 "$http_only" /etc/nginx/sites-available/situ.conf
  rm -f "$http_only"
  nginx -t
  systemctl reload nginx
  certbot certonly --webroot -w /var/lib/situ/acme \
    --non-interactive --agree-tos -m "${LETSENCRYPT_EMAIL}" --keep-until-expiring \
    -d "${DOMAIN_ROOT}" \
    -d "www.${DOMAIN_ROOT}" \
    -d "app.${DOMAIN_ROOT}" \
    -d "admin.${DOMAIN_ROOT}" \
    -d "ayuda.${DOMAIN_ROOT}" \
    -d "api.${DOMAIN_ROOT}"
  render "${SCRIPT_DIR}/situ.conf.template" /etc/nginx/sites-available/situ.conf
fi

if [[ ! -f /etc/letsencrypt/renewal-hooks/deploy/situ-reload.sh ]]; then
  install -d /etc/letsencrypt/renewal-hooks/deploy
  printf '#!/bin/sh\nsystemctl reload nginx\n' > /etc/letsencrypt/renewal-hooks/deploy/situ-reload.sh
  chmod 755 /etc/letsencrypt/renewal-hooks/deploy/situ-reload.sh
fi

nginx -t
systemctl reload nginx
systemctl enable --now certbot.timer || true
log "nginx recargado para ${DOMAIN_ROOT}."
