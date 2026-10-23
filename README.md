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
- Docker, para Postgres/PostGIS, Redis, Mailpit y MinIO.
- `dotnet-ef` 10, solo para aplicar migraciones:

```bash
dotnet tool install --global dotnet-ef
```

## Arranque rápido

Desde la raíz del repositorio:

```bash
pnpm install
pnpm infra:up
dotnet ef database update \
  --project apps/backend/src/Situ.Infrastructure/Situ.Infrastructure.csproj \
  --startup-project apps/backend/src/Situ.Api/Situ.Api.csproj
pnpm dev:api
```

`pnpm infra:up` ejecuta `docker compose up -d`. La API no aplica migraciones al arrancar: hay que correr `dotnet ef database update`. La migración inicial está en `apps/backend/src/Situ.Infrastructure/Persistence/Migrations/`.

La base local es la de `docker-compose.yml` y `apps/backend/src/Situ.Api/appsettings.json`: host `localhost`, puerto **5433**, base `situ`, usuario `situ`, contraseña `situ_dev`. Postgres del compose publica `5433` para no chocar con un Postgres que ya escuche en el 5432 del host.

Las apps de JavaScript leen `NEXT_PUBLIC_API_URL` y `EXPO_PUBLIC_API_URL`. Copia `.env.example` a `.env` si quieres fijarlas; si no, el cliente usa `http://localhost:5080`.

En otra terminal, el cliente que vayas a usar:

```bash
pnpm dev:web
pnpm dev:landing
pnpm dev:support
pnpm dev:mobile
```

`pnpm infra:down` apaga los contenedores.

## Scripts

| Script | Qué hace |
| --- | --- |
| `pnpm infra:up` | Levanta Postgres/PostGIS 17, Redis 7, Mailpit y MinIO |
| `pnpm infra:down` | Apaga esa infraestructura |
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
| Postgres | `localhost:5433` |
| Redis | `localhost:6379` |
| Mailpit (bandeja de correo de prueba) | http://localhost:8025 (SMTP en el puerto 1025) |
| MinIO (S3 de imágenes y video) | API http://localhost:9000, consola http://localhost:9001 |

El usuario de MinIO en desarrollo es `situ` y la contraseña `situ_dev_secret`.
