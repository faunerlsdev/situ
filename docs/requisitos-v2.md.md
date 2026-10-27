# SITU - Requisitos funcionales v2

Sep 18, 2026 · @Fauner Lara Silva

## 1. Resumen y decisiones cerradas

SITU es una red social informativa con mapa estilo Waze, disponible en app móvil (iOS y Android) y en web, que lanza en Colombia con arquitectura multi-país. Este v2 consolida v1 y v1.1 e incorpora tus últimas respuestas. Cada requisito tiene un ID (RF-XXX-NN) para convertirlo en historia de usuario, y lo marcado *(sugerido)* no viene de ti y necesita tu aprobación.

| Tema | Decisión | Efecto principal |
| --- | --- | --- |
| Edad mínima | 14 años | Fecha de nacimiento obligatoria y autorización del representante legal para todo menor de 18 (sección 3) |
| Perfil en el mapa | Solo perfiles públicos | Los perfiles privados nunca aparecen en el mapa general |
| Ubicación de perfil público | Tiempo real por defecto, configurable | Si se desactiva, solo se ve la ubicación de su último reporte |
| Compartir con amigos | Interruptor independiente | La ubicación solo se ve entre amigos, incluso con perfil privado |
| Anonimato | Reportes, comentarios y transmisiones en vivo | Ícono y nombre genéricos ("Situero anónimo") y "Miembro desde" |
| Transmisión guardada como reporte | Se elimina a los 2 meses | No pasa al plazo de 1 año de los reportes |
| Retención | Transmisiones 2 meses, reportes 1 año | Ambos plazos configurables desde el admin |
| Web | Por ahora solo ver transmisiones | Por ahora, para transmitir es obligatorio usar la app móvil; la función web se agregará más adelante |
| Alcance | Lanzamiento en Colombia, listo para otros países | Catálogo geográfico, idiomas, monedas y textos legales por país |

## 2. Roles y superficies

SITU tiene cuatro superficies y siete roles; las capacidades de cada rol dependen del plan y de la superficie.

| Superficie | Quién la usa | Qué permite |
| --- | --- | --- |
| App móvil (iOS y Android) | Todos los usuarios | Experiencia completa, incluida la transmisión en vivo y las alertas en segundo plano |
| Web de usuario | Todos los usuarios | Mapa, reportes, amigos, perfil, ajustes y suscripción; por ahora **solo ver** transmisiones |
| Dashboard web | Plan Avanzada (empresas y medios) | Monitoreo, alertas, estadísticas y exportación |
| Panel de administración | Equipo de SITU | Configuración, catálogos, moderación y métricas |

| Rol | Alcance |
| --- | --- |
| Visitante | Ve el mapa de forma limitada; no publica ni interactúa |
| Usuario gratuito | Todas las funciones básicas, con publicidad |
| Suscriptor Básica | Sin publicidad |
| Suscriptor Pro | Sin publicidad, noticias importantes y primicia |
| Suscriptor Avanzada | Dashboard para empresas y medios, con cuentas multiusuario |
| Moderador | Cola de denuncias, retiro de contenido y sanciones |
| Administrador | Configuración global, roles, planes y catálogos |

## 3. Registro, acceso y seguridad

Para registrarse hay que tener al menos 14 años, confirmar el correo, aceptar los textos legales y, si es menor de 18, contar con la autorización de su representante legal; la cuenta se protege con bloqueo de 15 minutos por intentos fallidos y límite de dispositivos.

**Registro y edad**

- **RF-AUT-01:** El registro pide correo, contraseña, nombre de usuario único, **fecha de nacimiento**, país, departamento (o estado o provincia según el país) y ciudad. La ciudad queda como *ciudad de interés*.
- **RF-AUT-02:** **Edad mínima de 14 años.** Quien no la cumpla no puede crear la cuenta y ve un mensaje neutro. Se impide el reintento inmediato con otra fecha en el mismo dispositivo *(sugerido)*.
- **RF-AUT-03:** La edad mínima es un parámetro por país, porque la ley cambia según el territorio. Inicia en 14 para Colombia *(sugerido)*.
- **RF-AUT-04:** La fecha de nacimiento no se puede editar después del registro. Los cambios pasan por soporte *(sugerido)*.
- **RF-AUT-05:** Al cumplir 18 años, la cuenta pasa automáticamente al régimen de adulto y se avisa al usuario de las funciones que se habilitan *(sugerido)*.
- **RF-AUT-06:** Aceptación de Términos y Política de Tratamiento de Datos en el registro, guardando **fecha y versión aceptada**. Ambos son enlaces externos configurables por país e idioma.

**Acceso y protección de la cuenta**

- **RF-AUT-07:** **Confirmación de correo** con enlace o código que vence. Sin confirmar, el usuario puede ver pero no publicar ni transmitir.
- **RF-AUT-08:** **Recuperación de contraseña** con token de un solo uso y vigencia limitada.
- **RF-AUT-09:** **Bloqueo de 15 minutos** tras N intentos fallidos (sugerido: 5). Un flujo exitoso de "Olvidé mi contraseña" desbloquea antes. N y los minutos son configurables.
- **RF-AUT-10:** **Límite de dispositivos** por cuenta (N configurable, quizá distinto por plan). La web cuenta como un dispositivo. Si se supera, se pide cerrar sesión en otro. Pantalla "Mis dispositivos" con cierre remoto.
- **RF-AUT-11:** Cierre de sesión y **eliminación de cuenta desde la app**, requisito de Apple y Google.
- **RF-AUT-12:** Doble factor opcional y aviso por correo ante inicio de sesión en un dispositivo nuevo *(sugerido)*.

