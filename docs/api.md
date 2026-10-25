# API de reportes y categorías

Base: `/api/v1`. JSON en camelCase. Los enums van como texto. Los errores son `ProblemDetails`: `title` en inglés, `detail` en español y `code` estable en inglés. Un 429 incluye `retryAfter` en ISO-8601.

La sesión de usuario es el JWT normal. La política `Staff` exige el claim `role=Staff`, que se emite al iniciar sesión si hay una asignación activa. El permiso se vuelve a comprobar en base de datos (`categories.manage` o `complaints.resolve`).

## Catálogo

`GET /api/v1/report-categories`

Acepta sesión o anónimo. Límite: 120 peticiones por minuto por IP.

Cabeceras: `Accept-Language` (si empieza por `es`, usa `es-CO`; si no hay traducción, español). `If-None-Match` con el ETag anterior.

Respuesta 200:

```json
{
  "catalogVersion": 1,
  "locale": "es-CO",
  "adultCatalog": true,
  "cityId": "00000000-0000-0000-0000-000000000000",
  "categories": [
    {
      "id": "55313b4b-bfcb-9343-4b29-2d7cd06cea72",
      "parentCategoryId": null,
      "code": "accidente_transito",
      "name": "Accidente de tránsito",
      "description": "Choques, atropellos y vehículos varados.",
      "buttonIconUrl": "http://localhost:5080/assets/categories/accidente_transito.png",
      "mapIconUrl": "http://localhost:5080/assets/categories/accidente_transito.png",
      "colorHex": "#E53935",
      "sortOrder": 1,
      "ttlMinutes": 90,
      "importanceWeight": 8,
      "mediaRequirement": "None",
      "isAdultOnly": false
    }
  ]
}
```

Cabeceras de respuesta: `ETag: "{version}:{locale}:{adult}:{cityId}"` y `Cache-Control: private, max-age=300`. Si `If-None-Match` coincide, responde 304 sin cuerpo.

Solo entran categorías activas. Un override de ciudad gana al de país: si `isEnabled` es false, la categoría no aparece y el TTL puede cambiar. Las `isAdultOnly` se ocultan si quien consulta tiene menos de 18 años, no tiene sesión o trae el token de tutor. La lista es plana; el cliente arma el árbol con `parentCategoryId` y la ordena por `sortOrder`.

Si el archivo de ícono no existe, el cliente usa uno por defecto.

## Administración

Todas exigen política Staff y permiso `categories.manage`. Cada cambio escribe `AuditLog` y sube `reports.catalog_version`.

| Método | Ruta | Cuerpo |
| --- | --- | --- |
| POST | `/api/v1/admin/report-categories` | `code`, `name`, `description`, `buttonIconUrl`, `mapIconUrl`, `colorHex`, `sortOrder`, `ttlMinutes`, `importanceWeight`, `mediaRequirement`, `parentCategoryId`, `isAdultOnly` |
| PUT | `/api/v1/admin/report-categories/{id}` | los mismos campos, sin `code` |
| POST | `/api/v1/admin/report-categories/{id}/active` | `{ "isActive": true }` |
| PUT | `/api/v1/admin/report-categories/order` | `{ "categoryIds": ["..."] }` |
| PUT | `/api/v1/admin/report-categories/{id}/overrides` | `countryId` o `cityId`, `isEnabled`, `ttlMinutes` |
| PUT | `/api/v1/admin/report-categories/{id}/translations` | `locale`, `name`, `description` |
| POST | `/api/v1/moderation/reports/{id}/remove` | `{ "reason": "..." }` permiso `complaints.resolve`. Deja un `ModerationAction`. |

El dominio rechaza un código que no sea `^[a-z0-9_]{2,40}$`, un color que no sea `#RRGGBB`, un TTL fuera de 5 minutos a 30 días y un peso fuera de 0 a 10.

## Multimedia

`POST /api/v1/reports/media` con `multipart/form-data`, campo `file`. Sesión de usuario. Límite: 30 por hora, por usuario e IP.

El servidor mira los bytes, no la extensión. Imagen JPEG, PNG o WebP: máximo 10 MB. Se reescribe a JPEG (SkiaSharp, MIT), se quita EXIF y GPS, el lado largo queda en 2048 px y se genera una miniatura de 480 px. Video MP4 o MOV: máximo 100 MB y 60 s; se guarda tal cual. WebM no se acepta. La transcodificación queda pendiente.

Respuesta 200, con el archivo ya en estado Ready:

```json
{
  "mediaId": "018f...",
  "type": "Image",
  "url": "/api/v1/reports/media/018f.../content?exp=1760000000&sig=abc",
  "thumbnailUrl": "/api/v1/reports/media/018f.../content?exp=1760000000&sig=abc&kind=thumb"
}
```

