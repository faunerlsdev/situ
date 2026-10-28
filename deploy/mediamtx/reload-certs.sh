#!/bin/sh
# Copy the existing certificate where situ-mediamtx can read it, then reload.
set -eu

if [ -f /etc/situ/live.env ]; then
  # shellcheck disable=SC1091
  . /etc/situ/live.env
fi

domain=${DOMAIN_ROOT:?Falta DOMAIN_ROOT}
source="/etc/letsencrypt/live/${domain}"
target=/etc/mediamtx/tls

if [ ! -f "${source}/fullchain.pem" ] || [ ! -f "${source}/privkey.pem" ]; then
  echo "Aún no hay certificado para live.${domain}." >&2
  exit 1
fi

install -d -o situ-mediamtx -g situ-mediamtx -m 750 "$target"
install -o situ-mediamtx -g situ-mediamtx -m 640 "${source}/fullchain.pem" "${target}/fullchain.pem"
install -o situ-mediamtx -g situ-mediamtx -m 640 "${source}/privkey.pem" "${target}/privkey.pem"
if command -v systemctl >/dev/null 2>&1; then
  systemctl reload situ-mediamtx.service || systemctl restart situ-mediamtx.service || true
fi
