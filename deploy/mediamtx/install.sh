#!/usr/bin/env bash
# Idempotent MediaMTX install for Ubuntu. Safe to run again.
set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/../lib/common.sh"
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/../lib/ports.sh"
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/../lib/nginx.sh"
situ_load_ports
situ_apply_domain
require_root

# A reinstall may find these ports already held by situ-mediamtx. Another process still stops the script.
ours=""
if systemctl is-active --quiet situ-mediamtx.service; then
  ours=$(systemctl show -p MainPID --value situ-mediamtx.service 2>/dev/null || true)
fi
reinstall=0
for port in "$SITU_RTMPS_PORT" "$SITU_HLS_PORT" "$SITU_MTX_API_PORT"; do
  if ! situ_port_listening "$port"; then
    continue
  fi
  owner=$(ss -lntpH "sport = :${port}" 2>/dev/null | sed -n '1s/.*pid=\([0-9][0-9]*\).*/\1/p' || true)
  if [[ -z "$ours" || "$ours" == "0" || "$owner" != "$ours" ]]; then
    die "${port} ya está ocupado por otro proceso. MediaMTX no se instala. El ${SITU_RTMPS_PORT} solo se usa si está libre o si ya lo tiene situ-mediamtx."
  fi
  reinstall=1
done
if [[ "$reinstall" == 1 ]]; then
  log "Reinstalo: esos puertos ya los tiene situ-mediamtx. /etc/situ/live.env no se regenera."
fi

: "${DOMAIN_ROOT:?Define SITU_DOMAIN_ROOT en ports.env}"
valid_hostname "${DOMAIN_ROOT}" || die "DOMAIN_ROOT no es un nombre de host."

VERSION=v1.21.3
ARCH=$(uname -m)
case "$ARCH" in
  x86_64) ASSET=linux_amd64 ;;
  aarch64|arm64) ASSET=linux_arm64 ;;
  *) die "Arquitectura no soportada: ${ARCH}" ;;
esac
TARBALL="mediamtx_${VERSION}_${ASSET}.tar.gz"
URL="https://github.com/bluenviron/mediamtx/releases/download/${VERSION}/${TARBALL}"
LIVE_HOST="live.${DOMAIN_ROOT}"
ENV_FILE=/etc/situ/live.env
AUTH_URL="${LIVE_AUTH_URL:-http://127.0.0.1:${SITU_API_PORT}/internal/live/authorize}"

install -d -m 755 /etc/mediamtx /etc/mediamtx/tls /opt/situ/bin /usr/local/bin
if ! id situ-mediamtx >/dev/null 2>&1; then
  useradd --system --home /var/lib/situ/live --shell /usr/sbin/nologin --user-group situ-mediamtx
fi
install -d -o situ-mediamtx -g situ -m 2750 /var/lib/situ/live
install -d -o situ-mediamtx -g situ-mediamtx -m 750 /etc/mediamtx/tls

if [[ ! -f "$ENV_FILE" ]]; then
  ingest=$(openssl rand -hex 32)
  hls=$(openssl rand -hex 32)
  cdn=$(openssl rand -hex 32)
  install -m 600 /dev/null "$ENV_FILE"
  cat > "$ENV_FILE" <<EOF
DOMAIN_ROOT="${DOMAIN_ROOT}"
LIVE_AUTH_URL="${AUTH_URL}"
LIVE_AUTH_PORT="${SITU_LIVE_AUTH_PORT}"
LIVE_INGEST_SECRET="${ingest}"
LIVE_HLS_SECRET="${hls}"
HLS_CDN_SECRET="${cdn}"
EOF
  chmod 600 "$ENV_FILE"
  log "Secretos nuevos en ${ENV_FILE}. Este script no los imprime."
else
  log "Se conserva ${ENV_FILE}."
fi

set -a
# shellcheck disable=SC1090
source "$ENV_FILE"
set +a
: "${LIVE_INGEST_SECRET:?Falta LIVE_INGEST_SECRET}"
: "${LIVE_HLS_SECRET:?Falta LIVE_HLS_SECRET}"
: "${HLS_CDN_SECRET:?Falta HLS_CDN_SECRET}"
AUTH_URL="${LIVE_AUTH_URL:-$AUTH_URL}"
case "$AUTH_URL" in
  *\?*) AUTH_FOR_MTX="${AUTH_URL}&secret=${LIVE_INGEST_SECRET}" ;;
  *) AUTH_FOR_MTX="${AUTH_URL}?secret=${LIVE_INGEST_SECRET}" ;;
esac

