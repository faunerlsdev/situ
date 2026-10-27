# Despliegue en un VPS Ubuntu de prueba

Este manual es para un servidor propio, con acceso por llave SSH, donde nginx, PostgreSQL, Node y .NET 10 ya están instalados. La prueba cerrada es de unas 15 personas. Los mismos archivos sirven después en un servidor más grande: cambian el tamaño de disco, la memoria de PostgreSQL y el DNS.

No hay dominio corto. Los enlaces compartidos son `https://app.<DOMAIN_ROOT>/r/{id}`.

`DOMAIN_ROOT` lo defines tú al ejecutar. No está escrito en los scripts.

## Puertos reales

La API escucha en `127.0.0.1:5080`. La web en `3002`, el panel en `3001`, la landing en `3003` y el centro de ayuda en `3004`. nginx es el único proceso público, en 80 y 443.

## DNS

Crea estos registros antes de pedir el certificado. `<IPv4>` y `<IPv6>` son las direcciones del VPS. Si el VPS no tiene IPv6, omite AAAA.

| Nombre | Tipo | Valor |
| --- | --- | --- |
| `<DOMAIN_ROOT>` | A | `<IPv4>` |
| `<DOMAIN_ROOT>` | AAAA | `<IPv6>` |
| `www.<DOMAIN_ROOT>` | A / AAAA | las mismas |
| `app.<DOMAIN_ROOT>` | A / AAAA | las mismas |
| `admin.<DOMAIN_ROOT>` | A / AAAA | las mismas |
| `ayuda.<DOMAIN_ROOT>` | A / AAAA | las mismas |
| `api.<DOMAIN_ROOT>` | A / AAAA | las mismas |

`www` redirige al ápice. Los demás nombres sirven su propia app.

Baja el TTL a 300 unos minutos antes de un cambio de servidor. Súbelo otra vez cuando el humo quede en verde.

## Qué queda en el servidor

| Ruta | Uso |
| --- | --- |
| `/opt/situ/releases/<fecha>` | Una publicación. Se conservan las últimas 3. |
| `/opt/situ/current` | Enlace a la publicación activa. |
| `/var/lib/situ/media` | Archivos subidos. No vive dentro del release. |
| `/etc/situ/*.env` | Variables. Modo `600`, dueño root. |
| `/etc/situ/age.key` | Clave privada de age. No sale del servidor. |
| `/var/backups/situ` | Respaldos. |

El usuario del sistema es `situ`. No tiene login.

## Orden de la primera instalación

En tu Mac, dentro del repositorio:

```bash
scp -r deploy <usuario>@<servidor>:/tmp/situ-deploy
ssh <usuario>@<servidor>
sudo bash /tmp/situ-deploy/provision.sh
```

`provision.sh` es idempotente. Verifica ASP.NET Core Runtime 10, Node 22 o superior, pnpm, PostgreSQL y PostGIS. No reinstala lo que ya está. Si falta PostGIS, instala solo el paquete `postgresql-<versión>-postgis-3`. Crea el usuario, los directorios, swap de 2 GB si no hay, `unattended-upgrades`, ufw (22 con límite, 80 y 443) y fail2ban para SSH y nginx.

Si SSH todavía acepta contraseña, el script avisa y no lo cambia. Para cerrarlo:

```bash
sudo CONFIRM_SSH_HARDENING=yes bash /tmp/situ-deploy/provision.sh
```

Hazlo desde una sesión que ya entra con llave. Si te equivocas, esa sesión sigue abierta, pero una nueva no entra con contraseña.

Secretos, todavía en el servidor:

```bash
sudo DOMAIN_ROOT=<tu-dominio> bash /tmp/situ-deploy/gen-secrets.sh
```

El comando sale con código 2 y lista nombres de variables. No imprime valores. Escribe a mano, en `/etc/situ`, lo que sigue en `CHANGE_ME`:

- `api.env`: `Email__Host`, `Email__Username`, `Email__Password`, `Captcha__SecretKey`, `Captcha__SiteKey`, `Notifications__ExpoAccessToken`
- `web.env`: `NEXT_PUBLIC_GOOGLE_MAPS_KEY`
- `nginx.env`: `LETSENCRYPT_EMAIL`, `APPLE_TEAM_ID`, `ANDROID_SHA256`
- `backup.env` y `monitor.env`: SMTP de alerta y `ALERT_TO`

