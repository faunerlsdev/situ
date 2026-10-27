# Contenido generado por usuarios

Apple 1.2 y la política de contenido generado de Play piden filtro, denuncia, bloqueo, contacto y borrado de la cuenta. Esto es lo que hay en el código.

| Requisito | Dónde se ve | Archivo |
| --- | --- | --- |
| Aceptar los términos en el registro, con enlace | En Registro, cada documento vigente es un enlace (Términos de uso y Política de privacidad) y la casilla dice «Acepto los términos de uso y la política de privacidad.» Sin marcarla no se crea la cuenta. La URL sale de los documentos legales del país. | `apps/mobile/app/(auth)/registro.tsx` |
| Denunciar un reporte | Botón Denunciar en la ficha y en la tarjeta del mapa. Envía `targetType: Report`. | `apps/mobile/features/reports/report-detail.tsx`, `apps/mobile/features/reports/map-screen.tsx` |
| Denunciar un comentario | Botón Denunciar en cada comentario que no es tuyo. Envía `targetType: Comment`. | `apps/mobile/features/interaction/comment-section.tsx` |
| Denunciar a una persona | Botón Denunciar usuario cuando el autor tiene nombre y `userId`. Envía `targetType: User`. Si el autor es anónimo, la denuncia es del reporte o del comentario: la API no muestra quién es. | Los tres archivos de arriba |
| Bloquear | Botón Bloquear en el reporte y en el comentario. La lista para desbloquear está en Privacidad. | `apps/mobile/features/interaction/block-content.ts`, `apps/mobile/app/bloqueados.tsx` |
| Filtro de términos | `profile.blocked_terms` rechaza el texto en el perfil, en la descripción de un reporte (al crear y al editar) y en un comentario (al crear y al editar). La respuesta es `content.not_allowed` o, en el perfil, `profile.content_not_allowed`. El valor de fábrica es `odio,nazi,porn,xxx,puta,mierda`. Se cambia en el panel, sin publicar otra app. | `apps/backend/src/Situ.Application/Features/Profiles/ProfileRules.cs`, `CreateReport/Handler.cs`, `UpdateReport/Handler.cs`, `Interaction/Comments/Handler.cs` |
| Contacto y plazo | «Contactar soporte» abre `legal.support_url`. El plazo sale de `moderation.response_commitment`. El texto de fábrica es: «Revisamos cada denuncia. Si el contenido afecta a un menor, lo ocultamos de inmediato. El resto lo atendemos en menos de 24 horas.» Se ve en Mi cuenta y en la hoja de denuncia. | `apps/mobile/app/cuenta.tsx`, `apps/mobile/features/interaction/complaint-sheet.tsx` |
| Eliminar la cuenta dentro de la app | Mi cuenta pide la contraseña y programa el borrado. | `apps/mobile/app/cuenta.tsx` |
| URL de eliminación de datos | `https://app.${DOMAIN_ROOT}/cuenta/eliminar`. La página la publica la web. La app muestra esa URL cuando el build tiene `EXPO_PUBLIC_DOMAIN_ROOT`. Ese es el valor del campo de Play. | `apps/mobile/lib/store-links.ts` |

Antes de enviar a las tiendas, en el panel (o en `system_settings`) deja estas claves apuntando al dominio público, no a `somossitu.com`:

- `legal.terms_url` → `https://${DOMAIN_ROOT}/terminos`
- `legal.privacy_url` → `https://${DOMAIN_ROOT}/privacidad`
- `legal.support_url` → `https://ayuda.${DOMAIN_ROOT}/contacto`

Esas páginas no pueden contener `LEGAL_PLACEHOLDER` ni el texto «Texto en revisión». La primera versión de las tiendas no usa `--skip-landing` ni `ALLOW_LEGAL_PLACEHOLDER=1`.
