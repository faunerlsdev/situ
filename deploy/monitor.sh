#!/usr/bin/env bash
# Email once per condition per day. The message does not include secrets.
set -euo pipefail

if [[ "$(id -u)" -ne 0 ]]; then
  printf 'monitor.sh se ejecuta como root.\n' >&2
  exit 1
fi

set -a
# shellcheck disable=SC1091
source /etc/situ/monitor.env
set +a

: "${ALERT_TO:?Falta ALERT_TO}"
DISK_WARN="${DISK_WARN:-70}"
DISK_CRIT="${DISK_CRIT:-85}"
RAM_WARN="${RAM_WARN:-85}"
CERT_WARN_DAYS="${CERT_WARN_DAYS:-21}"
state_dir=/var/lib/situ/monitor-state
install -d -m 700 "$state_dir"

send_once() {
  local key="$1"
  local subject="$2"
  local detail="$3"
  local today
  today=$(date -u +%Y%m%d)
  local mark="${state_dir}/${key}"
  if [[ -f "$mark" && "$(cat "$mark")" == "$today" ]]; then
    return 0
  fi
  local body
  body=$(mktemp)
  printf '%s\n%s\n' "$subject" "$detail" > "$body"
  if /usr/bin/python3 /opt/situ/bin/alert.py "$subject" "$body"; then
    printf '%s\n' "$today" > "$mark"
  fi
  rm -f "$body"
}

clear_mark() {
  rm -f "${state_dir}/$1"
}

disk_used=$(df -P / | awk 'NR == 2 { gsub("%", "", $5); print $5 }')
if [[ "$disk_used" -ge "$DISK_CRIT" ]]; then
  send_once disk-crit "SITU disco crítico" "El disco está al ${disk_used}%."
elif [[ "$disk_used" -ge "$DISK_WARN" ]]; then
  send_once disk-warn "SITU disco alto" "El disco está al ${disk_used}%."
else
  clear_mark disk-crit
  clear_mark disk-warn
fi

ram_used=$(awk '/MemTotal/ { total = $2 } /MemAvailable/ { avail = $2 } END { if (total > 0) printf "%d", (total - avail) * 100 / total; else print 0 }' /proc/meminfo)
if [[ "$ram_used" -ge "$RAM_WARN" ]]; then
  send_once ram "SITU memoria alta" "La RAM usada está al ${ram_used}%."
else
  clear_mark ram
fi

for service in situ-api situ-web situ-admin situ-landing situ-support; do
  if systemctl is-active --quiet "$service"; then
    clear_mark "down-${service}"
  else
    send_once "down-${service}" "SITU servicio caído" "${service} no está activo."
  fi
done

newest=$(find /var/backups/situ/db/daily -name '*.dump.age' -printf '%T@\n' 2>/dev/null | sort -nr | head -n 1 || true)
now=$(date +%s)
if [[ -z "$newest" ]]; then
  uptime_seconds=$(awk '{ print int($1) }' /proc/uptime)
  if [[ "$uptime_seconds" -gt 93600 ]]; then
    send_once backup-old "SITU sin respaldo" "No hay un pg_dump cifrado y el servidor lleva más de 26 horas encendido."
  fi
else
  age_seconds=$(awk -v now="$now" -v newest="${newest%.*}" 'BEGIN { print int(now - newest) }')
  if [[ "$age_seconds" -gt 93600 ]]; then
    send_once backup-old "SITU respaldo viejo" "El último pg_dump tiene más de 26 horas."
  else
    clear_mark backup-old
  fi
fi

if [[ -n "${CERT_FILE:-}" && -f "$CERT_FILE" ]]; then
  if ! openssl x509 -checkend "$((CERT_WARN_DAYS * 86400))" -noout -in "$CERT_FILE" >/dev/null 2>&1; then
    send_once cert "SITU certificado por vencer" "El certificado vence en menos de ${CERT_WARN_DAYS} días."
  else
    clear_mark cert
  fi
fi