La URL no contiene la ruta del disco. Vale 15 minutos, o se puede pedir de nuevo con sesión si quien consulta puede ver el reporte. Un archivo que no se asocia a un reporte se borra a las 24 horas.

## Crear, editar y borrar

`POST /api/v1/reports`

```json
{
  "categoryId": "55313b4b-bfcb-9343-4b29-2d7cd06cea72",
  "description": "Choque en el semáforo, dos carros.",
  "latitude": 4.711,
  "longitude": -74.072,
  "visibility": "Public",
  "isAnonymous": false,
  "mediaIds": []
}
```

`visibility` es `Public` o `FriendsOnly`. La ciudad sale de la ciudad habilitada más cercana en 50 km, o de la ciudad del usuario. El TTL sale de la categoría y del override. El plan Free permite 10 reportes y 3 anónimos por día UTC.

Respuesta 200: un `report` con id, categoría, descripción, coordenadas, visibilidad, conteos, `expiresAt`, `author` y `previewUrl`.

`GET /api/v1/reports/{id}` añade `media`, `myConfirmation` y hasta 3 `comments`. Si el reporte venció, fue retirado o está en revisión, quien no es el autor recibe `404` con `code: report.not_available` y `reportStatus` en `Expired`, `Removed` o `UnderReview`. El texto es siempre "Este reporte ya no está disponible." y no dice por qué lo moderaron. El autor sigue viendo el suyo.

`GET /api/v1/reports/mine?cursor&limit` lista los propios, también los anónimos (`isAnonymous: true`). El cursor opaco se devuelve en `nextCursor`.

`PATCH /api/v1/reports/{id}` con `{ "description": "..." }` solo lo hace el autor y solo si sigue Active. 204.

`DELETE /api/v1/reports/{id}` lo pasa a Removed. Los archivos se borran después, según la retención (365 días por defecto) y respetando un legal hold. 204.

## Autor anónimo

Si `isAnonymous` es true, `author` es:

```json
{
  "userId": null,
  "displayName": "Situero anónimo",
  "avatarUrl": null,
  "memberSinceYear": 2026,
  "memberSinceMonth": 10,
  "isAnonymous": true,
  "hideAuthorOnMap": true
}
```

No hay día, ni enlace al perfil. El nombre sale de `anonymity.display_name`. En "mis reportes" también se oculta el id; la insignia es `isAnonymous`.

## Mapa

`GET /api/v1/reports/map?minLat&minLon&maxLat&maxLon&zoom&categories&since&type&lat&lon`

- `categories`: códigos o ids separados por coma.
- `since`: `hour` o `today`.
- `type`: `friends` deja solo `FriendsOnly` (propios y de amigos aceptados).
- `lat` y `lon`: si vienen, cada punto trae `distanceMeters`.

Solo reportes Active y no vencidos. `Public` lo ve cualquiera que no esté bloqueado. `FriendsOnly` lo ven el autor y los amigos aceptados. Un bloqueo en cualquier sentido excluye el reporte. Un menor no recibe categorías solo para adultos.

Con `zoom` menor a 15 la respuesta agrupa con una cuadrícula (`ST_SnapToGrid` en PostGIS). Con `zoom` de 15 o más devuelve puntos. El tope es 500 elementos. Un cluster tiene `kind: "cluster"`, sin id ni autor. Un punto tiene `kind: "point"`.

Cada elemento trae `isMine`. Es true solo para quien consulta, también en sus reportes anónimos, y no revela el autor a nadie más. La respuesta lleva `Cache-Control: private, no-store`.

Índice espacial: `ix_reports_location` (GIST sobre `reports.location`). En la base local, `EXPLAIN` de un recorte de Bogotá con `status = 'Active'` y `expires_at > now()` usó `Index Scan using ix_reports_location`. El filtro de estado y de vencimiento queda como Filter encima de ese índice. La prueba automática corre contra `situ_test` si existe `ConnectionStrings__Test` o el user-secret `ConnectionStrings:Test`. Sin esa conexión se omite y no falla.

Los íconos viven en `wwwroot/assets/categories/<codigo>.png`, con `default.png` de respaldo. Son placeholders (el pin de marca). El catálogo guarda la ruta relativa y la respuesta la vuelve absoluta con `PublicBaseUrl` (si no está, usa `Email:ApiPublicBaseUrl`). El panel de administración ya puede cambiar `buttonIconUrl` y `mapIconUrl`; estos archivos no se reemplazan solos y más adelante el panel sustituirá estos placeholders.

## Sesión

`GET /api/v1/me` exige sesión de usuario. `Cache-Control: private, no-store`. El correo va enmascarado (`a***@correo.com`). No devuelve la fecha de nacimiento ni la edad. Incluye `displayName`, `bio`, `memberSinceYear`, `memberSinceMonth` y `canChangeUserNameAt` (solo si el nombre de usuario está en espera). El perfil sigue naciendo privado.

