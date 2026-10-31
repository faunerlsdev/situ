# Auditoría iOS para App Store Connect

Fecha: 30/10/2026. No hubo un rechazo previo de App Review pegado en el encargo.

## Stack

| Dato | Valor |
| --- | --- |
| Proyecto | Expo SDK 58.0.19, prebuild (no hay `ios/` en git). React Native 0.88.0, React 19.3.0 |
| Compilación | EAS Build, perfil `production`, `distribution: store`, `appVersionSource: remote`, `autoIncrement: true` |
| Xcode en esta Mac | 26.6 (17F113), SDK iOS 26.5 |
| Imagen EAS `production` | `sdk-58` (Xcode 27.1 en la nube). Un build local usa el Xcode de la Mac |
| Bundle ID | `com.somossitu.app` |
| Team ID | `Y27698UG63` (ya estaba en `app.json` y en `eas.json`) |
| Versión visible | `0.1.0`, igual que el `versionName` del AAB de Play |
| Build | entero remoto de EAS; cada `production` lo incrementa |
| Deployment target | 15.1, el mínimo de React Native 0.88 (`min_ios_version_supported`) |
| Dispositivos | solo iPhone (`supportsTablet: false`) |
| Firma | credenciales remotas de EAS, distribución App Store. No se inventaron certificados |

## Hallazgos

| Tema | Estado | Nota |
| --- | --- | --- |
| Xcode 26+ / SDK iOS 26 | 🔧 | La Mac ya cumple. El perfil `production` fija `ios.image` en `sdk-58` |
| Deployment target ≥ 13 | ✅ | 15.1 por React Native. No se añadió otro plugin |
| Versión = Play | ✅ | `0.1.0` en `app.json` y en el AAB |
| Bundle ID y Team ID | ✅ | `com.somossitu.app`, `Y27698UG63` |
| Ícono 1024 sin alfa | 🔧 | `ios.icon` apunta a `store-assets/appstore-icon-1024.png`. El ícono de Android no cambió |
| Launch screen `#1C1C1C` | ✅ | plugin `expo-splash-screen` con el logo vertical |
| Privacy manifest | 🔧 | `NSPrivacyAccessedAPITypes` con CA92.1, C617.1, E174.1 y 35F9.1. Se quitaron audio y device ID: el audio va dentro del video y el id del dispositivo es un UUID de la app, no el identificador del sistema |
| Cifrado de exportación | ✅ | `ITSAppUsesNonExemptEncryption` y `usesNonExemptEncryption` en false |
| ATS | ✅ | no hay `NSAllowsArbitraryLoads`. Sí hay `NSAllowsLocalNetworking` (ver 🟡) |
| Push | ✅ | `expo-notifications` en el perfil `store`. La clave APNs la gestiona EAS |
| Ubicación en background | ✅ | `isIosBackgroundLocationEnabled: false` y no hay `NSLocationAlways*` |
| IDFA | ✅ | no hay `NSUserTrackingUsageDescription` ni SDK de anuncios |
| Login | ✅ | correo y contraseña propios. No hay Google ni Facebook, así que Sign in with Apple no es obligatorio |
| Recuperar contraseña | ✅ | ruta `/recuperar` |
| Eliminar cuenta | ✅ | pantalla Mi cuenta, con contraseña, contra el endpoint real |
| Reportar y bloquear | ✅ | denuncias y bloqueo en la app. El texto de soporte dice revisión en menos de 24 h |
| Filtro de palabras antes de publicar | 🔧 | El backend rechaza el texto con 422 y `fieldErrors` antes de guardar reporte, comentario o título de transmisión. La lista está en `apps/backend/config/content-filter.txt` y se relee si cambia la fecha del archivo. No hay entidad de mensajes directos |
| Registro con datos extra | 🔧 | Celular, provincia y ciudad son opcionales en cliente y backend. La fecha de nacimiento sigue obligatoria. No existe un campo de género |
| Ubicación negada | 🔧 | El mapa queda en Bogotá (o en la ciudad de la cuenta) y el diálogo se cierra con «Ahora no» |
| Pestañas Próximamente | 🔧 | En iOS, `Platform.OS` oculta Chisme y Mensajes en el router y en la barra. Android sigue el flag del servidor |
| Menciones a Android | 🟡 | la lista de dispositivos dice «Android» si esa sesión es de Android. No hay enlace a Google Play |
| iPad | 🔧 | `supportsTablet: false`. No hacen falta capturas de iPad |
| Recorrido en dispositivo y TestFlight | ⏳ | el build local se detuvo: EAS no tiene un certificado de distribución validado para modo no interactivo. Hay que correr el mismo comando en una terminal y aceptar la firma |

## Cierre 10.1

1. Filtro: `ContentFilter` en `apps/backend/src/Situ.Application/Features/Moderation/ContentFilter.cs`. Reportes y comentarios responden 422 con el campo `description` o `text`. Un mensaje directo no tiene tabla ni endpoint de escritura.
2. Registro: celular, departamento y ciudad opcionales. País y fecha de nacimiento siguen obligatorios. El formulario no tiene género.
3. Ubicación: `apps/mobile/features/reports/map-screen.tsx` centra en Bogotá (`bogota`, línea 85) y el diálogo de permiso negado se puede cerrar.
4. Eliminar cuenta, EULA y soporte están en la app. La frase literal «tolerancia cero» no está en `docs/legal/terminos-situ-v1.0.md`; la sección 20 sí prohíbe acoso, amenazas y datos de terceros. No se reescribió el texto del abogado.
5. El manifiesto de la app sale de `apps/mobile/store-contract.js`, copiado en `app.config.js` línea 105. No se declara audio ni device ID.

