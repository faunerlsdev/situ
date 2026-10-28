# Shared port file. Source this; do not execute it.

if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
  printf 'Este archivo se importa, no se ejecuta.\n' >&2
  exit 1
fi

situ_ports_file() {
  local here
  here=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
  if [[ -f /etc/situ/ports.env ]]; then
    printf '%s\n' /etc/situ/ports.env
  else
    printf '%s\n' "${here}/ports.env"
  fi
}

situ_load_ports() {
  local file
  file=$(situ_ports_file)
  set -a
  # shellcheck disable=SC1090
  source "$file"
  set +a
  : "${SITU_API_PORT:?Falta SITU_API_PORT}"
  : "${SITU_ADMIN_PORT:?Falta SITU_ADMIN_PORT}"
  : "${SITU_WEB_PORT:?Falta SITU_WEB_PORT}"
  : "${SITU_LANDING_PORT:?Falta SITU_LANDING_PORT}"
  : "${SITU_SUPPORT_PORT:?Falta SITU_SUPPORT_PORT}"
  : "${SITU_RTMPS_PORT:?Falta SITU_RTMPS_PORT}"
  : "${SITU_HLS_PORT:?Falta SITU_HLS_PORT}"
  : "${SITU_MTX_API_PORT:?Falta SITU_MTX_API_PORT}"
  : "${SITU_LIVE_AUTH_PORT:?Falta SITU_LIVE_AUTH_PORT}"
  : "${SITU_POSTGRES_HOST:?Falta SITU_POSTGRES_HOST}"
  : "${SITU_DOMAIN_ROOT:?Falta SITU_DOMAIN_ROOT}"
  # Empty means the Unix socket. Do not default this to 5432.
  : "${SITU_POSTGRES_PORT?Falta SITU_POSTGRES_PORT}"
}

situ_port_listening() {
  local port="$1"
  [[ -n "$port" ]] || return 1
  ss -lntH 2>/dev/null | awk '{ print $4 }' | grep -Eq "(^|[.:\\]])${port}$"
}

situ_apply_domain() {
  if [[ -z "${DOMAIN_ROOT:-}" || "${DOMAIN_ROOT}" == *CHANGE_ME* ]]; then
    DOMAIN_ROOT="${SITU_DOMAIN_ROOT}"
  fi
  export DOMAIN_ROOT
}
