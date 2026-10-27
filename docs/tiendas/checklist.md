# Envío a TestFlight y a la pista interna de Play

Prueba cerrada: 12 a 15 personas, 14 días seguidos en Play, y TestFlight interno en iOS. Las cuentas de desarrollador ya existen. No ejecutes el build hasta tener el ícono a 1024 y los textos legales publicados. La política de versiones está en `docs/tiendas/versiones.md`.

## 0. Antes de cualquier build

1. Exporta el isotipo y el splash a los tamaños de `docs/tiendas/ficha.md`. Los archivos actuales miden 83 × 110 y 124 × 135.
2. Publica los textos del abogado en `https://DOMINIO/terminos` y `https://DOMINIO/privacidad`. No pueden decir «Texto en revisión» ni contener `LEGAL_PLACEHOLDER`. La primera versión que va a las tiendas no usa `--skip-landing` ni `ALLOW_LEGAL_PLACEHOLDER=1`.
3. En el servidor, apunta `legal.terms_url`, `legal.privacy_url` y `legal.support_url` a esas URLs públicas. El campo de eliminación de datos de Play es `https://app.DOMINIO/cuenta/eliminar`.
4. Crea los entornos de EAS `development`, `preview` y `production`, y carga las variables de `docs/tiendas/versiones.md`. Las claves de Maps no van en el repositorio.

## 1. Credenciales y enlaces

Universal Links y App Links abren `https://app.DOMINIO/r/{id}` en la ficha del reporte. Si la app no está instalada, esa misma URL la sirve la web.

- Team ID de Apple: `Y27698UG63`, ya en `app.json`. Para confirmarlo: `eas credentials` → iOS, o developer.apple.com → Membership.
- `ASC_APP_ID`: App Store Connect → la app → Información de la app → Apple ID (solo números). El CLI no acepta esa variable dentro de `eas.json`.
- Huella SHA-256 del certificado de firma de la app, para `assetlinks.json`: Play Console → Integridad de la app → Firma de la app → certificado de firma de la app → SHA-256. Pégala en `ANDROID_SHA256` antes de instalar nginx. La huella del certificado de carga de EAS es otra: `eas credentials` → Android. Para App Links de una build instalada desde Play usa la de Play, no la de EAS.
- Google Maps en Android. Con Play App Signing, la app instalada desde Play se firma con el certificado de Play, no con el de EAS. En Google Cloud, la restricción de la clave de Android tiene que incluir las dos SHA-1: la de `eas credentials` y la de Play Console → Integridad de la app → certificado de firma de la app. Si falta la de Play, el mapa falla solo en las compilaciones que instala Play y funciona en un APK instalado a mano.

## 2. Cuenta de revisión

En una máquina con la cadena de conexión de esa base, no en un log:

```bash
dotnet run --project apps/backend/src/Situ.Api -- store:seed-reviewer
```

En el VPS, con el entorno de la API ya cargado:

```bash
sudo bash -c 'set -a; source /etc/situ/api.env; set +a; runuser -u situ -- dotnet /opt/situ/current/api/Situ.Api.dll store:seed-reviewer'
```

Crea o rehace `revisor.tiendas@situ.test` / `revisor_tiendas`: adulto, correo confirmado, sin segundo factor. Imprime la contraseña una vez. No queda en el repositorio ni en un archivo de log del comando. Cópiala en los dos sitios:

- App Store Connect → la app → Información de la app → Notas, o el campo de usuario y contraseña de la revisión. Para TestFlight interno no hace falta enviarla a Apple todavía. Guárdala para cuando pidas la revisión de la ficha.
- Play Console → Política → Acceso de apps, si el formulario pide una cuenta. También en las instrucciones para el revisor de la pista, cuando exista.

Si la cuenta ya fue eliminada del todo, el comando se detiene. No intenta revivir una fila anonimizada.

## 3. Build

Desde `apps/mobile`, con la sesión de EAS del proyecto `faunerls.dev/situ`:

