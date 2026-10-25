# Panel de administración

La interfaz web llega en otro paso. Esta página describe la API del MVP.

Todas las rutas bajo `/api/v1/admin` exigen la política `Staff` (el token trae `role=Staff`) y, además, el permiso en la base en cada petición. Un token viejo no alcanza si la asignación se revocó. Fuera de Development, esas rutas se quitan del documento OpenAPI. El formulario público de retiro (`POST /api/v1/takedown-requests`) sí queda en la documentación.

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

## Sesión

El token de una cuenta con rol de personal dura 5 minutos. El de una cuenta normal dura 15. El refresh del personal dura 30 minutos y se renueva en cada refresh: si el panel pasa 30 minutos sin renovar, hay que volver a entrar. La misma duración aplica a la app móvil de esa cuenta, porque el rol viaja en la sesión normal.

El segundo factor existe como campos en `User` (`TwoFactorEnabled`, `TwoFactorSecret`) y no hay flujo de alta ni de verificación en el inicio de sesión. Queda pendiente exigirlo al personal.

Límite del panel: 60 peticiones por minuto por usuario e IP. La búsqueda de cuentas: 10 por minuto. El formulario público de retiro: 5 por hora por IP y 3 por hora por correo.

## Qué escribe la bitácora

Cada escritura guarda quién, la acción, la entidad, el antes y el después, la IP y la fecha. No guarda secretos ni el correo completo. Ver un correo completo (`users.view_email`) sí deja `users.view_email`, con el id de la cuenta y sin el correo.

## Ajustes

`GET /api/v1/admin/settings` agrupa por el prefijo de la clave. No lista claves con secreto, token, contraseña o SMTP. `PUT /api/v1/admin/settings/{key}` valida el rango: intentos de login de 3 a 10, minutos de bloqueo de 5 a 120, radio de notificación de 100 a 10000. `PUT /api/v1/admin/countries/{id}/minimum-age` acepta de 13 a 18. `panel.copy_by_country` guarda los textos del panel inferior por país e idioma. `GET /api/v1/app-config` los devuelve en `panelCopyByCountry`. No hay una caché en memoria: el ETag es el hash del cuerpo, así que el siguiente lectura ya trae el valor nuevo.

## Retiro público

`POST /api/v1/takedown-requests` responde `202` con `{ "accepted": true, "trackingCode": "TD-…" }` y no dice si el contenido existe. El campo `website` es un señuelo: si viene lleno, la respuesta es la misma y no se guarda nada. El plazo es el de `TakedownRequest.DefaultResponseWindow`, 15 días. El correo de confirmación lleva el número de seguimiento. Aceptar la solicitud retira el reporte, el comentario o el vivo y avisa a quien la pidió.