`Jwt__Secret`, `GuardianConsent__SigningKey`, `FileStorage__SigningKey` y las dos contraseñas de PostgreSQL ya quedaron generadas. Las tres claves de la aplicación son distintas. `Network__KnownProxies__0` queda en `127.0.0.1` porque nginx está en la misma máquina. La API solo acepta `X-Forwarded-For` y `X-Forwarded-Proto` desde esa IP.

Base de datos:

```bash
sudo bash /tmp/situ-deploy/postgres/apply.sh
```

Crea el rol `situ_migrator` (dueño de la base, corre las migraciones), el rol `situ_app` (solo `SELECT`, `INSERT`, `UPDATE`, `DELETE`, secuencias y funciones) y la base `situ`. `CREATE EXTENSION postgis` lo ejecuta el superusuario una vez. PostgreSQL escucha solo en localhost. Para crear también `situ_test`:

```bash
sudo APPLY_TEST_DATABASE=yes bash /tmp/situ-deploy/postgres/apply.sh
```

TLS y nginx. El archivo `/etc/situ/nginx.env` tiene que estar completo:

```bash
sudo bash -c 'set -a; source /etc/situ/nginx.env; set +a; bash /tmp/situ-deploy/nginx/install.sh'
```

Pide un certificado para el ápice, `www`, `app`, `admin`, `ayuda` y `api`. La renovación recarga nginx. `client_max_body_size` es 30 MB en la API, la web y el panel, alineado con el límite de video de esta etapa. `/api/v1/auth/` y `/api/v1/admin/auth/` tienen `limit_req`. `/hubs/map` hace el upgrade de WebSocket. Los dotfiles responden 403, salvo `/.well-known/`.

En `app.<dominio>`:

- `/.well-known/apple-app-site-association` con `Content-Type: application/json`
- `/.well-known/assetlinks.json` con el mismo tipo

El Team ID y la huella SHA-256 salen de `nginx.env`. El paquete y el bundle por defecto son `com.somossitu.app`.

Antes del primer release, en tu Mac, copia `deploy/env/build.env.example` a `deploy/env/build.env` (ese archivo no se versiona) y pon el dominio, el host SSH, el usuario y la clave de Maps. La landing no compila en producción mientras `apps/landing/content/legal/*.md` contenga `LEGAL_PLACEHOLDER`. Quita el marcador y el texto provisional, conserva el front matter, y comprueba:

```bash
pnpm legal:check
```

El release, desde la raíz del repositorio en tu Mac:

```bash
./deploy/release.sh --dry-run
./deploy/release.sh
```

Para publicar la API, la web, el panel y la ayuda antes de que lleguen los textos del abogado:

```bash
./deploy/release.sh --skip-landing
```

o, si quieres compilar la landing igual con el texto provisional:

```bash
ALLOW_LEGAL_PLACEHOLDER=1 ./deploy/release.sh
```

Las dos formas escriben una advertencia en rojo. `--skip-landing` no compila la landing; si ya había una en el release anterior, el servidor reutiliza esa copia. `ALLOW_LEGAL_PLACEHOLDER=1` salta `pnpm legal:check` y llama a `next build` directo, así que las páginas públicas pueden seguir diciendo "Texto en revisión". `smoke.sh` marca esas páginas en rojo y termina con error.

La primera versión enviada a App Store o Google Play no puede usar `--skip-landing` ni `ALLOW_LEGAL_PLACEHOLDER=1`. Esa entrega exige `pnpm legal:check` en verde y un `./deploy/release.sh` sin esas opciones. Si el humo encuentra "Texto en revisión", no subas esa build a las tiendas.

Compila las cuatro apps Next en modo standalone y publica la API para `linux-x64`. Copia el resultado a `/opt/situ/releases/<fecha>`. En el servidor, antes de mover `current`, ejecuta `dotnet Situ.Api.dll migrate` con el rol `situ_migrator`. Después cambia el enlace, reinicia los cinco servicios y espera `http://127.0.0.1:5080/health/ready`. Si no responde, vuelve al release anterior y conserva los últimos tres.

El usuario de SSH necesita `sudo` sin contraseña para ese script. Si entras como root con llave, no hace falta sudo.

El primer administrador, en el servidor:

```bash
sudo bash -c 'set -a; source /etc/situ/api.env; set +a; runuser -u situ --preserve-environment -- /usr/bin/dotnet /opt/situ/current/api/Situ.Api.dll admin:grant --email <correo> --role SuperAdmin'
```

