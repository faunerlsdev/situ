import { AppError, messageFromUnknown, notify } from '@situ/shared';
import type { ComplaintReasonCode } from '@situ/shared';
import NetInfo from '@react-native-community/netinfo';
import { useVideoPlayer, VideoView } from 'expo-video';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useEffect, useRef, useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { ComplaintSheet } from '@/features/interaction/complaint-sheet';
import { liveDebug } from '@/features/live/debug';
import { hlsProbeTargets, PLAYBACK_STALL_MS, PLAYBACK_START_MS, playbackEnd, playerFailureCopy, playerStatusCopy, readPlayerError, renewalDelayMs, watchCopy, watchFailure, type WatchPhase } from '@/features/live/logic';
import { subscribeLiveNotice } from '@/features/live/roster';
import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

export default function WatchLiveScreen() {
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const params = useLocalSearchParams<{ id: string }>();
  const id = Array.isArray(params.id) ? params.id[0] ?? '' : params.id ?? '';
  const [phase, setPhase] = useState<WatchPhase>('loading');
  const [playerNote, setPlayerNote] = useState<string | null>(null);
  const [source, setSource] = useState<string | null>(null);
  const [complaint, setComplaint] = useState(false);
  const [pending, setPending] = useState(false);
  const expiresAt = useRef(0);
  const phaseRef = useRef(phase);
  const idRef = useRef(id);
  const finished = useRef(false);
  const lastTime = useRef(0);
  const lastMovedAt = useRef(Date.now());
  const releaseRef = useRef<(next: 'ended' | 'timeout') => void>(() => undefined);
  const confirmRef = useRef<(cue: 'stall' | 'start' | 'error') => void>(() => undefined);
  const player = useVideoPlayer(null, () => undefined);
  const seenId = useRef(id);
  phaseRef.current = phase;
  idRef.current = id;
  if (seenId.current !== id) {
    seenId.current = id;
    finished.current = false;
    lastTime.current = 0;
    lastMovedAt.current = Date.now();
    setPhase('loading');
    setPlayerNote(null);
    setSource(null);
  }

  function release(next: 'ended' | 'timeout') {
    if (finished.current && phaseRef.current === next) {
      return;
    }
    finished.current = true;
    setPlayerNote(null);
    setSource(null);
    setPhase(next);
    player.pause();
    player.replace(null);
  }
  releaseRef.current = release;

  async function confirmLive(cue: 'stall' | 'start' | 'error') {
    const requested = id;
    if (finished.current) {
      return;
    }
    if (cue === 'start' && phaseRef.current !== 'loading') {
      return;
    }
    const online = await NetInfo.fetch();
    if (finished.current || idRef.current !== requested || (cue === 'start' && phaseRef.current !== 'loading')) {
      return;
    }
    if (online.isConnected === false) {
      setPhase('offline');
      return;
    }
    let detailStatus: string | null = null;
    let watchResult: 'ended' | 'offline' | null = null;
    try {
      detailStatus = (await api.live(id)).status;
    } catch (cause) {
      const failure = cause instanceof AppError ? watchFailure(cause.code, cause.apiCode) : 'unknown';
      if (failure === 'ended') {
        watchResult = 'ended';
      } else if (failure === 'offline') {
        watchResult = 'offline';
      } else {
        return;
      }
    }
    if (finished.current || idRef.current !== requested || (cue === 'start' && phaseRef.current !== 'loading')) {
      return;
    }
    const decision = playbackEnd({ offline: false, signalEnded: false, watchResult, cue, detailStatus });
    if (decision === 'ended' || decision === 'timeout') {
      release(decision);
      return;
    }
    if (decision === 'offline') {
      setPhase('offline');
    }
  }
  confirmRef.current = confirmLive;

  useEffect(() => {
    player.pause();
    player.replace(null);
    let stopped = false;
    void load(false);
    return () => {
      stopped = true;
    };

    async function load(renewing: boolean) {
      if (!id) {
        setPhase('ended');
        return;
      }
      const online = await NetInfo.fetch();
      if (stopped || finished.current) {
        return;
      }
      if (online.isConnected === false) {
        setPhase('offline');
        return;
      }
      try {
        const watch = await api.watchLive(id);
        if (stopped || finished.current) {
          return;
        }
        expiresAt.current = Date.parse(watch.expiresAt);
        setPlayerNote(playerStatusCopy('loading'));
        setSource(watch.url);
        if (__DEV__) {
          void probePlaylist(watch.url);
        }
      } catch (cause) {
        if (stopped || finished.current) {
          return;
        }
        const failure = cause instanceof AppError ? watchFailure(cause.code, cause.apiCode) : 'unknown';
        if (renewing && failure === 'offline') {
          setPhase('offline');
          return;
        }
        setPhase(failure === 'unknown' ? 'ended' : failure);
        if (failure === 'unknown') {
          notify.error(messageFromUnknown(cause) || 'No se pudo abrir la transmisión.');
        }
      }
    }
  }, [id, player]);

  useEffect(() => {
    if (!source) {
      return;
    }
    player.replace(source);
    player.play();
  }, [player, source]);

  useEffect(() => {
    return subscribeLiveNotice((notice) => {
      if (notice.id === id && notice.change === 'ended') {
        releaseRef.current('ended');
      }
    });
  }, [id]);

  useEffect(() => {
    if (phase !== 'loading') {
      return;
    }
    const timer = setTimeout(() => confirmRef.current('start'), PLAYBACK_START_MS);
    return () => clearTimeout(timer);
  }, [phase, id]);

  useEffect(() => {
    if (phase === 'ended' || phase === 'timeout' || phase === 'forbidden' || phase === 'full') {
      return;
    }
    const timer = setInterval(() => {
      if (finished.current) {
        return;
      }
      const time = Number.isFinite(player.currentTime) ? player.currentTime : 0;
      if (time > lastTime.current + 0.05) {
        lastTime.current = time;
        lastMovedAt.current = Date.now();
        return;
      }
      if (Date.now() - lastMovedAt.current < PLAYBACK_STALL_MS) {
        return;
      }
      lastMovedAt.current = Date.now();
      confirmRef.current('stall');
    }, 1000);
    return () => clearInterval(timer);
  }, [phase, player]);

  useEffect(() => {
    const subscription = player.addListener('statusChange', ({ status, oldStatus, error }) => {
      if (finished.current) {
        return;
      }
      const failure = readPlayerError(error);
      liveDebug('player', {
        status,
        previous: oldStatus ?? '',
        detail: failure.message ?? '',
        domain: failure.domain ?? '',
        code: failure.code ?? '',
      });
      if (status === 'error') {
        setPlayerNote(playerFailureCopy(failure));
        confirmRef.current('error');
        return;
      }
      if (status === 'readyToPlay') {
        setPlayerNote(null);
        setPhase('playing');
        return;
      }
      setPlayerNote(playerStatusCopy(status));
    });
    return () => subscription.remove();
  }, [player]);

  useEffect(() => {
    if (phase === 'ended' || phase === 'timeout') {
      finished.current = true;
      player.pause();
      player.replace(null);
      return;
    }
    if (phase === 'forbidden' || phase === 'full') {
      player.pause();
      player.replace(null);
    }
  }, [phase, player]);

  useEffect(() => {
    if (phase !== 'playing') {
      return;
    }
    let stopped = false;
    const delay = renewalDelayMs(expiresAt.current, Date.now());
    const timer = setTimeout(() => {
      void renew();
    }, delay);
    return () => {
      stopped = true;
      clearTimeout(timer);
    };

    async function renew() {
      if (stopped || !id) {
        return;
      }
      try {
        const watch = await api.watchLive(id);
        if (stopped || finished.current) {
          return;
        }
        expiresAt.current = Date.parse(watch.expiresAt);
        setSource(watch.url);
        if (__DEV__) {
          void probePlaylist(watch.url);
        }
      } catch (cause) {
        if (stopped || finished.current) {
          return;
        }
        const failure = cause instanceof AppError ? watchFailure(cause.code, cause.apiCode) : 'unknown';
        if (failure === 'offline') {
          setPhase('offline');
          return;
        }
        if (failure === 'ended' || failure === 'unknown') {
          releaseRef.current('ended');
          return;
        }
        setPhase(failure);
        player.pause();
        player.replace(null);
      }
    }
  }, [id, phase, player, source]);

  async function report(reason: ComplaintReasonCode, details: string) {
    setPending(true);
    try {
      await api.reportLive(id, { reason, details });
      setComplaint(false);
      notify.success('Recibimos la denuncia. La transmisión sigue siendo anónima.');
    } catch (cause) {
      notify.error(messageFromUnknown(cause) || 'No se pudo enviar la denuncia.');
    } finally {
      setPending(false);
    }
  }

  return (
    <View style={[styles.screen, { paddingTop: insets.top + 8, paddingBottom: insets.bottom + 16 }]}>
      <Pressable accessibilityRole="button" accessibilityLabel="Volver" onPress={() => router.back()} style={styles.back}>
        <Text style={styles.backLabel}>Volver</Text>
      </Pressable>
      <VideoView style={styles.video} player={player} nativeControls contentFit="contain" allowsPictureInPicture={false} />
      <Text style={styles.status}>{playerNote ?? watchCopy(phase)}</Text>
      <Text style={styles.body}>Esta transmisión es anónima.</Text>
      {phase === 'playing' || phase === 'offline' ? (
        <Pressable accessibilityRole="button" accessibilityLabel="Denunciar" onPress={() => setComplaint(true)} style={styles.report}>
          <Text style={styles.reportLabel}>Denunciar</Text>
        </Pressable>
      ) : null}
      <ComplaintSheet
        visible={complaint}
        pending={pending}
        duplicateMessage={null}
        onClose={() => setComplaint(false)}
        onSubmit={(reason, details) => void report(reason, details)}
      />
    </View>
  );
}

