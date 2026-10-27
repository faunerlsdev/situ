# Ficha de las tiendas

Idioma de la ficha: español (Colombia). El idioma principal que pide App Store Connect en `eas.json` es `es-MX`, que es el español de Latinoamérica que ofrece Apple. El nombre de la app es SITU.

## Textos

| Campo | Texto |
| --- | --- |
| Nombre | SITU |
| Subtítulo (Apple, 30 caracteres) | Lo que pasa cerca de ti |
| Descripción corta (Play, 80 caracteres) | Reporta lo que pasa en tu zona y míralo en el mapa. |
| Palabras clave (Apple, 100 caracteres, sin el nombre) | mapa,reportes,ciudad,alertas,comunidad,tráfico,seguridad |
| Categoría | Apple: Navegación. Play: Mapas y navegación. |

Descripción larga:

SITU es un mapa para contar lo que está pasando en un lugar: un accidente, un hurto, un retén, una vía cerrada o basura acumulada. Abres el mapa, ves lo que hay cerca y, si estás ahí, publicas un reporte con foto o video.

Puedes publicar con tu nombre de usuario o como anónimo. Las demás personas no ven quién eres cuando eliges el anonimato. Puedes comentar, confirmar si el hecho sigue ahí y recibir un aviso cuando algo ocurre cerca o en tu ciudad.

Si un contenido no debería estar, lo denuncias. Si no quieres ver a alguien, lo bloqueas. Un menor de edad necesita la autorización de su representante para usar la cuenta. Desde los 14 años, con esa autorización. Puedes eliminar tu cuenta en la app o en la página pública de eliminación.

Esta versión no tiene anuncios ni compras.

## Notas para quien revisa

Cuenta de revisión: la crea el comando `store:seed-reviewer` descrito en `docs/tiendas/checklist.md`. Correo `revisor.tiendas@situ.test`, usuario `revisor_tiendas`, adulto, correo ya confirmado, sin segundo factor. La contraseña se imprime una sola vez en la consola.

Para ver un reporte no hace falta estar en la calle. La cuenta puede publicar desde el mapa. La ubicación se pide solo con la app abierta. No hay inicio de sesión con Apple, Google ni Facebook. No hay anuncios ni compras. La transmisión en vivo no está en esta versión.

La eliminación de la cuenta está en Mi cuenta. También está en `https://app.DOMINIO/cuenta/eliminar`.

## Clasificación de edad y contenido

Responde así:

- Contenido generado por usuarios: sí. Hay filtro de términos, denuncia, bloqueo y un plazo de respuesta visible.
- Ubicación: sí, solo mientras se usa la app. Precisa y aproximada.
- Menores: la cuenta se puede crear desde los 14 años. Por debajo de la mayoría de edad hace falta la autorización de un representante. No está dirigida a menores de 13 ni de 14.
- Violencia, accidentes y hechos de la calle: pueden aparecer en un reporte porque eso es lo que la gente publica. No es contenido gráfico propio de la app. Marca la frecuencia que ofrezca el cuestionario como ocasional, no como el tema principal de un juego.
- Anuncios: no.
- Compras: no. Los planes Free, Basic, Pro y Advanced existen en el producto, pero esta versión no vende nada dentro de la app. Cuando se cobre, hay que cambiar la declaración de compras, el formulario de pagos y, si aplica, el de suscripciones.
- Datos de contacto y contraseña: sí, cuenta propia.

En Apple, el cuestionario de contenido generado sin un filtro previo total suele dar 17+. La edad mínima de la cuenta sigue siendo 14 con representante. La clasificación de la ficha y la edad mínima de registro no son el mismo número: la ficha describe el contenido que otra persona puede publicar; el registro impide cuentas por debajo de 14.

## Público y política de familias de Play

El público es de 14 años en adelante. En el formulario de público objetivo marca los grupos de adolescentes y adultos. No marques menores de 13.

