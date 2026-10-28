# Create the situ user, directories and units. Source this; do not execute it.
# Does not install packages or reload nginx.

if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
  printf 'Este archivo se importa, no se ejecuta.\n' >&2
  exit 1
fi

situ_install_layout() {
  local deploy_dir
  deploy_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)

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

  if [[ ! -f /etc/situ/ports.env ]]; then
    install -m 644 "${deploy_dir}/ports.env" /etc/situ/ports.env
    log "Puertos copiados a /etc/situ/ports.env."
  else
    log "Se conserva /etc/situ/ports.env."
  fi
  install -m 640 -o root -g situ /etc/situ/ports.env /opt/situ/ports.env

  install -m 755 "${deploy_dir}/lib/start-next.sh" /opt/situ/bin/start-next.sh
  install -m 755 "${deploy_dir}/lib/start-api.sh" /opt/situ/bin/start-api.sh
  install -m 755 "${deploy_dir}/backup.sh" /opt/situ/bin/backup.sh
  install -m 755 "${deploy_dir}/restore.sh" /opt/situ/bin/restore.sh
  install -m 755 "${deploy_dir}/monitor.sh" /opt/situ/bin/monitor.sh
  install -m 755 "${deploy_dir}/lib/alert.py" /opt/situ/bin/alert.py
  install -m 755 "${deploy_dir}/smoke.sh" /opt/situ/bin/smoke.sh

  install -d /etc/systemd/system
  local unit
  for unit in situ-api situ-web situ-admin situ-landing situ-support situ-backup situ-monitor; do
    install -m 644 "${deploy_dir}/systemd/${unit}.service" "/etc/systemd/system/${unit}.service"
  done
  install -m 644 "${deploy_dir}/systemd/situ-backup.timer" /etc/systemd/system/situ-backup.timer
  install -m 644 "${deploy_dir}/systemd/situ-monitor.timer" /etc/systemd/system/situ-monitor.timer
  systemctl daemon-reload
  log "Unidades situ-* instaladas. No reinicié nginx."
}
