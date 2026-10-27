# Estado del proyecto frente a requisitos v2

Auditoría de solo lectura del 27 de octubre de 2026 contra `docs/requisitos-v2.md.md`. Cada ID se contrastó con código, pantallas y pruebas. No se reejecutó la suite.

Estados: COMPLETO, PARCIAL, SOLO BACKEND, NO INICIADO, DIFERIDO CONSCIENTE. No hubo requisitos que quedaran solo en móvil o solo en el panel, ni alguno cuyo estado entero sea no verificable. La revisión de un abogado sí es no verificable y va en las secciones 4 y 7.

## 1. Resumen ejecutivo

Se analizaron **147** requisitos (RF-XXX-NN).

| Estado | Cantidad | % |
| --- | ---: | ---: |
| COMPLETO | 10 | 6,8 % |
| PARCIAL | 70 | 47,6 % |
| SOLO BACKEND | 45 | 30,6 % |
| NO INICIADO | 20 | 13,6 % |
| DIFERIDO CONSCIENTE | 2 | 1,4 % |

Por fase del documento (cada ID en una sola fase):

| Fase | IDs | Completo | Parcial | Solo backend | No iniciado | Diferido |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 1. MVP | 92 | 10 (10,9 %) | 66 (71,7 %) | 2 (2,2 %) | 13 (14,1 %) | 1 (1,1 %) |
| 2. Social y vivo | 37 | 0 | 3 (8,1 %) | 27 (73,0 %) | 6 (16,2 %) | 1 (2,7 %) |
| 3. Monetización | 10 | 0 | 1 (10 %) | 8 (80 %) | 1 (10 %) | 0 |
| 4. Empresas | 8 | 0 | 0 | 8 (100 %) | 0 | 0 |
| 5. Web live | 0 | — | — | — | — | — |

La fase 5 no tiene ID propio: queda cubierta por RF-LIV-02 y RF-WEB-02 (hoy la web no transmite; transmitir desde el navegador es futuro).

Por sección:

| Sección | IDs | Completo | Parcial | Solo backend | No iniciado | Diferido |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Registro y acceso (AUT) | 12 | 5 | 5 | 0 | 2 | 0 |
| Menores (MIN) | 12 | 2 | 6 | 2 | 2 | 0 |
| Perfil (PER) | 6 | 0 | 3 | 3 | 0 | 0 |
| Ubicación (UBI) | 11 | 0 | 0 | 7 | 4 | 0 |
| Mapa (MAP) | 10 | 0 | 7 | 0 | 3 | 0 |
| Menú (MEN) | 2 | 0 | 2 | 0 | 0 | 0 |
| Panel chismosear (PAN) | 4 | 0 | 3 | 0 | 0 | 1 |
| Categorías (CAT) | 4 | 1 | 3 | 0 | 0 | 0 |
| Reportes (REP) | 6 | 0 | 6 | 0 | 0 | 0 |
| Anonimato (ANO) | 9 | 1 | 6 | 0 | 1 | 1 |
| Transmisión (LIV) | 12 | 0 | 1 | 11 | 0 | 0 |
| Amigos (AMI) | 4 | 0 | 0 | 4 | 0 | 0 |
| Notificaciones (NOT) | 7 | 0 | 7 | 0 | 0 | 0 |
| Publicidad (MON) | 3 | 0 | 0 | 1 | 2 | 0 |
| Suscripciones (SUS) | 4 | 0 | 1 | 2 | 1 | 0 |
| Algoritmo (ALG) | 6 | 0 | 0 | 6 | 0 | 0 |
| Dashboard (AVA) | 8 | 0 | 0 | 8 | 0 | 0 |
| Admin (ADM) | 5 | 1 | 3 | 0 | 1 | 0 |
| Moderación (MOD) | 3 | 0 | 3 | 0 | 0 | 0 |
| Global (GLO) | 7 | 0 | 5 | 1 | 1 | 0 |
| Web (WEB) | 6 | 0 | 4 | 0 | 2 | 0 |
| Retención (RET) | 6 | 0 | 5 | 0 | 1 | 0 |

El proyecto no está cerca de abrirse a usuarios reales. El móvil ya cubre cuenta, menores, mapa de reportes, anonimato y notificaciones por ciudad y cercanía, y el panel modera, pero la web no crea reportes ni tiene perfil, el visitante no ve el mapa, no hay prueba de punta a punta en un teléfono, y faltan el formulario público de retiro, la revisión legal y la operación de producción.

## 2. Tabla por sección

### Registro, acceso y seguridad

