/** Product live rules. The server still decides who can watch and when a stream ends. */

export const LIVE_TITLE_MAX = 80;
export const LIVE_MAX_MINUTES = 30;
export const RENEW_LEAD_MS = 30_000;
export const CONNECT_GRACE_MS = 12_000;
export const RECONNECT_GRACE_MS = 8_000;
export const WARN_BEFORE_END_MS = 60_000;

export const ANONYMITY_REMINDER = 'Tu transmisión es anónima, pero el video puede mostrar rostros y lugares';

export type AirState = 'connecting' | 'live' | 'reconnecting' | 'ended';
export type PublisherEvent = 'connecting' | 'live' | 'reconnecting' | 'failed';
export type WatchPhase = 'loading' | 'playing' | 'ended' | 'timeout' | 'forbidden' | 'offline' | 'full';

export const PLAYBACK_STALL_MS = 10_000;
export const PLAYBACK_START_MS = 20_000;
export const ROSTER_POLL_MS = 45_000;
export const ROSTER_DEBOUNCE_MS = 1_000;

export type LiveStartState = { enabled: boolean; reason: string | null };

/**
 * The option stays on screen. A missing gate disables it and supplies the Spanish reason.
 * Age is separate from canPublish: canPublish is only User.Status == Active.
 */
export function liveStartState(input: {
  profileKnown: boolean;
  isMinor: boolean;
  canPublish: boolean;
  publishBlockedReason: string | null;
  platform: 'ios' | 'android' | 'web';
  publisherAvailable: boolean;
  quotaFull: boolean;
}): LiveStartState {
  if (!input.profileKnown) {
    return { enabled: false, reason: 'Revisando si puedes transmitir.' };
  }
  if (input.isMinor) {
    return { enabled: false, reason: 'En esta versión un menor no puede transmitir.' };
  }
  if (!input.canPublish) {
    return { enabled: false, reason: liveAccountReason(input.publishBlockedReason) };
  }
  if (!input.publisherAvailable) {
    return { enabled: false, reason: 'Transmitir no está disponible en este dispositivo.' };
  }
  if (input.quotaFull) {
    return { enabled: false, reason: 'Ahora mismo no hay cupo para otra transmisión.' };
  }
  return { enabled: true, reason: null };
}

function liveAccountReason(reason: string | null): string {
  switch (reason) {
    case 'suspended':
      return 'Tu cuenta está suspendida. No puedes transmitir.';
    case 'banned':
      return 'Tu cuenta está bloqueada. No puedes transmitir.';
    case 'email_not_confirmed':
      return 'Confirma tu correo para transmitir.';
    case 'guardian_consent_pending':
      return 'Falta la autorización de tu representante.';
    case 'deletion_requested':
      return 'No puedes transmitir mientras eliminas la cuenta.';
    case 'deleted':
      return 'Esta cuenta ya no puede transmitir.';
    default:
      return 'No tienes permiso para publicar.';
  }
}

/** The phone only publishes the URL the API returned, and only when it is RTMPS on 1936. */
export function publishUrlError(url: string): string | null {
  let parsed: URL;
  try {
    parsed = new URL(url);
  } catch {
    return 'La dirección de publicación no es válida.';
  }
  const id = parsed.pathname.replace(/^\/+/, '');
  if (parsed.protocol !== 'rtmps:' || parsed.port !== '1936' || !/^[a-f0-9]{32}$/.test(id)) {
    return parsed.protocol !== 'rtmps:' || parsed.port !== '1936'
      ? 'La transmisión solo sale por RTMPS en el puerto seguro.'
      : 'La dirección de publicación no es válida.';
  }
  if (parsed.searchParams.get('user') !== 'situ' || !(parsed.searchParams.get('pass') ?? '')) {
    return 'La dirección de publicación no trae la clave de un solo uso.';
  }
  return null;
}

/** Safe fields for a dev log. The path and the key stay out. */
export function publishTargetLog(url: string): { scheme: string; host: string; port: string } | null {
  try {
    const parsed = new URL(url);
    return { scheme: parsed.protocol.replace(':', ''), host: parsed.hostname, port: parsed.port };
  } catch {
    return null;
  }
}

/**
 * How each native publisher will split one URL. Neither side receives the app
 * and the stream name as separate arguments. iOS (HaishinKit) cuts at the last
 * slash after `://`: the left side is the connect URL and the right side is the
 * stream name. Android (RootEncoder) treats a single path segment as the app
 * and leaves the stream name empty, and it refuses to open the socket only when
 * the app is empty. Booleans only: the path itself is a secret.
 */