```json
{
  "id": "55313b4b-bfcb-9343-4b29-2d7cd06cea72",
  "userName": "ana",
  "displayName": "Ana",
  "bio": null,
  "memberSinceYear": 2026,
  "memberSinceMonth": 10,
  "maskedEmail": "a***@correo.com",
  "canChangeUserNameAt": null,
  "avatar": { "kind": "Default", "url": null },
  "locale": "es-CO",
  "emailConfirmed": true,
  "status": "Active",
  "isMinor": false,
  "canPublish": true,
  "publishBlockedReason": null,
  "homeCity": {
    "id": "55313b4b-bfcb-9343-4b29-2d7cd06cea73",
    "name": "Bogotá",
    "latitude": 4.711,
    "longitude": -74.072,
    "countryId": "55313b4b-bfcb-9343-4b29-2d7cd06cea74",
    "countryName": "Colombia"
  },
  "plan": {
    "code": "free",
    "tier": "Free",
    "limits": { "maxReportsPerDay": 10, "maxAnonymousPostsPerDay": 3, "maxDevices": 2, "showsAds": true }
  },
  "profile": { "visibility": "Private", "shareLocationWithFriends": false },
  "guardian": null
}
```

Si `canPublish` es false, `publishBlockedReason` es `email_not_confirmed`, `guardian_consent_pending`, `suspended`, `banned`, `deletion_requested` o `deleted`. `guardian` solo viene si `isMinor` es true: `status` de la autorización (`None` si aún no hay solicitud) y `permissions.liveStreaming` / `permissions.shareLocationWithFriends`.

`POST /api/v1/auth/confirm-email/resend-self` reenvía la confirmación de la sesión. No pide el correo.

## Perfil y cuenta

`PATCH /api/v1/me/profile` con `{ "userName", "displayName", "bio", "homeCityId" }`. Cada campo es opcional. Límite: 30 por hora, por usuario e IP.

- `userName` usa las reglas del value object (3 a 30, minúsculas, dígitos, `_` y `.`). Es único sin distinguir mayúsculas. Hay un índice `ux_users_user_name_lower`. Los reservados (`profile.reserved_usernames`, por defecto admin, soporte, situ y otros) responden igual que uno tomado: `409 user.username_taken`.
- Un cambio de nombre cada `profile.username_change_days` (30). Si es antes, `429 profile.username_change_limited` con `retryAfter`.
- `bio` máximo 160. `displayName` máximo 60. Una lista configurable (`profile.blocked_terms`) rechaza texto evidentemente indebido con `400 profile.content_not_allowed`.
- `homeCityId` tiene que existir y estar habilitada. Actualiza la ciudad principal y no toca las ciudades adicionales.
- La fecha de nacimiento no se edita. No hay endpoint para eso.

`GET /api/v1/users/username-available?userName=ana` exige sesión. Límite: 10 por minuto, por usuario e IP. Responde `{ "available": true }`. Reservado, tomado e inválido dan `available: false` y nada más.

Avatares:

| Método | Ruta | Qué hace |
| --- | --- | --- |
| GET | `/api/v1/avatars/gallery` | Íconos activos, ordenados, con URL absoluta. Hay 12 placeholders de marca. |
| PUT | `/api/v1/me/avatar` | `{ "galleryItemId": "..." }` |
| DELETE | `/api/v1/me/avatar` | Vuelve al ícono genérico. 204. |
| POST | `/api/v1/me/avatar/photo` | `multipart`, campo `file`. Solo adultos. |

Un menor recibe `403 avatar.minor_photo_not_allowed`. La foto se valida por los bytes (JPG, PNG o WebP), máximo 5 MB (`profile.avatar_max_bytes`), se recorta al centro en 512×512, se recodifica a JPEG y se le quita el EXIF. Se borra el archivo anterior. La URL de `GET /me` es firmada y caduca.

`POST /api/v1/me/password` con `{ "currentPassword", "newPassword", "keepCurrentSession": true }`. La misma operación sigue en `POST /api/v1/auth/password`. Verifica la actual, aplica la política de contraseñas, invalida los enlaces de recuperación y cierra las demás sesiones. `keepCurrentSession: false` cierra también la actual. Un intento fallido cuenta para el bloqueo de inicio de sesión (`423 auth.locked` al quinto). Se envía un aviso al correo; si el correo falla, el cambio igual queda hecho. Límite: el de autenticación, 30 por minuto por IP.

Dispositivos. `GET /api/v1/me/devices` y `DELETE /api/v1/me/devices/{id}` aceptan la sesión normal y el token corto de límite de dispositivos. `GET /api/v1/auth/devices` y `DELETE /api/v1/auth/devices/{id}` siguen siendo las mismas operaciones. Cada ítem trae `deviceName`, `platform`, `lastSeenAt`, `isActive` e `isCurrent`. Revocar cierra la sesión, el token push y la ubicación. El dispositivo actual responde `409 auth.current_device`; para ese está cerrar sesión. Repetir el DELETE de uno ya revocado responde 204.

