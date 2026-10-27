#!/usr/bin/env bash
# Nightly pg_dump (age-encrypted) and an incremental media snapshot.
# The unencrypted working copy stays root-only. The age files are what leave the machine.
set -euo pipefail

if [[ "$(id -u)" -ne 0 ]]; then
  printf 'backup.sh se ejecuta como root.\n' >&2
  exit 1
fi

set -a
# shellcheck disable=SC1091
source /etc/situ/backup.env
set +a

: "${AGE_RECIPIENT:?Falta AGE_RECIPIENT}"

alert() {
  local body
  body=$(mktemp)
  printf 'El respaldo de SITU falló en %s.\nRevisa journalctl -u situ-backup.\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > "$body"
  /usr/bin/python3 /opt/situ/bin/alert.py "SITU respaldo falló" "$body" || true
  rm -f "$body"
}
trap alert ERR

install -d -m 700 /var/backups/situ/db/daily /var/backups/situ/db/weekly /var/backups/situ/media /var/backups/situ/media-work

stamp=$(date -u +%Y%m%d)
dump="/var/backups/situ/db/daily/situ-${stamp}.dump.age"
sudo -u postgres pg_dump -Fc situ | age -r "$AGE_RECIPIENT" -o "$dump"
chmod 600 "$dump"

if [[ "$(date -u +%u)" -eq 7 ]]; then
  cp -a "$dump" "/var/backups/situ/db/weekly/situ-${stamp}.dump.age"
fi

work="/var/backups/situ/media-work/${stamp}"
mkdir -p "$work"
if [[ -d /var/backups/situ/media-work/latest ]]; then
  rsync -a --delete --link-dest=/var/backups/situ/media-work/latest /var/lib/situ/media/ "${work}/"
else
  rsync -a --delete /var/lib/situ/media/ "${work}/"
fi
chmod 700 "$work"
ln -sfn "$stamp" /var/backups/situ/media-work/latest
tar -C "$work" -cf - . | age -r "$AGE_RECIPIENT" -o "/var/backups/situ/media/${stamp}.tar.age"
chmod 600 "/var/backups/situ/media/${stamp}.tar.age"

find /var/backups/situ/db/daily -name '*.dump.age' -mtime +14 -delete
find /var/backups/situ/db/weekly -name '*.dump.age' -mtime +56 -delete
find /var/backups/situ/media -name '*.tar.age' -mtime +14 -delete
find /var/backups/situ/media-work -mindepth 1 -maxdepth 1 -type d -mtime +2 -exec rm -rf {} +

if [[ -n "${RCLONE_REMOTE:-}" ]]; then
  rclone copy /var/backups/situ/db "${RCLONE_REMOTE}/db"
  rclone copy /var/backups/situ/media "${RCLONE_REMOTE}/media"
fi

trap - ERR
printf 'Respaldo %s listo.\n' "$stamp"
