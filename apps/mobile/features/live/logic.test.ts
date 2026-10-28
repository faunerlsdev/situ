import { describe, expect, it } from 'vitest';

import { liveIdleMode, liveRecordMode } from './ios-audio';
import { clearBroadcast, holdBroadcast, readBroadcast } from './session';
import {
  ANONYMITY_REMINDER,
  CONNECT_GRACE_MS,
  LIVE_MAX_MINUTES,
  LIVE_TITLE_MAX,
  RECONNECT_GRACE_MS,
  RENEW_LEAD_MS,
  airLabel,
  liveStartState,
  mediamtxPublishTarget,
  nativePublishUrl,
  publishTargetLog,
  rtmpTargetShape,
  publishUrlError,
  publisherFailureCopy,
  scrubLiveDetail,
  formatDuration,
  liveFailureCopy,
  locatedStreams,
  minutesWarning,
  reduceAir,
  renewalDelayMs,
  shouldEndInBackground,
  shouldGiveUp,
  shouldRenewPlayback,
  titleError,
  liveShareNote,
  visibilityLabel,
  hlsProbeTargets,
  playerFailureCopy,
  playerStatusCopy,
  readPlayerError,
  PLAYBACK_STALL_MS,
  PLAYBACK_START_MS,
  ROSTER_POLL_MS,
  playbackEnd,
  readLiveNotice,
  shouldPollLiveRoster,
  visibleLiveCount,
  watchCopy,
  watchFailure,
} from './logic';

const ready = {
  profileKnown: true,
  isMinor: false,
  canPublish: true,
  publishBlockedReason: null,
  platform: 'android' as const,
  publisherAvailable: true,
  quotaFull: false,
};