| ID | Resumen | Estado | Evidencia | Qué falta |
| --- | --- | --- | --- | --- |
| RF-AUT-01 | Registro con correo, clave, usuario, nacimiento, país, región y ciudad | COMPLETO | `RegisterUser/Request.cs`, `apps/mobile/app/(auth)/registro.tsx`, `apps/web/src/components/register-form.tsx`, `IdentityHandlerTests.cs` | También pide teléfono, que el documento no lista |
| RF-AUT-02 | Edad mínima 14 y mensaje neutro; sin reintento de fecha en el mismo dispositivo | PARCIAL | `Country.MinimumAge`, `User.Register` en `UserRegistrationTests.cs` | No hay bloqueo de otra fecha en el mismo dispositivo |
| RF-AUT-03 | Edad mínima por país, 14 en Colombia | COMPLETO | `Country.cs`, `admin/countries/{id}/minimum-age`, `apps/admin/src/app/(panel)/configuracion/page.tsx` | — |
| RF-AUT-04 | La fecha de nacimiento no se edita; el cambio pasa por soporte | PARCIAL | `UpdateMyProfile/Handler.cs` no recibe fecha de nacimiento | No hay trámite de soporte para corregirla |
| RF-AUT-05 | A los 18 años la cuenta pasa a adulto y se avisa | NO INICIADO | No hay job ni aviso | Todo |
| RF-AUT-06 | Aceptar términos y política, con fecha y versión, enlaces por país e idioma | PARCIAL | `AcceptedDocumentIds` en el registro, `ListLegalDocumentsEndpoint.cs`, `ConsentRecord.cs` | Un solo idioma de interfaz; no se comprobó el texto legal publicado |
| RF-AUT-07 | Confirmar correo; sin eso se ve y no se publica | COMPLETO | `ConfirmEmailEndpoint.cs`, `CreateReport/Handler.cs`, pantallas `confirmar-correo` en móvil y web | — |
| RF-AUT-08 | Recuperar contraseña con token de un uso | COMPLETO | `PasswordResetEndpoints.cs`, `ResetPassword/Handler.cs`, pantallas recuperar/restablecer, `PasswordResetHandlerTests.cs` | — |
| RF-AUT-09 | Bloqueo 15 minutos, N intentos, el reset desbloquea, ambos configurables | COMPLETO | `LockoutPolicy.cs`, `User.ResetPassword` llama `Unlock()`, settings `auth.lockout_minutes` y `auth.max_failed_attempts`, `LockoutTests.cs` | — |
| RF-AUT-10 | Límite de dispositivos, la web cuenta, pantalla y cierre remoto | PARCIAL | `SignIn/Handler.cs` `DeviceLimitAsync`, `SessionEndpoints.cs`, `apps/mobile/app/dispositivos.tsx` y `limite-dispositivos.tsx` | La web guarda `situ_web_did` y no tiene pantalla de dispositivos |
| RF-AUT-11 | Cerrar sesión y eliminar la cuenta desde la app | PARCIAL | `AccountDeletion/Handler.cs`, `CompleteAccountDeletionsService.cs`, `apps/mobile/app/eliminacion.tsx` | No está en la web |
| RF-AUT-12 | Doble factor opcional del usuario y aviso de dispositivo nuevo | NO INICIADO | El 2FA existe solo para el personal: `Administration/Auth/MfaHandler.cs` | Nada para la cuenta de usuario |

### Menores

| ID | Resumen | Estado | Evidencia | Qué falta |
| --- | --- | --- | --- | --- |
| RF-MIN-01 | Autorización del representante antes de activar la cuenta | COMPLETO | `GuardianConsentEndpoints.cs`, `AuthorizeGuardianConsent/Handler.cs`, `apps/web/src/app/tutor/autorizar/page.tsx`, `apps/mobile/app/tutor/autorizar.tsx`, `GuardianConsentFlowTests.cs` | La validez jurídica de la firma no está dictaminada |
| RF-MIN-02 | Escucha del menor, en lenguaje claro, y guardar su opinión | PARCIAL | Casilla en `registro.tsx` y `AcknowledgeMinorNotice` | No hay una pantalla propia adaptada a la edad |
| RF-MIN-03 | Prueba: fecha, versión, documento, correo verificado y firma interna | PARCIAL | `AuthorizeGuardianConsent/Handler.cs` (`signer.Sign`), `GuardianConsent.cs` | Un abogado debe decir si esa firma basta (Ley 527) |
| RF-MIN-04 | Perfil privado obligatorio; no salen en el mapa general | SOLO BACKEND | `UserProfile.MakePublic` rechaza edad 17 en `UserProfilePresenceTests.cs` | No hay API ni pantalla para volver público un perfil, así que la regla no se ejerce en la app |
| RF-MIN-05 | Ubicación con amigos apagada; prenderla pide permiso del representante y recordatorio | PARCIAL | Checkbox en `tutor/autorizar.tsx`; `ShareLocationWithFriends` nace en falso | No hay interruptor usable ni recordatorio |
| RF-MIN-06 | Minimización, sin publicidad personalizada ni contenido adulto | PARCIAL | No hay red de anuncios | El registro exige teléfono; no hay exclusión de categorías adultas |
| RF-MIN-07 | El representante consulta, rectifica, revoca y pide supresión | PARCIAL | `revoke`, `review` y `apps/web/src/app/tutor/gestionar/page.tsx`; borrado de cuenta | Rectificar datos del menor no tiene flujo; el soporte es un enlace |
| RF-MIN-08 | A los 7 días sin respuesta se borran los datos del menor | COMPLETO | `ExpireGuardianConsents/Handler.cs` llama `ErasePendingRegistration`, `GuardianConsentExpiryService.cs` | — |
| RF-MIN-09 | El menor solo transmite a amigos, puede ir anónimo, y bajo 16 necesita permiso | SOLO BACKEND | `LiveStream.Start` y `LiveStreamStartTests.cs`; el permiso está en el formulario del tutor | No hay transmisión |
| RF-MIN-10 | Aviso al subir imagen de un menor, denuncia prioritaria y retiro rápido | NO INICIADO | No hay aviso en `report-form.tsx` | Todo |
| RF-MIN-11 | A los 18 acepta de nuevo términos y política | NO INICIADO | — | Todo |
| RF-MIN-12 | Módulo de verificación de edad desacoplado y activable por país | PARCIAL | `Country.ChangeMinimumAge` y la pantalla de configuración | No hay documento ni reconocimiento facial |

### Perfil y ubicación

