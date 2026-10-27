#!/usr/bin/env bash
# Create the application role, the migration role, the database and PostGIS.
# Passwords come from /etc/situ/postgres.env and are not printed.
set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/../lib/common.sh"

require_root

env_file=/etc/situ/postgres.env
if [[ ! -f "${env_file}" ]]; then
  die "Falta ${env_file}. Ejecuta gen-secrets.sh y completa ese archivo."
fi
set -a
# shellcheck disable=SC1090,SC1091
source "${env_file}"
set +a

if [[ -z "${APP_PASSWORD:-}" || -z "${MIGRATOR_PASSWORD:-}" ]]; then
  die "postgres.env debe definir APP_PASSWORD y MIGRATOR_PASSWORD."
fi
if [[ "${APP_PASSWORD}" == *CHANGE_ME* || "${MIGRATOR_PASSWORD}" == *CHANGE_ME* ]]; then
  die "Las contraseñas de postgres.env siguen en CHANGE_ME."
fi

run_sql() {
  sudo -u postgres psql -v ON_ERROR_STOP=1 \
    -v app_password="${APP_PASSWORD}" \
    -v migrator_password="${MIGRATOR_PASSWORD}"
}

run_sql <<'SQL'
SELECT CASE
    WHEN EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'situ_migrator') THEN 'SELECT 1'
    ELSE format('CREATE ROLE situ_migrator LOGIN PASSWORD %L', :'migrator_password')
END
\gexec
SELECT CASE
    WHEN EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'situ_app') THEN 'SELECT 1'
    ELSE format('CREATE ROLE situ_app LOGIN PASSWORD %L', :'app_password')
END
\gexec
SELECT 'CREATE DATABASE situ OWNER situ_migrator'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'situ')
\gexec
SQL

if [[ "${APPLY_TEST_DATABASE:-}" == "yes" ]]; then
  run_sql <<'SQL'
SELECT 'CREATE DATABASE situ_test OWNER situ_migrator'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'situ_test')
\gexec
SQL
fi

grant_db() {
  local database="$1"
  sudo -u postgres psql -v ON_ERROR_STOP=1 -v dbname="$database" -d "$database" <<'SQL'
CREATE EXTENSION IF NOT EXISTS postgis;
GRANT CONNECT ON DATABASE :"dbname" TO situ_app;
GRANT USAGE ON SCHEMA public TO situ_app;
GRANT USAGE ON SCHEMA public TO situ_migrator;
GRANT CREATE ON SCHEMA public TO situ_migrator;
ALTER DEFAULT PRIVILEGES FOR ROLE situ_migrator IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO situ_app;
ALTER DEFAULT PRIVILEGES FOR ROLE situ_migrator IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO situ_app;
ALTER DEFAULT PRIVILEGES FOR ROLE situ_migrator IN SCHEMA public
    GRANT EXECUTE ON FUNCTIONS TO situ_app;
SQL
}

grant_db situ
if [[ "${APPLY_TEST_DATABASE:-}" == "yes" ]]; then
  grant_db situ_test
fi

unset APP_PASSWORD MIGRATOR_PASSWORD
log "Base situ lista. PostGIS creado. situ_app no puede crear ni borrar tablas."