describe('live start visibility', () => {
  it('enables an adult on Android when the publisher loaded and there is room', () => {
    expect(liveStartState(ready)).toEqual({ enabled: true, reason: null });
  });

  it('keeps the option visible and explains every blocked state', () => {
    expect(liveStartState({ ...ready, profileKnown: false }).reason).toBe('Revisando si puedes transmitir.');
    expect(liveStartState({ ...ready, isMinor: true }).reason).toBe('En esta versión un menor no puede transmitir.');
    expect(liveStartState({ ...ready, canPublish: false, publishBlockedReason: 'suspended' }).reason).toBe('Tu cuenta está suspendida. No puedes transmitir.');
    expect(liveStartState({ ...ready, canPublish: false, publishBlockedReason: 'email_not_confirmed' }).reason).toBe('Confirma tu correo para transmitir.');
    expect(liveStartState({ ...ready, canPublish: false, publishBlockedReason: null }).reason).toBe('No tienes permiso para publicar.');
    expect(liveStartState({ ...ready, platform: 'ios' })).toEqual({ enabled: true, reason: null });
    expect(liveStartState({ ...ready, platform: 'ios', publisherAvailable: false }).reason).toBe('Transmitir no está disponible en este dispositivo.');
    expect(liveStartState({ ...ready, publisherAvailable: false }).reason).toBe('Transmitir no está disponible en este dispositivo.');
    expect(liveStartState({ ...ready, quotaFull: true }).reason).toBe('Ahora mismo no hay cupo para otra transmisión.');
  });

  it('records on iOS only in the foreground and releases the microphone afterwards', () => {
    expect(liveRecordMode.allowsRecording).toBe(true);
    expect(liveRecordMode.shouldPlayInBackground).toBe(false);
    expect(liveRecordMode.allowsBackgroundRecording).toBe(false);
    expect(liveIdleMode.allowsRecording).toBe(false);
    expect(liveIdleMode.shouldPlayInBackground).toBe(false);
  });

  it('accepts only an RTMPS publish URL on port 1936 with the one-time key in the query', () => {
    const id = '0123456789abcdef0123456789abcdef';
    const key = 'abcdefabcdefabcdefabcdefabcdefab';
    const canonical = `rtmps://live.somossitu.com:1936/${id}?user=situ&pass=${key}`;
    expect(publishUrlError(canonical)).toBeNull();
    expect(publishUrlError('rtmps://live.example:1936/path')).toBe('La dirección de publicación no es válida.');
    expect(publishUrlError(`rtmps://live.example:1936/${id}`)).toBe('La dirección de publicación no trae la clave de un solo uso.');
    expect(publishUrlError('rtmp://live.example:1935/path')).toBe('La transmisión solo sale por RTMPS en el puerto seguro.');
    expect(publishUrlError('https://live.example/hls/path')).toBe('La transmisión solo sale por RTMPS en el puerto seguro.');
    expect(publishUrlError('not a url')).toBe('La dirección de publicación no es válida.');
    expect(publishTargetLog('rtmps://live.somossitu.com:1936/path')).toEqual({ scheme: 'rtmps', host: 'live.somossitu.com', port: '1936' });
    expect(rtmpTargetShape('rtmps://live.somossitu.com:1936/abc')).toEqual({
      scheme: 'rtmps',
      host: 'live.somossitu.com',
      port: '1936',
      tls: true,
      iosConnectHasApp: false,
      iosStreamPresent: true,
      androidAppPresent: true,
      androidStreamPresent: false,
      hasUser: false,
      hasPass: false,
    });
    expect(rtmpTargetShape('rtmps://live.somossitu.com:1936/live/abc')).toMatchObject({
      iosConnectHasApp: true,
      iosStreamPresent: true,
      androidAppPresent: true,
      androidStreamPresent: true,
    });
    expect(rtmpTargetShape('rtmps://live.somossitu.com:1936')).toMatchObject({
      androidAppPresent: false,
      androidStreamPresent: false,
      iosStreamPresent: false,
    });
    const resolved = { path: id, user: 'situ', pass: key };
    expect(mediamtxPublishTarget(nativePublishUrl(canonical, 'ios'), 'ios')).toEqual(resolved);
    expect(mediamtxPublishTarget(nativePublishUrl(canonical, 'android'), 'android')).toEqual(resolved);
    expect(nativePublishUrl(canonical, 'android')).toBe(`rtmps://live.somossitu.com:1936/?${id}?user=situ&pass=${key}`);
    expect(publisherFailureCopy('connectionFailed', 'Endpoint malformed, should be: rtmp://ip:port/appname/streamname')).toContain('saludo RTMP');
    expect(scrubLiveDetail('falló rtmps://user:secret@live.somossitu.com:1936/abc deadbeefdeadbeef')).toBe('falló [url] [secret]');
    expect(publisherFailureCopy('authError', 'publishBadName: not authorized')).toContain('clave solo sirve una vez');
    expect(publisherFailureCopy('connectionFailed', 'handshake tls')).toContain('RTMPS');
  });

  it('keeps the anonymity reminder exact', () => {
    expect(ANONYMITY_REMINDER).toBe('Tu transmisión es anónima, pero el video puede mostrar rostros y lugares');
  });
});

describe('live start validation', () => {
  it('rejects an empty title and one past 80 characters', () => {
    expect(titleError('   ')).toBe('Escribe un título.');
    expect(titleError('a'.repeat(LIVE_TITLE_MAX + 1))).toBe('El título puede tener hasta 80 caracteres.');
    expect(titleError(' Accidente en la 30 ')).toBeNull();
  });

  it('labels the two visibilities', () => {
    expect(liveShareNote(false, 'Public')).toBe('Sin ubicación, la transmisión no se avisará por cercanía.');
    expect(liveShareNote(true, 'Public')).toContain('300 metros');
    expect(liveShareNote(false, 'Friends')).toBeNull();
    expect(visibilityLabel('Friends')).toBe('Solo amigos');
    expect(visibilityLabel('FriendsOnly')).toBe('Solo amigos');
    expect(visibilityLabel('Public')).toBe('Público');
  });
});