| ID | Resumen | Estado | Evidencia | Qué falta |
| --- | --- | --- | --- | --- |
| RF-PER-01 | Avatar genérico, galería o foto propia con recorte | PARCIAL | `ProfileEndpoints.cs`, `Profiles/Avatars/Handler.cs`, `apps/mobile/app/editar-perfil.tsx`, `apps/admin` avatares | Web sin perfil; no hay recorte |
| RF-PER-02 | Editar nombre, usuario, biografía, ciudad y contraseña | PARCIAL | `UpdateMyProfile/Handler.cs`, `ChangePasswordEndpoint.cs`, pantallas móviles `editar-perfil` y `cambiar-contrasena` | Web sin estas pantallas |
| RF-PER-03 | Privado por defecto; pasar a público muestra el ícono en el mapa | SOLO BACKEND | `UserProfile.CreateDefault` deja `Private`; `MakePublic` | Sin API ni pantalla |
| RF-PER-04 | Un privado solo lo ven amigos y nunca el mapa general | SOLO BACKEND | `UserProfile.PublicPresence` | El mapa de reportes no dibuja personas |
| RF-PER-05 | Bloquear y denunciar usuarios | PARCIAL | `InteractionEndpoints.cs` bloques y denuncias `ContentTargetType.User`, `apps/mobile/app/bloqueados.tsx` | La web denuncia reportes, no personas |
| RF-PER-06 | Consentimiento explícito al hacerse público, con fecha y versión | SOLO BACKEND | `PublicProfileConsentedAt` en `MakePublic` | Nadie llama `MakePublic` desde la aplicación |
| RF-UBI-01 | Interruptor de ubicación con amigos, apagado, con consentimiento | SOLO BACKEND | `EnableFriendLocationSharing` | Sin endpoint ni pantalla |
| RF-UBI-02 | Solo amigos aceptados la ven | SOLO BACKEND | `FriendPresence` | El mapa no consulta amistades |
| RF-UBI-03 | Todos los amigos o una lista | SOLO BACKEND | `LocationSharingMode.SelectedFriends`, tabla `location_share_recipients` | Sin API |
| RF-UBI-04 | Duración: 1 h, 8 h o hasta apagar | SOLO BACKEND | `FriendLocationSharingEndsAt` | Sin UI |
| RF-UBI-05 | Indicador permanente mientras se comparte | NO INICIADO | — | Todo |
| RF-UBI-06 | Al bloquear o eliminar un amigo, deja de ver la ubicación al instante | SOLO BACKEND | Bloqueos en `Interaction/Blocks/Handler.cs` | No hay emisión de ubicación que cortar |
| RF-UBI-07 | Solo el móvil emite; la web muestra y no emite | NO INICIADO | — | No hay canal de ubicación |
| RF-UBI-08 | Sin señal, "última vez visto" | NO INICIADO | — | Todo |
| RF-UBI-09 | Modo invisible temporal | SOLO BACKEND | `ActivateGhostMode` | Sin API |
| RF-UBI-10 | Zonas de privacidad (casa, trabajo) | SOLO BACKEND | `PrivacyZone.cs`; solo se borra en `AccountDeletion/Handler.cs` | Sin alta ni consulta |
| RF-UBI-11 | Si publica anónimo, el avatar sale del mapa mientras dure | NO INICIADO | — | Todo |

### Mapa, menú y panel

| ID | Resumen | Estado | Evidencia | Qué falta |
| --- | --- | --- | --- | --- |
| RF-MAP-01 | Mapa centrado en el usuario, con permiso de GPS | PARCIAL | `apps/mobile/features/reports/map-screen.tsx`; web `features/map/map-screen.tsx` | La web no centra sola; el visitante no carga reportes (`MapReports` exige usuario) |
| RF-MAP-02 | Botón "¿Dónde estoy?" y seguimiento | PARCIAL | Botón de ubicación en el mapa móvil y en la web | No se comprobó seguimiento continuo |
| RF-MAP-03 | Reportes por zona, ícono de categoría y clustering | PARCIAL | `GET /reports/map`, `ReportMapReader.cs` agrupa si zoom &lt; 15, pines en móvil y web | La prueba PostGIS se omite sin `situ_test` |
| RF-MAP-04 | Tarjeta con categoría, hora, distancia, autor, "Sigue ahí", comentar, compartir y denunciar | PARCIAL | Tarjeta móvil y `apps/web/src/features/reports/report-panel.tsx` | Sin prueba en dispositivo ni E2E con sesión |
| RF-MAP-05 | Transmisiones con cámara roja y pulso | NO INICIADO | El mapa real no las pinta; `map-placeholder.tsx` es un adelanto | Todo |
| RF-MAP-06 | Avatares públicos y de amigos que comparten ubicación | NO INICIADO | `GetReportMap` no lee `UserLocation` | Todo en el mapa |
| RF-MAP-07 | Filtros por categoría, tiempo y tipo (amigos, en vivo) | PARCIAL | Chips de categoría, última hora y hoy en móvil y web | "Solo amigos" y "solo en vivo" no existen; la web manda `friendsOnly: false` |
| RF-MAP-08 | TTL por categoría; sale por vencimiento o por "Ya no está" | PARCIAL | `ExpireReportsHandler`, `ConfirmReport/Handler.cs`, `ReportMaintenanceService.cs` | No visto en un dispositivo |
| RF-MAP-09 | Modo día/noche y buscador de lugares | NO INICIADO | El formulario móvil sí geocodifica (`report-form.tsx` `searchAddress`) | El mapa no busca ni cambia de modo |
| RF-MAP-10 | Íconos en tiempo real sin recargar | PARCIAL | Hub `/hubs/map`, `use-map-realtime.ts`, `apps/web/src/features/map/use-map-live.ts`, `MapRealtimeTests.cs` | Sin prueba del socket con un ticket real |
| RF-MEN-01 | El ícono de usuario abre el menú | PARCIAL | `apps/mobile/app/(tabs)/perfil.tsx` y `cuenta.tsx` | La web no tiene ese menú |
| RF-MEN-02 | Perfil, reportes, amigos, notificaciones, privacidad, suscripción, legales, ayuda y salir | PARCIAL | `cuenta.tsx` abre legales, dispositivos, notificaciones y salir | Suscripción no está; amigos es un texto vacío; web sin menú |
| RF-PAN-01 | Barra "Quiero chismosear" o "Quiero reportar", texto remoto, y micrófono | PARCIAL | `panel.browse_copy` / `panel.report_copy`, `GetAppConfig`, barra en móvil y web | El micrófono no está |
| RF-PAN-02 | Cuadrícula de categorías con ícono, nombre y descripción | PARCIAL | Sheet móvil y web `map-screen.tsx` | En la web, elegir categoría abre "próximamente" |
| RF-PAN-03 | Dictado por voz para buscar categoría o redactar | DIFERIDO CONSCIENTE | Comentario en `map-screen.tsx`: "dictado por voz al final del MVP" | Todo |
| RF-PAN-04 | Tres estados y botón "Transmitir en vivo" solo en el móvil | PARCIAL | Bottom sheet del mapa móvil | El botón de transmitir se dejó fuera a propósito (mismo comentario); la web no tiene tres estados |

