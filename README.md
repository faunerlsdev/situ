# SITU

Red social e informativa para móvil y web, con un mapa al estilo de Waze. Quien usa la app reporta lo que pasa en un lugar (accidentes, hurtos, retenes, basura), tiene amigos y recibe avisos de su ciudad y de lo que ocurra en un radio de 1 km. Reportes, comentarios y transmisiones pueden ser anónimos.

La transmisión en vivo se inicia solo en la app móvil. La web, la landing y el centro de ayuda sirven para ver y comentar. La protección de menores sigue la Ley 1581 de 2012 (habeas data): si una función choca con la privacidad de un menor, gana la privacidad. Los planes son Free, Basic, Pro y Advanced.

La lógica de negocio vive en el backend. Los clientes la consumen.

## Repositorio

| Ruta | Qué es |
| --- | --- |
| `apps/backend` | API .NET 10 (`Situ.Api`, `Situ.Application`, `Situ.Domain`, `Situ.Infrastructure`) |
| `apps/web` | App web, puerto 3002 |
| `apps/admin` | Panel de administración, puerto 3001 |
| `apps/landing` | Sitio de producto, puerto 3003 |
| `apps/support` | Centro de ayuda, puerto 3004 |
| `apps/mobile` | App Expo (SDK 58) con Expo Router |
| `packages/shared` | Tokens de marca, tipos y cliente HTTP |
| `packages/config` | TypeScript y ESLint compartidos |

Más detalle en [docs/architecture.md](docs/architecture.md) y [docs/domain-model.md](docs/domain-model.md).

## Requisitos

- Node.js 22 o superior y pnpm 10.28.0 (`packageManager` del `package.json` raíz).
- .NET SDK 10 (`apps/backend/global.json`, `10.0.100` con `rollForward` al feature más reciente).
- PostgreSQL 14 o superior con PostGIS, instalado en local (macOS).
- `dotnet-ef` 10, solo para aplicar migraciones:

```bash
dotnet tool install --global dotnet-ef
```

## Base de datos local

PostgreSQL 14 o superior con la extensión PostGIS, instalada de forma local en macOS. La base se llama `situ`.

a) Verificar la versión:

```bash
psql --version
```

b) Instalar PostGIS si falta. Postgres.app ya lo incluye. Con Homebrew:

```bash
brew install postgis
```

c) Crear la base y la extensión:

```bash
createdb situ
psql situ -c "CREATE EXTENSION IF NOT EXISTS postgis;"
psql situ -c "SELECT postgis_version();"
```

## Configuración y secretos

`apps/backend/src/Situ.Api/appsettings.json` solo tiene valores públicos: logging, `AllowedHosts` y JWT (`Issuer`, `Audience`, `AccessTokenMinutes`, `RefreshTokenDays`). No lleva secreto.

`apps/backend/src/Situ.Api/appsettings.Development.json` no se versiona: está en `.gitignore`. Ahí puede ir la cadena `ConnectionStrings:Default` de tu máquina y el logging de desarrollo.

En desarrollo, los valores sensibles viven en user-secrets de `Situ.Api` (fuera del repositorio):

```bash
dotnet user-secrets init --project apps/backend/src/Situ.Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=situ;Username=TU_USUARIO;Password=TU_CLAVE" --project apps/backend/src/Situ.Api
dotnet user-secrets set "Jwt:Secret" "<cadena aleatoria de al menos 64 caracteres>" --project apps/backend/src/Situ.Api
```

En producción los mismos valores llegan por variables de entorno, con el formato `Seccion__Clave`:

- `ConnectionStrings__Default`
- `Jwt__Secret`
- `GuardianConsent__SigningKey` (Production y Staging)

En Development, si `GuardianConsent:SigningKey` no está configurada, la API usa una clave fija definida en el código y escribe un warning en el log: las firmas generadas no tienen validez. Esa clave no se pide por user-secrets.

En Production y Staging la clave es obligatoria y debe tener al menos 32 bytes. En producción no puede perderse ni cambiarse: se usa para verificar consentimientos ya firmados. El respaldo va en un gestor de secretos. No va en la base de datos ni en archivos versionados.

Si falta algo, el proceso no arranca. El mensaje es uno de estos:

- `Falta Jwt:Secret. En desarrollo configúralo con dotnet user-secrets. En producción define la variable de entorno Jwt__Secret.`
- `Jwt:Secret debe tener al menos 32 caracteres.`
- `Falta la cadena de conexión ConnectionStrings:Default. En desarrollo configúrala con dotnet user-secrets. En producción define la variable de entorno ConnectionStrings__Default.`
- `Falta GuardianConsent:SigningKey. Defínela con la variable de entorno GuardianConsent__SigningKey`
- En Production también se rechazan placeholders, las claves de desarrollo (`situ-dev-guardian-consent-signing-key` y `situ-development-media-signing-key`) y un secreto repetido entre JWT, consentimiento y medios. Hacen falta `FileStorage__SigningKey`, `FileStorage__Root`, `Notifications__ExpoAccessToken`, `Captcha__SecretKey`, SMTP real, `PublicBaseUrl` https y `Cors__AllowedOrigins`. El detalle está en `docs/deploy.md`.