export function rtmpTargetShape(url: string): {
  scheme: string;
  host: string;
  port: string;
  tls: boolean;
  iosConnectHasApp: boolean;
  iosStreamPresent: boolean;
  androidAppPresent: boolean;
  androidStreamPresent: boolean;
  hasUser: boolean;
  hasPass: boolean;
} | null {
  let parsed: URL;
  try {
    parsed = new URL(url);
  } catch {
    return null;
  }
  const scheme = parsed.protocol.replace(':', '').toLowerCase();
  const path = parsed.pathname.replace(/^\/+/, '');
  const slash = path.indexOf('/');
  const iosStream = slash === -1 ? path : path.slice(slash + 1);
  const iosApp = slash === -1 ? '' : path.slice(0, slash);
  const cuts: number[] = [];
  for (let index = 0; index < path.length; index += 1) {
    if (path[index] === '/') {
      cuts.push(index);
    }
  }
  const second = cuts[1];
  const androidApp = cuts.length === 0 ? path : path.slice(0, second ?? cuts[0]);
  const androidStream = path.startsWith(androidApp) ? path.slice(androidApp.length).replace(/^\//, '') : '';
  return {
    scheme,
    host: parsed.hostname,
    port: parsed.port,
    tls: scheme === 'rtmps',
    iosConnectHasApp: iosApp.length > 0,
    iosStreamPresent: iosStream.length > 0,
    androidAppPresent: androidApp.length > 0,
    androidStreamPresent: androidStream.length > 0,
    hasUser: parsed.searchParams.get('user') === 'situ',
    hasPass: (parsed.searchParams.get('pass') ?? '').length > 0,
  };
}

const publishId = /^[a-f0-9]{32}$/;

/**
 * URL handed to the native publisher. MediaMTX reads user and pass only from the
 * RTMP URL query, then the path is tcUrl's path plus the publish name.
 * iOS (HaishinKit) keeps the query on the publish name, so the canonical URL
 * already becomes path {id}. Android (RootEncoder) treats one path segment as
 * the app and strips a single leading '?'. An empty path whose query starts
 * with the id makes its tcUrl carry the query and leaves the stream name empty,
 * which MediaMTX resolves to the same path.
 */
export function nativePublishUrl(url: string, platform: 'ios' | 'android'): string {
  const parsed = new URL(url);
  const id = parsed.pathname.replace(/^\/+/, '').split('/').filter(Boolean)[0] ?? '';
  const user = parsed.searchParams.get('user') ?? '';
  const pass = parsed.searchParams.get('pass') ?? '';
  const query = `user=${encodeURIComponent(user)}&pass=${encodeURIComponent(pass)}`;
  const origin = `${parsed.protocol}//${parsed.hostname}:${parsed.port}`;
  if (platform === 'android') {
    return `${origin}/?${id}?${query}`;
  }
  return `${origin}/${id}?${query}`;
}

export function mediamtxPublishTarget(url: string, platform: 'ios' | 'android'): { path: string; user: string; pass: string } {
  if (platform === 'android') {
    return joinRtmp(androidTcUrl(url), androidStreamName(url));
  }
  const parts = iosSplit(url);
  return joinRtmp(parts.connect, parts.streamKey);
}

function iosSplit(url: string): { connect: string; streamKey: string } {
  const queryAt = url.indexOf('?');
  const pathPart = queryAt === -1 ? url : url.slice(0, queryAt);
  const queryPart = queryAt === -1 ? '' : url.slice(queryAt);
  const scheme = pathPart.indexOf('://');
  const start = scheme === -1 ? 0 : scheme + 3;
  const slash = pathPart.lastIndexOf('/');
  if (slash < start) {
    return { connect: pathPart, streamKey: queryPart };
  }
  return { connect: pathPart.slice(0, slash), streamKey: pathPart.slice(slash + 1) + queryPart };
}

function androidTcUrl(url: string): string {
  const parsed = new URL(url);
  const { app } = androidNames(parsed);
  const port = parsed.port ? `:${parsed.port}` : '';
  return `${parsed.protocol}//${parsed.hostname}${port}${app ? `/${app}` : ''}`;
}

function androidStreamName(url: string): string {
  return androidNames(new URL(url)).stream;
}

function androidNames(parsed: URL): { app: string; stream: string } {
  const path = decodeURIComponent(parsed.pathname.replace(/^\/+/, ''));
  const query = parsed.search.startsWith('?') ? decodeURIComponent(parsed.search.slice(1)) : '';
  const source = path.length === 0 ? query : path;
  const cuts: number[] = [];
  for (let index = 0; index < source.length; index += 1) {
    if (source[index] === '/') {
      cuts.push(index);
    }
  }
  const second = cuts[1];
  const app = cuts.length === 0 ? source : source.slice(0, second ?? cuts[0]);
  const combined = `${path}${query ? `?${query}` : ''}`;
  const full = combined.startsWith('?') ? combined.slice(1) : combined;
  let stream = full.startsWith(app) ? full.slice(app.length) : full;
  if (stream.startsWith('/')) {
    stream = stream.slice(1);
  }
  if (stream.startsWith('?')) {
    stream = stream.slice(1);
  }
  return { app, stream };
}

function joinRtmp(connect: string, streamKey: string): { path: string; user: string; pass: string } {
  const base = new URL(connect);
  if (streamKey.length === 0 || (streamKey.startsWith('?') && connect.endsWith(streamKey))) {
    return {
      path: base.pathname.replace(/^\/+|\/+$/g, ''),
      user: base.searchParams.get('user') ?? '',
      pass: base.searchParams.get('pass') ?? '',
    };
  }
  const queryAt = streamKey.indexOf('?');
  const name = queryAt === -1 ? streamKey : streamKey.slice(0, queryAt);
  const extra = queryAt === -1 ? '' : streamKey.slice(queryAt + 1);
  const params = new URLSearchParams(base.search.replace(/^\?/, ''));
  if (extra.length > 0) {
    const more = new URLSearchParams(extra);
    for (const [key, value] of more) {
      params.set(key, value);
    }
  }
  const path = `${base.pathname.replace(/\/$/, '')}/${name}`.replace(/^\/+/, '').replace(/\/+$/, '');
  return { path, user: params.get('user') ?? '', pass: params.get('pass') ?? '' };
}

export function publishPathIsOpaque(path: string): boolean {
  return publishId.test(path);
}

export function scrubLiveDetail(value: string): string {
  return value
    .replace(/[a-z][a-z0-9+.-]*:\/\/\S+/gi, '[url]')
    .replace(/\b[0-9a-f]{16,}\b/gi, '[secret]');
}

export function publisherFailureCopy(event: string, message: string): string {
  const text = `${event} ${message}`.toLowerCase();
  if (text.includes('auth') || text.includes('publishbadname') || text.includes('not authorized')) {
    return 'El servidor rechazó la clave de publicación. Hay que empezar de nuevo: esa clave solo sirve una vez.';
  }
  if (text.includes('malformed') || text.includes('appname/streamname')) {
    return 'La dirección de publicación no trae aplicación y nombre de stream, y el publicador no abre el saludo RTMP.';
  }
  if (text.includes('tls') || text.includes('ssl') || text.includes('handshake') || text.includes('certificate')) {
    return 'No se pudo abrir la conexión segura RTMPS.';
  }
  if (text.includes('timed out') || text.includes('timeout')) {
    return 'El servidor no confirmó la transmisión a tiempo.';
  }
  if (text.includes('network') || text.includes('connection-lost') || text.includes('offline')) {
    return 'Se cortó la red antes de confirmar la transmisión.';
  }
  if (text.includes('refused') || text.includes('unreachable') || text.includes('failed to connect')) {
    return 'No se pudo llegar al servidor de transmisión.';
  }
  return 'No se pudo conectar. Cerramos la transmisión para no dejarla abierta.';
}

export function titleError(title: string): string | null {
  const trimmed = title.trim();
  if (trimmed.length === 0) {
    return 'Escribe un título.';
  }
  if (trimmed.length > LIVE_TITLE_MAX) {
    return 'El título puede tener hasta 80 caracteres.';
  }
  return null;
}

export const LIVE_SHARE_NOTE = 'Si la agregas, el mapa muestra un punto aproximado: unos 300 metros si es pública y unos 25 metros si es solo para amigos.';
export const LIVE_WITHOUT_PLACE_NOTE = 'Sin ubicación, la transmisión no se avisará por cercanía.';

export function liveShareNote(share: boolean, visibility: 'Friends' | 'Public'): string | null {
  if (share) {
    return LIVE_SHARE_NOTE;
  }
  if (visibility === 'Public') {
    return LIVE_WITHOUT_PLACE_NOTE;
  }
  return null;
}

export function visibilityLabel(value: string): string {
  return value === 'Friends' || value === 'FriendsOnly' ? 'Solo amigos' : 'Público';
}

export function airLabel(state: AirState): string {
  switch (state) {
    case 'connecting':
      return 'Conectando';
    case 'live':
      return 'Al aire';
    case 'reconnecting':
      return 'Reconectando';
    case 'ended':
      return 'Terminada';
  }
}

export function formatDuration(elapsedMs: number): string {
  const total = Math.max(0, Math.floor(elapsedMs / 1000));
  const minutes = Math.floor(total / 60);
  const seconds = total % 60;
  return `${minutes}:${seconds.toString().padStart(2, '0')}`;
}

export function minutesWarning(elapsedMs: number, maxMinutes = LIVE_MAX_MINUTES, warnBeforeMs = WARN_BEFORE_END_MS): 'none' | 'soon' | 'over' {
  const limit = maxMinutes * 60_000;
  if (elapsedMs >= limit) {
    return 'over';
  }
  if (elapsedMs >= limit - warnBeforeMs) {
    return 'soon';
  }
  return 'none';
}

export function reduceAir(state: AirState, event: PublisherEvent | 'give_up' | 'stop'): AirState {
  if (state === 'ended' || event === 'give_up' || event === 'stop') {
    return 'ended';
  }
  if (event === 'live') {
    return 'live';
  }
  if (event === 'reconnecting' || event === 'failed') {
    return 'reconnecting';
  }
  return state === 'live' ? 'reconnecting' : 'connecting';
}

export function shouldGiveUp(input: { state: AirState; sinceMs: number; nowMs: number }): boolean {
  if (input.state === 'ended' || input.state === 'live') {
    return false;
  }
  const grace = input.state === 'connecting' ? CONNECT_GRACE_MS : RECONNECT_GRACE_MS;
  return input.nowMs - input.sinceMs >= grace;
}

export function shouldEndInBackground(state: AirState, appState: string): boolean {
  return appState === 'background' && state !== 'ended';
}

export function renewalDelayMs(expiresAtMs: number, nowMs: number, leadMs = RENEW_LEAD_MS): number {
  if (!Number.isFinite(expiresAtMs) || !Number.isFinite(nowMs)) {
    return leadMs;
  }
  return Math.max(0, expiresAtMs - leadMs - nowMs);
}

export function shouldRenewPlayback(expiresAtMs: number, nowMs: number, leadMs = RENEW_LEAD_MS): boolean {
  return nowMs >= expiresAtMs - leadMs;
}

export function watchFailure(code: string | undefined, apiCode: string | undefined): Exclude<WatchPhase, 'loading' | 'playing' | 'timeout'> | 'unknown' {
  if (code === 'NETWORK' || code === 'TIMEOUT') {
    return 'offline';
  }
  if (apiCode === 'live.viewers_full') {
    return 'full';
  }
  if (apiCode === 'live.not_found' || code === 'NOT_FOUND') {
    return 'ended';
  }
  if (apiCode === 'live.forbidden' || apiCode === 'live.unauthorized' || code === 'FORBIDDEN' || code === 'UNAUTHORIZED') {
    return 'forbidden';
  }
  return 'unknown';
}

export type PlayerFailure = {
  message: string | null;
  domain: string | null;
  code: string | null;
};

/** Reads an expo-video error. Domain and code are present only when the native event includes them. */
export function readPlayerError(error: unknown): PlayerFailure {
  if (!error || typeof error !== 'object') {
    return { message: null, domain: null, code: null };
  }
  const record = error as Record<string, unknown>;
  const message = typeof record.message === 'string' ? scrubLiveDetail(record.message) : null;
  const domain = typeof record.domain === 'string' ? record.domain : null;
  const codeValue = record.code;
  const code = typeof codeValue === 'number' || typeof codeValue === 'string' ? String(codeValue) : null;
  return { message, domain, code };
}

export function playerFailureCopy(error: PlayerFailure): string {
  const detail = [error.domain, error.code].filter((part) => part && part.length > 0).join(' ');
  if (detail) {
    return `No se pudo reproducir la transmisión (${detail}).`;
  }
  return 'No se pudo reproducir la transmisión.';
}

export function playerStatusCopy(status: string): string {
  switch (status) {
    case 'readyToPlay':
      return 'Al aire';
    case 'error':
      return 'No se pudo reproducir la transmisión.';
    default:
      return 'Cargando la transmisión…';
  }
}

/** Relative URIs from a playlist, in order. The caller resolves them against the playlist URL. */
export function hlsProbeTargets(playlist: string, limit = 4): string[] {
  const targets: string[] = [];
  for (const line of playlist.split('\n')) {
    const mapped = /URI="([^"]+)"/.exec(line);
    const uri = mapped?.[1] ?? (line.length > 0 && !line.startsWith('#') ? line.trim() : '');
    if (!uri) {
      continue;
    }
    targets.push(uri);
    if (targets.length >= limit) {
      break;
    }
  }
  return targets;
}

