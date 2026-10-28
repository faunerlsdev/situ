import { useVideoPlayer, VideoView } from 'expo-video';
import { useEffect, useRef, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Publisher } from '@/features/live-poc/publisher';
import { livePocEnabled, playbackUrl, publishUrl } from '@/features/live-poc/urls';
import { theme } from '@/theme/theme';

export default function LivePocScreen() {
  const insets = useSafeAreaInsets();
  const [host, setHost] = useState('192.168.1.20');
  const [streamPath, setStreamPath] = useState('poc');
  const [secret, setSecret] = useState('');
  const [secure, setSecure] = useState(false);
  const [token, setToken] = useState('');
  const [mode, setMode] = useState<'publish' | 'watch'>('publish');

  if (!livePocEnabled) {
    return (
      <View style={[styles.screen, { paddingTop: insets.top + 24 }]}>
        <Text style={styles.title}>Prueba no disponible</Text>
      </View>
    );
  }

  let target = '';
  let targetError = '';
  try {
    target = mode === 'publish'
      ? publishUrl({ host, path: streamPath, secure })
      : playbackUrl({ host, path: streamPath, secure, token });
  } catch (error) {
    targetError = error instanceof Error ? error.message : 'La dirección no es válida.';
  }

  return (
    <ScrollView style={styles.screen} contentContainerStyle={[styles.content, { paddingTop: insets.top + 16, paddingBottom: insets.bottom + 24 }]}>
      <Text style={styles.title}>Prueba de vivo</Text>
      <Text style={styles.body}>Solo aparece en un build de desarrollo. No es la transmisión del producto.</Text>
      <Field label="Servidor" value={host} onChangeText={setHost} />
      <Field label="Nombre" value={streamPath} onChangeText={setStreamPath} autoCapitalize="none" />
      <Field label={secure ? 'Clave de publicación' : 'Clave (vacía en la Mac)'} value={secret} onChangeText={setSecret} secureTextEntry />
      <Field label="Token HLS" value={token} onChangeText={setToken} autoCapitalize="none" />
      <View style={styles.row}>
        <Choice label={secure ? 'RTMPS' : 'RTMP local'} onPress={() => setSecure((value) => !value)} />
        <Choice label={mode === 'publish' ? 'Publicar' : 'Ver'} onPress={() => setMode((value) => (value === 'publish' ? 'watch' : 'publish'))} />
      </View>
      <Text style={styles.body}>{targetError || target}</Text>
      {mode === 'publish' ? <Publisher url={targetError ? '' : target} secret={secret} /> : <Player url={targetError ? '' : target} />}
    </ScrollView>
  );
}

function Player({ url }: { url: string }) {
  const [latency, setLatency] = useState<number | null>(null);
  const [error, setError] = useState('');
  const started = useRef(0);
  const reported = useRef(false);
  const player = useVideoPlayer(null, () => undefined);
  useEffect(() => {
    const subscription = player.addListener('statusChange', ({ status, error: playerError }) => {
      if (status === 'readyToPlay' && started.current > 0 && !reported.current) {
        reported.current = true;
        setLatency(Date.now() - started.current);
      }
      if (status === 'error') {
        setError(playerError?.message ?? 'No se pudo reproducir el HLS.');
      }
    });
    return () => subscription.remove();
  }, [player]);

  function play() {
    if (!url) {
      setError('Falta la dirección.');
      return;
    }
    setError('');
    setLatency(null);
    reported.current = false;
    started.current = Date.now();
    player.replace(url);
    player.play();
  }

  return (
    <View style={styles.block}>
      <VideoView style={styles.preview} player={player} nativeControls contentFit="contain" />
      <Choice label="Reproducir" onPress={play} />
      <Text style={styles.body}>
        {latency === null ? 'Aún no hay primer cuadro.' : `Primer cuadro del reproductor: ${latency} ms. Eso no es la latencia de punta a punta.`}
      </Text>
      <Text style={styles.body}>{error}</Text>
    </View>
  );
}

function Field({ label, ...props }: { label: string; value: string; onChangeText: (value: string) => void; secureTextEntry?: boolean; autoCapitalize?: 'none' }) {
  return (
    <View style={styles.block}>
      <Text style={styles.label}>{label}</Text>
      <TextInput {...props} placeholderTextColor={theme.colors.textPlaceholder} keyboardAppearance="dark" autoCorrect={false} style={styles.input} />
    </View>
  );
}

function Choice({ label, onPress }: { label: string; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={label} onPress={onPress} style={styles.button}>
      <Text style={styles.buttonLabel}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: theme.colors.bg },
  content: { gap: 12, paddingHorizontal: 20 },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 28, lineHeight: 34 },
  label: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 13 },
  body: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 15, lineHeight: 22 },
  block: { gap: 8 },
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  input: {
    minHeight: 48,
    borderRadius: theme.radius.input,
    borderWidth: 1,
    borderColor: theme.colors.border,
    paddingHorizontal: 12,
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sans,
    fontSize: 16,
    backgroundColor: theme.colors.input,
  },
  button: { minHeight: 44, borderRadius: theme.radius.button, backgroundColor: theme.colors.primary, paddingHorizontal: 14, alignItems: 'center', justifyContent: 'center' },
  buttonLabel: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  preview: { height: 280, borderRadius: theme.radius.card, backgroundColor: theme.colors.surface, overflow: 'hidden' },
});