pinned=$(grep "[* ]${TARBALL}$" "${SCRIPT_DIR}/checksums.sha256" || true)
[[ -n "$pinned" ]] || die "No hay checksum fijado para ${TARBALL}."
expected=${pinned%% *}

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
curl -fsSL "$URL" -o "${work}/${TARBALL}"
curl -fsSL "https://github.com/bluenviron/mediamtx/releases/download/${VERSION}/checksums.sha256" -o "${work}/upstream.sha256"
upstream=$(grep "[* ]${TARBALL}$" "${work}/upstream.sha256" || true)
[[ "${upstream%% *}" == "$expected" ]] || die "El checksum publicado no coincide con el fijado en el repositorio."
(cd "$work" && printf '%s  %s\n' "$expected" "$TARBALL" | sha256sum -c -)
tar -xzf "${work}/${TARBALL}" -C "$work" mediamtx
install -m 755 "${work}/mediamtx" /usr/local/bin/mediamtx
printf '%s\n' "$VERSION" > /usr/local/bin/mediamtx.version

sed \
  -e "s|__LIVE_AUTH_URL__|${AUTH_FOR_MTX}|g" \
  -e "s|__HLS_CDN_SECRET__|${HLS_CDN_SECRET}|g" \
  -e "s|__SITU_HLS_PORT__|${SITU_HLS_PORT}|g" \
  -e "s|__SITU_MTX_API_PORT__|${SITU_MTX_API_PORT}|g" \
  -e "s|__SITU_RTMPS_PORT__|${SITU_RTMPS_PORT}|g" \
  "${SCRIPT_DIR}/mediamtx.yml" > /etc/mediamtx/mediamtx.yml
chown root:situ-mediamtx /etc/mediamtx/mediamtx.yml
chmod 640 /etc/mediamtx/mediamtx.yml

install -m 755 "${SCRIPT_DIR}/poc-auth.py" /opt/situ/bin/situ-live-auth.py
install -m 755 "${SCRIPT_DIR}/reload-certs.sh" /opt/situ/bin/mediamtx-reload-certs.sh
install -m 644 "${SCRIPT_DIR}/situ-mediamtx.service" /etc/systemd/system/situ-mediamtx.service
install -m 644 "${SCRIPT_DIR}/situ-live-auth.service" /etc/systemd/system/situ-live-auth.service

install -d /etc/letsencrypt/renewal-hooks/deploy
cat > /etc/letsencrypt/renewal-hooks/deploy/situ-reload.sh <<'EOF'
#!/bin/sh
systemctl reload nginx || true
if [ -x /opt/situ/bin/mediamtx-reload-certs.sh ]; then
  /opt/situ/bin/mediamtx-reload-certs.sh
fi
EOF
chmod 755 /etc/letsencrypt/renewal-hooks/deploy/situ-reload.sh

if [[ -f /etc/situ/nginx.env ]]; then
  set -a
  # shellcheck disable=SC1091
  source /etc/situ/nginx.env
  set +a
  situ_apply_domain
fi

if [[ ! -L /etc/nginx/sites-enabled/situ-limits ]]; then
  die "Falta sites-enabled/situ-limits. Corre primero deploy/nginx/install.sh. No toqué nginx."
fi

main_cert="/etc/letsencrypt/live/${DOMAIN_ROOT}/fullchain.pem"
if [[ ! -f "$main_cert" ]]; then
  die "Falta el certificado de ${DOMAIN_ROOT}. No creo uno nuevo para live."
fi
if ! openssl x509 -in "$main_cert" -noout -ext subjectAltName | grep -Fq "DNS:${LIVE_HOST}"; then
  die "Amplía el certificado existente, sin crear otro: sudo certbot certonly --cert-name ${DOMAIN_ROOT} --expand -d ${DOMAIN_ROOT} -d www.${DOMAIN_ROOT} -d api.${DOMAIN_ROOT} -d app.${DOMAIN_ROOT} -d admin.${DOMAIN_ROOT} -d ayuda.${DOMAIN_ROOT} -d ${LIVE_HOST}"
fi

DOMAIN_ROOT="$DOMAIN_ROOT" /opt/situ/bin/mediamtx-reload-certs.sh

sed \
  -e "s|somossitu.com|${DOMAIN_ROOT}|g" \
  -e "s|__HLS_CDN_SECRET__|${HLS_CDN_SECRET}|g" \
  "${SCRIPT_DIR}/../nginx/situ-live.conf" > /etc/nginx/sites-available/situ-live
situ_nginx_enable situ-live
situ_nginx_reload || die "nginx -t falló. Quité solo el enlace de SITU de esta ejecución."

systemctl daemon-reload
systemctl disable --now situ-live-auth.service 2>/dev/null || true
systemctl enable --now situ-mediamtx.service
systemctl restart situ-mediamtx.service
log "MediaMTX ${VERSION} escucha RTMPS en ${SITU_RTMPS_PORT}. HLS queda en 127.0.0.1:${SITU_HLS_PORT} y la API de control en 127.0.0.1:${SITU_MTX_API_PORT}."
log "Si el firewall bloquea ${SITU_RTMPS_PORT}/tcp, ábrelo tú. Este script no toca ufw."
