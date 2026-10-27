# Versiones

La versión que ve la persona es `expo.version` en `apps/mobile/app.json`. Esta prueba cerrada sale como **0.1.0**. Súbela en `app.json` solo cuando publiques otra versión que la persona deba distinguir (por ejemplo 0.2.0). No la subas en cada build interno.

`eas.json` usa `cli.appVersionSource: remote`. El `buildNumber` de iOS y el `versionCode` de Android viven en EAS, no en el repositorio. Los perfiles `preview` y `production` tienen `autoIncrement: true`, así que cada build de esos perfiles suma uno. El perfil `development` no incrementa: es el cliente de desarrollo.

No hay actualizaciones OTA. `expo-updates` no es dependencia y ningún perfil define `channel`. Un cambio de código llega a los testers con un build nuevo y, en las tiendas, con `eas submit` o la carga manual.

Los perfiles leen las variables del entorno de EAS del mismo nombre (`development`, `preview`, `production`). No van en el repositorio:

| Variable | Valor |
| --- | --- |
| `EXPO_PUBLIC_DOMAIN_ROOT` | El dominio raíz, sin esquema. Ejemplo: `midominio.com`. |
| `API_BASE_URL` o `EXPO_PUBLIC_API_URL` | `https://api.${EXPO_PUBLIC_DOMAIN_ROOT}`. Si solo está `API_BASE_URL`, el build la copia a `EXPO_PUBLIC_API_URL`. Si no hay ninguna y sí hay dominio, usa `https://api.` más el dominio. |
| `EXPO_PUBLIC_GOOGLE_MAPS_API_KEY_ANDROID` | Clave de Maps restringida a Android. |
| `EXPO_PUBLIC_GOOGLE_MAPS_API_KEY_IOS` | Clave de Maps restringida a iOS. Sin esta clave, iOS usa Apple Maps. |
| `GOOGLE_SERVICES_JSON` | Ruta al archivo de Firebase para push en Android. Sin él, Android arranca sin push. |
| `ASC_APP_ID` | Apple ID numérico de la app en App Store Connect. No cabe dentro de `eas.json`: ese campo solo acepta dígitos y el CLI no lo sustituye. Se pasa con `--asc-app-id`. |
| `GOOGLE_SERVICE_ACCOUNT_KEY_PATH` | Ruta al JSON de la cuenta de servicio de Play. `eas.json` ya trae `$GOOGLE_SERVICE_ACCOUNT_KEY_PATH`, y ese campo sí se sustituye. |

El identificador es `com.somossitu.app` en iOS y en Android. El Team ID de Apple ya escrito en la config es `Y27698UG63`.

## Una sola copia de React

El SDK 58 pide `react` 19.3.0 y `react-native` 0.88.0. La web, el admin, la landing y soporte pedían `react` ^19.3.1. Con `node-linker=hoisted` (`.npmrc`, lo que Expo necesita en un monorepo pnpm) esas dos versiones convivían: 19.3.0 en la raíz y 19.3.1 dentro de `react-hook-form` y `@tanstack/react-query`. Metro puede cargar las dos y la app se rompe.

Decisión: todo el monorepo resuelve `react` y `react-dom` 19.3.0, y `react-native` 0.88.0. Los overrides están en `pnpm-workspace.yaml` (pnpm 10 no lee `pnpm.overrides` del `package.json`). Las apps de Next quedaron en 19.3.0, el mismo parche que ya usaba el móvil. `apps/mobile/metro.config.js` apunta `extraNodeModules` a esas copias hoisted y suma la raíz del repo a `watchFolders` y `nodeModulesPaths`. `resolver.disableHierarchicalLookup` se deja en el valor de Expo (`false`): expo-doctor falla si se pone en `true`, y con una sola copia en disco no hace falta.