**Reglas para usuarios de 14 a 17 años**

La normativa colombiana de datos sí aplica a SITU y es estricta con los menores: la autorización del representante legal deja de ser una recomendación y pasa a ser **obligatoria para todo usuario menor de 18**. Lo que sigue es un resumen de las fuentes revisadas, no asesoría legal.

| Norma | Qué exige | Efecto en SITU |
| --- | --- | --- |
| Ley 1581 de 2012, art. 7 | Prevalecen los derechos de niños y adolescentes; como regla general se proscribe tratar sus datos, salvo los de naturaleza pública | SITU no puede tratar datos de menores sin cumplir los requisitos de abajo |
| Corte Constitucional (control de la ley) | La prohibición no es absoluta: se pueden tratar datos de menores si responde a su interés superior y se aseguran sus derechos prevalentes | Es la base que permite tener usuarios menores |
| Decreto 1377 de 2013, art. 12 (hoy Decreto 1074 de 2015, art. 2.2.2.25.2.9) | El tratamiento debe respetar el interés superior y los derechos fundamentales del menor. Cumplido eso, el representante legal autoriza de forma previa, expresa e informada, después de que el menor haya sido escuchado; su opinión se valora según su madurez y capacidad de entender | Flujo de autorización del representante, con escucha del menor |
| Ley 1581, art. 12 y Decreto 1377, art. 8 | Conservar prueba de la autorización | Guardar la evidencia de cada autorización |
| Ley 1098 de 2006, art. 3 | Es sujeto de derechos toda persona menor de 18 años (niño: 0 a 12; adolescente: 12 a 18) | Los usuarios de 14 a 17 siguen siendo menores para efectos de datos |

Tres conclusiones para el diseño:

1. **Los 14 años no son un umbral legal de datos.** En las fuentes revisadas no aparece una edad desde la cual el menor pueda autorizar por sí solo el tratamiento de sus datos; los 14 años son una decisión de producto de SITU. Mientras un abogado no confirme otra cosa, todo menor de 18 necesita a su representante legal.
2. **La SIC ya exige mecanismos a las plataformas.** Ordenó a TikTok implementar un procedimiento apropiado, efectivo y demostrable para cumplir esos requisitos especiales (Resolución 67413 de 2021).
3. **Hay proyectos de ley en trámite que pueden cambiar la edad mínima.** Aún no son ley, y conviene verificar su estado antes del lanzamiento:
   - Un proyecto del representante Germán Rozo regularía el registro y el acceso de menores de 14 años a redes sociales, con excepciones para plataformas diseñadas para ellos con control parental. Exigiría verificar la identidad de **todos** los usuarios, explicar los algoritmos y publicar informes semestrales, con multas de la SIC de hasta 5.000 salarios mínimos. Recibió ponencia positiva y pasa a primer debate en la Comisión Sexta de la Cámara (Blu Radio, 9 de octubre de 2026).
   - Otro proyecto, de Jesús Lorduy y Enrique Cabrales, plantea que menores de 16 no usen redes sociales sin verificación de edad (El Universal, 22 de julio de 2026).

**Requisitos para menores de 14 a 17 años**

- **RF-MIN-01:** **Autorización del representante legal obligatoria antes de activar la cuenta.** El menor solo escribe el correo de su representante. Ese correo trae un enlace con la información del menor y el aviso completo de privacidad (qué datos, para qué, derechos). En el enlace, el representante completa su nombre, número de documento y correo, verifica ese correo y autoriza de forma expresa. Al terminar, SITU le envía un correo de constancia con una firma digital interna. Sin autorización la cuenta no se activa.
- **RF-MIN-02:** **Escucha del menor.** Antes de enviar la solicitud, el menor ve una pantalla en lenguaje claro y adaptado a su edad sobre qué datos se recogen y para qué, y confirma su opinión. Se guarda el registro.
- **RF-MIN-03:** **Prueba de la autorización:** fecha, hora, versión del texto, medio, nombre, número de documento y correo verificado del representante, y la firma digital interna del correo de constancia (un abogado debe confirmar que esta firma basta como prueba, según la Ley 527 de 1999), conservados mientras la cuenta exista y el plazo legal posterior.
- **RF-MIN-04:** **Perfil privado obligatorio.** No pueden hacerlo público ni aparecer en el mapa general.
- **RF-MIN-05:** **Compartir ubicación con amigos** desactivado por defecto. Activarlo exige una autorización específica del representante, porque es una finalidad adicional *(sugerido)*. Recordatorio periódico de que está activo.
- **RF-MIN-06:** **Minimización:** solo se piden los datos estrictamente necesarios para el servicio. **Sin publicidad personalizada ni perfilamiento comercial**, y sin categorías o contenido para adultos.
- **RF-MIN-07:** **Derechos del titular ejercidos por el representante:** consultar, rectificar, revocar la autorización y pedir la supresión de los datos, con un enlace de gestión en el correo y un canal de soporte *(sugerido)*.
- **RF-MIN-08:** Si el representante no responde en 7 días (plazo confirmado), la solicitud vence y se eliminan los datos del menor *(sugerido)*.
- **RF-MIN-09:** **Transmisiones y anonimato:** los menores solo transmiten con visibilidad para **amigos aceptados**, y **pueden hacerlo de forma anónima, como en reportes y comentarios, con la identidad guardada internamente. Como el rostro y la voz pueden identificar al menor, en una transmisión anónima el difuminado de rostro y la voz modificada vienen activados por defecto (sugerido). Los menores de 16 años necesitan además un permiso específico de su representante para transmitir, como exige Meta (sugerido)** *(confirmado; pendiente de revisión legal)*.
- **RF-MIN-10:** **Imágenes de menores en contenido de terceros:** al subir fotos o videos se avisa que la imagen de un menor es un dato personal protegido, con denuncia prioritaria y retiro rápido de contenido con menores *(sugerido)*.
- **RF-MIN-11:** **Mayoría de edad:** al cumplir 18 años el usuario acepta de nuevo, directamente, los Términos y la Política de Datos *(sugerido)*.
- **RF-MIN-12:** El **módulo de verificación de edad e identidad** (documento, representante, reconocimiento) queda desacoplado y activable por país. Por ahora la edad mínima sigue en 14 años, sin un umbral nuevo, pero el parámetro queda listo para subirla (por ejemplo a 16) si se aprueban los proyectos de ley *(sugerido)*.