## Configuración de la app

`GET /api/v1/app-config` es público. Lee `SystemSettings`, responde con ETag y `Cache-Control: public, max-age=60`. Un `If-None-Match` igual devuelve 304.

```json
{
  "panelCopy": { "browse": "Quiero chismosear", "report": "Quiero reportar algo" },
  "mediaLimits": { "maxImages": 5, "maxVideos": 1, "maxVideoSeconds": 60, "maxImageBytes": 10485760, "maxVideoBytes": 104857600 },
  "reportDescriptionMaxLength": 1000,
  "confirmationMaxDistanceMeters": 2000,
  "anonymityDisplayName": "Situero anónimo",
  "versions": {
    "ios": { "minSupported": "0.1.0", "latest": "0.1.0" },
    "android": { "minSupported": "0.1.0", "latest": "0.1.0" },
    "web": { "minSupported": "0.1.0", "latest": "0.1.0" }
  },
  "legal": {
    "termsUrl": "https://somossitu.com/terminos",
    "privacyPolicyUrl": "https://somossitu.com/privacidad",
    "supportUrl": "https://somossitu.com/soporte"
  }
}
```

El dominio sigue rechazando una descripción de más de 1000 caracteres aunque el ajuste diga otra cosa. `legal` sale de `legal.terms_url`, `legal.privacy_url` y `legal.support_url`. Esas direcciones son marcadores hasta que exista la web.

## Confirmación

`PUT /api/v1/reports/{id}/confirmation`

```json
{ "type": "StillThere", "latitude": 4.711, "longitude": -74.072 }
```

`type` es `StillThere` o `GoneNow`. Un voto por usuario: enviarlo de nuevo reemplaza el anterior y no duplica el conteo. El autor no vota. Hay que estar a menos de `reports.confirmation_radius_meters` (2000 m). `StillThere` alarga la vida un 25 % del TTL, con mínimo de 10 minutos. Si los votos de "ya no está" llegan a 3 y superan el doble de los "sigue ahí", el reporte pasa a Expired.

## Comentarios

Sesión de usuario. La API no distingue si el cliente es la web o el móvil. Quien comenta tiene que poder publicar (`canPublish`). Límite de escritura: 60 por hora, por usuario e IP (`interaction-write`).

`POST /api/v1/reports/{id}/comments`

```json
{ "text": "Sigue el trancón en la esquina.", "isAnonymous": true, "parentCommentId": null }
```

`text` máximo 500. `parentCommentId` solo apunta a un comentario raíz del mismo reporte: las respuestas no se anidan más de un nivel. Un reporte vencido, retirado o en revisión responde `comment.report_closed`. Si es `FriendsOnly`, solo lo ve y comenta un amigo aceptado del autor.

```json
{
  "id": "018f...",
  "text": "Sigue el trancón en la esquina.",
  "author": {
    "userId": null,
    "displayName": "Situero anónimo 1",
    "avatarUrl": null,
    "memberSinceYear": 2026,
    "memberSinceMonth": 10,
    "isAnonymous": true,
    "hideAuthorOnMap": true
  },
  "createdAt": "2026-10-24T15:00:00Z",
  "commentCount": 1
}
```

Con `isAnonymous`, el servidor guarda el autor y no lo devuelve. El número sale de `anonymity.display_name` y es estable dentro de ese reporte: la misma persona es siempre "Situero anónimo 1" ahí, y en otro reporte el número empieza de nuevo. Un menor de 18 solo puede firmar con su nombre en un reporte `FriendsOnly`. En uno público tiene que ir anónimo; si no, `403 comment.minor_public_identity`. El comentario no se reescribe solo.

`GET /api/v1/reports/{id}/comments?cursor=&limit=20` devuelve raíces de más reciente a más antiguo y, dentro de cada una, las respuestas visibles de más antigua a más reciente. `nextCursor` es opaco. Cada comentario trae `isMine`, también si es anónimo. Un bloqueo `Full` oculta los comentarios de esa persona en los dos sentidos. Un bloqueo `AnonymousContentOnly` oculta solo los anónimos, y solo para quien bloqueó.

`PATCH /api/v1/comments/{id}` con `{ "text": "..." }` y `DELETE /api/v1/comments/{id}` son solo del autor. El borrado es lógico y también retira las respuestas visibles. `Report.commentCount` baja en cada comentario que estaba visible.

El tope diario de comentarios anónimos es `comments.max_anonymous_per_day` (30). Al pasarlo: `429 comment.anonymous_daily_limit`.

