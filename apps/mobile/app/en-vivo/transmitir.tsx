import { confirm, messageFromUnknown } from '@situ/shared';
import { useQuery } from '@tanstack/react-query';
import NetInfo from '@react-native-community/netinfo';
import { activateKeepAwakeAsync, deactivateKeepAwake } from 'expo-keep-awake';
import { useNavigation, useRouter } from 'expo-router';
import { useEffect, useRef, useState } from 'react';
import { AppState, Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { liveDebug } from '@/features/live/debug';
import { LiveCamera } from '@/features/live/publisher';
import type { LiveCameraHandle } from '@/features/live/publisher-types';
import {
  LIVE_MAX_MINUTES,
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
  const airRef = useRef(air);
  airRef.current = air;

  const viewers = useQuery({
    queryKey: ['live', 'detail', session.current?.id],
    enabled: air === 'live' && session.current !== null,
    queryFn: () => api.live(session.current?.id ?? ''),
    refetchInterval: 15_000,
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
        if (!accepted || !event.data?.action) {
          return;
        }
        void finish('user').then(() => nav.dispatch(event.data?.action));
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

  const elapsed = air === 'live' && liveSince.current !== null ? now - liveSince.current : 0;
  const warning = air === 'live' ? minutesWarning(elapsed) : 'none';

  return (
    <View style={styles.screen}>
      <View style={styles.preview}>
        {air === 'ended' ? <View style={styles.preview} /> : <LiveCamera ref={camera} onEvent={apply} onFailure={(message) => { void finish('publisher', message); }} />}
      </View>
      <View style={[styles.panel, { paddingBottom: insets.bottom + 16 }]}>
        <Text style={styles.status}>{airLabel(air)}</Text>
        <Text style={styles.body}>{session.current?.title ?? ''}</Text>
        {air === 'live' ? <Text style={styles.body}>{formatDuration(elapsed)}</Text> : null}
        {typeof viewers.data?.viewers === 'number' && air === 'live' ? <Text style={styles.body}>{`${viewers.data.viewers} espectadores`}</Text> : null}
        {warning === 'soon' ? <Text style={styles.warning}>{`Esta transmisión se cierra al llegar a ${LIVE_MAX_MINUTES} minutos. Falta cerca de un minuto.`}</Text> : null}
        {notice ? <Text accessibilityRole="alert" style={styles.notice}>{notice}</Text> : null}
        {air === 'ended' ? (
          <Pressable accessibilityRole="button" accessibilityLabel="Volver al mapa" onPress={() => router.back()} style={styles.stop}>
            <Text style={styles.stopLabel}>Volver</Text>
          </Pressable>
        ) : (
          <Pressable accessibilityRole="button" accessibilityLabel="Terminar" onPress={() => router.back()} style={styles.stop}>
            <Text style={styles.stopLabel}>Terminar</Text>
          </Pressable>
        )}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: theme.colors.bg },
  preview: { flex: 1, backgroundColor: theme.colors.surface },
  panel: { gap: 8, paddingHorizontal: 20, paddingTop: 16 },
  status: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 28, lineHeight: 34 },
  body: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 16, lineHeight: 22 },
  warning: { color: theme.colors.warning, fontFamily: theme.fontFamily.sansBold, fontSize: 16, lineHeight: 22 },
  notice: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 16, lineHeight: 22 },
  stop: { minHeight: 52, borderRadius: theme.radius.button, backgroundColor: theme.colors.danger, alignItems: 'center', justifyContent: 'center' },
  stopLabel: { color: theme.colors.textOnDanger, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
});