describe('live air state', () => {
  it('moves from connecting to live and then to a short reconnect', () => {
    expect(reduceAir('connecting', 'live')).toBe('live');
    expect(reduceAir('live', 'failed')).toBe('reconnecting');
    expect(reduceAir('reconnecting', 'live')).toBe('live');
    expect(reduceAir('reconnecting', 'give_up')).toBe('ended');
    expect(reduceAir('ended', 'live')).toBe('ended');
    expect(airLabel('live')).toBe('Al aire');
    expect(airLabel('reconnecting')).toBe('Reconectando');
  });

  it('gives up after the connect grace and sooner after a drop', () => {
    expect(shouldGiveUp({ state: 'connecting', sinceMs: 0, nowMs: CONNECT_GRACE_MS - 1 })).toBe(false);
    expect(shouldGiveUp({ state: 'connecting', sinceMs: 0, nowMs: CONNECT_GRACE_MS })).toBe(true);
    expect(shouldGiveUp({ state: 'reconnecting', sinceMs: 0, nowMs: RECONNECT_GRACE_MS })).toBe(true);
    expect(shouldGiveUp({ state: 'live', sinceMs: 0, nowMs: RECONNECT_GRACE_MS + 1 })).toBe(false);
  });

  it('ends when the app goes to the background and warns before the cap', () => {
    expect(shouldEndInBackground('live', 'background')).toBe(true);
    expect(shouldEndInBackground('live', 'inactive')).toBe(false);
    expect(shouldEndInBackground('ended', 'background')).toBe(false);
    expect(minutesWarning((LIVE_MAX_MINUTES * 60_000) - 60_000)).toBe('soon');
    expect(minutesWarning((LIVE_MAX_MINUTES * 60_000) - 60_001)).toBe('none');
    expect(minutesWarning(LIVE_MAX_MINUTES * 60_000)).toBe('over');
    expect(formatDuration(65_000)).toBe('1:05');
  });
});

describe('live playback token', () => {
  it('renews 30 seconds before the 120 second token expires', () => {
    const expiresAt = 120_000;
    expect(shouldRenewPlayback(expiresAt, expiresAt - RENEW_LEAD_MS)).toBe(true);
    expect(shouldRenewPlayback(expiresAt, expiresAt - RENEW_LEAD_MS - 1)).toBe(false);
    expect(renewalDelayMs(expiresAt, 0)).toBe(90_000);
    expect(renewalDelayMs(Number.NaN, 0)).toBe(RENEW_LEAD_MS);
  });

  it('maps API failures to a screen state and prefers the Spanish detail', () => {
    expect(watchFailure('NETWORK', undefined)).toBe('offline');
    expect(watchFailure('NOT_FOUND', 'live.not_found')).toBe('ended');
    expect(watchFailure('FORBIDDEN', 'live.forbidden')).toBe('forbidden');
    expect(watchFailure('CONFLICT', 'live.viewers_full')).toBe('full');
    expect(watchCopy('ended')).toBe('Esta transmisión terminó.');
    expect(watchCopy('offline')).toBe('Sin conexión. La imagen se queda en el último momento.');
    expect(playerStatusCopy('loading')).toBe('Cargando la transmisión…');
    expect(playerStatusCopy('readyToPlay')).toBe('Al aire');
    expect(playerFailureCopy(readPlayerError({ message: 'https://live.example/hls/x/1.aa/index.m3u8', domain: 'AVFoundationErrorDomain', code: -12939 }))).toBe(
      'No se pudo reproducir la transmisión (AVFoundationErrorDomain -12939).',
    );
    expect(readPlayerError({ message: 'https://live.example/secret' }).message).toBe('[url]');
    expect(playerFailureCopy(readPlayerError({ message: 'cannot open' }))).toBe('No se pudo reproducir la transmisión.');
    const playlist = '#EXTM3U\n#EXT-X-STREAM-INF:CODECS="avc1.42c01f,mp4a.40.2"\nmain_stream.m3u8\n#EXT-X-MAP:URI="init.mp4"\nseg0.ts\nseg1.ts\nseg2.ts\nseg3.ts\n';
    expect(hlsProbeTargets(playlist)).toEqual(['main_stream.m3u8', 'init.mp4', 'seg0.ts', 'seg1.ts']);
    expect(liveFailureCopy('live.minors_cannot_stream', 'En esta versión un menor no puede transmitir.')).toBe('En esta versión un menor no puede transmitir.');
    expect(liveFailureCopy('live.limit_concurrent', undefined)).toBe('Ahora mismo no hay cupo para otra transmisión.');
    expect(liveFailureCopy('live.already_live', '  Ya tienes una transmisión activa.  ')).toBe('Ya tienes una transmisión activa.');
  });
});