`CommentAddedEvent` solo se escribe en el log, sin el id de quien comentó. Cuando existan notificaciones, el autor del reporte recibe el aviso aunque el comentario sea anónimo, sin saber quién fue.

## Reacciones

`PUT /api/v1/reports/{id}/reaction` y `PUT /api/v1/comments/{id}/reaction`

```json
{ "type": "Like" }
```

`type` es `Like`, `Love`, `Haha`, `Wow`, `Sad` o `Angry`. Un voto por usuario: otro `PUT` reemplaza. `DELETE` en la misma ruta lo quita.

```json
{ "type": "Love", "counts": [{ "type": "Like", "count": 1 }, { "type": "Love", "count": 1 }] }
```

El detalle y Mis reportes incluyen `reactionCounts` y `myReaction`. `Report.reactionCount` cuenta solo las reacciones del reporte, no las de sus comentarios. Misma visibilidad, bloqueo y estado que los comentarios.

## Denuncias y moderación

`POST /api/v1/complaints`

```json
{ "targetType": "Comment", "targetId": "018f...", "reason": "Spam", "details": null }
```

`targetType` es `Report`, `Comment` o `User`. `reason` es `FalseInformation`, `Harassment`, `Violence`, `SexualContent`, `MinorSafety`, `PrivacyViolation`, `Spam` u `Other`. No se puede denunciar el mismo objetivo dos veces mientras la denuncia siga abierta, ni denunciarse a uno mismo o al propio contenido. Para un autor anónimo se denuncia el reporte o el comentario; la respuesta no trae su identidad.

```json
{ "id": "018f...", "status": "Open", "isPriority": false }
```

`MinorSafety`, `SexualContent` y `Violence` son prioritarios: el reporte pasa a `UnderReview` o el comentario a `Hidden` con una sola denuncia. Con `moderation.complaint_threshold` denuncias de personas distintas (3) pasa lo mismo, aunque el motivo no sea prioritario. El autor lo ve como "En revisión" en Mis reportes.

Personal (política `Staff` y permiso `complaints.resolve`). Cada cambio deja `ModerationAction` y `AuditLog`.

| Método | Ruta | Cuerpo |
| --- | --- | --- |
| GET | `/api/v1/moderation/complaints?status=&reason=` | Cola por prioridad y antigüedad. Seguridad de menores primero. |
| POST | `/api/v1/moderation/complaints/{id}/start-review` | Pasa la denuncia a `InReview`. |
| POST | `/api/v1/moderation/complaints/{id}/uphold` | `{ "resolution": "...", "action": "Remove" }` o `RemoveAndSuspend`. Suspender exige `users.suspend`. |
| POST | `/api/v1/moderation/complaints/{id}/dismiss` | `{ "resolution": "..." }`. Si ya no hay otra denuncia que obligue a revisión, restaura lo que estaba en revisión automática. |
| POST | `/api/v1/moderation/anonymous-identity/reveal` | Permiso `anonymity.reveal`. |

`POST /api/v1/moderation/anonymous-identity/reveal`

```json
{
  "targetType": "Report",
  "targetId": "018f...",
  "complaintId": "018f...",
  "justification": "La denuncia exige identificar al autor real."
}
```

Hace falta una denuncia no rechazada de ese objetivo, o una justificación de al menos 20 caracteres. Antes de responder se escriben `AnonymousIdentityAccessLog` y `AuditLog`. Si el log no se puede guardar, la respuesta es `anonymity.log_failed` y no trae identidad.

```json
{ "userId": "018f...", "userName": "ana", "displayName": "Ana" }
```

Al resolver, el denunciante recibirá un aviso genérico. Por ahora solo se registra `ComplaintResolvedEvent`, sin imprimir quién fue denunciado.

## Bloqueos

`POST /api/v1/blocks` con `{ "userId": "018f..." }` crea un bloqueo `Full`. `GET /api/v1/blocks` devuelve cada bloqueo con un `blockId`. En uno `Full` viene el nombre público y el `userId`. En uno `AnonymousContentOnly` no viene ni el id ni el nombre: solo "Usuario anónimo" y `originLabel` ("desde un comentario del 12 oct").

```json
{
  "blocks": [
    { "blockId": "018f...", "scope": "Full", "userId": "018f...", "displayName": "Ana", "originLabel": null },
    { "blockId": "018f...", "scope": "AnonymousContentOnly", "userId": null, "displayName": "Usuario anónimo", "originLabel": "desde un comentario del 12 oct" }
  ]
}
```

`DELETE /api/v1/blocks/{blockId}` desbloquea por el id del bloqueo. Si el cliente viejo todavía manda el `userId` de un bloqueo `Full`, también lo quita. No desbloquea un anónimo por el id de la persona.

