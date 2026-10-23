# Feedback y capa HTTP

Las pantallas no llaman a `fetch`, `axios`, `alert()` ni `window.confirm`. El cliente vive en `packages/shared/src/core/http` y el feedback en el `FeedbackProvider` de cada app.

| Situación | Qué usar |
| --- | --- |
| Error de validación de un campo | Inline, bajo el input. En Login y Registro, píldora blanca con texto `#DE0506`. |
| Lectura (`useQuery`) falla | Estado de error en la pantalla, con ilustración y "Reintentar". No toast. |
| Escritura correcta | `notify.success`. |
| Escritura falla por red o servidor | `notify.error` con acción "Reintentar". |
| Acción destructiva | `confirm({ type: 'danger' })`. El botón usa el verbo ("Eliminar") y "Cancelar". |
| Permiso, sesión o bloqueo | `alertModal`. El botón principal es "Entendido" en `#FF6400`. |
| Esperando respuesta | Loader en el botón, skeleton en listas o la barra de 2px en el mapa. Si la respuesta llega en menos de 300ms, no se muestra el loader. |

`MutationCache.onError` muestra el toast de error salvo `meta.silentError`, `VALIDATION`, `CANCELLED` o `UNAUTHORIZED`.

Los textos de producto están en `packages/shared/src/core/feedback/messages.ts` y `errorMessages.ts`. Un código de API conocido usa esa copia. Nunca se muestra el `title`, el `traceId` ni un stack.

La sesión web sigue en cookies `httpOnly`. En móvil el token sale de SecureStore. Un 401 con sesión dispara un solo refresh. Si el refresh falla, el modal "Tu sesión expiró" aparece una sola vez.

Cancelar una petición (`CANCELLED`) no muestra feedback. Los GET se reintentan hasta 2 veces ante red, timeout o 5xx. POST, PUT y DELETE no se reintentan.

En desarrollo, `reportError` escribe el `cause` en consola. En producción queda el mismo gancho, sin tokens ni datos personales, listo para conectar un servicio de errores.