export function watchCopy(phase: WatchPhase): string {
  switch (phase) {
    case 'loading':
      return 'Cargando la transmisión…';
    case 'playing':
      return 'Al aire';
    case 'ended':
      return 'Esta transmisión terminó.';
    case 'timeout':
      return 'La transmisión no arrancó. Vuelve a intentar.';
    case 'forbidden':
      return 'No tienes permiso para ver esta transmisión.';
    case 'offline':
      return 'Sin conexión. La imagen se queda en el último momento.';
    case 'full':
      return 'Esta transmisión ya no admite más personas.';
  }
}

export type PlaybackCue = 'none' | 'stall' | 'start' | 'error';

export type LiveNotice = { id: string; change: string };

/** Hub payload for one stream. Accepts the camelCase and PascalCase shapes SignalR may send. */
export function readLiveNotice(payload: unknown): LiveNotice | null {
  if (!payload || typeof payload !== 'object') {
    return null;
  }
  const record = payload as Record<string, unknown>;
  const id = typeof record.publicId === 'string' ? record.publicId : typeof record.PublicId === 'string' ? record.PublicId : '';
  const change = typeof record.change === 'string' ? record.change : typeof record.Change === 'string' ? record.Change : '';
  if (!id || !change) {
    return null;
  }
  return { id, change };
}

