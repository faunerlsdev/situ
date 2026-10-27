#!/usr/bin/env bash
# Idempotent host preparation for a small Ubuntu VPS. Does not reinstall
# runtimes that are already present, and does not change SSH without
# CONFIRM_SSH_HARDENING=yes.
set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/lib/common.sh"

require_root

if [[ ! -r /etc/os-release ]]; then
  die "No se pudo leer /etc/os-release."
fi
# shellcheck disable=SC1091
source /etc/os-release
case "${VERSION_ID}" in
  22.04 | 24.04) log "Ubuntu ${VERSION_ID}" ;;
  *) die "Hace falta Ubuntu 22.04 o 24.04. Esta máquina es ${VERSION_ID:-desconocida}." ;;
esac

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    die "Falta el comando ${1}. Instálalo antes de continuar: ${2}"
  fi
}

require_cmd nginx "apt-get install nginx"
require_cmd psql "apt-get install postgresql"
require_cmd node "instala Node.js 22 LTS"
require_cmd dotnet "instala ASP.NET Core Runtime 10"

if ! dotnet --list-runtimes | grep -q 'Microsoft.AspNetCore.App 10\.'; then
  die "Falta Microsoft.AspNetCore.App 10. No reinstalo el runtime."
fi

node_major=$(node -p 'process.versions.node.split(".")[0]')
if [[ "${node_major}" -lt 22 ]]; then
  die "Node es $(node -v). Hace falta Node 22 LTS o superior. No lo reinstalo."
fi

if ! command -v pnpm >/dev/null 2>&1; then
  log "pnpm no está. Lo activo con corepack, sin reinstalar Node."
  corepack enable
  corepack prepare pnpm@10.28.0 --activate
fi

if ! command -v pnpm >/dev/null 2>&1; then
  die "pnpm sigue sin estar disponible."
fi

pg_ready=0
if sudo -u postgres psql -tAc 'SELECT 1' >/dev/null 2>&1; then
  pg_ready=1
fi
if [[ "${pg_ready}" -ne 1 ]]; then
  die "PostgreSQL no responde como el usuario postgres. No reinstalo el servidor."
fi

if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_available_extensions WHERE name = 'postgis'" | grep -q 1; then
  pg_major=$(psql --version | awk '{print $3}' | cut -d. -f1)
  postgis_package="postgresql-${pg_major}-postgis-3"
  log "PostGIS no está disponible. Instalo solo ${postgis_package}."
  apt-get update
  ensure_package "$postgis_package"
fi

apt-get update
ensure_package ufw
ensure_package fail2ban
ensure_package unattended-upgrades
ensure_package age
ensure_package certbot
ensure_package python3-certbot-nginx
ensure_package acl

if [[ ! -f /etc/apt/apt.conf.d/20auto-upgrades ]]; then
  printf 'APT::Periodic::Update-Package-Lists "1";\nAPT::Periodic::Unattended-Upgrade "1";\n' \
    > /etc/apt/apt.conf.d/20auto-upgrades
fi
systemctl enable --now unattended-upgrades

if ! id situ >/dev/null 2>&1; then
  useradd --system --home /var/lib/situ --shell /usr/sbin/nologin --create-home situ
  log "Usuario situ creado."
else
  log "El usuario situ ya existe."
fi

install -d -o root -g situ -m 750 /opt/situ /opt/situ/releases /opt/situ/bin
install -d -o situ -g situ -m 750 /var/lib/situ /var/lib/situ/media /var/lib/situ/well-known /var/lib/situ/dataprotection
install -d -o root -g root -m 755 /var/lib/situ/acme
install -d -m 700 /etc/situ
install -d -o root -g root -m 700 /var/backups/situ /var/backups/situ/db/daily /var/backups/situ/db/weekly /var/backups/situ/media /var/backups/situ/media-work

install -m 755 "${SCRIPT_DIR}/lib/start-next.sh" /opt/situ/bin/start-next.sh
install -m 755 "${SCRIPT_DIR}/backup.sh" /opt/situ/bin/backup.sh
install -m 755 "${SCRIPT_DIR}/restore.sh" /opt/situ/bin/restore.sh
install -m 755 "${SCRIPT_DIR}/monitor.sh" /opt/situ/bin/monitor.sh
install -m 755 "${SCRIPT_DIR}/lib/alert.py" /opt/situ/bin/alert.py
install -m 755 "${SCRIPT_DIR}/smoke.sh" /opt/situ/bin/smoke.sh

