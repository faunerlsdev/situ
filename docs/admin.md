# Panel de administración

La API del panel y la interfaz en `apps/admin` (puerto 3001).

Las rutas de gestión bajo `/api/v1/admin` exigen una sesión con el segundo factor completado (`role=Staff` y `scope=mfa`) y, además, el permiso en la base en cada petición. Un token viejo no alcanza si la asignación se revocó. Quedan fuera de esa exigencia el ingreso (`/api/v1/admin/auth/*`), la inscripción (`/api/v1/admin/2fa/setup` y `/enable`) y `GET /api/v1/admin/me`. Restablecer el 2FA de otra cuenta sí exige la sesión completa. Fuera de Development, las rutas `/admin/` se quitan del documento OpenAPI. El formulario público de retiro (`POST /api/v1/takedown-requests`) sí queda en la documentación.

## Primer administrador

No hay un usuario por defecto ni un endpoint público para crear personal. La cuenta ya tiene que existir. Desde la máquina de la API:

```bash
dotnet run --project apps/backend/src/Situ.Api -- admin:grant --email tu@correo.com --role SuperAdmin
```

Roles aceptados: `SuperAdmin`, `Moderator`, `Support`, `ContentManager`, `Auditor` y `Finance`. Si el correo no existe, el comando termina con error y deja `staff.grant_failed` en la bitácora, sin guardar el correo. Si ya tenía el rol, no crea otra fila.

## Roles sembrados

| Rol | Permisos |
| --- | --- |
| Super admin | Todos |
| Moderador | `complaints.resolve`, `users.view`, `users.suspend`, `live.cut`, `takedowns.handle` |
| Soporte | `users.view` |
| Gestor de contenido | `categories.manage`, `plans.view` (solo lectura) |
| Auditor | `audit.view` |
| Finanzas | `plans.manage`, `audit.view` (rol que ya existía) |

Permisos nuevos: `avatars.manage`, `legalholds.manage`, `metrics.view`, `users.view_email`, `plans.view`. Revelar un autor anónimo sigue en `anonymity.reveal`, que queda en Super admin. El moderador sembrado ya no lo trae.

Un superadministrador no puede dejar el sistema sin ningún Super admin.

## Sesión y segundo factor

El personal no entra por `/api/v1/auth/login` ni renueva esa sesión. Esos caminos responden 403 `auth.staff_use_panel`: "Las cuentas del equipo ingresan por el panel de administración". El ingreso del panel es `POST /api/v1/admin/auth/login` con correo y contraseña. El error de credenciales es el mismo si el correo no existe, la contraseña no coincide o la cuenta no es del equipo.

Si la cuenta aún no tiene autenticador, la respuesta trae `mfaEnrollmentRequired` y un token de 5 minutos que solo sirve para `POST /api/v1/admin/2fa/setup` y `POST /api/v1/admin/2fa/enable`. El setup devuelve el secreto y la URI `otpauth` una vez. El secreto se guarda cifrado con Data Protection. Enable confirma un código TOTP (SHA1, 6 dígitos, 30 segundos, tolerancia de un paso) y devuelve 10 códigos de recuperación, que se muestran una sola vez y se guardan hasheados. Cada código sirve una vez.

Si el autenticador ya está activo, el login devuelve `mfaRequired` y un `mfaToken` de 5 minutos. `POST /api/v1/admin/auth/mfa` acepta `code` o `recoveryCode` y emite la sesión. El mismo paso TOTP no se puede reutilizar. Cinco códigos fallidos bloquean el factor 15 minutos. La sesión completa trae `role=Staff` y `scope=mfa`. Sin ese scope, el resto de `/api/v1/admin` responde 403. `GET /api/v1/admin/me` acepta el rol de personal para que la interfaz sepa qué ocultar.

El token de acceso del personal dura 5 minutos. El refresh dura 30 y se renueva en cada `POST /api/v1/admin/auth/refresh`. `POST /api/v1/admin/auth/logout` revoca esa sesión. El panel web renueva el token cerca de los 4 minutos y cierra la sesión si pasan 30 minutos sin actividad.

Un superadministrador con `staff.manage` puede restablecer el 2FA de otra cuenta con `POST /api/v1/admin/2fa/reset`. No puede hacerlo sobre sí mismo. La otra cuenta pierde el autenticador, los códigos y las sesiones, y tiene que inscribirse de nuevo.

## Panel web

```bash
pnpm dev:admin
```

Abre http://localhost:3001. Copia `apps/admin/.env.example` a `apps/admin/.env` si quieres cambiar la API. `API_BASE_URL` la usa solo el servidor de Next (por defecto `http://127.0.0.1:5080`). `APP_ENV` muestra Development o Production en la cabecera. El navegador no guarda el token: el BFF lo pone en cookies `httpOnly`, `SameSite=Strict` y `Secure` fuera de local. La CSP de producción no permite `eval`. En local se añade `unsafe-eval` porque el runtime de desarrollo de React lo necesita. El formulario de ingreso es `POST`: si el JavaScript no carga, la contraseña no queda en la URL.