## Arranque rápido

Desde la raíz del repositorio, con la base `situ` creada y los user-secrets configurados:

```bash
pnpm install
dotnet ef database update \
  --project apps/backend/src/Situ.Infrastructure/Situ.Infrastructure.csproj \
  --startup-project apps/backend/src/Situ.Api/Situ.Api.csproj
pnpm dev:api
```

La API no aplica migraciones al arrancar: hay que correr `dotnet ef database update`. La migración inicial está en `apps/backend/src/Situ.Infrastructure/Persistence/Migrations/`.

## Primer administrador

No hay una cuenta de personal por defecto. Con una cuenta ya registrada, y con la API detenida o en otra terminal:

```bash
dotnet run --project apps/backend/src/Situ.Api -- admin:grant --email tu@correo.com --role SuperAdmin
```

El comando asigna el rol, lo anota en la bitácora y no abre ningún endpoint público. Con el mismo acceso al servidor, `admin:reset-2fa` restablece el segundo factor de una cuenta del equipo. `dev:seed` crea datos de ejemplo y solo corre en Development. Los roles, la IP real y el detalle están en [docs/admin.md](docs/admin.md).

Las apps de JavaScript leen `NEXT_PUBLIC_API_URL` y `EXPO_PUBLIC_API_URL`. Copia `.env.example` a `.env` si quieres fijarlas; si no, el cliente usa `http://localhost:5080`.

En otra terminal, el cliente que vayas a usar:

```bash
pnpm dev:web
pnpm dev:landing
pnpm dev:support
pnpm dev:mobile
```

## Identificadores de la app

El identificador de la aplicación es `com.somossitu.app`, el mismo en Android y en iOS. Corresponde al dominio somossitu.com. La única fuente en el código es la constante `APP_ID` de `apps/mobile/app.config.js`. El nombre visible es SITU, el slug de Expo es `situ` y el esquema de enlaces profundos es `situ`.

El proyecto de EAS es `@faunerls.dev/situ`. El owner y el `projectId` salen de `apps/mobile/app.config.js` (`EAS_PROJECT_ID` puede reemplazar el id). Los perfiles están en `apps/mobile/eas.json`: `development` y `preview` se distribuyen en interno; `production` incrementa la versión en EAS (`appVersionSource: remote`), porque la config es dinámica y la CLI no puede escribirla.

No lo cambies después de crear las credenciales de Firebase, APNs o Google Maps, ni después de publicar en las tiendas. Si cambia, hay que hacer de nuevo esto:

1. Crear otra app de Android y otra de iOS en Firebase con el identificador nuevo, y volver a bajar `google-services.json` y `GoogleService-Info.plist`.
2. Regenerar la clave APNs en Apple Developer y volver a subirla a Firebase.
3. Reconfigurar las restricciones de las API keys de Google Maps: Android con el paquete `com.somossitu.app` más el SHA-1, e iOS con el bundle ID `com.somossitu.app`.

Las carpetas `apps/mobile/android` y `apps/mobile/ios` están en `.gitignore`. La copia local ya usa `com.somossitu.app`. Si vuelves a generarlas, hazlo desde `APP_ID`, en `apps/mobile`:

```bash
npx expo prebuild --clean
```

## Mapa

El mapa de la app móvil usa Google Maps si hay una API key de esa plataforma. Las keys no van en el repositorio: viven en `apps/mobile/.env`, que está ignorado. Copia los nombres desde `apps/mobile/.env.example`.

En Google Cloud, en el proyecto de SITU:

1. Activa **Maps SDK for Android** y **Maps SDK for iOS**.
2. Crea dos API keys, una por plataforma.
3. Restringe la de Android a apps Android, con el nombre de paquete `com.somossitu.app` y el SHA-1 del keystore con el que firmas (el de depuración sale de `keytool -list -v -keystore ~/.android/debug.keystore -alias androiddebugkey -storepass android`).
4. Restringe la de iOS a apps iOS, con el bundle ID `com.somossitu.app`.

En `apps/mobile/.env`:

```bash
EXPO_PUBLIC_GOOGLE_MAPS_API_KEY_ANDROID=
EXPO_PUBLIC_GOOGLE_MAPS_API_KEY_IOS=
```

Esas variables se hornean en el cliente nativo. Cambiarlas exige recompilar la app (`npx expo run:android --device` o `npx expo run:ios --device` desde `apps/mobile`). Recargar Metro no alcanza. Si ya existe la carpeta `apps/mobile/android`, genera de nuevo el proyecto nativo antes de compilar:

