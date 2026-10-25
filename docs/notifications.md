# Notificaciones

Borrador para la política de tratamiento y para revisión de un abogado. No es un concepto jurídico. Describe lo que hace el backend en esta fase.

Canales de esta fase: push móvil (Expo) y el centro de notificaciones dentro de la app. El correo sigue limitado a los avisos de seguridad que ya existían (confirmación, restablecer contraseña, representante, eliminación de cuenta). La notificación web queda pendiente. No hay transmisión en vivo ni noticias importantes: los interruptores se guardan, pero esos disparadores no corren.

## Qué ubicación se guarda, por cuánto tiempo y para qué

La ubicación de alertas existe solo para decidir si un reporte cae dentro del radio de la persona. No es la ubicación del mapa, no se comparte con otros usuarios y ningún endpoint la devuelve.

Se guarda, por dispositivo:

- latitud y longitud redondeadas a 3 decimales (unos 110 m)
- la precisión que envió el teléfono, si vino
- el dispositivo y la persona
- cuándo se reportó y cuándo caduca

Caduca a los 30 minutos (`notifications.alert_location_ttl_minutes`). Un job la borra cuando vence. También se borra al cerrar sesión, al cerrar todas las sesiones, al revocar el dispositivo, al desactivar las alertas de reportes cercanos, con `DELETE /api/v1/devices/current/alert-location` y al pedir o completar la eliminación de la cuenta. Ese DELETE no quita el token de push.

Solo se acepta si las alertas de reportes cercanos están activas, como máximo una vez por minuto por dispositivo, y si la sesión trae el dispositivo (`did` en el token). Un menor solo puede enviarla si un representante autorizó la ubicación. Si no, la API responde `403 notification.proximity_not_allowed_minor` y esa persona sigue recibiendo alertas por ciudad.

No se escribe en logs. El registro sensible de Entity Framework sigue apagado. El token de Expo tampoco se escribe en logs. Si Expo responde `DeviceNotRegistered`, se borra el token de ese dispositivo.

## Disparadores

Un reporte genera destinatarios solo si está activo. Ciudad y proximidad exigen además que sea público. El de amigo también corre en un reporte solo para amigos, nunca si el reporte es anónimo.

| Disparador | A quién | Anónimo |
| --- | --- | --- |
| Ciudad de interés | Quien tenga esa ciudad como principal (la del registro) o como ciudad adicional | Sí |
| Proximidad | Quien tenga un dispositivo con ubicación de alertas vigente a una distancia menor o igual que su propio radio (1000 m por defecto) | Sí |
| Amigo | Amistades aceptadas del autor | No, nunca (RF-ANO-07) |

Un reporte anónimo y solo para amigos no avisa a nadie. Un reporte con nombre y solo para amigos avisa solo a amigos aceptados.

Hoy no hay endpoint de solicitudes de amistad, así que el disparador de amigo no tiene a quién avisar hasta que existan amistades aceptadas. El código ya lo contempla.

Prioridad si una persona califica por varios: amigo, luego proximidad, luego ciudad. Recibe una sola notificación por reporte. El índice único es (persona, tipo de objetivo, objetivo).

Un comentario en un reporte avisa al autor del reporte, también si el comentario es anónimo. El texto no dice quién comentó. Cada comentario es una fila (el objetivo es el comentario) y la respuesta incluye `reportId` para abrir el reporte.

## Exclusiones

Siempre se deja fuera:

- al autor del reporte
- a quien tenga un bloqueo `Full` en cualquier sentido
- a quien bloqueó al autor solo en anónimo, cuando el reporte es anónimo
- a cuentas suspendidas, baneadas, eliminadas o con eliminación pedida
- a quien silenció esa categoría
- a quien tiene la categoría apagada en su ciudad
- a un menor, si la categoría es solo para adultos
- a quien apagó ese tipo de aviso

`notifyNearbyReports` apaga ciudad y proximidad juntas. `notifyFriendReports` apaga el disparador de amigo. Comentarios no tienen interruptor propio: entran al centro y el push respeta el push global y el horario de silencio.

Quien no tiene fila de preferencias se trata como el valor por defecto: push activo, radio 1000 m, ciudad y amigos activos, zona de su ciudad o `America/Bogota`, sin horario de silencio.

## Envío

