#!/usr/bin/env bash
# Report missing host dependencies. Does not install packages, change ufw,
# or reload services. PostgreSQL is detected on its socket; TCP 5432 is not required.
set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/lib/ports.sh"
situ_load_ports

fail=0
note() { printf '%s\n' "$*"; }

postgres_package() {
  local version_id=""
  local package="postgresql-18-postgis-3"
  if [[ -r /etc/os-release ]]; then
    # shellcheck disable=SC1091
    version_id=$(. /etc/os-release && printf '%s' "${VERSION_ID:-}")
  fi
  case "$version_id" in
    22.04) package="postgresql-14-postgis-3" ;;
    24.04) package="postgresql-16-postgis-3" ;;
    26.04) package="postgresql-18-postgis-3" ;;
  esac
  printf '%s' "$package"
}

postgres_install_command() {
  printf 'sudo apt-get update && sudo apt-get install -y postgresql %s\n' "$(postgres_package)"
}

psql_as_postgres() {
  runuser -u postgres -- psql -v ON_ERROR_STOP=1 -tAc "$1"
}

if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-runtimes 2>/dev/null | grep -q 'Microsoft.AspNetCore.App 10\.'; then
  fail=1
  printf 'Falta ASP.NET Core Runtime 10 (basta 10.0.13). No hace falta el SDK y no lo instalo.\n' >&2
else
  note "ASP.NET Core Runtime 10 está instalado. El servidor solo ejecuta."
fi

if ! command -v node >/dev/null 2>&1 || ! node -e 'process.exit(Number(process.versions.node.split(".")[0]) >= 22 ? 0 : 1)'; then
  fail=1
  printf 'Hace falta Node.js 22 o superior para ejecutar server.js. No lo instalo. Este servidor usa Node 24.\n' >&2
else
  note "Node $(node -p 'process.versions.node') está instalado. pnpm no hace falta en el servidor."
fi

if ! command -v nginx >/dev/null 2>&1; then
  fail=1
  printf 'Falta nginx. No lo instalo. Comando: sudo apt-get install -y nginx\n' >&2
else
  note "nginx está instalado."
fi

if ! command -v certbot >/dev/null 2>&1; then
  fail=1
  printf 'Falta certbot. No lo instalo. Comando: sudo apt-get install -y certbot\n' >&2
fi

postgres_up=0
if id postgres >/dev/null 2>&1 && command -v runuser >/dev/null 2>&1 && command -v psql >/dev/null 2>&1; then
  if psql_as_postgres 'SELECT 1' >/dev/null 2>&1; then
    postgres_up=1
  fi
fi

if [[ "$postgres_up" -eq 1 ]]; then
  pg_port=$(psql_as_postgres 'SHOW port' | tr -d '[:space:]')
  pg_socket=$(psql_as_postgres 'SHOW unix_socket_directories' | tr -d '[:space:]')
  note "PostgreSQL responde por el socket ${pg_socket}. SHOW port=${pg_port}."
  if situ_port_listening "$pg_port"; then
    note "También hay TCP en el puerto ${pg_port}."
  else
    note "No hay TCP en el puerto ${pg_port}. Con SITU_POSTGRES_PORT vacío la API usa el socket."
  fi
  postgis=$(psql_as_postgres "SELECT 1 FROM pg_available_extensions WHERE name = 'postgis'" | tr -d '[:space:]' || true)
  if [[ "$postgis" != "1" ]]; then
    fail=1
    printf 'PostgreSQL está, pero falta PostGIS. No lo instalo. Comando: sudo apt-get install -y %s\n' "$(postgres_package)" >&2
  else
    note "PostGIS está disponible."
  fi
else
  fail=1
  printf 'PostgreSQL no responde por socket ni se pudo consultar. No asumo el puerto 5432.\n' >&2
  printf 'No lo instalo. Comando exacto:\n%s\n' "$(postgres_install_command)" >&2
fi

exit "$fail"
