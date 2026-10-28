# Despliegue

Servidor Ubuntu 26.04 compartido, con correo, nginx, MariaDB, Redis y otro MediaMTX. Estos scripts no instalan paquetes y no tocan ufw, fail2ban, swap, SSH ni nginx ajeno. El dominio y los puertos salen de `deploy/ports.env` (por defecto `somossitu.com`, API `15080`, panel `15081`, web `15082`, landing `15083`, ayuda `15084`). PostgreSQL 18 se usa por el socket `/var/run/postgresql`; el puerto TCP no se asume. En el servidor hace falta el runtime de ASP.NET Core 10 y Node 24. pnpm y el SDK se usan solo en el Mac.

Copia `deploy/` al servidor una vez. Si el DNS no es `somossitu.com`, cambia `SITU_DOMAIN_ROOT` antes de empezar.

## Primera vez

En el servidor:

```bash
sudo bash deploy/prereqs-check.sh
sudo bash deploy/check-ports.sh
sudo bash deploy/gen-secrets.sh
sudo bash deploy/postgres/apply.sh
sudo bash deploy/nginx/install.sh
```

`prereqs-check.sh` solo verifica. Si falta PostgreSQL o PostGIS, imprime el comando y no lo ejecuta. `check-ports.sh` falla si 15080–15084, 1936, 18890, 19998 o 5099 ya están ocupados. `gen-secrets.sh` crea el usuario `situ`, las unidades y `/etc/situ`. Sale con código 2 hasta que completes a mano los `CHANGE_ME` (correo, captcha, Maps, SMTP). El Team ID de Apple, si sigue en `CHANGE_ME`, usa `Y27698UG63`. La huella `ANDROID_SHA256` puede esperar: sin ella se instala nginx y `assetlinks.json` queda vacío. `postgres/apply.sh` crea solo los roles `situ_app` y `situ_migrator` y la base `situ`. `nginx/install.sh` escribe sitios `situ-*` en `sites-available`, los enlaza y recarga nginx después de `nginx -t`. Si esa prueba falla, quita solo esos enlaces. Antes de ese comando, `LETSENCRYPT_EMAIL` en `/etc/situ/nginx.env` tiene que ser un correo real.

MediaMTX es aparte y solo si el 1936 está libre:

```bash
sudo bash deploy/mediamtx/install.sh
```

## Actualizar

En el Mac, con `deploy/env/build.env` (host, usuario SSH y clave de Maps):

```bash
./deploy/release.sh
```

Compila en el Mac, copia el release y reinicia solo `situ-*`. Si `health/ready` no responde, vuelve al release anterior.

## Qué ya no hacen

No instalan paquetes. No tocan ufw, fail2ban, swap, SSH ni journald. No reinician nginx ni PostgreSQL. No borran el sitio por defecto ni editan `conf.d` de otros proyectos. No abren el 1936.