describe('live playback end', () => {
  const idle = { offline: false, signalEnded: false, watchResult: null, cue: 'none' as const, detailStatus: null };

  it('ends from the hub, from a stalled player, and from a watch that says the stream is gone', () => {
    expect(readLiveNotice({ publicId: 'abc', change: 'ended' })).toEqual({ id: 'abc', change: 'ended' });
    expect(readLiveNotice({ PublicId: 'abc', Change: 'started' })).toEqual({ id: 'abc', change: 'started' });
    expect(playbackEnd({ ...idle, signalEnded: true })).toBe('ended');
    expect(playbackEnd({ ...idle, watchResult: 'ended' })).toBe('ended');
    expect(playbackEnd({ ...idle, cue: 'stall', detailStatus: null })).toBe('ask');
    expect(playbackEnd({ ...idle, cue: 'stall', detailStatus: 'Ended' })).toBe('ended');
    expect(playbackEnd({ ...idle, cue: 'error', detailStatus: 'Cut' })).toBe('ended');
    expect(playbackEnd({ ...idle, cue: 'stall', detailStatus: 'Live' })).toBe('keep');
    expect(playbackEnd({ ...idle, cue: 'start', detailStatus: 'Live' })).toBe('timeout');
    expect(playbackEnd({ ...idle, cue: 'start', detailStatus: 'Ended' })).toBe('ended');
    expect(playbackEnd({ ...idle, offline: true, signalEnded: false, watchResult: 'offline' })).toBe('offline');
    expect(watchCopy('timeout')).toBe('La transmisión no arrancó. Vuelve a intentar.');
    expect(PLAYBACK_STALL_MS).toBe(10_000);
    expect(PLAYBACK_START_MS).toBe(20_000);
  });
});

describe('live roster', () => {
  it('counts only streams that are still live and polls only when the hub is down in the foreground', () => {
    expect(visibleLiveCount([{ status: 'Live' }, { status: 'Live' }, { status: 'Ended' }, { status: 'Cut' }, {}])).toBe(3);
    expect(visibleLiveCount([])).toBe(0);
    expect(shouldPollLiveRoster(true, false)).toBe(true);
    expect(shouldPollLiveRoster(true, true)).toBe(false);
    expect(shouldPollLiveRoster(false, false)).toBe(false);
    expect(ROSTER_POLL_MS).toBe(45_000);
  });
});

describe('live map markers', () => {
  it('keeps a marker only when both coordinates exist', () => {
    const streams = locatedStreams([
      { id: 'a', latitude: 4.7, longitude: -74.1 },
      { id: 'b', latitude: null, longitude: -74.1 },
      { id: 'c', latitude: 4.7, longitude: null },
      { id: 'd', latitude: Number.NaN, longitude: 1 },
    ]);
    expect(streams.map((item) => item.id)).toEqual(['a']);
  });
});

describe('live publish session', () => {
  it('forgets the one-time key when the broadcast ends', () => {
    holdBroadcast({ id: 'path', publishUrl: 'rtmps://live.example:1936/path', publishKey: 'one-time', title: 'Prueba' });
    expect(readBroadcast()?.id).toBe('path');
    clearBroadcast();
    expect(readBroadcast()).toBeNull();
  });
});