Límites de medios para esta etapa, porque el video no se transcodifica. Ejecútalo una vez, conectado como `situ_migrator` o como postgres:

```sql
UPDATE system_settings SET value = '5242880'  WHERE key = 'reports.max_image_bytes';
UPDATE system_settings SET value = '3'        WHERE key = 'reports.max_images';
UPDATE system_settings SET value = '26214400' WHERE key = 'reports.max_video_bytes';
UPDATE system_settings SET value = '20'       WHERE key = 'reports.max_video_seconds';
```

Eso es 5 MB por imagen, 3 imágenes, 25 MB y 20 segundos por video. nginx rechaza un cuerpo mayor a 30 MB. Si más adelante subes esos valores en el panel, sube también `client_max_body_size` y vuelve a cargar nginx.

Humo, desde tu Mac, cuando el DNS ya resuelve:

```bash
DOMAIN_ROOT=<tu-dominio> ./deploy/smoke.sh
```

Revisa `/health/live`, `/health/ready`, `/api/v1/app-config`, la página de login, la negociación de `/hubs/map`, un login falso del panel (espera 400, 401 o 429) y las cabeceras `Strict-Transport-Security` y `X-Content-Type-Options`.

Temporizadores, en el servidor, después de completar el SMTP de alerta:

```bash
sudo systemctl enable --now situ-backup.timer situ-monitor.timer
```

Correo de prueba, sin registrar el contenido:

```bash
sudo bash -c 'set -a; source /etc/situ/api.env; set +a; runuser -u situ --preserve-environment -- /usr/bin/dotnet /opt/situ/current/api/Situ.Api.dll mail:test --to <correo>'
```

La salida es `Correo de prueba enviado.` o el tipo de la excepción. No imprime el mensaje.

## Cómo publica `release.sh`

En la Mac exporta `DOMAIN_ROOT`, `NEXT_PUBLIC_DOMAIN_ROOT`, `NEXT_PUBLIC_API_URL` y `NEXT_PUBLIC_GOOGLE_MAPS_KEY` antes de `next build`. Next deja esos valores dentro del paquete del navegador. El mismo dominio va en los `.env` del servidor para las páginas que se renderizan al pedirlas.

Sin opciones extra, el script corre `pnpm legal:check` y el build de producción de la landing. Ese build falla mientras quede `LEGAL_PLACEHOLDER`. `--skip-landing` y `ALLOW_LEGAL_PLACEHOLDER=1` sirven solo para la prueba cerrada anterior a los textos del abogado. No forman parte de la primera versión enviada a las tiendas.

`dotnet publish` es framework-dependent: usa el runtime 10 que ya está en el VPS. No hace falta el SDK en el servidor.

## Health

`GET /health/live` solo dice que el proceso responde. `GET /health/ready` y `GET /health` comprueban PostgreSQL y que `/var/lib/situ/media` se puede escribir. La respuesta lleva el nombre del chequeo y el estado. No lleva rutas, cadenas de conexión ni el contenido del archivo de prueba.

## Cabeceras

La web y el panel ponen su propia CSP (mapa, Turnstile y el origen de la API). nginx no agrega una segunda CSP ahí. La landing y la ayuda reciben una CSP desde nginx: la ayuda puede llamar a `https://api.<dominio>`. En las cuatro apps van HSTS y `nosniff`.

## Secretos que tiran abajo el arranque

En `ASPNETCORE_ENVIRONMENT=Production` la API no arranca si falta un secreto, si es un placeholder (`CHANGE_ME`, `placeholder`, `situ-dev`, `situ-development`) o si coincide con la clave de desarrollo. También exige que JWT, consentimiento del tutor y firma de medios sean tres valores distintos, que `PublicBaseUrl` sea `https` y no localhost, que el SMTP no sea localhost ni el puerto 1025, que Captcha no esté en `Development`, que `Notifications__RequireExpoAccessToken=true` tenga token, y que `Cors__AllowedOrigins` tenga al menos un origen `https`.

Fuera de Development, `FileStorage__SigningKey` vacío o igual a `situ-development-media-signing-key` también aborta el proceso.

## Rotar secretos

1. Genera el valor nuevo en un archivo aparte. No lo pegues en el chat ni en el journal.
2. Edita `/etc/situ/api.env`.
3. `sudo systemctl restart situ-api`.
4. Si la API no pasa `/health/ready`, devuelve el valor anterior y reinicia.