Para no conocer a un autor anónimo: `POST /api/v1/reports/{id}/block-author` y `POST /api/v1/comments/{id}/block-author`. La respuesta es siempre `{ "blocked": true }`, también si ya lo habías bloqueado. Ese alcance solo oculta, para quien bloquea, los reportes, comentarios y reacciones anónimos de esa persona. No oculta lo que publica con nombre, no corta la amistad ni la ubicación, y la otra persona no se entera. Un bloqueo `Full` sigue siendo en los dos sentidos: desaparece todo el contenido, se cancela la amistad y se corta la ubicación compartida. Desbloquear no devuelve la amistad. Todavía no hay endpoint de solicitudes de amistad: cuando exista, tiene que rechazar el envío si hay un bloqueo `Full`.

`GET /api/v1/me` incluye `deletionScheduledFor` cuando la cuenta está en `DeletionRequested`.

## Eliminar cuenta

`POST /api/v1/me/deletion-request` con `{ "password": "..." }`. Pide la contraseña otra vez, deja la cuenta en `DeletionRequested`, cierra sesiones y dispositivos, borra los tokens push y envía un correo con la fecha y cómo cancelar. La gracia es `account.deletion_grace_days` (14).

```json
{ "scheduledFor": "2026-11-07T15:00:00Z" }
```

Durante la gracia se puede iniciar sesión. `canPublish` queda en false y `/me` trae `publishBlockedReason: deletion_requested`. `POST /api/v1/me/deletion-request/cancel` reactiva la cuenta. Un job horario completa la eliminación al vencer la gracia. Si hay una retención legal activa, la solicitud pasa a `BlockedByLegalHold` y no se borra nada. El detalle de qué se borra y qué se conserva está en `docs/account-deletion.md`. La solicitud hecha por el representante de un menor queda pendiente.

## Notificaciones

Exigen sesión de usuario. El dispositivo actual sale del claim `did` del access token. Un token viejo, sin ese claim, no puede registrar push ni ubicación de alertas (`notification.device_required`).

`GET /api/v1/me/notification-settings` crea la fila si no existe. El radio por defecto es 1000 m. La zona horaria sale de la ciudad de interés, o `America/Bogota`.

```json
{
  "pushEnabled": true,
  "nearbyRadiusMeters": 1000,
  "notifyNearbyReports": true,
  "notifyFriendReports": true,
  "notifyFriendLives": true,
  "notifyImportantNews": true,
  "quietHoursStart": "22:00",
  "quietHoursEnd": "07:00",
  "timeZoneId": "America/Bogota"
}
```

`PUT /api/v1/me/notification-settings` recibe el mismo cuerpo. El radio va de 100 a 10000. El silencio pide inicio y fin, o ninguno. Apagar `notifyNearbyReports` borra la ubicación de alertas. Los interruptores de vivos de amigos y de noticias se guardan; esos disparadores todavía no corren.

`GET /api/v1/me/interest-cities` lista las ciudades adicionales. La principal sigue siendo la del registro y no aparece aquí.

```json
{
  "cities": [{ "cityId": "018f...", "name": "Medellín" }],
  "maximum": 3
}
```

`PUT /api/v1/me/interest-cities/{cityId}` agrega una. `DELETE` la quita. El máximo es `notifications.max_interest_cities` (3). No se puede repetir la ciudad principal.

`GET /api/v1/me/category-interests` devuelve `{ "mutedCategoryIds": [] }`. Sin filas, se notifican todas las categorías disponibles. `PUT` reemplaza la lista silenciada.

`PUT /api/v1/devices/current/push-token` con `{ "token": "ExponentPushToken[...]" }` guarda el token en el dispositivo de la sesión. `DELETE` en la misma ruta lo borra y también borra la ubicación de alertas. Cerrar sesión, revocar el dispositivo o eliminar la cuenta hacen lo mismo. Límite: 10 por minuto (`notification-device`), además del tope de un minuto del dominio para la ubicación.

`PUT /api/v1/devices/current/alert-location` con `{ "latitude": 4.71149, "longitude": -74.07249, "accuracyMeters": 30 }` responde `204` y no devuelve la coordenada. Se guarda redondeada a 3 decimales, 30 minutos, solo si la proximidad está activa. Un menor sin autorización de ubicación del representante recibe `403 notification.proximity_not_allowed_minor`.

`DELETE /api/v1/devices/current/alert-location` borra solo la ubicación aproximada del dispositivo de la sesión y responde `204`. No toca el token de push. Si no había ubicación, también responde `204`. Usa el mismo límite de 10 por minuto (`notification-device`).

`GET /api/v1/notifications?limit=20&cursor=` devuelve las más recientes primero.