### Categorías, reportes y anonimato

| ID | Resumen | Estado | Evidencia | Qué falta |
| --- | --- | --- | --- | --- |
| RF-CAT-01 | CRUD de categorías en el admin, sin publicar la app | COMPLETO | `AdminEndpoints.cs` categorías, `apps/admin/.../categorias/category-list.tsx`, `AdminPanelFlowTests.cs` | — |
| RF-CAT-02 | Nombre, descripción, íconos, color, orden, activo, TTL y peso | PARCIAL | `ReportCategory.cs` (`TtlMinutes`, `ImportanceWeight`), iconos en el admin | Confirmar color en la ficha de admin; el peso no alimenta un algoritmo en marcha |
| RF-CAT-03 | Subcategorías, visibilidad por lugar y si exige foto o video | PARCIAL | Overrides y traducciones: `AdminUpsertCategoryOverride` | Sin subcategorías ni flag de medio obligatorio |
| RF-CAT-04 | La app consume el catálogo con caché y versión | PARCIAL | `GET /report-categories`, setting `reports.catalog_version`, catálogo en el móvil | La web lo pide en cada visita; no hay prueba de invalidación |
| RF-REP-01 | Formulario con texto, varias imágenes y videos, límites y compresión | PARCIAL | `UploadReportMedia`, `CreateReport`, `report-form.tsx` | Web sin formulario; `SkiaReportMediaProcessor.cs` deja el video sin transcodificar |
| RF-REP-02 | Ubicación por GPS, pin movible o dirección | PARCIAL | `report-form.tsx` | Solo en el móvil |
| RF-REP-03 | Visibilidad público o solo amigos; los menores siguen la sección 3 | PARCIAL | `CreateReport/Handler.cs` `CanPublishReport` | Un menor en público queda anónimo; no se puede crear desde la web |
| RF-REP-04 | Subida en segundo plano, reintentos y progreso | PARCIAL | Cola `uploads.enqueue` en `report-form.tsx` | Es una cola de la pantalla, no una tarea del sistema |
| RF-REP-05 | Editar y eliminar reportes propios | PARCIAL | `PATCH` y `DELETE /reports/{id}`, `my-reports.tsx` | Web no |
| RF-REP-06 | Reaccionar, comentar, compartir, confirmar y denunciar | PARCIAL | `InteractionEndpoints.cs`, detalle móvil, `report-panel.tsx` | La web no muestra reacciones |
| RF-ANO-01 | Interruptor "Publicar como anónimo" en reporte, comentario y transmisión | PARCIAL | `report-form.tsx`, `comment-composer.tsx`, comentarios web | No existe en una transmisión |
| RF-ANO-02 | Ícono genérico, nombre configurable y "Miembro desde" mes y año | PARCIAL | `AnonymousIdentity.cs`, setting `anonymity.display_name`, `ReportSupport.cs` | — |
| RF-ANO-03 | Sin enlace al perfil, sin agregar amigo y sin pin del autor | PARCIAL | La tarjeta usa el autor presentado; los eventos del hub no llevan autor (`MapRealtimeTests.cs`) | No hay pin de persona que ocultar |
| RF-ANO-04 | "Situero anónimo 1", "2" dentro de la misma publicación | PARCIAL | `AnonymousParticipant.Assign` en `InteractionSupport.cs` | No se revisó en una conversación real |
| RF-ANO-05 | En una transmisión anónima, aviso de cara y voz, y desenfoque | DIFERIDO CONSCIENTE | El propio requisito dice que puede ir a una fase posterior; `FacesBlurred` y `VoiceModified` están en el dominio | Sin video |
| RF-ANO-06 | La identidad real se conserva para moderación y autoridades | COMPLETO | `AuthorId` en `Report.cs`; `moderation/anonymous-identity/reveal`; pruebas de que el mapa y las notificaciones no filtran al autor | Los términos todavía no lo dicen en un texto publicado |
| RF-ANO-07 | Lo anónimo no avisa "tu amigo publicó"; sí avisa por ciudad y por 1 km | PARCIAL | `Notifications/Dispatch/Handler.cs` omite amigos si `IsAnonymous`; `NotificationFlowTests.cs` | La parte de transmisiones no existe |
| RF-ANO-08 | "Mis reportes" muestra la insignia Anónimo y permite borrar | PARCIAL | `my-reports.tsx` y `report-detail.tsx` | Web sin "Mis reportes" |
| RF-ANO-09 | La moderación puede quitar el anonimato a quien abuse | NO INICIADO | No hay sanción de ese tipo en `ModerationAction` | Todo |

### Transmisión, amigos y notificaciones