install -d /etc/systemd/journald.conf.d
install -m 644 "${SCRIPT_DIR}/systemd/journald-situ.conf" /etc/systemd/journald.conf.d/situ.conf
systemctl restart systemd-journald

install -d /etc/systemd/system
for unit in situ-api situ-web situ-admin situ-landing situ-support situ-backup situ-monitor; do
  install -m 644 "${SCRIPT_DIR}/systemd/${unit}.service" "/etc/systemd/system/${unit}.service"
done
install -m 644 "${SCRIPT_DIR}/systemd/situ-backup.timer" /etc/systemd/system/situ-backup.timer
install -m 644 "${SCRIPT_DIR}/systemd/situ-monitor.timer" /etc/systemd/system/situ-monitor.timer
systemctl daemon-reload

if ! swapon --show | awk 'NR > 1 { found = 1 } END { exit found ? 0 : 1 }'; then
  if [[ ! -f /swapfile ]]; then
    fallocate -l 2G /swapfile || dd if=/dev/zero of=/swapfile bs=1M count=2048 status=none
    chmod 600 /swapfile
    mkswap /swapfile >/dev/null
  fi
  swapon /swapfile
  if ! grep -q '/swapfile' /etc/fstab; then
    printf '/swapfile none swap sw 0 0\n' >> /etc/fstab
  fi
  log "Swap de 2 GB activo."
else
  log "Ya hay swap. No lo reemplazo."
fi

if ufw status | grep -q 'Status: active'; then
  log "ufw ya está activo."
else
  ufw default deny incoming
  ufw default allow outgoing
  ufw limit 22/tcp
  ufw allow 80/tcp
  ufw allow 443/tcp
  ufw --force enable
fi

install -m 644 "${SCRIPT_DIR}/fail2ban/jail.local" /etc/fail2ban/jail.local
systemctl enable --now fail2ban
systemctl reload fail2ban || systemctl restart fail2ban

password_auth=$(sshd -T 2>/dev/null | awk '$1 == "passwordauthentication" { print $2 }' || true)
permit_root=$(sshd -T 2>/dev/null | awk '$1 == "permitrootlogin" { print $2 }' || true)
if [[ "${password_auth}" == "yes" || "${permit_root}" == "yes" ]]; then
  log "AVISO: SSH acepta contraseña (passwordauthentication=${password_auth:-?} permitrootlogin=${permit_root:-?})."
  log "No cambio sshd. Para cerrarlo: CONFIRM_SSH_HARDENING=yes sudo ./deploy/provision.sh"
  if [[ "${CONFIRM_SSH_HARDENING:-}" == "yes" ]]; then
    install -d /etc/ssh/sshd_config.d
    cat > /etc/ssh/sshd_config.d/99-situ-keys.conf <<'EOF'
PasswordAuthentication no
KbdInteractiveAuthentication no
ChallengeResponseAuthentication no
PermitRootLogin prohibit-password
EOF
    sshd -t
    systemctl reload ssh || systemctl reload sshd
    log "SSH quedó solo con llave. La sesión actual no se corta."
  fi
else
  log "SSH ya está solo con llave."
fi

pg_conf_dir=$(find /etc/postgresql -mindepth 2 -maxdepth 2 -type d -name main -print -quit)
if [[ -z "${pg_conf_dir}" ]]; then
  die "No encontré /etc/postgresql/*/main."
fi
conf_target="${pg_conf_dir}/conf.d/situ.conf"
if [[ ! -f "${conf_target}" ]] || ! cmp -s "${SCRIPT_DIR}/postgres/situ.conf" "${conf_target}"; then
  install -m 644 "${SCRIPT_DIR}/postgres/situ.conf" "${conf_target}"
  systemctl restart postgresql
  log "PostgreSQL reiniciado para aplicar memoria y listen_addresses."
else
  log "La configuración de PostgreSQL ya estaba aplicada."
fi

log "Provision lista. Siguiente: sudo ./deploy/postgres/apply.sh"