async function probePlaylist(url: string, depth = 2): Promise<void> {
  try {
    const response = await fetch(url);
    const type = response.headers.get('content-type') ?? '';
    const buffer = await response.arrayBuffer();
    liveDebug('hls-fetch', { status: response.status, type, bytes: buffer.byteLength });
    if (depth <= 0 || !type.includes('mpegurl') || buffer.byteLength > 64_000) {
      return;
    }
    const text = new TextDecoder().decode(buffer);
    for (const relative of hlsProbeTargets(text)) {
      await probePlaylist(new URL(relative, url).href, depth - 1);
    }
  } catch (cause) {
    liveDebug('hls-fetch', { status: 0, type: '', bytes: 0, detail: cause instanceof Error ? cause.message : '' });
  }
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: theme.colors.bg, paddingHorizontal: 20, gap: 12 },
  back: { minHeight: 44, alignSelf: 'flex-start', justifyContent: 'center' },
  backLabel: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  video: { flex: 1, borderRadius: theme.radius.card, backgroundColor: theme.colors.surface, overflow: 'hidden' },
  status: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 18, lineHeight: 24 },
  body: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 15, lineHeight: 22 },
  report: { minHeight: 48, borderRadius: theme.radius.button, backgroundColor: theme.colors.surface, alignItems: 'center', justifyContent: 'center' },
  reportLabel: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
});