| ID | Resumen | Estado | Evidencia | Qué falta |
| --- | --- | --- | --- | --- |
| RF-LIV-01 | Iniciar transmisión con título, categoría y lugar | SOLO BACKEND | `LiveStream.Start` | Sin endpoint, sin LiveKit/Agora/IVS y sin pantalla |
| RF-LIV-02 | Solo se transmite desde el móvil; la web dice que abras la app; el límite es configurable | PARCIAL | La web no tiene botón de ir en vivo (`docs/web.md`) | No hay parámetro ni proveedor que encender |
| RF-LIV-03 | Credenciales solo a la app móvil verificada | SOLO BACKEND | No hay emisor de credenciales | La restricción hoy es "no existe la función" |
| RF-LIV-04 | Ícono de cámara mientras dura y desaparece al terminar | SOLO BACKEND | `LiveStream.IsLive` | El mapa no lo dibuja |
| RF-LIV-05 | Contador, comentarios y reacciones, también anónimos | SOLO BACKEND | `LiveViewer.cs` | Sin reproducción |
| RF-LIV-06 | Aviso a amigos y cercanos; el anónimo no avisa a amigos | SOLO BACKEND | El dispatch de reportes ya omite amigos anónimos | No hay evento de transmisión |
| RF-LIV-07 | Al terminar, guardar como reporte o descartar | SOLO BACKEND | `ReportId` en `LiveStream` | Sin flujo |
| RF-LIV-08 | Lo guardado se borra a los 2 meses, plazo configurable | SOLO BACKEND | `DefaultRecordingRetention` y setting `live.recording_retention_days` | El job de purga no recorre transmisiones |
| RF-LIV-09 | Un moderador corta y un usuario denuncia | SOLO BACKEND | Estado `Cut` y permiso `live.cut` | Sin acción usable |
| RF-LIV-10 | Sin publicidad durante la transmisión | SOLO BACKEND | No hay anuncios en ninguna superficie | No hay transmisión donde aplicarlo |
| RF-LIV-11 | Los menores solo transmiten a amigos | SOLO BACKEND | `MinorProtectionPolicy.CanStreamTo`, prueba de dominio | Sin transmisión |
| RF-LIV-12 | Copia interna cifrada de corta vida | SOLO BACKEND | `InternalCopyExpiresAt`, setting `live.internal_copy_retention_days` | Sin cifrado ni archivo |
| RF-AMI-01 | Buscar por nombre de usuario | SOLO BACKEND | `Friendship.cs`; `apps/mobile/app/(tabs)/amigos.tsx` es un texto | Sin endpoint de búsqueda de personas |
| RF-AMI-02 | Invitar por correo | SOLO BACKEND | `FriendInvitation.cs` | Sin envío |
| RF-AMI-03 | Enviar, aceptar, rechazar, cancelar, eliminar y bloquear | SOLO BACKEND | Métodos en `Friendship.cs`; el bloqueo de usuarios sí está en interacción | La amistad no tiene API |
| RF-AMI-04 | Lista con perfil, reportes y ubicación si la comparten | SOLO BACKEND | La pestaña Amigos no lista a nadie | Todo lo visible |
| RF-NOT-01 | Activar o apagar cada tipo | PARCIAL | `NotificationEndpoints.cs`, `notification-settings.tsx` | Web sin ajustes; "en vivo" sale deshabilitado |
| RF-NOT-02 | Ciudad de interés y ciudades adicionales | PARCIAL | `GET/PUT /me/interest-cities`; la ciudad del registro alimenta el dispatch | No confirmé el alta de ciudades extra en la pantalla |
| RF-NOT-03 | Radio (1 km) y categorías de interés | PARCIAL | `notification-settings.tsx` (`RADIUS_STEPS`), `category-interests` | — |
| RF-NOT-04 | Horario de silencio | PARCIAL | `QuietHoursStart` en `Settings/Handler.cs`; el dispatch marca `quiet_hours` | — |
| RF-NOT-05 | Push, notificación web, centro interno y correo opcional | PARCIAL | Token push, `NotificationDispatchService.cs`, tab `notificaciones` | Sin push web ni correo de alertas |
| RF-NOT-06 | Deduplicar, agrupar y limitar frecuencia | PARCIAL | `notifications.max_pushes_per_hour`, `NotificationFlowTests.cs` | — |
| RF-NOT-07 | La proximidad de menores respeta la sección 3 | PARCIAL | El payload de proximidad no lleva coordenadas (`NotificationFlowTests.cs`) | No hay una regla adicional de menores sobre el aviso |

### Publicidad, suscripciones y noticias

| ID | Resumen | Estado | Evidencia | Qué falta |
| --- | --- | --- | --- | --- |
| RF-MON-01 | Anuncios AdMob o equivalentes en móvil y web | NO INICIADO | — | Todo |
| RF-MON-02 | Pines patrocinados | SOLO BACKEND | `SponsoredPin.cs` | Sin API ni mapa |
| RF-MON-03 | Consentimiento de anuncios personalizados; nada en vivos, alertas críticas ni menores | NO INICIADO | — | Todo |
| RF-SUS-01 | Compra, renovación, cancelación, cambio y recuperar compras | SOLO BACKEND | `Subscription.cs` | Sin flujo de compra |
| RF-SUS-02 | Compras de Apple y Google, y pasarela web | NO INICIADO | No hay StoreKit, Play Billing, Wompi, PayU ni Stripe | Todo. El modelo `Payment` no se usa |
| RF-SUS-03 | Planes y precios por país, editables en el admin | PARCIAL | `ListAdminPlans` es solo lectura; `PlanPrice.cs`; el límite de dispositivos lee el plan | No se editan precios ni beneficios |
| RF-SUS-04 | Factura electrónica e IVA | SOLO BACKEND | No hay emisor de factura | Todo lo legal y operativo |
| RF-ALG-01 | Puntaje de importancia | SOLO BACKEND | `ImportanceScoringConfig.cs`, `ImportanceScoringConfigTests.cs` | Nadie calcula puntajes de reportes |
| RF-ALG-02 | Umbral para pasar a "Noticia importante" | SOLO BACKEND | `PromotionThreshold`, `NewsItem.cs` | Sin job |
| RF-ALG-03 | Primicia para Pro y retraso para el resto | SOLO BACKEND | — | Sin audiencia de planes |
| RF-ALG-04 | Umbrales editables en el admin | SOLO BACKEND | La config no está en la pantalla de settings | Sin UI |
| RF-ALG-05 | Lo anónimo usa la reputación real | SOLO BACKEND | `UserReputation.cs` | Sin cómputo |
| RF-ALG-06 | Reputación y detección de falsos o duplicados | SOLO BACKEND | La entidad existe | Sin detector |