Crear el reporte no espera el fan-out. Al guardar, se escribe un trabajo en `notification_jobs` (outbox en PostgreSQL, sin Redis). Un `BackgroundService` los toma de a `notifications.dispatch_batch_size` (50), con hasta 5 reintentos. Si el mismo reporte se procesa otra vez, el índice único evita una segunda notificación.

El push sale en lotes de hasta `notifications.push_batch_size` (100, tope de Expo). El mensaje lleva `channelId` `zone-alerts` (el mismo canal que crea la app), `priority` `high`, `sound` `default` y `badge` con las no leídas de esa persona después de crear la notificación.

`data` solo tiene identificadores: `notificationId`, `type`, `targetType`, `targetId` y `reportId`. En un comentario, `reportId` es el reporte y `targetId` es el comentario. El resumen agrupado usa `type` `Summary` y no trae `reportId`. No van el nombre, el texto, la distancia ni las coordenadas. Un reporte anónimo no agrega ninguna pista del autor.

`Notifications:RequireExpoAccessToken` vale `false` si no se define y el entorno es Development, y `true` en cualquier otro entorno. Con token, el envío lleva `Authorization: Bearer`. Sin token y con el requisito apagado, se llama a Expo igual, sin ese encabezado. Sin token y con el requisito encendido, no se envía nada: al arrancar se escribe una sola advertencia y la API sigue viva.

Cada intento queda en `notification_deliveries` como `Pending`, `Sent`, `Failed` o `Suppressed`. El error es un código estable, sin datos personales.

Anti-spam: como máximo `notifications.max_pushes_per_hour` (6) pushes por persona. Al pasar el límite, el resto entra al centro con la entrega suprimida (`hourly_limit`) y se manda un solo resumen: "Hay N reportes nuevos en tu zona". Las siguientes del mismo hora local solo actualizan ese resumen y no vuelven a empujar.

Horario de silencio: se evalúa en la zona IANA de la persona. Durante el silencio la notificación queda en el centro y el push no se envía (`quiet_hours`). No se pospone.

## Textos

Están en un solo lugar, en español de Colombia, sin nombres, sin la descripción del reporte y sin coordenadas.

- Cerca: "Accidente de tránsito a 400 m"
- Ciudad: "Nuevo reporte en Bogotá: Hurto"
- Amigo: "Un amigo reportó: Hurto"
- Comentario: "Alguien comentó tu reporte."
- Resumen: "Hay 5 reportes nuevos en tu zona"

## Consulta de proximidad

PostGIS, `ST_DWithin` sobre `geography`, índice GIST `ix_device_alert_locations_location`. El radio es el de cada persona (`COALESCE` a 1000 m). Si no tiene preferencias, igual entra. Una fila por persona, la del dispositivo más cercano.

Medido el 24 oct 2026 sobre la base local, dentro de una transacción que se revirtió, con 3 ubicaciones vigentes (los dispositivos que había). El planificador eligió `Seq Scan` en `device_alert_locations`: con tan pocas filas el índice sale más caro. El índice GIST sí está creado. El filtro `ST_DWithin` se aplicó en el `Hash Left Join`.

```text
Unique  (cost=30.76..30.77 rows=1 width=24) (actual time=73.802..73.808 rows=2.00 loops=1)
  ->  Sort
        Sort Key: d.user_id, st_distance(...)
        ->  Hash Left Join
              Filter: (COALESCE(s.notify_nearby_reports, true) AND st_dwithin(...))
              ->  Seq Scan on device_alert_locations d
                    Filter: (expires_at > now())
                    (actual time=0.004..0.009 rows=3.00 loops=1)
              ->  Hash
                    ->  Seq Scan on notification_settings s
Execution Time: 74.214 ms
```

Esas 3 filas no quedaron guardadas. Cuando la tabla crezca, el mismo predicado puede usar el GIST. La prueba de integración contra `situ_test` (dentro y fuera del radio) se omite hasta que exista `ConnectionStrings:Test`.

## Centro

`GET /api/v1/notifications` va de la más reciente a la más antigua, con cursor. Cada ítem trae id, tipo, título, texto, objetivo, `reportId`, fecha, si fue leída y si sigue disponible. Disponible es false si el reporte venció, se retiró o está en revisión: la app no debe abrirlo. Nunca viene el autor de un reporte anónimo.
