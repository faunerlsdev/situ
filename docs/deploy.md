# Despliegue

Servidor Ubuntu 26.04 compartido, con correo, nginx, MariaDB, Redis y otro MediaMTX. Estos scripts no instalan paquetes y no tocan ufw, fail2ban, swap, SSH ni nginx ajeno. El dominio y los puertos salen de `deploy/ports.env` (por defecto `somossitu.com`, API `15080`, panel `15081`, web `15082`, landing `15083`, ayuda `15084`). PostgreSQL 18 se usa por el socket `/var/run/postgresql`; el puerto TCP no se asume. En el servidor hace falta el runtime de ASP.NET Core 10 y Node 24. pnpm y el SDK se usan solo en el Mac. La API se publica con `dotnet publish -r linux-x64`. Las fotos usan `SkiaSharp.NativeAssets.Linux.NoDependencies`: el `libSkiaSharp.so` va en esa salida y no pide `libfontconfig` ni `libfreetype` del sistema. No se usa HarfBuzz. Al arrancar, el proceso carga Skia; si no puede, no llega a escuchar. `/health/ready` también incluye la comprobación `skia`.

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

MediaMTX de SITU es aparte, con usuario y servicio `situ-mediamtx`, y solo si el 1936 está libre. No usa el MediaMTX que ya está en 1935, 18888 y 19997. Si `situ-mediamtx` ya escucha esos puertos, `install.sh` reinstala y no regenera `/etc/situ/live.env`.

```bash
sudo bash deploy/mediamtx/install.sh
```

Eso deja RTMPS en 1936, HLS en `127.0.0.1:18890` y la API de control en `127.0.0.1:19998`, con tope de memoria 768 MB. La ruta de publicación es solo `"~^[a-f0-9]{32}$"` (`source: publisher`). No hay ruta comodín. `LIVE_AUTH_URL` queda en `http://127.0.0.1:15080/internal/live/authorize` y se aplica a publicar y a leer HLS. La API de control no pasa por esa URL: sigue pudiendo expulsar con `POST /v3/rtmps/conns/kick/{id}`. El proceso temporal del puerto 5099 no se enciende. Antes de servir `live.somossitu.com`, amplía el certificado que ya existe (no crees otro) y activa el sitio:

```bash
sudo certbot certonly --cert-name somossitu.com --expand \
  -d somossitu.com -d www.somossitu.com -d api.somossitu.com \
  -d app.somossitu.com -d admin.somossitu.com -d ayuda.somossitu.com \
  -d live.somossitu.com
sudo cp deploy/nginx/situ-live.conf /etc/nginx/sites-available/situ-live
# Sustituye __HLS_CDN_SECRET__ en ese archivo con el valor de /etc/situ/live.env. No lo imprimas.
sudo ln -sfn /etc/nginx/sites-available/situ-live /etc/nginx/sites-enabled/situ-live
sudo nginx -t && sudo systemctl reload nginx
```

`install.sh` ya copia ese sitio y sustituye el secreto. Si el certificado todavía no tiene `live.somossitu.com`, el `nginx -t` falla y el script quita solo el enlace de SITU de esa ejecución.

## Actualizar solo MediaMTX

En el Mac, desde la raíz del repo. Esto no copia `/etc/situ/live.env`:

```bash
scp deploy/mediamtx/mediamtx.yml USUARIO@SERVIDOR:/tmp/situ-mediamtx.yml
scp deploy/mediamtx/install.sh USUARIO@SERVIDOR:/tmp/situ-mediamtx-install.sh
```

En el servidor, dentro de la copia de `deploy/` que ya usas:

```bash
sudo install -m 644 /tmp/situ-mediamtx.yml deploy/mediamtx/mediamtx.yml
sudo install -m 755 /tmp/situ-mediamtx-install.sh deploy/mediamtx/install.sh
sudo bash deploy/mediamtx/install.sh
sudo systemctl restart situ-mediamtx.service
sudo systemctl is-active situ-mediamtx.service
```

`install.sh` conserva `/etc/situ/live.env` si el archivo ya existe y vuelve a escribir `/etc/mediamtx/mediamtx.yml` con esos secretos. No abras ese archivo ni el journal si una línea trae `secret=`. Para comprobar la ruta sin imprimir el secreto:

```bash
sudo awk '
  /authHTTPAddress:/ { print "authHTTPAddress: set" }
  /"~\^\[a-f0-9\]\{32\}\$":/ { print "opaque-path: yes" }
  /^[[:space:]]*all_others:/ || /^[[:space:]]*all:/ { print "wildcard: yes" }
' /etc/mediamtx/mediamtx.yml
curl -s -o /dev/null -w '%{http_code}\n' http://127.0.0.1:19998/v3/rtmps/conns/list
```

La primera salida tiene que decir `authHTTPAddress: set` y `opaque-path: yes`, sin `wildcard: yes`. El `curl` tiene que responder `200`: la API de control sigue en localhost y no pide el secreto de ingest.

La clave de un solo uso va en la query de `publishUrl` (`user=situ&pass=`). Eso lo resuelve la API: se despliega con `./deploy/release.sh`. MediaMTX ya lee esa query; no hace falta reinstalarlo otra vez si la ruta opaca ya está aplicada. La app deja de usar `setAuthorization`.

El manifiesto HLS lleva el token en el path (`/hls/<id>/<token>/index.m3u8`) y MediaMTX lo sirve como MPEG-TS (`hlsVariant: mpegts`, segmentos de 2 s). Hay que desplegar la API si cambió el gate, y el sitio nginx junto con MediaMTX: `install.sh` copia `situ-live.conf` y `mediamtx.yml`, sustituye los secretos, recarga nginx y reinicia `situ-mediamtx`. No abras `live.env` ni el yml renderizado. El access log de `live` no guarda la URI. No hay cookie de reproducción.

## Actualizar

En el Mac, con `deploy/env/build.env` (host, usuario SSH y clave de Maps):

```bash
./deploy/release.sh
```

Compila en el Mac, copia el release y reinicia solo `situ-*`. Si `health/ready` no responde, vuelve al release anterior.

## Qué ya no hacen

No instalan paquetes. No tocan ufw, fail2ban, swap, SSH ni journald. No reinician nginx ni PostgreSQL. No borran el sitio por defecto ni editan `conf.d` de otros proyectos. No abren el 1936.