### Dashboard, admin y moderación

| ID | Resumen | Estado | Evidencia | Qué falta |
| --- | --- | --- | --- | --- |
| RF-AVA-01 | Mapa en tiempo real para la empresa, solo ver | SOLO BACKEND | `Organization.cs` | No hay app de dashboard |
| RF-AVA-02 | Filtros por zona, categoría, tiempo e importancia | SOLO BACKEND | — | — |
| RF-AVA-03 | Alertas por zona, categoría o palabras | SOLO BACKEND | `AlertRule.cs` | Sin evaluación |
| RF-AVA-04 | Vista de noticias importantes | SOLO BACKEND | `NewsItem.cs` | — |
| RF-AVA-05 | Estadísticas y exportar CSV o PDF | SOLO BACKEND | — | — |
| RF-AVA-06 | Cuentas multiusuario y roles de organización | SOLO BACKEND | `OrganizationMember.cs` | — |
| RF-AVA-07 | API y webhooks para terceros | SOLO BACKEND | Permiso `organizations.verify` | Sin API pública de empresas |
| RF-AVA-08 | No revela anónimos ni ubicación de personas | SOLO BACKEND | — | No hay superficie que pueda filtrar |
| RF-ADM-01 | Categorías, avatares, geografía, planes, textos y enlaces legales | PARCIAL | Categorías, avatares, settings y edad mínima | Geografía y planes no se editan; los legales son URLs en settings |
| RF-ADM-02 | Radio, intentos, bloqueo, dispositivos, edad, TTL, umbrales y retención | PARCIAL | `configuracion/page.tsx` edita `SystemSetting`; retención en `retencion/page.tsx` | Los pesos del algoritmo no están en esa pantalla |
| RF-ADM-03 | RBAC y bitácora | COMPLETO | `Staff/Handler.cs`, `Audit/Handler.cs`, `bitacora/page.tsx`, `personal/page.tsx`, pruebas del panel | El 2FA del personal es adicional al documento |
| RF-ADM-04 | Métricas de usuarios, reportes, ingresos y conversión | PARCIAL | `GetAdminDashboard`, `resumen/page.tsx` | Ingresos y conversión no tienen cobros |
| RF-ADM-05 | Preclasificación de desnudez, violencia u ofensas | NO INICIADO | — | Todo |
| RF-MOD-01 | Cola de denuncias y sanciones | PARCIAL | `denuncias/page.tsx`, `Complaints/Handler.cs`, warn/suspend/ban en `AdminEndpoints.cs` | No hay transmisiones que moderar |
| RF-MOD-02 | Ver la identidad anónima con permiso y bitácora | PARCIAL | `anonymous-identity/reveal`, `AnonymousIdentityAccessLog`, `GET /admin/anonymous-identity-access-logs` | Hace falta confirmar el botón en la ficha de denuncia |
| RF-MOD-03 | Retiro por aviso de un tercero | PARCIAL | `POST /takedown-requests`, `retiros/page.tsx`, `AdminPanelFlowTests.cs` | No hay formulario en la web pública ni en la app |

### Alcance global, web y retención

| ID | Resumen | Estado | Evidencia | Qué falta |
| --- | --- | --- | --- | --- |
| RF-GLO-01 | Catálogo país, región y ciudad, con el nombre del nivel intermedio | PARCIAL | `Country.cs`, `ListCountriesEndpoint.cs`, semilla en `CatalogSeed.cs` | El admin no crea ni edita lugares |
| RF-GLO-02 | País habilitado o no; al inicio solo Colombia | PARCIAL | `Country.IsEnabled` en la semilla | No hay interruptor en el panel |
| RF-GLO-03 | Textos, fechas, zonas y unidades por idioma; empieza en es-CO | PARCIAL | `ReportCategoryTranslation`, `DistanceUnit` en el país | Las pantallas están fijas en español |
| RF-GLO-04 | Precio, moneda, pasarela e impuesto por país | SOLO BACKEND | `PlanPrice.cs` | Sin cobro |
| RF-GLO-05 | Términos y política por país e idioma | PARCIAL | `LegalDocument`, `legal.terms_url` y `legal.privacy_url`, landing `/terminos` y `/privacidad` | No se auditó el texto jurídico |
| RF-GLO-06 | Categorías, TTL y parámetros sobrescribibles por lugar | PARCIAL | Overrides de categoría en el admin | Los parámetros globales no se sobrescriben por ciudad |
| RF-GLO-07 | Revisión legal antes de habilitar otro país | NO INICIADO | — | Es un proceso, no una pantalla |
| RF-WEB-01 | Paridad con la app, sin dejar fuera perfil, amigos, ajustes ni suscripción | PARCIAL | `apps/web`: registro, ingreso, mapa, ver/confirmar/comentar/denunciar | Falta crear reportes, perfil, amigos, ajustes y suscripción (`docs/web.md`) |
| RF-WEB-02 | La web solo ve transmisiones, con comentarios y reacciones | PARCIAL | No hay botón de transmitir | Tampoco se pueden ver, porque no hay vivos |
| RF-WEB-03 | La sesión web cuenta en el límite de dispositivos | PARCIAL | Login web envía `platform: Web` y `situ_web_did` | Sin pantalla para cerrar ese dispositivo |
| RF-WEB-04 | Push web y centro de notificaciones | NO INICIADO | — | Todo |
| RF-WEB-05 | Anuncios del plan gratis y cobro con pasarela | NO INICIADO | — | Todo |
| RF-WEB-06 | Responsive, escritorio con panel lateral y vista móvil | PARCIAL | Mapa web de 360 px hacia arriba, `docs/web.md` | No hay layout de escritorio con panel lateral; el smoke no cubre ese layout |
| RF-RET-01 | Plazos configurables, también por país o plan | PARCIAL | `Retention/Handler.cs`, `retencion/page.tsx`, settings `report.retention_days` | `RetentionPolicy` no distingue país ni plan |
| RF-RET-02 | Vencido en el mapa sigue en "Mis reportes" hasta la purga | PARCIAL | `ExpireReportsHandler` cambia el estado; `GET /reports/mine` | No se comprobó que el vencido siga visible para el autor |
| RF-RET-03 | La purga borra también imagen y video | PARCIAL | `RetainReportsHandler` llama `storage.DeleteAsync` | No borra grabaciones de vivos |
| RF-RET-04 | Denuncia, orden o investigación frenan la purga | PARCIAL | `LegalHold` se excluye en `RetainReportsHandler`; alta en el admin | — |
| RF-RET-05 | Aviso al autor y enlace para descargar antes de borrar | NO INICIADO | `DataExportRequest` solo se elimina con la cuenta | Sin aviso ni descarga |
| RF-RET-06 | Al borrar la cuenta se borran o anonimizan los datos | PARCIAL | `AccountDeletion/Handler.cs`, gracia `account.deletion_grace_days`, `CompleteAccountDeletionsService.cs`, `InteractionFlowTests.cs` | Solo desde el móvil |