La landing quedó en el puerto 3003 para dejar el 3001 al panel.

Si un administrador pierde el teléfono y los códigos de recuperación, otro superadministrador entra al panel, abre Personal y usa Restablecer 2FA con un motivo. La cuenta afectada vuelve a la inscripción en el siguiente ingreso. No hay un camino para que esa persona se restablezca sola.

### Procedimiento de emergencia: solo con acceso al servidor

Si nadie del equipo puede entrar al panel, desde la máquina de la API:

```bash
dotnet run --project apps/backend/src/Situ.Api -- admin:reset-2fa --email tu@correo.com
```

Borra el secreto y los códigos de recuperación de esa cuenta y revoca sus sesiones activas. El siguiente ingreso pide inscribir el autenticador de nuevo. Falla si el correo no existe o la cuenta no es del equipo. Deja `staff.2fa_reset_console` en la bitácora, sin el correo y sin secretos. No existe un endpoint HTTP para esto.

Límite del panel: 60 peticiones por minuto por usuario e IP. La búsqueda de cuentas: 10 por minuto. El formulario público de retiro: 5 por hora por IP y 3 por hora por correo.

## IP real del cliente

Los límites de frecuencia, la bitácora y los bloqueos leen la IP de la conexión. `X-Forwarded-For` y `X-Forwarded-Proto` solo se aceptan si esa conexión llega desde una dirección de `Network:KnownProxies`. Una cabecera enviada por cualquier otro origen se ignora, también desde localhost.

En Development la lista va vacía y esas cabeceras no se leen. Fuera de Development la lista es obligatoria: si falta o alguna entrada no es una IP, la API no arranca. En el entorno del servidor:

```bash
Network__KnownProxies__0=10.0.0.8
```

El panel reenvía a la API la primera dirección de `X-Forwarded-For` (o `X-Real-IP` si no viene) y el protocolo. Esa cabecera solo cuenta cuando la IP del proceso de Next está en `Network:KnownProxies`. El proxy delante del panel es quien debe escribir la IP del navegador.

## Qué escribe la bitácora

Cada escritura guarda quién, la acción, la entidad, el antes y el después, la IP y la fecha. No guarda secretos ni el correo completo. Ver un correo completo (`users.view_email`) sí deja `users.view_email`, con el id de la cuenta y sin el correo.

## Ajustes

`GET /api/v1/admin/settings` agrupa por el prefijo de la clave. No lista claves con secreto, token, contraseña o SMTP. `PUT /api/v1/admin/settings/{key}` valida el rango: intentos de login de 3 a 10, minutos de bloqueo de 5 a 120, radio de notificación de 100 a 10000. `PUT /api/v1/admin/countries/{id}/minimum-age` acepta de 13 a 18. `panel.copy_by_country` guarda los textos del panel inferior por país e idioma. `GET /api/v1/app-config` los devuelve en `panelCopyByCountry`. No hay una caché en memoria: el ETag es el hash del cuerpo, así que el siguiente lectura ya trae el valor nuevo.

## Retiro público

`POST /api/v1/takedown-requests` responde `202` con `{ "accepted": true, "trackingCode": "TD-…" }` y no dice si el contenido existe. El campo `website` es un señuelo: si viene lleno, la respuesta es la misma y no se guarda nada. El plazo es el de `TakedownRequest.DefaultResponseWindow`, 15 días. El correo de confirmación lleva el número de seguimiento. Aceptar la solicitud retira el reporte, el comentario o el vivo y avisa a quien la pidió.

## Datos de ejemplo (solo Development)

Sirven para probar la cola de denuncias del panel. No corren si el entorno no es Development.

```bash
dotnet run --project apps/backend/src/Situ.Api -- dev:seed
dotnet run --project apps/backend/src/Situ.Api -- dev:seed --clean
```

Es idempotente. Crea tres adultos con la contraseña local `Semilla123`:

| Correo | Usuario |
| --- | --- |
| ana.ejemplo@situ.test | ana_ejemplo |
| bruno.ejemplo@situ.test | bruno_ejemplo |
| carla.ejemplo@situ.test | carla_ejemplo |

También crea ocho reportes cerca de Bogotá, en categorías distintas, unos con autor visible y otros anónimos; comentarios; denuncias abiertas, algunas prioritarias, dos de ellas sobre contenido anónimo; y un retiro pendiente que vence en 12 horas (entra en "por vencer" del tablero, cuya ventana es de 2 días). El texto lleva el prefijo `[dev-seed]`. `--clean` borra esas filas y las tres cuentas. No las vuelve a crear: para eso se corre `dev:seed` otra vez.
