import { confirm, messageFromUnknown, notify } from '@situ/shared';
import { useQuery } from '@tanstack/react-query';
import NetInfo from '@react-native-community/netinfo';
import { activateKeepAwakeAsync, deactivateKeepAwake } from 'expo-keep-awake';
import { useNavigation, useRouter } from 'expo-router';
import { useEffect, useRef, useState } from 'react';
import { SymbolView } from 'expo-symbols';
import { AppState, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { liveDebug } from '@/features/live/debug';
import { LiveCommentList, LiveReactionMark, LiveTopBar, liveLikeCount, useKeyboardInset } from '@/features/live/overlay';
import { LiveCamera } from '@/features/live/publisher';
import type { LiveCameraHandle } from '@/features/live/publisher-types';
import {
  LIVE_MAX_MINUTES,
  LIVE_TITLE_MAX,
  airLabel,
  publishUrlError,
  formatDuration,
  minutesWarning,
  reduceAir,
  shouldEndInBackground,
  shouldGiveUp,
  type AirState,
  type PublisherEvent,
} from '@/features/live/logic';
import { clearBroadcast, readBroadcast } from '@/features/live/session';
import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

const AWAKE = 'situ-live';

export default function BroadcastScreen() {
  const insets = useSafeAreaInsets();
  const keyboard = useKeyboardInset();
  const router = useRouter();
  const navigation = useNavigation();
  const camera = useRef<LiveCameraHandle>(null);
  const session = useRef(readBroadcast());
  const ended = useRef(false);
  const airSince = useRef(Date.now());
  const liveSince = useRef<number | null>(null);
  const [air, setAir] = useState<AirState>(session.current ? 'connecting' : 'ended');
  const [now, setNow] = useState(Date.now());
  const [notice, setNotice] = useState<string | null>(session.current ? null : 'No hay una transmisión para publicar. Vuelve a empezar.');
  const [title, setTitle] = useState(session.current?.title ?? '');
  const [visibility, setVisibility] = useState<'Public' | 'Friends'>('Public');
  const [saving, setSaving] = useState(false);
  const airRef = useRef(air);
  airRef.current = air;

  const liveId = session.current?.id ?? '';
  const viewers = useQuery({
    queryKey: ['live', 'detail', liveId],
    enabled: air === 'live' && liveId.length > 0,
    queryFn: () => api.live(liveId),
    refetchInterval: 3000,
  });
  const room = useQuery({
    queryKey: ['live-room', liveId],
    enabled: air === 'live' && liveId.length > 0,
    queryFn: () => api.liveRoom(liveId),
    refetchInterval: 3000,
  });

  useEffect(() => {
    void activateKeepAwakeAsync(AWAKE);
    return () => {
      void deactivateKeepAwake(AWAKE);
    };
  }, []);

  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, []);

  useEffect(() => {
    const current = session.current;
    if (!current) {
      return;
    }
    if (publishUrlError(current.publishUrl)) {
      void finish('url');
      return;
    }
    camera.current?.start(current.publishUrl, current.publishKey);
  }, []);

  useEffect(() => {
    const subscription = AppState.addEventListener('change', (next) => {
      if (shouldEndInBackground(airRef.current, next)) {
        void finish('background');
      }
    });
    return () => subscription.remove();
  }, []);

  useEffect(() => {
    const unsubscribe = NetInfo.addEventListener((state) => {
      if (state.isConnected === false && airRef.current === 'live') {
        apply('reconnecting');
      }
    });
    return () => unsubscribe();
  }, []);

  useEffect(() => {
    const nav = navigation as unknown as {
      addListener: (event: 'beforeRemove', callback: (event: { preventDefault: () => void; data?: { action?: unknown } }) => void) => () => void;
      dispatch: (action: unknown) => void;
    };
    return nav.addListener('beforeRemove', (event) => {
      if (ended.current || !session.current) {
        return;
      }
      event.preventDefault();
      void confirm({
        title: 'Terminar transmisión',
        message: 'Dejará de verse para quienes la están mirando.',
        type: 'danger',
        confirmText: 'Terminar',
        cancelText: 'Seguir',
      }).then((accepted) => {
        if (!accepted) {
          return;
        }
        void finish('user').then(() => {
          if (router.canGoBack() && event.data?.action) {
            nav.dispatch(event.data.action);
            return;
          }
          router.replace('/mapa');
        });
      });
    });
  }, [navigation]);

  useEffect(() => {
    if (air === 'ended') {
      return;
    }
    if (shouldGiveUp({ state: air, sinceMs: airSince.current, nowMs: now })) {
      liveDebug('give-up', { state: air, waitedMs: now - airSince.current });
      void finish(air === 'reconnecting' ? 'network' : 'connect');
    }
  }, [air, now]);

  useEffect(() => {
    if (air !== 'live' || liveSince.current === null) {
      return;
    }
    if (minutesWarning(now - liveSince.current) === 'over') {
      void finish('limit');
    }
  }, [air, now]);

  function apply(event: PublisherEvent) {
    setAir((current) => {
      const next = reduceAir(current, event);
      if (next !== current) {
        airSince.current = Date.now();
      }
      if (next === 'live' && liveSince.current === null) {
        liveSince.current = Date.now();
      }
      return next;
    });
  }

  async function finish(reason: 'user' | 'background' | 'network' | 'connect' | 'limit' | 'url' | 'publisher', detail?: string) {
    if (ended.current) {
      return;
    }
    ended.current = true;
    camera.current?.stop();
    const id = session.current?.id;
    clearBroadcast();
    session.current = null;
    setAir('ended');
    if (reason === 'background') {
      setNotice('La transmisión terminó porque la app pasó a segundo plano. El video en vivo solo funciona con la app abierta.');
    } else if (reason === 'network') {
      setNotice('Se cortó la conexión. Cerramos la transmisión para no dejarla abierta.');
    } else if (reason === 'connect') {
      setNotice('No se pudo conectar. Cerramos la transmisión para no dejarla abierta.');
    } else if (reason === 'url') {
      setNotice('La transmisión solo sale por RTMPS en el puerto seguro.');
    } else if (reason === 'publisher') {
      setNotice(detail ?? 'No se pudo conectar. Cerramos la transmisión para no dejarla abierta.');
    } else if (reason === 'limit') {
      setNotice(`La transmisión llegó a los ${LIVE_MAX_MINUTES} minutos y se cerró.`);
    } else {
      setNotice('Transmisión terminada.');
    }
    liveDebug('end', { reason, idPresent: Boolean(id) });
    if (!id) {
      return;
    }
    try {
      await api.endLive(id);
    } catch (cause) {
      const message = messageFromUnknown(cause);
      if (message) {
        setNotice(message);
      }
    }
  }

  function leave() {
    if (router.canGoBack()) {
      router.back();
      return;
    }
    router.replace('/mapa');
  }

  async function stop() {
    if (ended.current || airRef.current === 'ended') {
      router.replace('/mapa');
      return;
    }
    const accepted = await confirm({
      title: 'Terminar transmisión',
      message: 'Dejará de verse para quienes la están mirando.',
      type: 'danger',
      confirmText: 'Terminar',
      cancelText: 'Seguir',
    });
    if (!accepted) {
      return;
    }
    await finish('user');
    leave();
  }

  const elapsed = air === 'live' && liveSince.current !== null ? now - liveSince.current : 0;
  const warning = air === 'live' ? minutesWarning(elapsed) : 'none';

  async function present(nextVisibility: 'Public' | 'Friends', nextTitle: string) {
    const id = session.current?.id;
    const trimmed = nextTitle.trim();
    if (!id || trimmed.length === 0 || air === 'ended') {
      return;
    }
    setSaving(true);
    try {
      await api.updateLive(id, { title: trimmed, visibility: nextVisibility });
      if (session.current) {
        session.current.title = trimmed;
      }
      setVisibility(nextVisibility);
      setTitle(trimmed);
    } catch (cause) {
      notify.error(messageFromUnknown(cause) || 'No se pudo actualizar la transmisión.');
    } finally {
      setSaving(false);
    }
  }

  const watching = typeof room.data?.viewers === 'number' ? room.data.viewers : typeof viewers.data?.viewers === 'number' ? viewers.data.viewers : null;
  const shownTitle = (room.data?.title || title).trim();
  const bottom = keyboard > 0 ? keyboard + 8 : insets.bottom + 12;

  return (
    <View style={styles.screen}>
      <View style={styles.preview}>
        {air === 'ended' ? <View style={styles.preview} /> : <LiveCamera ref={camera} onEvent={apply} onFailure={(message) => { void finish('publisher', message); }} />}
      </View>
      <View style={[styles.stage, { paddingTop: insets.top + 8, paddingBottom: bottom }]} pointerEvents="box-none">
        <LiveTopBar
          title={air === 'ended' ? '' : shownTitle}
          viewers={air === 'live' ? watching : null}
          status={air === 'live' ? formatDuration(elapsed) : airLabel(air)}
          onAir={air === 'live'}
        />
        <View style={styles.stageFill} pointerEvents="none" />
        {air === 'live' ? <LiveReactionMark count={liveLikeCount(room.data?.reactionCounts)} /> : null}
        {air === 'live' ? <LiveCommentList comments={room.data?.comments ?? []} /> : null}
        {warning === 'soon' ? <Text style={styles.warning}>{`Se cierra a los ${LIVE_MAX_MINUTES} minutos. Falta cerca de un minuto.`}</Text> : null}
        {notice ? <Text accessibilityRole="alert" style={styles.notice}>{notice}</Text> : null}
        {air === 'ended' ? (
          <View style={styles.controls}>
            <Pressable accessibilityRole="button" accessibilityLabel="Volver al mapa" onPress={() => router.replace('/mapa')} style={styles.end}>
              <Text style={styles.endLabel}>Volver</Text>
            </Pressable>
          </View>
        ) : (
          <View style={styles.controls}>
            <TextInput
              accessibilityLabel="Título de la transmisión"
              value={title}
              onChangeText={setTitle}
              onEndEditing={() => void present(visibility, title)}
              maxLength={LIVE_TITLE_MAX}
              placeholder="Título"
              placeholderTextColor={theme.colors.textPlaceholder}
              style={styles.input}
              returnKeyType="done"
            />
            <View style={styles.tools}>
              <Pressable accessibilityRole="button" accessibilityState={{ selected: visibility === 'Public' }} accessibilityLabel="Público" disabled={saving} onPress={() => void present('Public', title)} style={[styles.chip, visibility === 'Public' && styles.chipOn]}>
                <Text style={[styles.chipLabel, visibility === 'Public' && styles.chipLabelOn]}>Público</Text>
              </Pressable>
              <Pressable accessibilityRole="button" accessibilityState={{ selected: visibility === 'Friends' }} accessibilityLabel="Entre amigos" disabled={saving} onPress={() => void present('Friends', title)} style={[styles.chip, visibility === 'Friends' && styles.chipOn]}>
                <Text style={[styles.chipLabel, visibility === 'Friends' && styles.chipLabelOn]}>Entre amigos</Text>
              </Pressable>
              <View style={styles.toolsFill} />
              <Pressable accessibilityRole="button" accessibilityLabel="Finalizar transmisión" onPress={() => void stop()} style={styles.end}>
                <SymbolView name={stopIcon} tintColor={theme.colors.danger} size={12} />
                <Text style={styles.endLabel}>Finalizar</Text>
              </Pressable>
            </View>
          </View>
        )}
      </View>
    </View>
  );
}