```bash
eas build --profile production --platform all
```

Eso sube el `buildNumber` y el `versionCode`. No publica solo. El perfil `preview` es distribución interna, por si quieres un binario antes de las tiendas.

## 4. TestFlight

1. En App Store Connect crea la app con el bundle `com.somossitu.app` si aún no existe. Copia el Apple ID numérico.
2. Cuando el build de iOS termine:

```bash
eas submit --platform ios --profile production --asc-app-id "$ASC_APP_ID"
```

3. Espera a que App Store Connect procese el build. En TestFlight, el grupo interno (usuarios de App Store Connect, hasta el límite de la cuenta) puede instalarlo sin revisión beta. La revisión de Apple hace falta para un grupo externo y para la ficha pública.
4. Responde el cifrado: no usa cifrado distinto del exento. La config ya lleva `ITSAppUsesNonExemptEncryption` en falso.

## 5. Play, primera vez y las siguientes

La primera versión de una app nueva en Play se sube a mano desde Play Console. `eas submit` funciona después de que la app y la primera versión existan.

1. Crea la app con el paquete `com.somossitu.app`.
2. Completa las declaraciones de la sección 6. Play no deja publicar en ninguna pista, tampoco la interna, sin la ficha, la clasificación de contenido y la política de privacidad en una URL pública.
3. En Probar y publicar → Pruebas → Prueba interna, crea la versión y sube el AAB que descargaste del build de EAS.
4. Añade la SHA-1 de Play a la clave de Maps, como dice la sección 1, y publica otra vez solo si el mapa de esa build sale en blanco. La primera instalación ya usa la firma de Play.
5. Desde la segunda versión:

```bash
eas submit --platform android --profile production
```

El perfil manda a la pista `internal` con estado `completed`. La cuenta de servicio sale de `GOOGLE_SERVICE_ACCOUNT_KEY_PATH`.

## 6. Declaraciones que Play pide antes de cualquier pista

- Ficha: textos y capturas de `docs/tiendas/ficha.md`, gráfico de funciones e ícono de 512.
- Política de privacidad: URL pública de la política, ya con el texto del abogado.
- Seguridad de los datos: la tabla de `docs/tiendas/privacidad-datos.md`.
- Anuncios: no. Compras dentro de la app: no.
- Clasificación de contenido: cuestionario de la ficha. Contenido generado, ubicación, adolescentes desde 14 con autorización.
- Público objetivo: 14 en adelante. No entrar a Diseñada para familias.
- Acceso a la app: cuenta de revisión, si la pide.
- Noticias, gobierno o COVID: no aplica.
- URL de eliminación de datos: `https://app.DOMINIO/cuenta/eliminar`.

## 7. Los 14 días y el paso a producción

Play, para una cuenta personal, pide una prueba cerrada con al menos 12 testers que hayan aceptado la invitación durante 14 días seguidos antes de dejar pedir acceso a producción.

1. Crea un grupo de correo de Google, o escribe de 12 a 15 correos, en Prueba cerrada → Testers. La pista interna no cuenta para esos 14 días: usa la pista cerrada cuando vayas a pedir producción. La pista interna sirve para el primer humo con el equipo.
2. Copia el enlace de inscripción que muestra la consola y mándaselo a esas personas. Tienen que abrir el enlace con la cuenta de Google que añadiste, aceptar ser tester e instalar la app desde Play.
3. En el panel de la pista cerrada, Play muestra cuántos testers están activos y cuántos días seguidos llevas. No reinicies la prueba ni quites a alguien por debajo de 12: el contador de 14 días seguidos vuelve a empezar.
4. Cuando lleve 14 días con 12 o más, aparece la opción de pedir acceso a producción. Responde el formulario con el mismo alcance de esta ficha. Después de la aprobación, la pista de producción se puede actualizar con otro `eas submit` cambiando la pista, o subiendo el AAB a mano.

TestFlight interno no tiene esa espera de 14 días. La ficha pública de Apple sí pasa por revisión.