```json
{
  "notifications": [
    {
      "id": "018f...",
      "type": "NearbyReport",
      "title": "En tu ciudad",
      "body": "Nuevo reporte en Bogotá: Hurto",
      "targetType": "Report",
      "targetId": "018f...",
      "reportId": "018f...",
      "createdAt": "2026-10-24T15:00:00Z",
      "isRead": false,
      "available": true
    }
  ],
  "nextCursor": null
}
```

En un comentario, `targetType` es `Comment` y `reportId` es el reporte para abrirlo. `available` es false si el reporte venció, se retiró o está en revisión. No viene el autor.

`GET /api/v1/notifications/unread-count` devuelve `{ "count": 1 }`. `POST /api/v1/notifications/{id}/read` y `POST /api/v1/notifications/read-all` marcan leídas.

El detalle de disparadores, de la ubicación que se guarda y del plan de la consulta está en `docs/notifications.md`.

## Panel de administración

El detalle de roles, el comando del primer administrador y la sesión corta del personal está en `docs/admin.md`. Fuera de Development, OpenAPI no incluye las rutas `/api/v1/admin`.

`GET /api/v1/admin/me` exige `role=Staff`. Responde el usuario, los roles y los permisos que hoy tiene en la base.

```json
{ "userId": "…", "userName": "ana", "roles": [{ "id": "…", "name": "Super admin" }], "permissions": ["audit.view", "metrics.view"] }
```

`GET /api/v1/admin/dashboard` (permiso `metrics.view`) devuelve solo conteos: usuarios, activos en 7 días, reportes activos, reportes por categoría en 7 días, denuncias abiertas y prioritarias, retiros vencidos y por vencer, y eliminaciones pendientes.

`GET /api/v1/admin/complaints/{id}` (permiso `complaints.resolve`) trae el texto, los medios, el estado y los conteos. Si el contenido es anónimo, `authorHidden` es true y no vienen el autor ni sus sanciones. La cola sigue en `GET /api/v1/moderation/complaints`.

`POST /api/v1/admin/users/{id}/suspend` con `{ "reason": "Reincidencia", "until": "2026-11-01T00:00:00Z" }`. Las otras sanciones son `warn`, `ban`, `reinstate` y `force-logout`, todas con `reason`. Usan `User.Suspend`, `Ban` y `Reinstate`, y dejan `ModerationAction`.

`GET /api/v1/admin/users?query=ana` busca el usuario exacto o, si hay `@`, el correo exacto. Mínimo 3 caracteres. `GET /api/v1/admin/users/{id}?revealEmail=true` muestra el correo completo solo con `users.view_email` y lo anota en la bitácora. La ficha trae `isMinor` y no trae fecha de nacimiento, ubicaciones ni reportes anónimos.

`POST /api/v1/takedown-requests` es público:

```json
{ "requesterName": "Ana Pérez", "requesterEmail": "ana@correo.com", "targetType": "Report", "targetId": "…", "description": "Aparezco en el reporte.", "website": "" }
```

Responde `202` con `{ "accepted": true, "trackingCode": "TD-A1B2C3D4" }`. En el panel, `GET /api/v1/admin/takedowns` trae `daysRemaining` y `overdue`. `start-review`, `accept` y `reject` exigen el permiso `takedowns.handle`. Aceptar y rechazar piden `resolution`.

## Códigos de error