## 3. Hecho pero sin verificar

- Pruebas que se omiten si no existe `ConnectionStrings:Test` apuntando a `situ_test`: `ReportMapPostGisTests.cs`, `NotificationProximityPostGisTests.cs`, `AdminPanelPostgresTests.cs`. El clustering y la proximidad de 1 km no quedaron corridos contra PostGIS en esta lectura.
- No hay prueba de dispositivo de cámara, subida de foto, push, GPS en segundo plano ni del mapa nativo. El comentario de teclado y la subida por `expo-file-system` están en código, no en un teléfono revisado aquí.
- Playwright de la web solo cubre el mapa sin clave de Google y el formulario de ingreso (`apps/web/e2e/map.spec.ts`). No hay E2E con usuario, reporte abierto ni ticket de SignalR.
- El ticket de tiempo real tiene pruebas de dominio y de que la query no se lee fuera de `/hubs/map`. No se abrió un WebSocket real.
- El correo sale por `SmtpEmailSender`. No se confirmó un buzón de entrega.
- `AddHealthChecks` está en `Program.cs`. No hay tablero ni alerta ligada a ese chequeo.
- Esta auditoría no reejecutó `dotnet test` ni `pnpm test`.

## 4. Brechas críticas para lanzar el MVP

- **Web incompleta.** La fase 1 pide la web sin transmisión. Hoy no se crea un reporte, no hay perfil, no hay dispositivos y no se elimina la cuenta (`RF-WEB-01`, `RF-AUT-10`, `RF-AUT-11`).
- **Visitante.** La sección 2 dice que ve el mapa de forma limitada. `MapReports` responde 401 sin usuario. Sin sesión el mapa web no trae reportes.
- **Menores.** El flujo del representante está construido, pero la escucha del menor es una casilla, el registro pide teléfono y la firma interna no tiene dictamen (Ley 527 de 1999, sección 13 del documento).
- **Habeas Data.** Hay registro de aceptación, revocación, supresión de la cuenta y borrado a los 7 días si el representante no responde. Faltan rectificación clara, descarga previa (`RF-RET-05`) y la política publicada y revisada.
- **Terceros en un chisme.** La API pública de retiro y la cola del panel existen. No hay formulario visible en la web ni en la app (`RF-MOD-03`).
- **Tiendas.** Bloquear y denunciar está en el móvil, no en la web. Eliminar la cuenta está en el móvil, no en la web. No hay ficha de privacidad ni de contenido generado lista para App Store o Play.
- **Moderación.** Hay cola, sanciones y revelado de anónimos con bitácora. No hay preclasificación automática (`RF-ADM-05`) ni revisión humana demostrada sobre un caso real.
- **Retención.** El job borra reportes vencidos y respeta bloqueos legales. No avisa al autor ni ofrece descarga.

## 5. Desviaciones

- Un reporte público de un menor se publica como anónimo (`CreateReport/Handler.cs` y el texto del formulario). El documento exige perfil privado y reglas de transmisión; no dice que el reporte público cambie solo a anónimo.
- El anonimato de reportes nace encendido (`PreferAnonymousReports = true`). El documento pide el interruptor, no ese valor inicial.
- El registro exige teléfono. El RF-AUT-01 no lo incluye.
- El doble factor es obligatorio para el personal del panel y no existe para el usuario. El documento solo sugiere 2FA de usuario.
- Hay pestañas móviles Mensajes y Chisme vacías (`mensajes.tsx`, `chisme.tsx`). El documento no las pide.
- El mapa agrupa en el servidor cuando el zoom es menor a 15, no en el cliente.
- Existen tablas de amigos, vivos, planes, noticias, organizaciones y pines patrocinados sin producto que las use.
- La web usa un ticket de un solo uso para el hub y cookies `httpOnly`. El documento no lo pide; es una medida de más, no un recorte.
- El dictado y el botón de transmitir se omitieron a propósito en el mapa móvil.

## 6. Deuda técnica y riesgos

