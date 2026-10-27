#!/usr/bin/env bash
# Restore an age-encrypted dump. Refuses to replace the live database unless
# CONFIRM_RESTORE=yes. --check decrypts and lists the archive, then deletes the temp file.
set -euo pipefail

if [[ "$(id -u)" -ne 0 ]]; then
  printf 'restore.sh se ejecuta como root.\n' >&2
  exit 1
fi

CHECK=0
DUMP=""
TARGET="situ_restore_test"
MEDIA=""
MEDIA_DEST=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --check) CHECK=1; shift ;;
    --dump) DUMP="${2:-}"; shift 2 ;;
    --target-database) TARGET="${2:-}"; shift 2 ;;
    --media) MEDIA="${2:-}"; shift 2 ;;
    --media-dest) MEDIA_DEST="${2:-}"; shift 2 ;;
    *) printf 'Argumento desconocido: %s\n' "$1" >&2; exit 1 ;;
  esac
done

if [[ -z "$DUMP" || ! -f "$DUMP" ]]; then
  printf 'uso: restore.sh --dump <archivo.age> [--target-database situ_restore_test] [--check]\n' >&2
  exit 1
fi
if [[ ! "$TARGET" =~ ^[a-zA-Z0-9_]+$ ]]; then
  printf 'Nombre de base inválido.\n' >&2
  exit 1
fi
if [[ "$TARGET" == "situ" && "${CONFIRM_RESTORE:-}" != "yes" && "$CHECK" -ne 1 ]]; then
  printf 'Para reemplazar la base situ: CONFIRM_RESTORE=yes ./restore.sh --dump ... --target-database situ\n' >&2
  exit 1
fi

key=/etc/situ/age.key
if [[ ! -f "$key" ]]; then
  printf 'Falta %s\n' "$key" >&2
  exit 1
fi

tmp=$(mktemp)
chmod 600 "$tmp"
age -d -i "$key" -o "$tmp" "$DUMP"

if [[ "$CHECK" -eq 1 ]]; then
  pg_restore -l "$tmp" >/dev/null
  shred -u "$tmp" 2>/dev/null || rm -f "$tmp"
  printf 'El archivo se descifró y pg_restore pudo listarlo. No restauré nada.\n'
  exit 0
fi

if [[ "$TARGET" != "situ" ]]; then
  sudo -u postgres psql -v ON_ERROR_STOP=1 -c "SELECT 1 FROM pg_database WHERE datname = '${TARGET}'" | grep -q 1 \
    || sudo -u postgres createdb -O situ_migrator "$TARGET"
fi

sudo -u postgres pg_restore --clean --if-exists --no-owner --role=situ_migrator -d "$TARGET" "$tmp"
shred -u "$tmp" 2>/dev/null || rm -f "$tmp"
printf 'Base %s restaurada desde el respaldo.\n' "$TARGET"

if [[ -n "$MEDIA" ]]; then
  if [[ -z "$MEDIA_DEST" ]]; then
    printf 'Falta --media-dest\n' >&2
    exit 1
  fi
  media_tmp=$(mktemp -d)
  age -d -i "$key" "$MEDIA" | tar -C "$media_tmp" -xf -
  rsync -a "${media_tmp}/" "${MEDIA_DEST}/"
  rm -rf "$media_tmp"
  printf 'Medios restaurados en %s\n' "$MEDIA_DEST"
fi
