# Eliminación de cuenta

Borrador para revisión de un abogado. No es un concepto jurídico. Cubre lo que hace hoy el job `CompleteAccountDeletionsJob` cuando vence la gracia de `account.deletion_grace_days` (14 días) y no hay una retención legal activa.

La solicitud es `POST /api/v1/me/deletion-request`. Durante la gracia la persona puede entrar y cancelar con `POST /api/v1/me/deletion-request/cancel`. Si entra, la sesión sirve para ver `/me` y cancelar; no puede publicar, comentar ni reaccionar (`canPublish` es false y el motivo es `deletion_requested`).

Si hay un `LegalHold` activo sobre la cuenta, uno de sus reportes o uno de sus comentarios, no se borra nada. La solicitud queda en `BlockedByLegalHold` y queda un `AuditLog` (`account_deletion.blocked_by_legal_hold`) para que el personal lo vea.

La fila de `User` no se elimina. Se anonimiza para que las referencias de auditoría sigan teniendo un id. Lo mismo pasa con `UserProfile`.

## Qué se borra o se anonimiza

| Dato | Qué pasa |
| --- | --- |
| Correo, usuario, nombres, teléfono, contraseña, fecha de nacimiento, segundo factor | Se reemplazan. El correo queda en `@removed.invalid`, los nombres en "Eliminado", la contraseña en un valor inutilizable y la fecha en 2000-01-01 (la columna no admite vacío). |
| Perfil público | El nombre pasa a "Cuenta eliminada". Se quitan biografía, avatar propio, perfil público, ubicación en tiempo real y ubicación compartida con amigos. La fila queda. |
| Reportes, confirmaciones y archivos de medios | Se eliminan los reportes de la persona, sus confirmaciones y los archivos del almacenamiento (original y miniatura). |
| Comentarios y reacciones | Se eliminan los suyos y los que estaban en sus reportes. En reportes de otras personas se descuentan el conteo de comentarios y el de reacciones. |
| Número anónimo (`AnonymousParticipant`) | Se elimina. No queda el índice que usaba dentro de un reporte. |
| Amistades, invitaciones, bloqueos | Se eliminan. |
| Ubicación | Se eliminan ubicaciones, zonas de privacidad y destinatarios de ubicación compartida. |
| Dispositivos, refresh tokens, tokens de seguridad, intentos de inicio de sesión | Se eliminan. El token push se borra ya al pedir la eliminación, junto con el cierre de sesiones. |
| Ubicación de alertas (`device_alert_locations`) | Se borra al pedir la eliminación y otra vez al completar el borrado. No se devuelve por la API. |
| Notificaciones, entregas, preferencias, ciudades e intereses | Se eliminan. |
| Solicitudes de exportación de datos | Se eliminan. |
| Asignación de personal (`StaffAssignment`) | Se elimina, para que una cuenta borrada no siga pasando la política de staff. |

## Qué se conserva

| Dato | Por qué se deja |
| --- | --- |
| Fila de `User` anonimizada y fila de `UserProfile` vaciada | Hace falta un id estable para auditoría y moderación. No quedan correo, teléfono ni nombre real. |
| `GuardianConsent` y los registros ligados al flujo del representante | Constancia de la autorización cuando el titular era menor. Conserva el correo del representante y la versión del aviso. Un abogado debe decir si eso puede reducirse más. |
| `ConsentRecord` | Constancia de aceptaciones. |
| `AuditLog`, `ModerationAction`, `AnonymousIdentityAccessLog` | Obligación de dejar rastro de accesos a identidad anónima y de decisiones de moderación. Pueden mencionar el id de la cuenta, no el correo nuevo. |
| `Complaint` y `TakedownRequest` | Expediente de denuncias. No se reescriben. |
| `LegalHold` | La retención que impidió o condicionó el borrado. |
| `AccountDeletionRequest` | Queda en `Completed` o `BlockedByLegalHold`, con la fecha. |
| `Payment` y `Subscription` | Referencia del proveedor. No hay número de tarjeta en estas tablas. |
| Pines patrocinados | Pueden estar atados a un pago. No se tocan. |

## Pendiente

El representante de un menor también puede pedir la supresión. Ese camino no está implementado: hoy solo la persona titular, con su contraseña, pide la eliminación.
