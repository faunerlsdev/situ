#!/usr/bin/env bash
# provision.sh no prepara el servidor. Solo corre las comprobaciones.
set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
printf '%s\n' "provision.sh ya no crea usuarios, unidades ni toca el sistema. Sigue prereqs-check.sh." >&2
exec bash "${SCRIPT_DIR}/prereqs-check.sh"
