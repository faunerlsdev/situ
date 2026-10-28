# Prueba de transmisión en vivo

Esto no es el producto. No hay API, base de datos ni pantalla final. Sirve para comprobar que un teléfono puede publicar H.264/AAC a MediaMTX y que otro puede ver el HLS.

MediaMTX queda fijado en **v1.21.3** (25 de octubre de 2026). El binario se baja de GitHub y el checksum tiene que coincidir con `deploy/mediamtx/checksums.sha256` y con el `checksums.sha256` de esa release.

## Librería

Se evaluaron tres caminos:

| Librería | Licencia | Mantenimiento | Expo 58 / arquitectura nueva |
| --- | --- | --- | --- |
| `@api.video/react-native-livestream` | MIT | El paquete existe, pero en arquitectura nueva `startStreaming` no conecta y no dispara los callbacks. | No sirve. |
| `react-native-nodemediaclient` | La pila NodeMedia no es un módulo MIT de arquitectura nueva. | En Fabric el RTMP a veces sale, pero la vista previa queda negra. | No sirve. |
| `react-native-nitro-rtmp-publisher` 1.0.1 | MIT | Publicada en octubre de 2026. iOS usa HaishinKit, Android usa RootEncoder. Pide `react-native-nitro-modules` >= 0.37. | Hecha para Fabric y trae plugin de Expo. Probada por sus autores en Expo 57 / React Native 0.86. Esta app es Expo 58 / React Native 0.88, así que el enlace nativo todavía no se comprobó en un teléfono. |

WHIP con `react-native-webrtc` quedaría como reserva si el build nativo de Nitro falla en SDK 58. MediaMTX puede recibir WHIP, pero el teléfono tendría que atravesar NAT. En un VPS eso pide un TURN propio, puertos UDP abiertos y más latencia. RTMPS en el 1936 evita ese compromiso mientras la librería compile. No se abre WebRTC en la configuración de producción.

El plugin desactiva el servicio en primer plano de cámara y micrófono (`android.disableForegroundService`). La transmisión de esta prueba muere si la app pasa a segundo plano. La ubicación en segundo plano sigue bloqueada.

## Ajustes de video

`prepareVideo(1280, 720, 30, 2500000, 2, rotación)` y `prepareAudio(128000, 44100, true)`. Códecs H.264 y AAC. `streamMode` es `lowLatency`. MediaMTX arma HLS de baja latencia con segmentos de 1 segundo y partes de 200 ms, y alarga el segmento hasta incluir un keyframe. El piso esperado, sumando ese keyframe de 2 segundos y el buffer del reproductor, es de unos 3 a 6 segundos. No hay una medición real: no se publicó desde un teléfono. La clave de publicación no va en la URL: el teléfono llama `setAuthorization('situ', clave)` antes de conectar.

## Mac, sin TLS

En la raíz del repositorio:

```bash
bash deploy/mediamtx/dev-mac.sh
```

Eso baja el binario de macOS, comprueba el checksum y deja RTMP en `0.0.0.0:1935` y HLS en `0.0.0.0:8888`, sin autenticación. RTSP, SRT, WebRTC y MoQ quedan apagados. v1.21 enciende MoQ si no se dice lo contrario, y eso abriría 8892 y 8893. Ctrl-C lo detiene. No uses `mediamtx.dev.yml` en el servidor.

La IP de la Mac en la red local sale con `ipconfig getifaddr en0`. En el teléfono, en un build de desarrollo, Cuenta muestra "Prueba de transmisión". Pon esa IP, el nombre `poc`, deja la clave vacía y elige RTMP local.

Publicar: `rtmp://<ip-de-la-mac>:1935/poc`

Ver, en otro teléfono o en el mismo después de detener la cámara: `http://<ip-de-la-mac>:8888/poc/index.m3u8`

iOS ya permite red local. Android solo acepta HTTP claro si el cliente se compiló con `EXPO_PUBLIC_LIVE_POC=1`. Ese flag no va en el build de tienda.

Reconstruir el cliente nativo, desde `apps/mobile`:

```bash
EXPO_PUBLIC_LIVE_POC=1 npx expo prebuild --clean
EXPO_PUBLIC_LIVE_POC=1 npx expo run:ios
EXPO_PUBLIC_LIVE_POC=1 npx expo run:android
```

`run:ios` y `run:android` necesitan Xcode o Android SDK en esta máquina. Expo Go no incluye el módulo nativo.

## Servidor

El orden está en `docs/deploy.md`: DNS de `live.<dominio>`, nginx, y después:

```bash
sudo bash deploy/mediamtx/install.sh
bash deploy/mediamtx/check-capacity.sh
```

Producción:

- RTMPS estricto en `1936/tcp`. RTMP sin cifrar, RTSP, SRT y WebRTC apagados. La dirección RTMP queda en `127.0.0.1:11935` para no usar el `1935` del otro MediaMTX. El cifrado estricto no abre ese puerto. Ningún script toca ufw: si `1936/tcp` está cerrado, ábrelo tú.
- HLS de baja latencia solo en `127.0.0.1:18890`. nginx lo publica en `https://live.<dominio>/hls/` con un token de 120 segundos. El máximo aceptado es 180.
- API de control en `127.0.0.1:19998`.
- Grabación fMP4 en `/var/lib/situ/live`, segmentos de una hora, sin borrado automático.
- `situ-mediamtx` limita la memoria a 768 MB y la CPU a un núcleo. Los logs van a journald.

La autorización HTTP queda parametrizada en `LIVE_AUTH_URL` (`/etc/situ/live.env`). Hoy apunta a `http://127.0.0.1:5099/authorize`, un proceso temporal. Cuando exista la API, cambia esa URL a `http://127.0.0.1:15080/internal/live/authorize`, recarga MediaMTX y apaga el temporal:

```bash
sudo systemctl disable --now situ-live-auth.service
sudo systemctl reload situ-mediamtx.service
```

El temporal acepta `publish` solo si `password` es `LIVE_INGEST_SECRET` y el path tiene letras, números, guion o guion bajo. Acepta `read` solo desde `127.0.0.1`. nginx exige además el token y manda `Authorization: Bearer` con `HLS_CDN_SECRET` hacia MediaMTX. No se confía en `X-Forwarded-For`, para que una lectura externa no se haga pasar por localhost.

MediaMTX hace POST con este cuerpo. Una respuesta 2xx permite la acción. El campo `expireAt` es opcional y cierra la sesión al llegar esa hora:

```json
{
  "ip": "203.0.113.8",
  "user": "situ",
  "password": "secreto",
  "token": "",
  "action": "publish",
  "path": "poc",
  "protocol": "rtmp",
  "id": null,
  "query": "",
  "userAgent": ""
}
```

Token HLS, HMAC-SHA256 en hexadecimal de `expires.path` con `LIVE_HLS_SECRET`. `expires` es Unix en segundos. Ejemplo, sin usar un secreto real:

```bash
node deploy/mediamtx/token.mjs --secret "$LIVE_HLS_SECRET" --path poc --ttl 120
```

La URL queda `https://live.<dominio>/hls/poc/index.m3u8?token=<token>`. nginx deja una cookie `situ_live` para los segmentos siguientes. En el teléfono pega el token en el campo y marca RTMPS.

Cortar una publicación y ver espectadores, en el servidor:

```bash
curl -s http://127.0.0.1:19998/v3/rtmps/conns/list
curl -s -X POST http://127.0.0.1:19998/v3/rtmps/conns/kick/<id>
curl -s http://127.0.0.1:19998/v3/paths/get/poc
curl -s http://127.0.0.1:19998/v3/hls/sessions/list
```

`monitor.sh` avisa si `situ-mediamtx` está caído y si el disco de `/var/lib/situ/live` pasa de 70 % o de 85 %.

## Qué no se pudo verificar

- Publicar y reproducir desde un iPhone o un Android. No se midió la latencia de punta a punta, ni la batería, ni el comportamiento al bloquear la pantalla.
- El build nativo de Nitro sobre React Native 0.88.
- certbot y systemd en el VPS. Estos scripts no tocan ufw. `check-capacity.sh` lo ejecutas tú en el servidor.

## Preguntas para el backend

- El path público no puede ser el id del usuario. Un anónimo y un menor no pueden quedar identificables en la URL ni en los logs de nginx.
- La autorización de cada `publish` y cada `read` tiene que repetir amistad, bloqueo, visibilidad y la regla `CanStreamTo`. MediaMTX no conoce amigos.
- Un menor de 16 necesita la autorización de vivo del representante antes de que el endpoint devuelva 200.
- Quién firma el token HLS, cuánto vive y si `expireAt` de MediaMTX debe coincidir con el fin de la transmisión.
- La grabación del dominio prevé 60 días para la copia pública y 7 para la copia interna. MediaMTX no borra. El job de la aplicación tiene que hacerlo.
- El corte de un moderador es `POST /v3/rtmps/conns/kick/{id}`. Hay que guardar ese id al conectar.
- El conteo de espectadores sale de `GET /v3/paths/get/{name}` y de las sesiones HLS. Hace falta decidir cada cuánto se consulta, sin meter coordenadas ni la clave en logs.
- La web solo podrá reproducir el HLS con token. No publica.
- Antes de mandar la app a las tiendas hay que decidir si este módulo nativo sigue dentro del binario o se retira hasta el producto final.