`GuardianConsent__SigningKey` y `FileStorage__SigningKey` verifican datos ya guardados. Rotarlas invalida consentimientos firmados y URLs de medios vigentes. Hazlo solo si la clave se filtró, y avisa a la prueba. `Jwt__Secret` cierra todas las sesiones. Las contraseñas de PostgreSQL se cambian con `ALTER ROLE ... PASSWORD` y el mismo valor en `postgres.env`, `api.env` y `migrate.env`.

La clave privada de age no se rota con el resto. Si se pierde, los respaldos viejos no se pueden leer. Guarda una copia fuera del servidor, cifrada, en un lugar que no sea el mismo disco.

## Desplegar y volver atrás

```bash
./deploy/release.sh
```

Para volver a un release que sigue en disco:

```bash
ssh <usuario>@<servidor> 'sudo ln -sfn /opt/situ/releases/<fecha> /opt/situ/current && sudo systemctl restart situ-api situ-web situ-admin situ-landing situ-support'
```

Eso no deshace una migración. Si la migración nueva no es compatible con el código anterior, restaura el respaldo de la base en vez de solo mover el enlace.

## Respaldos y restauración

`situ-backup.timer` corre a las 02:30 UTC. Guarda 14 volcados diarios y 8 semanales (el del domingo), cifrados con age. Los medios se copian con `rsync --link-dest` y además queda un tar cifrado. La copia de trabajo sin cifrar es root y modo `700`. Si `RCLONE_REMOTE` está vacío, nada sale del servidor. Si lo llenas con un remoto rclone ya configurado (`nombre:ruta`), se copian solo los archivos age.

Probar una restauración sin tocar la base viva:

```bash
sudo /opt/situ/bin/restore.sh --check --dump /var/backups/situ/db/daily/situ-<fecha>.dump.age
sudo /opt/situ/bin/restore.sh --dump /var/backups/situ/db/daily/situ-<fecha>.dump.age --target-database situ_restore_test
```

`--check` descifra, pide a `pg_restore` la lista y borra el temporal. La segunda forma carga una base aparte. Para reemplazar `situ` hace falta `CONFIRM_RESTORE=yes` y conviene detener `situ-api` antes. Los medios:

```bash
sudo /opt/situ/bin/restore.sh --dump <dump.age> --target-database situ_restore_test \
  --media /var/backups/situ/media/<fecha>.tar.age --media-dest /var/lib/situ/media
```

Si el respaldo falla, `backup.sh` manda un correo. El cuerpo solo dice que falló y a qué hora. El monitor avisa, como máximo una vez al día, si el disco pasa de 70 % o de 85 %, si la RAM usada pasa de 85 %, si un servicio está caído, si el último volcado tiene más de 26 horas o si el certificado vence en menos de 21 días.

## Pasar a un servidor más grande

1. En el servidor nuevo, repite provision, postgres, env, nginx y TLS. Puedes copiar `/etc/situ` y `/etc/situ/age.key` por `scp`. No los subas a git.
2. En el servidor viejo, corre `sudo /opt/situ/bin/backup.sh`.
3. Medios: `rsync -a /var/lib/situ/media/ <usuario>@<nuevo>:/var/lib/situ/media/`.
4. En el nuevo, `restore.sh` hacia la base `situ` con `CONFIRM_RESTORE=yes`, y el tar de medios solo si el rsync no alcanzó a copiarlos.
5. Publica con `./deploy/release.sh` apuntando `DEPLOY_HOST` al servidor nuevo, o copia `/opt/situ/current` si es el mismo build.
6. Cambia los registros A y AAAA.
7. Corre `smoke.sh`. Deja el servidor viejo apagado para escrituras, pero no lo borres, hasta que la prueba lleve un día estable.
8. En el nuevo, sube la memoria de PostgreSQL en `/etc/postgresql/*/main/conf.d/situ.conf` (`shared_buffers`, `effective_cache_size`, `max_connections`) y reinicia PostgreSQL. Los valores de ahora están pensados para una máquina de 4 GB que también corre las apps.

## Máquina de partida

4 GB de RAM, 2 vCPU y 40 GB de disco alcanzan para 15 personas con los límites de video de arriba. El swap de 2 GB es una red, no capacidad de trabajo. La API tiene tope de 768 MB; la web, 512 MB; el panel, 384 MB; landing y ayuda, 320 MB. Journald se queda en 200 MB y 14 días.

## Qué no entra todavía

Sentry, una lista de IP para el panel y MediaMTX. El video se guarda tal cual llega.