- `SkiaReportMediaProcessor.cs` deja un TODO: el video no se transcodifica ni genera poster.
- Tres familias de pruebas se saltan sin la base `situ_test`.
- `appsettings.Development.json` guarda la clave de firma de desarrollo dentro del repositorio. No debe usarse fuera de local. Producción tiene que salir de un almacén, no de ese archivo.
- Hay rate limiting en `Program.cs` (login, denuncias, takedown, panel). No hay captcha en el registro ni en el ingreso.
- Hay chequeo de salud y no hay OpenTelemetry, Sentry ni tablero de errores en el código de la API.
- No hay respaldo ni restauración descritos en el código.
- El correo depende de SMTP. Si esa cuenta falla, no salen confirmaciones ni autorizaciones de menores.
- El mapa web depende de una clave de Google restringida por referrer. Sin ella la pantalla avisa y no dibuja el mapa.
- Los medios viven en almacenamiento local de la API. No hay almacén de objetos ni CDN.
- Un solo proceso hace purga, vencimiento de tutores, avisos y despacho del mapa. No hay separación de workers.

## 7. Pendientes que no son de código

- Dominio, DNS, certificados y hosting de API, web, landing y panel.
- Cuentas de Apple y Google, ficha de privacidad, eliminación de cuenta y política de contenido generado.
- Clave de Google Maps para web, distinta de Android e iOS, con restricción por referrer.
- Captcha de un proveedor real.
- Correo transaccional con dominio propio (SPF, DKIM, DMARC).
- Abogado de Habeas Data: firma interna y Ley 527 de 1999, flujo de menores, transmisiones anónimas de menores, retención y proyectos de ley de edad (sección 13).
- Textos de la política de tratamiento y de los términos, por país, y marca ante la SIC si aplica.
- Pasarela, facturación electrónica e IVA, cuando llegue la fase 3.
- Proveedor de video, cuando llegue la fase 2.

## 8. Hoja de ruta

### Necesario para lanzar el MVP

1. **Paridad web mínima.** Alcance: crear reporte con medios, perfil básico, mis dispositivos, eliminar cuenta y menú. Cierra RF-WEB-01, RF-WEB-03, RF-WEB-06, RF-AUT-10, RF-AUT-11, RF-REP-01, RF-REP-05, RF-PER-01, RF-PER-02. Depende del almacenamiento de medios. Esfuerzo: L.
2. **Mapa para quien no tiene cuenta y formulario de retiro.** Alcance: vista limitada sin sesión y formulario público de takedown, más denunciar y bloquear desde la web. Cierra el rol Visitante, RF-MOD-03 y la parte web de RF-PER-05. Esfuerzo: M.
3. **Menores y datos, cierre legal-técnico.** Alcance: pantalla de escucha, dejar de pedir datos de más, y empaquetar la prueba de autorización para el abogado. Cierra RF-MIN-02, RF-MIN-03 y RF-MIN-06. Depende del dictamen. Esfuerzo: M.
4. **Verificación real.** Alcance: correr PostGIS con `situ_test`, un teléfono iOS y uno Android (foto, mapa, push, borrado) y un E2E web con usuario. No cierra un ID nuevo; sostiene los que ya están en parcial. Esfuerzo: M.
5. **Operación.** Alcance: secretos fuera del repo, correo de dominio, captcha, respaldos, salud con alerta. Cierra requisitos no funcionales de la sección 12. Esfuerzo: L.
6. **Ficha de tiendas.** Alcance: enlaces de privacidad y términos definitivos, eliminación de cuenta visible, bloqueo y denuncia ya probados. Depende de los textos legales y de las cuentas de tienda. Esfuerzo: S.

### Después del MVP

7. **Amigos.** Alcance: buscar, invitar, aceptar y listar. Cierra RF-AMI-01 a 04. Esfuerzo: L.
8. **Perfil público y ubicación.** Alcance: consentimiento, interruptores, emisión solo desde el móvil y avatares en el mapa. Cierra RF-PER-03, RF-PER-04, RF-PER-06, RF-UBI-01 a 08 y RF-MAP-06. Esfuerzo: L.
9. **Transmisión.** Alcance: proveedor de video, ícono en el mapa, ver desde la web, retención de 2 meses. Cierra RF-LIV-01 a 12, RF-MAP-05, RF-MIN-09 y RF-WEB-02. Esfuerzo: L.
10. **Planes y noticias.** Alcance: cobro móvil y web, factura, puntaje y primicia. Cierra RF-SUS-01 a 04, RF-MON-01, RF-MON-03 y RF-ALG-01 a 06. Esfuerzo: L.

El dashboard de empresas (RF-AVA-01 a 08) queda detrás de esos diez bloques.

## 9. Preguntas abiertas

1. ¿Un menor puede publicar un reporte público si queda anónimo, o el reporte público debe rechazarse?
2. ¿El teléfono es obligatorio en el registro?
3. ¿El anonimato debe venir encendido para todas las cuentas?
4. ¿El visitante ve pines reales sin cuenta, o el mapa vacío hasta que ingresa? Hoy la API exige sesión.
5. ¿La web tiene que crear reportes antes del primer lanzamiento, o el primer corte es solo móvil?
6. ¿La firma interna del correo de constancia alcanza como prueba (Ley 527 de 1999) o hace falta otro medio?
7. ¿Seguimos en 14 años aunque avancen los proyectos de ley de 14 y de 16?
8. ¿Google Maps queda como proveedor definitivo de la web y del móvil?
9. ¿Las pestañas Mensajes y Chisme se eliminan o tienen un alcance que el documento no escribió?
10. ¿La descarga del contenido antes de purgarlo (RF-RET-05, sugerido) entra al MVP?

## Conteo

IDs en el documento: AUT 12, MIN 12, PER 6, UBI 11, MAP 10, MEN 2, PAN 4, CAT 4, REP 6, ANO 9, LIV 12, AMI 4, NOT 7, MON 3, SUS 4, ALG 6, AVA 8, ADM 5, MOD 3, GLO 7, WEB 6, RET 6. Suma **147**.

Estados de esta auditoría: 10 + 70 + 45 + 20 + 2 = **147**.
