# Mapa en tiempo real

El hub avisa que algo cambió. No manda el reporte. Cada teléfono pide el contenido por la API, y esa respuesta aplica visibilidad, bloqueos, anonimato y menores para esa cuenta.

## Contrato del hub

`/hubs/map` (SignalR, WebSockets).

Autenticación: política `User`. El token de acceso va en el header `Authorization` en la negociación HTTP. En el WebSocket del teléfono va en la query `access_token`, y el servidor solo lee esa query en `/hubs/map`. La web no pone el access token en el navegador: pide `POST /api/v1/auth/realtime-ticket` (sesión obligatoria) y abre el WebSocket directo, sin negociación, con la query `ticket`. Así el ticket se presenta una sola vez. Ese ticket dura 30 segundos, es de un solo uso, queda ligado al usuario y al dispositivo web, y el hub lo invalida al aceptarlo. Si el socket se cae, la web pide otro ticket. La query `ticket` solo se lee en `/hubs/map`; en cualquier otra ruta no autentica. El token trae `did`. El dispositivo tiene que existir, estar activo y, para el ticket, ser web. Una cuenta de personal se rechaza.

El access token dura 15 minutos. Cuando llega `exp`, el servidor cierra el socket. La app pide un token nuevo y vuelve a conectar.

Métodos:

- `SubscribeCells(cellIds[])`
- `UnsubscribeCells(cellIds[])`

Límites, cerrando la conexión:

- máximo 16 celdas por conexión
- máximo 20 llamadas a `SubscribeCells` por minuto
- un id que no sea `x:y` entero, dentro del rango del mapa, cierra la conexión

Mensajes: "Demasiadas zonas abiertas a la vez.", "Demasiados cambios de zona. Espera un momento.", "La zona no es válida."

El servidor no guarda ni registra la ubicación de quien mira. Solo recuerda las celdas de esa conexión, en memoria, hasta que se cae.

## Evento

Canal `zone`. Cada elemento es exactamente:

```json
{ "type": "ReportCreated", "reportId": "…", "cellId": "-1482:94", "occurredAt": "2026-10-26T15:00:00Z" }
```

`type`: `ReportCreated`, `ReportUpdated`, `ReportResolved`, `ReportExpired`, `ReportRemoved`.

No viaja descripción, autor, visibilidad ni coordenada fina. `cellId` es la celda del reporte, no la de quien lo ve.

`ReportUpdated` cubre una confirmación que no resuelve el reporte, un cambio de descripción y la entrada o salida de revisión. `ReportResolved` es el mismo estado `Expired` de siempre, cuando "ya no está" gana. `ReportExpired` es el vencimiento por tiempo. `ReportRemoved` es el retiro del autor o de moderación.

El cliente, con `ReportCreated` o `ReportUpdated`, pide `GET /api/v1/reports/batch?ids=`. Con `ReportRemoved`, `ReportExpired` o `ReportResolved` quita el pin y no pide nada.

## Quién no entra al canal

Un reporte `FriendsOnly` no emite evento de zona. Tampoco uno cuyo autor es menor de edad, aunque sea público y anónimo. Siguen en el mapa por la carga REST (el mapa de siempre, y el filtro de amigos). En este MVP no aparecen ni desaparecen solos. Un reporte público de un adulto sí emite, también si es anónimo: el autor se resuelve después, en la API, con `ReportPresentation`.

## Celdas

Cuadrícula fija. Con el tamaño por defecto, 0.05 grados:

`cellId = floor(longitud / 0.05) + ":" + floor(latitud / 0.05)`

El mismo cálculo está en `Situ.Domain.Reports.MapCell` y en `packages/shared` (`cellId`, `cellsCovering`). Si el viewport toca más de 16 celdas, o cruza el antimeridiano, la app no se suscribe y el mapa sigue con la carga normal.

## Publicación

Los avisos se escriben en `map_zone_notices` después del commit, en el mismo punto que el outbox de notificaciones. Si ese insert falla, la operación original sigue y queda un log con el tipo de excepción.

