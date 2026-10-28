# nginx helpers for a shared server. Source this; do not execute it.
# Never restart nginx. On nginx -t failure, remove only the SITU links
# created by this process.

if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
  printf 'Este archivo se importa, no se ejecuta.\n' >&2
  exit 1
fi

SITU_NGINX_LINKS=()

situ_nginx_enable() {
  local name="$1"
  local path="/etc/nginx/sites-available/${name}"
  local link="/etc/nginx/sites-enabled/${name}"
  if [[ ! -f "$path" ]]; then
    printf 'No está %s\n' "$path" >&2
    return 1
  fi
  ln -sfn "$path" "$link"
  SITU_NGINX_LINKS+=("$link")
}

situ_nginx_reload() {
  if nginx -t; then
    systemctl reload nginx
    return 0
  fi
  printf 'nginx -t falló. Quito solo los enlaces de SITU de esta ejecución.\n' >&2
  local link
  for link in "${SITU_NGINX_LINKS[@]}"; do
    rm -f "$link"
  done
  SITU_NGINX_LINKS=()
  if ! nginx -t; then
    printf 'nginx sigue inválido después de quitar los enlaces. No recargué.\n' >&2
  fi
  return 1
}
