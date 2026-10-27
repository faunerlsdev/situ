# Datos para las fichas de privacidad

Las dos tablas salen del código actual. No hay SDK de anuncios ni de seguimiento. `PrivacyInfo.xcprivacy` declara `NSPrivacyTracking` en falso y la app bloquea `com.google.android.gms.permission.AD_ID`. El identificador del dispositivo lo crea la propia app (`apps/mobile/lib/device.ts`) y lo guarda en el llavero; no es el IDFA ni el identificador de publicidad de Android.

El tránsito con la API es HTTPS. La ubicación se pide solo mientras la app está abierta (`requestForegroundPermissionsAsync` en el mapa, el formulario y las alertas). No hay tarea en segundo plano y `ACCESS_BACKGROUND_LOCATION` está bloqueado.

Al borrar la cuenta, la app y la página `https://app.${DOMAIN_ROOT}/cuenta/eliminar` piden la eliminación. Hay 14 días de gracia (`account.deletion_grace_days`). Al cumplirse, el nombre, el correo, el teléfono y la fecha de nacimiento se sustituyen y la cuenta queda en estado eliminado. Los reportes y comentarios pueden quedar el tiempo de retención del contenido, sin el nombre de la persona. Una retención legal detiene el borrado.

## Google Play, Seguridad de los datos

| Dato | ¿Se recoge? | Finalidad | ¿Se comparte con terceros? | Cifrado en tránsito | Borrado |
| --- | --- | --- | --- | --- | --- |
| Correo | Sí, obligatorio | Cuenta, confirmación e inicio de sesión | Sí, con el proveedor de correo, solo para enviar el mensaje | Sí | Se anonimiza al terminar la gracia |
| Fecha de nacimiento | Sí, obligatoria | Saber si es menor y pedir autorización del representante | No | Sí | Se sustituye al terminar la gracia |
| Teléfono | Sí. Obligatorio para menores; opcional para adultos | Seguridad de la cuenta. No se muestra a otras personas | No | Sí | Se borra al terminar la gracia |
| Ubicación precisa | Sí, mientras se usa la app | Centrar el mapa, publicar un reporte, confirmar que estás cerca y, si la persona lo activa, alertas de proximidad | Sí, con Google Maps, cuando el mapa de Android usa el SDK de Google. La API de SITU también recibe el punto del reporte o de la alerta | Sí | La alerta de proximidad la borra el servidor a los 30 minutos. El punto de un reporte vive con ese reporte |
| Ubicación aproximada | Sí, el permiso grueso va junto al preciso | La misma del mapa y de la distancia | La misma que la precisa | Sí | La misma que la precisa |
| Fotos y videos | Sí, solo si la persona los elige | Reporte o foto de perfil | No con un tercero. Otras personas de SITU ven el reporte publicado | Sí | Se borran con el reporte o con la foto de perfil. La eliminación de cuenta anonimiza al autor |
| Contenido de usuario | Sí: nombre visible, usuario, bio, reportes y comentarios | La función de la red | No con un tercero. Lo ven otras personas de SITU según la visibilidad del reporte | Sí | La bio y el nombre se anonimizan con la cuenta. El texto de un reporte sigue la retención del reporte |
| Identificadores del dispositivo | Sí: UUID creado por la app y token de Expo Push | Sesión, límite de dispositivos y notificaciones | El token de push se envía a Expo para entregar la notificación. No se usa para anuncios ni seguimiento | Sí | El token se borra al cerrar la sesión o al eliminar la cuenta |
| Diagnósticos | No. No hay SDK de fallos ni de analítica | — | No | — | — |

En el formulario marca también: no se venden datos, no se usan para publicidad ni para seguimiento, y la recolección no es opcional en correo y fecha de nacimiento porque sin ellos no hay cuenta.

## Apple, etiquetas de privacidad

| Tipo | Vinculado a la identidad | Seguimiento | Finalidad | Terceros |
| --- | --- | --- | --- | --- |
| Correo | Sí | No | Funcionalidad de la app | Proveedor de correo, para el envío |
| Fecha de nacimiento | Sí | No | Funcionalidad de la app | Ninguno |
| Teléfono | Sí | No | Funcionalidad de la app | Ninguno |
| Ubicación precisa | Sí | No | Funcionalidad de la app | Google Maps, si la build de iOS lleva clave de Google. Si no lleva clave, el mapa es de Apple |
| Ubicación aproximada | Sí | No | Funcionalidad de la app | La misma que la precisa |
| Fotos o videos | Sí | No | Funcionalidad de la app | Ninguno. Otras personas de SITU ven lo publicado |
| Contenido de usuario | Sí | No | Funcionalidad de la app | Ninguno. Otras personas de SITU ven lo publicado |
| Identificadores del dispositivo | Sí | No | Funcionalidad de la app | Expo, solo el token de push |
| Diagnósticos | No se recogen | No | — | — |

`NSPrivacyAccessedAPITypes` en la config cubre las APIs que exigen motivo y que usan Expo y React Native dentro de la app: ajustes de la propia app (`CA92.1`), fechas de archivos del contenedor (`C617.1`), espacio en disco al guardar un archivo (`E174.1`) y tiempo de arranque para medir intervalos (`35F9.1`). No son datos que se envíen a un tercero.

Cifrado: la app solo usa HTTPS y el almacenamiento del sistema. `ITSAppUsesNonExemptEncryption` está en falso. En App Store Connect responde que no usa cifrado distinto del exento.