## Cierre 10.2

1. Términos v1.1: borrador en `docs/legal/terminos-situ-v1.1.md`, cláusula 20.X con el comentario HTML `PENDIENTE REVISIÓN DEL ABOGADO` (no es texto visible). La fila en `legal_documents` queda **inactiva** (`IsActive = false`, versión `1.1`, misma URL). La página pública sigue leyendo v1.0 (`apps/landing/src/lib/terms-policy.ts`). No se activó.
2. Transmisiones: se puede denunciar, con motivo, una transmisión que sigue en vivo. El bloqueo oculta las que están en curso y las ya terminadas en las pantallas que existen. No se puede denunciar una transmisión ya finalizada. No se implementó ese hueco: espera aprobación.
3. Eliminar cuenta: el borrado ahora quita transmisiones (filas y archivo de la grabación), el archivo de la foto de perfil y los bloqueos. Nombre, correo, celular y fecha de nacimiento se anonimizan. Auditoría y autorización del tutor se conservan. El texto de la app dice 14 días (`messages.ts`).
4. Rótulo de registro: `feedbackMessages.interestCity` en el formulario móvil. Sin cambio de validación. Android usa el mismo archivo.
5. Notas de App Review actualizadas. Siguen `<DEMO_USER>` y `<DEMO_PASSWORD>`. Se añadieron `<DEMO_DELETE_USER>` y `<DEMO_DELETE_PASSWORD>` para la segunda cuenta. No hay mención a mensajería.
6. Script `apps/backend/scripts/seed-review-demo`. No se ejecutó.
7. Comandos abajo. No se corrieron contra producción.

## Cierre 10.3

1. Denunciar una transmisión terminada: `ReportLiveHandler` usa `liveOnly: false`. El botón Denunciar también aparece en la fase `ended`. Un bloqueo sigue ocultando la transmisión.
2. Al confirmar «Eliminar mi cuenta» se cierra la sesión y se revocan los tokens de refresco. El perfil, los reportes, los comentarios y las transmisiones dejan de mostrarse enseguida. Las filas siguen guardadas. Si cancelan dentro de los 14 días, ese contenido vuelve a verse. El borrado definitivo sigue en el día 14.
3. Activar términos v1.1 es `apps/backend/scripts/activate-terms-v1.1`. No se ejecutó. La página sigue en v1.0.
4. El seed deja las dos cuentas adultas, con el correo confirmado y con la versión de términos que esté activa en ese momento.
5. Notas de App Review y cuestionario de edad actualizados.

### Comandos (no ejecutados)

Desde la raíz del repositorio. No los corras contra producción hasta confirmarlo. `./deploy/release.sh` publica API, web, panel, landing y ayuda, y en el servidor corre `dotnet Situ.Api.dll migrate`, que aplica todas las migraciones pendientes.

a) Ciudad y departamento opcionales. Se detiene en esa migración:

```bash
dotnet ef database update 20261030194500_OptionalRegistrationPlace \
  --project apps/backend/src/Situ.Infrastructure/Situ.Infrastructure.csproj \
  --startup-project apps/backend/src/Situ.Api/Situ.Api.csproj
```

b) Desplegar el backend (y el resto del release). No aplica solo el API: también compila y publica los sitios. No ejecuta la activación de la v1.1.

```bash
./deploy/release.sh
```

c) Activar términos v1.1: pone `IsActive` en la fila 1.1, retira la `2026-10-29`, apunta la landing a `terminos-situ-v1.1.md` y quita el comentario del abogado. Antes tiene que existir la fila, así que aplica `20261030210000_InactiveTermsV11` si el release aún no la corrió. La página pública cambia en el siguiente `./deploy/release.sh`.

```bash
dotnet ef database update 20261030210000_InactiveTermsV11 \
  --project apps/backend/src/Situ.Infrastructure/Situ.Infrastructure.csproj \
  --startup-project apps/backend/src/Situ.Api/Situ.Api.csproj
SITU_CONFIRM_TERMS_V11=activate bash apps/backend/scripts/activate-terms-v1.1
```

Para revertirlo: la fila 1.1 vuelve a inactiva, la `2026-10-29` queda vigente, la landing apunta otra vez a v1.0 y regresa el comentario del abogado. La página pública vuelve en el siguiente release.

```bash
SITU_CONFIRM_TERMS_V11=revert bash apps/backend/scripts/revert-terms-v1.1
```

d) Cuentas demo. Aceptan la versión de términos que esté vigente en ese momento, así que conviene correrlo después del paso c). Las contraseñas no se imprimen.

```bash
SITU_REVIEW_DEMO_PASSWORD='...' SITU_REVIEW_DELETE_PASSWORD='...' \
  bash apps/backend/scripts/seed-review-demo
```

`<DEMO_USER>` es `revisor.demo@situ.test` (`revisor_demo`). `<DEMO_DELETE_USER>` es `revisor.borrado@situ.test` (`revisor_borrado`).

## Archivos tocados

- `apps/mobile/app.json`
- `apps/mobile/app.config.js` no se reescribió; sigue leyendo el contrato
- `apps/mobile/eas.json`
- `apps/mobile/store-contract.js`
- `apps/mobile/app/(tabs)/_layout.tsx`
- `apps/mobile/features/store/store-config.test.ts`
- `docs/ios-release-audit.md`
- `docs/app-review-notes.md`
- `docs/app-store-connect-checklist.md`

Ninguna dependencia cambió de versión.
