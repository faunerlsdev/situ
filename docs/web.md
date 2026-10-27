# App web

La app pública vive en `apps/web` y corre en http://localhost:3002. El panel sigue en 3001 y la landing en 3003. El centro de ayuda pasó al puerto 3004 para dejar el 3002 libre.

```bash
pnpm dev:web
```

Copia `apps/web/.env.example` a `apps/web/.env.local` si vas a poner la clave del mapa. Sin esa clave la app abre y explica que falta; no se cae.

Antes de usar el ticket de tiempo real aplica la migración nueva:

```bash
dotnet ef database update \
  --project apps/backend/src/Situ.Infrastructure/Situ.Infrastructure.csproj \
  --startup-project apps/backend/src/Situ.Api/Situ.Api.csproj
```

## Variables

| Variable | Uso |
| --- | --- |
| `NEXT_PUBLIC_API_URL` | Origen de la API. Por defecto `http://127.0.0.1:5080`. El navegador solo lo usa para el WebSocket del mapa. El resto pasa por el BFF. |
| `NEXT_PUBLIC_GOOGLE_MAPS_KEY` | Clave del Maps JavaScript API. Vacía en el ejemplo. |

## Clave de Google Maps para la web

1. En Google Cloud, activa **Maps JavaScript API**.
2. Crea una clave distinta de las de Android e iOS.
3. Restríngela por **HTTP referrers**. En local: `http://localhost:3002/*` y `http://127.0.0.1:3002/*`. En producción, el dominio real, por ejemplo `https://tudominio/*`.
4. No la pegues en el repositorio. Va en `.env.local`.

La política de referrer de la página es `strict-origin-when-cross-origin` para que Google reciba el origen y la restricción funcione. `no-referrer` dejaría la clave inutilizable.

## Sesión

El navegador no ve el access token ni el refresh token. El BFF los guarda en cookies `httpOnly`, `SameSite=Strict` y `Secure` fuera de local. Las mutaciones exigen el encabezado `x-csrf-token`, igual que el valor de la cookie `situ_web_csrf`. El access dura 15 minutos y se renueva solo con el refresh de 180 días. No se aplica el cierre por inactividad del panel: esa regla es solo del personal.

El identificador del navegador (`situ_web_did`) es una cookie `httpOnly`. No se guarda el correo ni la fecha de nacimiento. Cuenta como un dispositivo del plan.

Si el ingreso responde que falta el acudiente, la espera sigue en `/representante`. El enlace que recibe el acudiente ya es la página `/tutor/autorizar`.

## Mapa

`MapView` está detrás de `GoogleMapView`. El estilo es el oscuro de la marca. Los pines usan el ícono de mapa de la categoría y, si no hay, un pin naranja. Al alejar el zoom la API agrupa (zoom menor a 15). Los filtros son categoría, última hora y hoy. "Mi ubicación" usa el permiso del navegador y no se guarda en el servidor. Confirmar "sigue ahí" o "ya no está" sí envía la coordenada, porque el contrato actual lo exige para medir la distancia; no queda como ubicación del perfil.

La carga es `GET /api/v1/reports/map` con unos 500 ms de espera tras mover el mapa. El tiempo real usa el mismo cálculo de celdas de `@situ/shared`, máximo 16. La pestaña oculta desconecta el hub. Si no hay tiempo real, se ve "Sin conexión en tiempo real" y al volver se recarga la zona.

El evento solo avisa. El contenido entra por `GET /api/v1/reports/batch`. La web abre el WebSocket directo con un ticket de un solo uso, sin negociación, para no gastar el ticket dos veces. Si se corta, pide otro ticket.

"Quiero chismosear" sale del texto de `app-config`. Elegir una categoría sin sesión lleva al ingreso. Con sesión abre `/reportar`, que por ahora dice que el formulario llega después. No hay control para ir en vivo.

## Compartir

`/r/{id}` arma Open Graph solo con la categoría y una zona aproximada (ciudad, o coordenadas redondeadas a un decimal). No incluye imagen ni texto del autor. Si el reporte no es público, es de un menor o ya no está visible, la página responde `noindex`.

## Diferencias con la app móvil

- No se crean reportes con foto o video, no está el perfil y no está el takedown. Van en el siguiente paso.
- No se inicia ni se ve una transmisión en vivo.
- El teléfono manda `access_token` al hub. La web manda `ticket`.
- El mapa del teléfono usa el SDK nativo. La web usa Google Maps JavaScript, cambiable detrás de `GoogleMapView`.

## Pruebas

```bash
pnpm --filter @situ/web test
pnpm --filter @situ/web typecheck
pnpm --filter @situ/web lint
```

El smoke de Playwright (`pnpm --filter @situ/web test:e2e`) abre el ingreso y el mapa sin clave. Un recorrido con usuario real contra la API local queda pendiente: hace falta una cuenta de prueba y la clave de Maps.
