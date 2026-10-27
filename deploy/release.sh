#!/usr/bin/env bash
# Build on the operator machine and publish a release to the VPS.
# --dry-run prints the plan and does not build, copy or restart.
set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT=$(cd "${SCRIPT_DIR}/.." && pwd)
DRY_RUN=0
SKIP_LANDING=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --dry-run) DRY_RUN=1 ;;
    --skip-landing) SKIP_LANDING=1 ;;
    *)
      printf 'Opción desconocida: %s\n' "$1" >&2
      printf 'Uso: release.sh [--dry-run] [--skip-landing]\n' >&2
      exit 1
      ;;
  esac
  shift
done

if [[ -f "${SCRIPT_DIR}/env/build.env" ]]; then
  set -a
  # shellcheck disable=SC1091
  source "${SCRIPT_DIR}/env/build.env"
  set +a
fi

: "${DOMAIN_ROOT:?Define DOMAIN_ROOT}"
: "${DEPLOY_HOST:?Define DEPLOY_HOST}"
: "${DEPLOY_USER:?Define DEPLOY_USER}"
: "${NEXT_PUBLIC_GOOGLE_MAPS_KEY:?Define NEXT_PUBLIC_GOOGLE_MAPS_KEY}"

if [[ "$DOMAIN_ROOT" == *CHANGE_ME* || "$DEPLOY_HOST" == *CHANGE_ME* || "$DEPLOY_USER" == *CHANGE_ME* || "$NEXT_PUBLIC_GOOGLE_MAPS_KEY" == *CHANGE_ME* ]]; then
  printf 'DOMAIN_ROOT, DEPLOY_HOST, DEPLOY_USER y NEXT_PUBLIC_GOOGLE_MAPS_KEY no pueden ser CHANGE_ME.\n' >&2
  exit 1
fi

export DOMAIN_ROOT
export NEXT_PUBLIC_DOMAIN_ROOT="${NEXT_PUBLIC_DOMAIN_ROOT:-$DOMAIN_ROOT}"
export NEXT_PUBLIC_API_URL="${NEXT_PUBLIC_API_URL:-https://api.${DOMAIN_ROOT}}"
export NEXT_PUBLIC_GOOGLE_MAPS_KEY

stamp=$(date -u +%Y%m%d%H%M%S)
tmp_root=${TMPDIR:-/tmp}
tmp_root=${tmp_root%/}
stage=$(mktemp -d "${tmp_root}/situ-release.XXXXXX")
trap 'rm -rf "$stage"' EXIT

run() {
  if [[ "$DRY_RUN" -eq 1 ]]; then
    printf 'dry-run:'
    printf ' %q' "$@"
    printf '\n'
  else
    "$@"
  fi
}

stage_next() {
  local name="$1"
  local src="${REPO_ROOT}/apps/${name}/.next/standalone"
  local dest="${stage}/${name}"
  mkdir -p "$dest"
  cp -a "${src}/." "$dest/"
  if [[ -d "${dest}/apps/${name}" ]]; then
    mkdir -p "${dest}/apps/${name}/.next"
    cp -a "${REPO_ROOT}/apps/${name}/.next/static" "${dest}/apps/${name}/.next/static"
    if [[ -d "${REPO_ROOT}/apps/${name}/public" ]]; then
      cp -a "${REPO_ROOT}/apps/${name}/public" "${dest}/apps/${name}/public"
    fi
  else
    mkdir -p "${dest}/.next"
    cp -a "${REPO_ROOT}/apps/${name}/.next/static" "${dest}/.next/static"
    if [[ -d "${REPO_ROOT}/apps/${name}/public" ]]; then
      cp -a "${REPO_ROOT}/apps/${name}/public" "${dest}/public"
    fi
  fi
}

cd "$REPO_ROOT"
allow_placeholder=0
if [[ "${ALLOW_LEGAL_PLACEHOLDER:-}" == "1" ]]; then
  allow_placeholder=1
fi
if [[ "$SKIP_LANDING" -eq 1 || "$allow_placeholder" -eq 1 ]]; then
  printf '\033[31m%s\033[0m\n' "ADVERTENCIA: este release no lleva los textos definitivos del abogado." >&2
  printf '\033[31m%s\033[0m\n' "No uses --skip-landing ni ALLOW_LEGAL_PLACEHOLDER=1 en la primera versión enviada a App Store o Google Play." >&2
fi
if [[ "$SKIP_LANDING" -eq 1 ]]; then
  printf '\033[31m%s\033[0m\n' "ADVERTENCIA: no se compila la landing. Se publican la API, la web, el panel y la ayuda." >&2
elif [[ "$allow_placeholder" -eq 1 ]]; then
  printf '\033[31m%s\033[0m\n' "ADVERTENCIA: la landing se compila aunque content/legal todavía tenga LEGAL_PLACEHOLDER." >&2
else
  run node apps/landing/scripts/legal-check.mjs
fi
run pnpm install --frozen-lockfile
run pnpm --filter @situ/web build
run pnpm --filter @situ/admin build
if [[ "$SKIP_LANDING" -eq 1 ]]; then
  printf 'Se omite el build de @situ/landing.\n'
elif [[ "$allow_placeholder" -eq 1 ]]; then
  run pnpm --filter @situ/landing exec next build
else
  run pnpm --filter @situ/landing build
fi
run pnpm --filter @situ/support build
run dotnet publish apps/backend/src/Situ.Api/Situ.Api.csproj -c Release -r linux-x64 --self-contained false -o "${stage}/api"

if [[ "$DRY_RUN" -eq 0 ]]; then
  stage_next web
  stage_next admin
  if [[ "$SKIP_LANDING" -eq 0 ]]; then
    stage_next landing
  fi
  stage_next support
  cp "${SCRIPT_DIR}/remote-release.sh" "${stage}/remote-release.sh"
  chmod 750 "${stage}/remote-release.sh"
fi

run rsync -a --delete -e "ssh -o BatchMode=yes" "${stage}/" "${DEPLOY_USER}@${DEPLOY_HOST}:/opt/situ/releases/${stamp}/"
run ssh -o BatchMode=yes "${DEPLOY_USER}@${DEPLOY_HOST}" "if [ \"\$(id -u)\" -eq 0 ]; then bash /opt/situ/releases/${stamp}/remote-release.sh ${stamp}; else sudo -n bash /opt/situ/releases/${stamp}/remote-release.sh ${stamp}; fi"

if [[ "$DRY_RUN" -eq 1 ]]; then
  printf 'dry-run: release %s hacia %s@%s. No compilé ni copié.\n' "$stamp" "$DEPLOY_USER" "$DEPLOY_HOST"
else
  printf 'Publicado %s\n' "$stamp"
fi