Un proceso en segundo plano agrupa lo pendiente: una tanda por celda, como máximo un aviso por reporte (retirar gana sobre crear). El intervalo por defecto es 1 segundo. Si SignalR falla, el aviso sigue pendiente y la operación que creó el reporte no se deshace.

## Configuración

Claves en `SystemSettings` (también están los mismos valores por defecto en `appsettings`, sección `MapRealtime`):

| Clave | Default |
| --- | --- |
| `map.cell_degrees` | `0.05` |
| `map.max_cells` | `16` (tope duro 16) |
| `map.subscribe_calls_per_minute` | `20` (tope duro 20) |
| `map.coalesce_interval_milliseconds` | `1000` |

`GET /api/v1/app-config` incluye `mapRealtime` (`cellDegrees`, `maxCells`, `coalesceIntervalMilliseconds`, `subscribeCallsPerMinute`) para que el teléfono use la misma cuadrícula.

## Observabilidad

Logs sin usuario, sin token y sin celda concreta:

- conexiones abiertas y cantidad de celdas suscritas
- eventos emitidos y celdas de esa tanda
- cierre por límite, por caducidad del token, o fallo del publicador (solo el tipo de excepción)

SignalR y el log de conexiones HTTP quedan en `Warning` para no escribir la query del token.

## Transmisiones

Cuando una transmisión pasa a estar al aire, o cuando una que estaba al aire termina, el hub `/hubs/map` manda `live` a los grupos `user:{id}` de quienes pueden verla. Si es pública, de un adulto y tiene ubicación redondeada, también llega a la celda de esa ubicación. El cuerpo es `{ "publicId": "<path>", "change": "started" | "ended" }`. No lleva nombre, título, cuenta ni coordenada. El teléfono vuelve a pedir `GET /api/v1/live`.

El mismo cambio encola `live.started` o `live.ended` en el outbox. La visibilidad de la transmisión reemplaza las demás audiencias:

- Solo amigos: el aviso va únicamente a amigos que pueden verla (`CanWatch`, bloqueos, menores). El texto es "Un amigo está en vivo." Al terminar, "La transmisión de un amigo terminó." Respeta `notifyFriendLives`. No usa cercanía ni ciudad.
- Público, y quien transmite compartió ubicación: avisa a quien tenga una ubicación de alertas vigente a menos de `live.notify_radius_m` metros (1000 por defecto, entre 100 y 10000) y a quien tenga la ciudad de la transmisión como ciudad de interés. Una persona que cae en los dos recibe un solo aviso: "Hay una transmisión en vivo cerca de ti." Respeta `notifyNearbyLives`. No avisa al resto de los amigos por ser amigos.
- Público sin ubicación: no hay punto ni ciudad que evaluar. No se envía ningún aviso. La transmisión sigue en la lista de En vivo. No se inventa la ciudad de la cuenta.

Nadie avisa a quien transmite. El texto no lleva nombre, título ni coordenada. Un toque abre el reproductor: en el push, `targetId` es el path público; en la bandeja, el servidor lo resuelve al mismo path. El silencio suprime el push y deja el aviso en la bandeja. El tope es `notifications.max_pushes_per_hour` (6). Una misma persona no recibe dos veces el mismo aviso de la misma transmisión. El reintento sigue en el outbox. Una transmisión pública que termina no dispara otro aviso masivo.

## Un solo nodo

Hoy hay un proceso de la API. Los grupos de SignalR viven en la memoria de ese proceso. No hace falta Redis: no hay un segundo nodo al que avisarle.

Para más de un proceso, los grupos en memoria no se ven entre sí. El paso siguiente es un backplane de SignalR (Redis) para que un aviso publicado en un nodo llegue a las conexiones de los otros. El outbox no cambia: sigue siendo la tabla, y cada nodo no debe publicar la misma fila. Eso se resuelve con un solo publicador (un worker) o marcando la fila publicada dentro de una transacción antes de emitir.
