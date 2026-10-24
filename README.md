# SITU

Red social e informativa para móvil y web, con un mapa al estilo de Waze. Quien usa la app reporta lo que pasa en un lugar (accidentes, hurtos, retenes, basura), tiene amigos y recibe avisos de su ciudad y de lo que ocurra en un radio de 1 km. Reportes, comentarios y transmisiones pueden ser anónimos.

La transmisión en vivo se inicia solo en la app móvil. La web, la landing y el centro de ayuda sirven para ver y comentar. La protección de menores sigue la Ley 1581 de 2012 (habeas data): si una función choca con la privacidad de un menor, gana la privacidad. Los planes son Free, Basic, Pro y Advanced.

La lógica de negocio vive en el backend. Los clientes la consumen.

## Repositorio

| Ruta | Qué es |
| --- | --- |
| `apps/backend` | API .NET 10 (`Situ.Api`, `Situ.Application`, `Situ.Domain`, `Situ.Infrastructure`) |
| `apps/web` | App web, puerto 3000 |
| `apps/landing` | Sitio de producto, puerto 3001 |
| `apps/support` | Centro de ayuda, puerto 3002 |
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

Las apps de JavaScript leen `NEXT_PUBLIC_API_URL` y `EXPO_PUBLIC_API_URL`. Copia `.env.example` a `.env` si quieres fijarlas; si no, el cliente usa `http://localhost:5080`.

En otra terminal, el cliente que vayas a usar:

```bash
pnpm dev:web
pnpm dev:landing
pnpm dev:support
pnpm dev:mobile
```

## Scripts

| Script | Qué hace |
| --- | --- |
| `pnpm dev:api` | `dotnet watch` de `Situ.Api` en http://localhost:5080 |
| `pnpm dev:web` | Next.js en http://localhost:3000 |
| `pnpm dev:landing` | Next.js en http://localhost:3001 |
| `pnpm dev:support` | Next.js en http://localhost:3002 |
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