/**
 * Whether the viewer should leave the player.
 * A hub "ended" or a watch that says the stream is gone wins.
 * Offline stays offline.
 * Stall, a player error, or the 20s start cap ask for the card when it is not loaded yet.
 */
export function playbackEnd(input: {
  offline: boolean;
  signalEnded: boolean;
  watchResult: 'ended' | 'offline' | null;
  cue: PlaybackCue;
  detailStatus: string | null;
}): 'ended' | 'timeout' | 'offline' | 'keep' | 'ask' {
  if (input.signalEnded || input.watchResult === 'ended') {
    return 'ended';
  }
  if (input.offline || input.watchResult === 'offline') {
    return 'offline';
  }
  if (input.cue === 'none') {
    return 'keep';
  }
  if (input.detailStatus === null) {
    return 'ask';
  }
  if (input.detailStatus !== 'Live') {
    return 'ended';
  }
  if (input.cue === 'start') {
    return 'timeout';
  }
  return 'keep';
}

export function isVisibleLive(stream: { status?: string }): boolean {
  return stream.status === undefined || stream.status === 'Live';
}

export function visibleLiveCount(streams: readonly { status?: string }[]): number {
  return streams.filter((item) => isVisibleLive(item)).length;
}

/** Backup poll only while the app is in front and the hub socket is down. */
export function shouldPollLiveRoster(foreground: boolean, hubConnected: boolean): boolean {
  return foreground && !hubConnected;
}

export function locatedStreams<T extends { latitude: number | null; longitude: number | null }>(
  streams: readonly T[],
): Array<T & { latitude: number; longitude: number }> {
  return streams.filter((item): item is T & { latitude: number; longitude: number } =>
    typeof item.latitude === 'number' && Number.isFinite(item.latitude)
    && typeof item.longitude === 'number' && Number.isFinite(item.longitude));
}

export function liveFailureCopy(apiCode: string | undefined, detail: string | undefined): string {
  const spoken = detail?.trim();
  if (spoken) {
    return spoken;
  }
  switch (apiCode) {
    case 'live.minors_cannot_stream':
      return 'En esta versión un menor no puede transmitir.';
    case 'live.already_live':
      return 'Ya tienes una transmisión activa.';
    case 'live.limit_concurrent':
      return 'Ahora mismo no hay cupo para otra transmisión.';
    case 'live.viewers_full':
      return 'Esta transmisión ya no admite más personas.';
    case 'live.not_found':
      return 'No encontramos esa transmisión.';
    case 'live.forbidden':
      return 'No puedes ver transmisiones.';
    default:
      return 'No se pudo completar la transmisión.';
  }
}