```bash
npx expo prebuild --platform android --clean
```

Sin la key de iOS, el mapa usa Apple Maps en oscuro. Sin la key de Android, la pestaña Mapa no monta el mapa y el resto de la app sigue disponible.

## Parche del panel inferior

`@gorhom/bottom-sheet` 5.2.14 es la última versión publicada y le pasa un arreglo de dependencias a los hooks de Reanimated. En Android e iOS, Reanimated 4.7 ignora ese arreglo y escribe un aviso por cada hook. El parche `patches/@gorhom__bottom-sheet@5.2.14.patch`, registrado en `pnpm-workspace.yaml`, deja el arreglo solo cuando `Platform.OS === 'web'`. En nativo el argumento queda `undefined`: el aviso no sale y el panel se anima igual. No se añadió `patch-package`; pnpm ya versiona el parche.

Quítalo cuando publiquen una versión de `@gorhom/bottom-sheet` que deje de pasar esas dependencias en nativo.

## Notificaciones push: credenciales

Las claves no van en el repositorio. `google-services.json`, `GoogleService-Info.plist` y el JSON de la cuenta de servicio de Firebase están en `.gitignore`.

`android.googleServicesFile` solo se define si `GOOGLE_SERVICES_JSON` apunta a un archivo que existe. Si la variable está vacía o el archivo no está, el campo se omite: la app arranca y, en desarrollo, la consola de Metro avisa que Android queda sin push. En un build de EAS o con `NODE_ENV=production` no avisa.

En local, en `apps/mobile/.env` (no se versiona):

```bash
GOOGLE_SERVICES_JSON=/ruta/fuera/del/repo/google-services.json
```

La ruta puede ser absoluta o relativa a `apps/mobile`.

Para los builds en la nube, sube ese archivo como variable de tipo archivo y visibilidad secreta. En la CLI 25 el comando vigente es `eas env:set` (`eas env:create` acepta los mismos flags y está marcado como obsoleto). Desde `apps/mobile`, una vez por entorno:

```bash
eas env:set --name GOOGLE_SERVICES_JSON --type file --value /ruta/fuera/del/repo/google-services.json --visibility secret --environment development --environment preview --environment production
```

Durante el build, EAS escribe el archivo fuera del proyecto y pone su ruta en `GOOGLE_SERVICES_JSON`.

Orden para dejar las credenciales de push. Todo desde `apps/mobile`:

1. `eas login`
2. `eas device:create` para registrar el iPhone donde vas a instalar el build interno.
3. `eas credentials`. En Android sube la clave FCM V1 (el JSON de la cuenta de servicio de Firebase, el de Cloud Messaging, no `google-services.json`). En iOS crea o sube la clave APNs.
4. `eas build --profile development`

El perfil `development` exige `expo-dev-client`. El identificador de las apps de Firebase y de la clave APNs es `com.somossitu.app`.

El servidor habla con la API de Expo. El token de acceso se crea en [expo.dev/settings/access-tokens](https://expo.dev/settings/access-tokens) y no se versiona. Desde `apps/backend/src/Situ.Api`:

```bash
dotnet user-secrets set "Notifications:ExpoAccessToken" "el-token"
```

`Notifications:RequireExpoAccessToken` sale en `false` en Development si no lo defines, y en `true` en cualquier otro entorno. En Development, sin token, el servidor igual llama a Expo y omite el encabezado. Fuera de Development, sin token, no envía: escribe una advertencia al arrancar y la API sigue viva. En el proyecto de Expo conviene activar la seguridad reforzada de notificaciones push (Enhanced push security) para que Expo rechace envíos sin ese token, y dejar el requisito encendido en producción.

El canal de Android es `zone-alerts` en la app y en el campo `channelId` del push.

## Scripts

| Script | Qué hace |
| --- | --- |
| `pnpm dev:api` | `dotnet watch` de `Situ.Api` en http://localhost:5080 |
| `pnpm dev:web` | Next.js en http://localhost:3002 |
| `pnpm dev:admin` | Panel en http://localhost:3001 |
| `pnpm dev:landing` | Next.js en http://localhost:3003 |
| `pnpm dev:support` | Next.js en http://localhost:3004 |
| `pnpm dev:mobile` | Metro de Expo (http://localhost:8081) |
| `pnpm api:test` | Pruebas de dominio y de arquitectura |
| `pnpm typecheck` | `tsc` de los paquetes TypeScript vía Turbo |
| `pnpm api:build` | Compila `apps/backend/Situ.slnx` |

## Servicios locales

| Servicio | Dirección |
| --- | --- |
| API | http://localhost:5080 |
| Salud (incluye la base) | http://localhost:5080/health |
| OpenAPI | http://localhost:5080/openapi/v1.json |
| Scalar | http://localhost:5080/scalar |
| Postgres | `localhost:5432`, base `situ` |