const stopIcon = { ios: 'stop.fill', android: 'stop', web: 'stop' } as const;

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: '#000000' },
  preview: { ...StyleSheet.absoluteFill, backgroundColor: '#000000' },
  stage: { ...StyleSheet.absoluteFill, justifyContent: 'flex-end', gap: 10 },
  stageFill: { flex: 1, flexShrink: 1, minHeight: 0 },
  controls: { gap: 8, paddingHorizontal: 12 },
  input: { minHeight: 40, borderRadius: 20, paddingHorizontal: 14, color: theme.colors.textPrimary, backgroundColor: 'rgba(20,20,20,0.45)', fontFamily: theme.fontFamily.sans, fontSize: 15 },
  tools: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  toolsFill: { flex: 1 },
  chip: { height: 32, paddingHorizontal: 12, borderRadius: 16, alignItems: 'center', justifyContent: 'center', backgroundColor: 'rgba(20,20,20,0.45)' },
  chipOn: { backgroundColor: 'rgba(20,20,20,0.72)' },
  chipLabel: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 14, lineHeight: 18 },
  chipLabelOn: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold },
  warning: { color: theme.colors.warning, fontFamily: theme.fontFamily.sans, fontSize: 13, lineHeight: 18, paddingHorizontal: 16 },
  notice: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 15, lineHeight: 20, paddingHorizontal: 16 },
  end: { height: 32, paddingHorizontal: 12, borderRadius: 16, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 6, alignSelf: 'flex-end', backgroundColor: 'rgba(20,20,20,0.45)' },
  endLabel: { color: theme.colors.danger, fontFamily: theme.fontFamily.sans, fontSize: 14, lineHeight: 18 },
});
