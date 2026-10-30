# App Store Connect: tareas manuales

El binario no llena estos formularios.

## Ficha

- Crear el app record: nombre SITU, idioma principal español, Bundle ID `com.somossitu.app`, SKU que elijas.
- Subtítulo, descripción, palabras clave y categoría. No menciones Android ni Google Play.
- URL de soporte: `https://ayuda.somossitu.com/contacto`
- URL de privacidad: `https://somossitu.com/privacidad`
- URL de términos, si el campo existe: `https://somossitu.com/terminos`
- Capturas de iPhone 6.9" (1320×2868 o 1290×2796), tema oscuro. No subas capturas de iPad: la app es solo iPhone.
- Clasificación por edad: ver la sección de abajo. La app de iOS no tiene mensajería directa.
- Precio gratis. Países: al menos Colombia.
- Si vendes en la Unión Europea, completa el estado de trader de la DSA.
- Cifrado: la app declara que no usa cifrado ajeno al de HTTPS. En el formulario de exportación responde que no está sujeta a las normas especiales.
- Usuarios internos de TestFlight antes de enviar a revisión.
- Cuenta demo en App Review Information: sustituye `<DEMO_USER>` y `<DEMO_PASSWORD>` en las notas. No borres esa cuenta. Crea otra para probar el borrado.
- Pega el texto de `docs/app-review-notes.md` en Notes.

## Privacidad de la app

Marca rastreo: no.

| Tipo de dato | ¿Se recoge? | Vinculado al usuario | Rastreo | Finalidad | Origen |
| --- | --- | --- | --- | --- | --- |
| Ubicación precisa | Sí | Sí | No | Función de la app | `expo-location`, mapa y reportes |
| Ubicación aproximada | Sí | Sí | No | Función de la app | punto compartido con amigos |
| Nombre | Sí | Sí | No | Función de la app | registro |
| Correo | Sí | Sí | No | Función de la app | registro y soporte |
| Teléfono | Sí | Sí | No | Función de la app | registro |
| Fecha de nacimiento | Sí | Sí | No | Función de la app, edad mínima | registro. No tiene clave propia en el manifest |
| Género | No | — | No | — | el registro no lo pregunta |
| Fotos y videos | Sí | Sí | No | Función de la app | reportes y perfil |
| Audio | Sí | Sí | No | Función de la app | video y transmisión |
| Contenido del usuario | Sí | Sí | No | Función de la app | reportes, comentarios, denuncias |
| ID de usuario | Sí | Sí | No | Función de la app | cuenta |
| ID de dispositivo | Sí | Sí | No | Función de la app | `situ.deviceId` en el llavero, más la plataforma |
| Interacción con el producto | Sí | Sí | No | Función de la app | sesión y preferencias |
| Diagnóstico, crashes, analítica, publicidad | No | — | No | — | no hay Sentry, Firebase Analytics ni anuncios |

El `PrivacyInfo.xcprivacy` declara los tipos de la tabla que tienen clave de Apple, vinculados al usuario, sin rastreo, finalidad «funcionalidad de la app». La fecha de nacimiento va en este cuestionario. El registro no pide género.

## Clasificación por edad

- Contenido generado por usuarios: sí. Reportes, comentarios y transmisiones. Hay filtro de texto antes de publicar, denuncia con motivo y bloqueo.
- Transmisiones en vivo: sí. Solo la app móvil las inicia. Se pueden denunciar, también si ya terminaron.
- Ubicación: sí, mientras la app está en uso. No hay ubicación en segundo plano.
- Cuentas de menores con tutor: la edad mínima en Colombia es 14. De 14 a 17 la cuenta queda pendiente hasta que el tutor abre el enlace del correo, verifica ese correo y autoriza. El perfil del menor es privado y no sale en el mapa público.

## Comando de subida

Cuando el `.ipa` exista y la ficha esté creada:

```bash
cd apps/mobile
eas submit -p ios --profile production --latest
```

`eas.json` ya tiene el Team ID `Y27698UG63`. El Apple ID de la app (`ASC_APP_ID`) no está en el repo: pásalo con `--asc-app-id` si el submit lo pide.