| HTTP | code | Cuándo |
| --- | --- | --- |
| 403 | `report.email_unconfirmed` | El correo no está confirmado |
| 403 | `report.pending_guardian` | Falta la autorización del representante |
| 403 | `report.cannot_publish` | La cuenta no está Active |
| 403 | `report.minor_public_identity` | Un menor intenta un Public con nombre |
| 400 | `report.category_unavailable` | Categoría inactiva o apagada en esa ciudad |
| 403 | `report.adult_only` | Categoría solo para mayores de 18 |
| 400 | `report.media_required` | La categoría exige foto o video |
| 404 | `report.media_not_found` | El mediaId no es de quien publica o no está Ready |
| 400 | `report.media_type` | El archivo no es JPEG, PNG, WebP, MP4 o MOV |
| 400 | `report.image_too_large` | La imagen supera el máximo |
| 400 | `report.video_too_large` | El video supera el máximo |
| 400 | `report.video_too_long` | El video dura más de lo permitido |
| 400 | `report.video_unreadable` | No se pudo leer la duración |
| 400 | `report.too_many_files` | Más de 5 imágenes o 1 video |
| 400 | `report.invalid_visibility` | Visibilidad distinta de Public o FriendsOnly |
| 400 | `report.invalid_confirmation` | Voto distinto de StillThere o GoneNow |
| 404 | `report.not_found` | No existe, o quien consulta no puede verlo |
| 404 | `report.not_available` | Venció, fue retirado o está en revisión. `reportStatus` dice cuál, sin el motivo de moderación |
| 403 | `report.not_author` | Otra persona intenta editar o borrar |
| 403 | `report.confirmation_own` | El autor vota su reporte |
| 400 | `report.confirmation_too_far` | El voto está lejos del reporte |
| 429 | `report.daily_limit` | Tope diario del plan |
| 429 | `report.anonymous_daily_limit` | Tope diario de anónimos |
| 403 | `category.forbidden` | Sin permiso de staff |
| 404 | `category.not_found` | La categoría no existe |
| 409 | `category.code_taken` | El código ya existe |
| 403 | `comment.email_unconfirmed` | El correo no está confirmado |
| 403 | `comment.pending_guardian` | Falta la autorización del representante |
| 403 | `comment.cannot_publish` | La cuenta no está Active (incluye eliminación solicitada) |
| 403 | `comment.minor_public_identity` | Un menor comenta con nombre en un reporte público |
| 400 | `comment.report_closed` | El reporte está vencido, retirado o en revisión |
| 404 | `comment.not_found` | El comentario no existe o ya no está visible |
| 403 | `comment.not_author` | Otra persona intenta editar o borrar el comentario |
| 400 | `comment.parent_invalid` | La respuesta no cuelga de un comentario raíz visible |
| 429 | `comment.anonymous_daily_limit` | Tope diario de comentarios anónimos |
| 404 | `reaction.not_found` | No hay una reacción que quitar |
| 404 | `complaint.not_found` | La denuncia no existe |
| 409 | `complaint.duplicate` | Ya hay una denuncia abierta de esa persona sobre ese objetivo |
| 400 | `complaint.self` | Se denuncia a sí mismo o su propio contenido |
| 404 | `complaint.target_not_found` | El objetivo no existe |
| 403 | `complaint.forbidden` | Sin permiso `complaints.resolve` |
| 403 | `users.suspend_forbidden` | Pidió suspender sin permiso `users.suspend` |
| 403 | `anonymity.forbidden` | Sin permiso `anonymity.reveal` |
| 403 | `anonymity.reveal_denied` | No hay denuncia ni una justificación válida |
| 500 | `anonymity.log_failed` | No se pudo escribir la bitácora; no se revela la identidad |
| 400 | `block.self` | Intenta bloquearse |
| 404 | `block.not_found` | El usuario o el bloqueo no existe |
| 401 | `account.password_invalid` | La contraseña de la eliminación no coincide |
| 409 | `account_deletion.already_scheduled` | Ya hay una eliminación en curso |
| 400 | `account_deletion.not_scheduled` | No hay una eliminación que cancelar |
| 409 | `notification.device_required` | El access token no trae el dispositivo actual |
| 404 | `notification.device_not_found` | El dispositivo no es de esta sesión o está revocado |
| 409 | `notification.proximity_disabled` | La proximidad está apagada y se envió una ubicación |
| 403 | `notification.proximity_not_allowed_minor` | Un menor sin autorización de ubicación del representante |
| 429 | `notification.alert_location_limited` | Más de una ubicación de alertas por minuto |
| 400 | `notification.timezone_invalid` | La zona IANA no es válida |
| 400 | `notification.quiet_hours` | El silencio tiene solo inicio o solo fin |
| 404 | `notification.city_not_found` | La ciudad adicional no existe o está apagada |
| 409 | `notification.interest_city_home` | Es la ciudad principal del registro |
| 409 | `notification.interest_city_exists` | La ciudad adicional ya estaba |
| 409 | `notification.interest_city_limit` | Se pasó el máximo del plan |
| 404 | `notification.category_not_found` | La categoría silenciada no existe |
| 404 | `notification.not_found` | La notificación no es de esta persona |

## Qué debe saber el cliente

- Guardar `catalogVersion` y el `ETag`. Volver a pedir el catálogo con `If-None-Match`. Armar el árbol con `parentCategoryId`.
- Subir cada archivo y después crear el reporte con esos `mediaIds`. No enviar el binario en el POST del reporte.
- No asumir que un Public de un menor se convierte en FriendsOnly. Hay que pedir anonimato o cambiar la visibilidad antes de enviar. Lo mismo vale para un comentario: en un reporte público el menor tiene que enviarlo anónimo.
- En un comentario anónimo, mostrar `displayName` tal cual ("Situero anónimo 1"). Ese número solo vale dentro de ese reporte.
- Un `409` o `403` de denuncia, bloqueo o comentario se muestra con `detail`. No intentar adivinar al autor anónimo: solo el endpoint de revelación, de personal, lo devuelve.
- Tratar `userId` null y `hideAuthorOnMap` como "no mostrar avatar ni abrir perfil".
- En el mapa, pintar `cluster` y `point` distinto. Pasar la posición del usuario si se quiere la distancia.
- Ante un 429, esperar hasta `retryAfter`.
- Los íconos viven en `/assets/categories/{code}.png`. Si fallan, usar el ícono por defecto.