Fuentes: [Concepto SIC sobre datos de menores](https://sedeelectronica.sic.gov.co/sites/default/files/normatividad/022019/Rad18_18267888PDG.PDF) · [Decreto 1377 de 2013](https://www1.funcionpublica.gov.co/eva/gestornormativo/norma.php?i=53646) · [Ley 1581 de 2012](https://www.alcaldiabogota.gov.co/sisjur/normas/Norma1.jsp?i=49981) · [Decreto 1356 de 2018 (cita la Ley 1098)](https://www.dmsjuridica.com/buscador_20179478954/legislacion/decretos/?p=52125) · [Resolución SIC 67413 de 2021](https://sedeelectronica.sic.gov.co/sites/default/files/estados/072022/RE67413-2021.pdf) · [Blu Radio, proyecto menores de 14](https://www.bluradio.com/nacion/proyecto-de-ley-busca-restringir-el-acceso-y-el-registro-para-menores-de-14-anos-a-redes-sociales-rg10) · [El Universal, proyecto menores de 16](https://www.eluniversal.com.co/colombia/2026/07/22/proyecto-de-ley-en-colombia-busca-restringir-acceso-a-redes-sociales-a-menores-de-16-anos/)

Referencia sobre transmisiones de menores (RF-MIN-09): Meta anunció en abril de 2025 que los menores de 16 años necesitan permiso de sus padres para transmitir en vivo ([CTV News](https://www.ctvnews.ca/sci-tech/article/kids-under-16-will-no-longer-be-allowed-to-livestream-on-instagram-without-parental-consent/)); YouTube subió a 16 años la edad mínima para transmitir y TikTok la fija en 18 ([TecMundo](https://www.tecmundo.com.br/redes-sociais/405373-youtube-vai-proibir-lives-de-menores-de-16-anos-a-partir-de-julho.htm)).

## 4. Perfil, privacidad y ubicación

Solo los perfiles públicos aparecen en el mapa general; compartir ubicación con amigos es una función aparte, que también funciona con perfil privado.

**Perfil**

- **RF-PER-01:** Avatar genérico por defecto. El usuario puede elegir un ícono de una **galería administrada** o subir su **propia foto**, con recorte y validación de tamaño.
- **RF-PER-02:** Editar nombre, usuario, biografía corta, ciudad de interés y contraseña.
- **RF-PER-03:** **Perfil privado por defecto.** En Configuración puede pasar a **público**: entonces su ícono se ve en el mapa y cualquiera puede abrir su perfil.
- **RF-PER-04:** Un perfil privado solo muestra sus datos y reportes a amigos aceptados, y **nunca aparece en el mapa general**.
- **RF-PER-05:** Bloquear y denunciar usuarios.
- **RF-PER-06:** Al pasar el perfil a público se muestra una **pantalla de consentimiento explícito** que dice que la ubicación será visible y cómo desactivarla. Se guarda fecha y versión.

**Los dos interruptores de ubicación**

Son independientes y ninguno depende del otro.

1. **Mostrar mi ubicación en tiempo real** (solo perfil público). Activado por defecto al hacerse público. Si se desactiva, el avatar público queda fijo en la ubicación de su **último reporte**.
2. **Compartir mi ubicación con amigos** (cualquier perfil). Desactivado por defecto. Si se activa, la ubicación en tiempo real se ve **únicamente entre amigos**.

| Configuración del usuario | Desconocidos ven en el mapa | Amigos ven en el mapa |
| --- | --- | --- |
| Privado, sin compartir | Nada | Nada |
| Privado, compartiendo con amigos | Nada | Tiempo real |
| Público, tiempo real activado | Tiempo real | Tiempo real |
| Público, tiempo real desactivado | Ubicación del último reporte | Ubicación del último reporte |
| Público, tiempo real desactivado, compartiendo con amigos | Ubicación del último reporte | Tiempo real |

**Requisitos de compartir con amigos**

- **RF-UBI-01:** Interruptor "Compartir mi ubicación con amigos", desactivado por defecto, con **consentimiento explícito** la primera vez (fecha y versión guardadas).
- **RF-UBI-02:** La ubicación solo se muestra a **amigos aceptados**. Ningún otro usuario la ve.
- **RF-UBI-03:** Se puede compartir con todos los amigos o con una **lista elegida** *(sugerido)*.
- **RF-UBI-04:** Duración a elegir: 1 hora, 8 horas o hasta desactivar *(sugerido)*.
- **RF-UBI-05:** Mientras se comparte, hay un **indicador permanente** en el mapa y en el menú.
- **RF-UBI-06:** Al eliminar o bloquear a un amigo, deja de ver la ubicación **de inmediato**.
- **RF-UBI-07:** La ubicación se emite **solo desde la app móvil**, con frecuencia configurable (por ejemplo cada 15 a 30 segundos). En segundo plano exige el permiso de ubicación "siempre". La web puede mostrar la ubicación de los amigos, pero no emitirla *(sugerido)*.
- **RF-UBI-08:** Si el usuario pierde señal por más de N minutos, sus amigos ven "última vez visto hace X" en lugar de un punto en movimiento *(sugerido)*.

**Protecciones adicionales** *(sugeridas)*

La ubicación de personas es la función de mayor riesgo de acoso de la app, por eso propongo:

- **RF-UBI-09:** **Modo invisible temporal** (1 h, 8 h, hasta mañana) que oculta la ubicación sin cambiar el perfil ni desactivar los interruptores.
- **RF-UBI-10:** **Zonas de privacidad** (casa, trabajo) donde la ubicación no se muestra a nadie.
- **RF-UBI-11:** Si un usuario con perfil público publica o transmite de forma **anónima**, su avatar se oculta del mapa mientras dure, para que nadie lo identifique por coincidencia de lugar.

## 5. Mapa, menú y panel "Quiero chismosear"

La pantalla principal es un mapa a pantalla completa con menú de usuario y un panel inferior deslizable, como Waze.

Waze no ofrece su mapa para incrustarlo en otras apps. Para una experiencia equivalente se usa Mapbox, Google Maps SDK o MapLibre con OpenStreetMap, y se puede añadir un botón "Abrir ruta en Waze o Google Maps" por enlace profundo.

**Mapa**

- **RF-MAP-01:** Al abrir la app se ve el mapa centrado en la ubicación del usuario, con permiso de GPS.
- **RF-MAP-02:** Botón **"¿Dónde estoy?"** que recentra el mapa y activa el seguimiento.
- **RF-MAP-03:** Los **reportes activos** aparecen como íconos por categoría apenas se abre el mapa, cargados por zona visible y agrupados (clustering) al alejar el zoom.
- **RF-MAP-04:** Al tocar un ícono se abre una **tarjeta inferior** con categoría, hora, distancia, autor (o "Situero anónimo"), vista previa y acciones: "Sigue ahí" / "Ya no está", comentar, compartir y denunciar.
- **RF-MAP-05:** Las **transmisiones en vivo** se muestran con un ícono de **cámara roja con pulso tipo radar**. Al tocarlo se abre el video.
- **RF-MAP-06:** Se muestran los avatares de **usuarios con perfil público** y los de **amigos que comparten ubicación** (sección 4). Los perfiles privados que no comparten no aparecen.
- **RF-MAP-07:** Filtros por categoría, por tiempo (última hora, hoy) y por tipo (solo amigos, solo en vivo).
- **RF-MAP-08:** Cada reporte tiene **vigencia (TTL) por categoría** y sale del mapa al vencer o por votos de "Ya no está".
- **RF-MAP-09:** Modo día y noche, y buscador de direcciones o lugares.
- **RF-MAP-10:** Los íconos se actualizan en **tiempo real** sin recargar.

**Menú y ajustes**

- **RF-MEN-01:** El **ícono de usuario** (genérico, de galería o foto propia) en la esquina del mapa abre el menú.
- **RF-MEN-02:** Opciones: mi perfil y ajustes de cuenta, mis reportes, amigos e invitaciones, **configuración de notificaciones**, privacidad y ubicación, mi suscripción, **Política de Tratamiento de Datos** y **Términos de Uso** (enlaces externos configurables desde el admin), ayuda y soporte, cerrar sesión.

**Panel inferior**

- **RF-PAN-01:** Barra deslizable con el texto **"Quiero chismosear"** o **"Quiero reportar algo"** y un **micrófono**. El texto es configurable remotamente.
- **RF-PAN-02:** Al deslizar hacia arriba se despliega una **cuadrícula de botones** con las categorías: ícono, nombre y descripción corta (Accidente de tránsito, Hurto, Puesto de control o retén, Basuras, etc.).
- **RF-PAN-03:** El micrófono permite **dictado por voz** para buscar una categoría o redactar el texto del reporte.
- **RF-PAN-04:** Tres estados: colapsado, medio y expandido. Incluye el botón **"Transmitir en vivo"**, visible **solo en la app móvil**.

## 6. Categorías, reportes y anonimato

El administrador define las categorías sin actualizar la app, los reportes son publicaciones con multimedia y ubicación, y el anonimato aplica a reportes, comentarios y transmisiones en vivo.

**Catálogo dinámico de categorías**

- **RF-CAT-01:** CRUD de categorías en el panel admin, sin necesidad de publicar una nueva versión de la app.
- **RF-CAT-02:** Cada categoría guarda nombre, descripción corta, **ícono** del botón, ícono del mapa, color, orden, estado activo o inactivo, **vigencia (TTL)** en el mapa y **peso de importancia** para el algoritmo de noticias.
- **RF-CAT-03:** Además *(sugerido)*: subcategorías, visibilidad por país o ciudad, y si la categoría exige foto o video.
- **RF-CAT-04:** La app consume el catálogo por API con caché y control de versiones.

**Reportes**

- **RF-REP-01:** Tras elegir la categoría se abre un formulario tipo publicación de red social con **texto, varias imágenes y videos** (límites de peso y duración, con compresión).
- **RF-REP-02:** La **ubicación** se toma por GPS y puede ajustarse moviendo el pin o buscando una dirección.
- **RF-REP-03:** Visibilidad: público en el mapa o solo amigos. Los usuarios de 14 a 17 años están sujetos a la sección 3.
- **RF-REP-04:** Subida en segundo plano con reintentos y barra de progreso.
- **RF-REP-05:** Editar y eliminar los reportes propios.
- **RF-REP-06:** Interacciones: reaccionar, comentar, compartir, **confirmar o desmentir** ("Sigue ahí" / "Ya no está") y denunciar.

**Anonimato (reportes, comentarios y transmisiones)**

- **RF-ANO-01:** Hay un interruptor **"Publicar como anónimo"** al crear un reporte, al escribir un comentario y al iniciar una transmisión en vivo.
- **RF-ANO-02:** Lo anónimo muestra un **ícono genérico**, un nombre genérico configurable desde el admin (por ejemplo **"Situero anónimo"**) y **"Miembro desde \[mes y año\]"**. Solo mes y año, no el día exacto.
- **RF-ANO-03:** No hay enlace al perfil real, botón de agregar amigo ni pin del usuario asociado.
- **RF-ANO-04:** En una misma publicación, cada persona anónima se distingue con un número ("Situero anónimo 1", "2") para poder seguir la conversación. La numeración no se repite entre publicaciones, para que nadie pueda rastrear a una persona *(sugerido)*.
- **RF-ANO-05:** En una **transmisión anónima**, el aviso previo dice claramente que la cara y la voz pueden identificar a la persona. Se ofrecen dos opciones: desenfoque de rostro y voz modificada *(sugerido, puede pasar a una fase posterior)*.
- **RF-ANO-06:** Internamente **se conserva la identidad del autor** para moderación, reputación del algoritmo y requerimientos legales. Los Términos lo dicen expresamente: el anonimato es frente a otros usuarios, no frente a SITU ni a las autoridades competentes.
- **RF-ANO-07:** Lo anónimo **no dispara** la notificación "tu amigo publicó", porque revelaría quién es. Sí dispara las notificaciones por ciudad y por 1 km. El autor de una publicación recibe la notificación normal cuando alguien comenta, aunque sea de forma anónima.
- **RF-ANO-08:** El autor ve sus contenidos anónimos en "Mis reportes" con la insignia "Anónimo" y puede eliminarlos.
- **RF-ANO-09:** Quien abuse del anonimato de forma reiterada puede perder la opción por decisión de moderación *(sugerido)*.

## 7. Transmisiones en vivo

Solo se puede transmitir desde la app móvil; la web permite ver, comentar y reaccionar, pero por ahora no transmitir.

- **RF-LIV-01:** Un usuario habilitado (correo confirmado, edad cumplida y sin sanciones) inicia una transmisión con título, categoría y ubicación asociada.
- **RF-LIV-02:** **Transmitir exige la app móvil** (iOS o Android). En la web no aparece el botón de transmitir; en su lugar se muestra "Para transmitir, abre la app" con enlace o código QR. La restricción es un parámetro de configuración, para habilitar la transmisión desde la web en el futuro sin rehacer la arquitectura.
- **RF-LIV-03:** El servidor solo entrega credenciales de transmisión a sesiones de la app móvil verificada, para que la restricción no dependa solo de la interfaz *(sugerido, con verificación de integridad de la app)*.
- **RF-LIV-04:** Mientras dura, aparece en el mapa el **ícono de cámara roja con pulso tipo radar**. Al terminar, desaparece.
- **RF-LIV-05:** Los espectadores, desde la app o la web, ven un contador y pueden comentar y reaccionar en vivo. Los comentarios pueden ser anónimos.
- **RF-LIV-06:** Al iniciar se notifica a amigos y a usuarios cercanos. Una transmisión anónima no notifica a los amigos (RF-ANO-07).
- **RF-LIV-07:** Al finalizar, el usuario decide entre **guardarla como reporte** o descartarla.
- **RF-LIV-08:** Una transmisión guardada como reporte **se elimina automáticamente a los 2 meses**, no a 1 año, junto con su video y su publicación. El plazo es configurable.
- **RF-LIV-09:** Moderadores pueden **cortar** una transmisión y los usuarios pueden denunciarla.
- **RF-LIV-10:** No se muestra publicidad durante una transmisión.
- **RF-LIV-11:** Los menores de 18 solo transmiten con visibilidad para amigos *(confirmado, ver RF-MIN-09)*.
- **RF-LIV-12:** Se conserva una **copia interna cifrada** por un plazo corto (por ejemplo 7 días, configurable), aunque el usuario la descarte, accesible solo para moderación y requerimientos legales. Debe decirse en los Términos *(sugerido)*.

## 8. Red de amigos y notificaciones

La red de amigos alimenta dos funciones clave: las notificaciones de reportes de amigos y la ubicación compartida entre amigos.

**Amigos**

- **RF-AMI-01:** Buscar personas por **nombre de usuario**.
- **RF-AMI-02:** **Invitar por correo.** Si la persona no tiene cuenta, recibe un enlace de registro que luego genera la solicitud.
- **RF-AMI-03:** Solicitudes de amistad: enviar, aceptar, rechazar y cancelar. Eliminar amigos y bloquear.
- **RF-AMI-04:** Lista de amigos con acceso a su perfil y reportes, según su privacidad, y a su ubicación si la comparten (RF-UBI-01).

**Reglas de disparo de notificaciones**

| Disparador | Condición | Excepción |
| --- | --- | --- |
| Ciudad de interés | Se crea un reporte en la ciudad de interés del usuario | Ninguna |
| Proximidad | Reporte a 1 km o menos de la ubicación actual, aunque esté fuera de su ciudad | Requiere permiso de ubicación en segundo plano; funciona de forma confiable solo en la app móvil |
| Amigo | Un amigo crea un reporte | No se dispara si el reporte es anónimo |
| Transmisión en vivo | Cercana o de un amigo | No se avisa a los amigos si es anónima |
| Noticia importante | Según el plan (sección 9) | Pro recibe la primicia antes que los demás |

**Configuración y comportamiento**

- **RF-NOT-01:** Activar o desactivar cada tipo de notificación.
- **RF-NOT-02:** Ciudad de interés y ciudades adicionales *(sugerido)*.
- **RF-NOT-03:** Radio de proximidad (por defecto 1 km) y categorías de interés.
- **RF-NOT-04:** Horario de silencio.
- **RF-NOT-05:** Canales: push móvil, notificación web y centro de notificaciones dentro de la app. Correo opcional.
- **RF-NOT-06:** Control anti-spam: deduplicación, agrupación y límite de frecuencia.
- **RF-NOT-07:** Las notificaciones de proximidad para usuarios de 14 a 17 años respetan las reglas de la sección 3.

## 9. Publicidad, suscripciones y noticias importantes

Todos acceden gratis con publicidad; quien no quiera anuncios paga una suscripción, y el plan Pro suma un algoritmo que detecta las noticias importantes y da la primicia.

| Plan | Qué incluye |
| --- | --- |
| Gratis | Todas las funciones básicas, con publicidad |
| Básica | Sin publicidad |
| Pro | Sin publicidad, **noticias importantes** y **primicia** (recibe la alerta antes que los demás) |
| Avanzada | Para empresas y medios: dashboard de monitoreo, alertas y análisis |

**Publicidad (plan gratuito)**

- **RF-MON-01:** Anuncios mediante una red publicitaria estándar (Google AdMob o Ad Manager): banners, anuncios nativos entre reportes e intersticiales moderados. La web usa su equivalente.
- **RF-MON-02:** Pines patrocinados en el mapa para negocios locales, como Waze *(sugerido)*.
- **RF-MON-03:** Consentimiento explícito para anuncios personalizados. Nunca se muestra publicidad durante una transmisión ni en una alerta crítica. Los usuarios de 14 a 17 años solo ven anuncios no personalizados.

**Suscripciones**

- **RF-SUS-01:** Compra, renovación automática, cancelación, cambio de plan y recuperación de compras.
- **RF-SUS-02:** Compras dentro de la app (Apple y Google) en móvil y pasarela de pago en la web (Wompi, PayU, Stripe u otra).
- **RF-SUS-03:** Precios, planes y beneficios administrables desde el panel, con precio y moneda por país.
- **RF-SUS-04:** Facturación electrónica e IVA según la normativa colombiana, con reglas por país configurables.

**Algoritmo de noticias importantes (Pro)**

- **RF-ALG-01:** Cada reporte recibe un **puntaje de importancia** que combina el peso de la categoría, el número de confirmaciones, los reportes cercanos en tiempo y espacio sobre el mismo hecho, la reputación del autor, la presencia de multimedia o de una transmisión con espectadores, la velocidad de crecimiento de las interacciones y la verificación por un moderador.
- **RF-ALG-02:** Si el puntaje supera un **umbral configurable**, el reporte pasa a "Noticia importante".
- **RF-ALG-03:** **Primicia:** los suscriptores Pro reciben la alerta de inmediato. Básica y Gratis la reciben después de un retraso configurable (por ejemplo 5 a 10 minutos) o no la reciben.
- **RF-ALG-04:** Umbrales y pesos se ajustan desde el admin. Se recomienda empezar con reglas heurísticas y evolucionar a aprendizaje automático.
- **RF-ALG-05:** Los reportes anónimos entran al algoritmo con la reputación real del autor, que se conserva internamente (RF-ANO-06) *(sugerido)*.
- **RF-ALG-06:** Sistema de reputación y detección de reportes falsos o duplicados *(sugerido)*.

## 10. Dashboard Avanzada, administración y moderación

El dashboard sirve a empresas y medios, y el panel admin permite configurar SITU y moderar contenido sin publicar nuevas versiones de la app.

**Dashboard web del plan Avanzada**

- **RF-AVA-01:** Mapa en tiempo real de reportes y transmisiones (solo ver, sin transmitir).
- **RF-AVA-02:** Filtros por zona, categoría, tiempo e importancia.
- **RF-AVA-03:** Alertas configurables por zona, categoría o palabras clave.
- **RF-AVA-04:** Vista de noticias importantes y primicias.
- **RF-AVA-05:** Estadísticas y exportación (CSV o PDF).
- **RF-AVA-06:** Cuentas multiusuario por organización, con roles.
- **RF-AVA-07:** API y webhooks *(sugerido)*. Los Términos deben definir los derechos de uso del contenido de terceros.
- **RF-AVA-08:** El dashboard **no expone la identidad** de los autores anónimos ni la ubicación de personas *(sugerido)*.

**Panel de administración**

- **RF-ADM-01:** Gestión de categorías, avatares predefinidos, catálogo geográfico, planes, textos remotos y enlaces legales por país e idioma.
- **RF-ADM-02:** Parámetros globales: radio de notificación, intentos de login, minutos de bloqueo, límite de dispositivos, edad mínima por país, TTL de categorías, umbrales del algoritmo y plazos de retención.
- **RF-ADM-03:** Roles y permisos (RBAC) y **bitácora de auditoría**.
- **RF-ADM-04:** Métricas: usuarios activos, reportes por categoría, ingresos y conversión de planes.
- **RF-ADM-05:** Preclasificación automática de contenido ofensivo, desnudez o violencia *(sugerido)*.

**Moderación**

- **RF-MOD-01:** **Cola de denuncias** para reportes, comentarios, transmisiones y usuarios. Sanciones: advertencia, suspensión, bloqueo y retiro de contenido.
- **RF-MOD-02:** Los moderadores pueden ver la identidad real detrás de contenido anónimo, solo con permiso específico y con registro en la bitácora de auditoría.
- **RF-MOD-03:** Proceso de retiro de contenido (*notice and takedown*) con formulario visible para terceros afectados por un "chisme".

## 11. Alcance global, web y retención de datos

SITU lanza en Colombia pero nace multi-país, tiene una web casi igual a la app (sin transmitir) y elimina automáticamente los contenidos vencidos.

**Alcance global**

- **RF-GLO-01:** El **catálogo geográfico** (país, región, ciudad) es administrable. El nombre del nivel intermedio es configurable por país (departamento, estado, provincia).
- **RF-GLO-02:** Cada país está **habilitado o no**. Al lanzamiento solo Colombia; el registro solo permite países habilitados.
- **RF-GLO-03:** **Internacionalización:** textos, fechas, zonas horarias y unidades (km o millas) por idioma y país. Inicia en español (es-CO), con estructura lista para otros idiomas.
- **RF-GLO-04:** Precios y moneda de suscripciones, pasarelas de pago e impuestos por país.
- **RF-GLO-05:** Términos y Política de Datos por país e idioma (enlaces externos).
- **RF-GLO-06:** Categorías, TTL y parámetros sobrescribibles por país o ciudad.
- **RF-GLO-07:** Revisión de cumplimiento legal antes de habilitar un país (por ejemplo GDPR en Europa, con su edad mínima propia) *(sugerido)*.

**Web de usuario**

- **RF-WEB-01:** Paridad funcional con la app: mapa estilo Waze, panel "Quiero chismosear / reportar", categorías dinámicas, reportes con multimedia, anonimato, amigos, perfil, ajustes y suscripción.
- **RF-WEB-02:** **La web solo permite ver transmisiones en vivo**, con comentarios y reacciones. Por ahora no permite transmitir (RF-LIV-02); esa función se agregará en una fase futura.
- **RF-WEB-03:** Sesión unificada con la app y sujeta al límite de dispositivos.
- **RF-WEB-04:** Notificaciones push web y centro de notificaciones.
- **RF-WEB-05:** Publicidad para el plan gratuito y cobro con pasarela web.
- **RF-WEB-06:** Diseño responsivo, con vista de escritorio (mapa amplio y panel lateral) y vista móvil.
- **Limitación técnica:** el navegador solo conoce la ubicación con la pestaña abierta. Las alertas de 1 km en segundo plano y la ubicación en tiempo real funcionan de forma confiable solo en la app móvil, y la interfaz web debe decirlo.

**Retención de datos**

| Contenido | Plazo | Notas |
| --- | --- | --- |
| Reportes (texto, multimedia y comentarios) | 1 año desde su creación | Configurable |
| Transmisión guardada como reporte | 2 meses desde que finaliza | Se elimina el video y la publicación asociada; configurable |
| Copia interna de transmisiones para moderación | 7 días | Solo moderación y requerimientos legales; configurable *(sugerido)* |
| Estadísticas agregadas y anónimas | Más tiempo que lo anterior | Para el dashboard, sin datos personales *(sugerido)* |

- **RF-RET-01:** Los plazos son **parámetros configurables** desde el admin, con posibilidad de sobrescribirlos por país o plan.
- **RF-RET-02:** Se distingue entre **vencido en el mapa** (por TTL de categoría, en horas o días) y **eliminado** (por retención). Un reporte vencido sigue en "Mis reportes" hasta cumplir su plazo.
- **RF-RET-03:** Un proceso automático de purga elimina también los archivos de imagen y video del almacenamiento, no solo los registros.
- **RF-RET-04:** El contenido con denuncia abierta, orden de autoridad o investigación **no se purga** hasta resolverse *(sugerido)*.
- **RF-RET-05:** Antes de eliminar, se avisa al autor con un enlace para **descargar** su contenido *(sugerido)*.
- **RF-RET-06:** Al eliminar la cuenta, se borran o anonimizan los datos personales según la Ley 1581 y las políticas de las tiendas.

## 12. Requisitos no funcionales, legal y fases

Los mayores riesgos de SITU son legales y de seguridad de las personas (menores, ubicación y terceros expuestos en "chismes"), no técnicos; por eso conviene revisar con un abogado antes del lanzamiento.

**No funcionales**

- **Plataformas:** app móvil iOS y Android (Flutter o React Native), web de usuario y dashboard (React o Next.js) y un backend común.
- **Tiempo real:** WebSockets o similar, con consultas geoespaciales (PostGIS o Redis GEO).
- **Video en vivo:** un servicio como LiveKit, Agora, Mux o Amazon IVS. La web solo reproduce.
- **Seguridad:** cifrado en tránsito y en reposo, contraseñas con hash seguro, protección contra abuso de la API y separación estricta entre identidad real y contenido anónimo.
- **Rendimiento:** tolerancia a mala conectividad, subida en segundo plano y consumo moderado de batería en las geocercas.
- **Accesibilidad e internacionalización** desde el inicio.

**Legal (Colombia)**

- **Ley 1581 de 2012 (Habeas Data):** registro de consentimientos, derechos de acceso, actualización y supresión, y política de tratamiento visible.
- **Menores de 18 años:** su tratamiento exige respetar el interés superior del menor y contar con la autorización de su representante legal (sección 3). Hay proyectos de ley en trámite que podrían subir la edad mínima o exigir verificación de identidad. Todo esto debe validarse con un abogado antes del lanzamiento.
- **Ubicación en tiempo real:** datos de alto riesgo; requieren consentimiento explícito, finalidad clara y opción de retiro inmediato.
- **Contenido sobre terceros:** riesgo de injuria, calumnia y exposición de datos personales. Se necesitan términos claros, denuncia visible y proceso de retiro rápido.
- **Tiendas de apps:** exigen bloquear y denunciar usuarios, eliminar la cuenta desde la app y moderación activa del contenido generado por usuarios.

**Fases de desarrollo**

| Fase | Contenido |
| --- | --- |
| 1. MVP | Registro, seguridad y edad mínima con autorización del representante legal para menores; perfil; mapa; panel inferior; categorías dinámicas; reportes con multimedia; anonimato en reportes y comentarios; notificaciones por ciudad y 1 km; web completa sin transmisión; panel admin y moderación; arquitectura multi-país |
| 2. Social y vivo | Red de amigos, perfil público con ubicación en tiempo real, compartir ubicación con amigos, transmisión en vivo solo desde la app (con anonimato), notificaciones de amigos y publicidad |
| 3. Monetización | Suscripciones Básica y Pro, algoritmo de noticias importantes y primicia |
| 4. Empresas | Dashboard de Avanzada, alertas, exportación y API |
| 5. Futuro | Transmisión en vivo desde la web |

## 13. Decisiones cerradas y pendientes

Con tus últimas respuestas quedan cerradas las decisiones de producto; solo falta que un abogado revise el flujo antes del lanzamiento.

| Tema | Decisión | Dónde está |
| --- | --- | --- |
| Menores y Habeas Data | La ley aplica: autorización del representante legal obligatoria para todo menor de 18, con escucha del menor | Sección 3, RF-MIN-01 a 03 |
| Verificación del representante | El menor solo escribe el correo del representante; en el enlace este completa nombre, documento y correo, verifica el correo y recibe una constancia con firma digital interna | RF-MIN-01, RF-MIN-03 |
| Plazo del representante | 7 días para responder | RF-MIN-08 |
| Transmisiones de menores | Permitidas, solo a amigos aceptados y con opción de anonimato. Meta también permite que los menores transmitan, con permiso de los padres por debajo de 16 años | RF-MIN-09 |
| Edad mínima | 14 años por ahora, sin nuevo umbral; el parámetro queda listo para subirla si se aprueba el proyecto de ley | RF-AUT-03, RF-MIN-12 |
| Alcance de la web | Se puede ver y comentar; lo único que no se puede es transmitir, por ahora | RF-WEB-02, RF-LIV-02 |
| Ubicación entre amigos desde la web | La app emite la ubicación; la web muestra la de los amigos, pero no la emite | RF-UBI-07 |
| Identidad detrás de lo anónimo | Solo moderadores con permiso especial y bitácora de auditoría; se entrega a las autoridades ante requerimiento legal | RF-ANO-06, RF-MOD-02 |

**Pendientes**

1. **Revisión legal:** que un abogado de Habeas Data confirme que la firma digital interna basta como prueba de la autorización (Ley 527 de 1999), el flujo de menores y las transmisiones anónimas de menores.
2. **Proyectos de ley:** seguir su trámite y activar el módulo de verificación de edad e identidad si se aprueban (RF-MIN-12).

## Decisiones posteriores a la v2

Fecha: 2026-10-27.

- **Teléfono obligatorio en el registro.** El teléfono se conserva en el registro de backend, móvil y web. Se guarda en formato internacional E.164, con selector de indicativo y Colombia +57 por defecto. No se exige unicidad y la API no dice si un número ya está registrado, para no permitir enumerar cuentas. Es dato personal: entra en la exportación, se borra al eliminar la cuenta, no va en logs ni en la bitácora, y en el panel solo se ve enmascarado con el permiso `users.view_email`, dejando registro de la consulta sin el número.
- **Teléfono de menores.** La clave `registration.phone_required_for_minors` nace en true. Si se pone en false, el teléfono es opcional solo para menores. Queda como pregunta abierta para el abogado por RF-MIN-06 (minimización).
- **Anonimato encendido por defecto para todos.** Las cuentas nuevas, adultas y menores, nacen con la preferencia de publicar como anónimo. No se pisan las elecciones de las cuentas que ya existen. El interruptor "Publicar como anónimo" se ve en el reporte y en el comentario, con el estado "Se publicará como Situero anónimo" o "Se publicará con tu nombre de usuario". El aviso de primer uso se guarda con fecha y versión. Un menor no puede apagarlo en una publicación pública.
- **Pestañas Mensajes y Chisme ocultas.** La bandera `features.social_tabs` nace apagada. Las pantallas siguen en el código y no aparecen en la navegación.
- **Mapa limitado para visitantes.** Sin cuenta, la web usa `GET /api/v1/public/reports/map`: solo reportes públicos de adultos de las últimas 24 horas, máximo 50 pines, sin autor, texto, medios ni conteos. La ubicación se redondea a 3 decimales de grado, unos 111 metros. Un token no amplía esa respuesta.
- **Formulario público de retiro.** La página `/retiro-de-contenido` usa `POST /api/v1/takedown-requests`, con captcha Cloudflare Turnstile verificado en el servidor. Si el verificador no responde, no se guarda nada. La respuesta es neutra, con número de seguimiento y plazo de 15 días.