No entres al programa Diseñada para familias. SITU no es una app infantil: tiene mapa, ubicación y reportes escritos por otras personas. Incluir adolescentes obliga a tener la moderación de contenido generado que ya está en la app, y a no mostrar anuncios. No declara la app como dirigida principalmente a niños.

## Anuncios y compras

Declara sin anuncios y sin compras en esta etapa. Cuando haya un plan de pago dentro de la app, actualiza: compras en la ficha de Play, compras dentro de la app en Apple, y el formulario de Seguridad de los datos si el pago añade datos de facturación. El número de tarjeta no lo guarda SITU. Cuando haya anuncios, actualiza la declaración de anuncios, el identificador de publicidad y `NSPrivacyTracking`. Hoy el permiso de anuncios está bloqueado a propósito.

## Capturas

Hacen falta capturas reales del build de producción, en el tema oscuro, sin datos de una persona real. Usa la cuenta de revisión. No subas un mockup de otra marca.

Orden y texto sugerido, el mismo para las dos tiendas:

1. Mapa con reportes cerca. Texto: «Mira lo que pasa cerca de ti».
2. Tarjeta de un reporte con denunciar y bloquear. Texto: «Denuncia o bloquea lo que no debería estar».
3. Formulario de un reporte con foto. Texto: «Cuenta lo que estás viendo».
4. Registro con la casilla de términos. Texto: «Aceptas los términos antes de crear la cuenta».
5. Mi cuenta, con soporte y eliminar cuenta. Texto: «Elimina tu cuenta cuando quieras».

Tamaños:

| Tienda | Pieza | Tamaño |
| --- | --- | --- |
| Apple | iPhone 6.9 pulgadas, obligatorio para la ficha | 1320 × 2868 px, o 1290 × 2796 px |
| Apple | iPad 13 pulgadas. La app declara `supportsTablet`, así que la ficha los pide | 2064 × 2752 px |
| Play | Teléfono, vertical. Mínimo dos | 1080 × 1920 px |
| Play | Gráfico de funciones, obligatorio antes de cualquier pista | 1024 × 500 px, JPG o PNG de 24 bits, sin transparencia |
| Play | Ícono de la ficha | 512 × 512 px, PNG de 32 bits |
| Apple | Ícono de la ficha | 1024 × 1024 px, PNG sin canal alfa |

## Ícono y splash que hay hoy

No cumplen. No los reemplacé por un dibujo nuevo.

| Archivo | Medida actual | Canal alfa | Lo que pide la tienda |
| --- | --- | --- | --- |
| `apps/mobile/assets/images/brand/situ-isotipo-naranja-claro.png` | 83 × 110 | Sí | Ícono iOS 1024 × 1024 sin alfa. Primer plano del ícono adaptable de Android, recomendado 1024 × 1024 |
| `apps/mobile/assets/images/brand/situ-logo-vertical-naranja.png` | 124 × 135 | Sí | Splash legible. El fondo de la config ya es `#1C1C1C` |

Exporta el mismo isotipo naranja claro, sin redibujar la marca, a 1024 × 1024. Para iOS, aplástalo sobre `#1C1C1C` y quita el alfa. Para Android, deja el isotipo en el primer plano y el fondo `#1C1C1C` que ya está en `app.json`. El splash puede ser el wordmark vertical centrado, exportado más grande, con el mismo fondo. El gráfico de funciones de Play, 1024 × 500, también sale de esa marca: isotipo y la palabra SITU sobre `#1C1C1C`, sin texto de más.

Hasta que esos archivos no estén en esas medidas, no lances `eas build`: la ficha de Apple rechaza el ícono con alfa o por debajo de 1024, y en el teléfono el ícono actual se ve pequeño.

`npx expo-doctor` ignora solo el error `NOT_SQUARE` de ese isotipo. La marca no se redibuja para cuadrarla. El resto de la verificación de schema sigue activo. La excepción está en `apps/mobile/package.json` (`expo.doctor.ignoreAssetErrorCodes`) y el filtro, en `apps/mobile/scripts/patch-expo-doctor.mjs`, porque expo-doctor 1.21.5 no tiene un interruptor para un solo campo.
