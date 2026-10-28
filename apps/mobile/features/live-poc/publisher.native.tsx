import { useMemo, useRef, useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { callback } from 'react-native-nitro-modules';
import { RtmpPublisherView, requestRtmpPermissions, type RtmpPublisherViewMethods } from 'react-native-nitro-rtmp-publisher';

import { livePocAudio, livePocVideo } from '@/features/live-poc/urls';
import { theme } from '@/theme/theme';

export function Publisher({ url, secret }: { url: string; secret: string }) {
  const publisher = useRef<RtmpPublisherViewMethods | null>(null);
  const [status, setStatus] = useState('Cámara detenida');
  const [bitrate, setBitrate] = useState(0);
  const [ready, setReady] = useState(false);

  const hybridRef = useMemo(
    () =>
      callback((ref: RtmpPublisherViewMethods) => {
        publisher.current = ref;
        const rotation = ref.getCameraOrientation();
        ref.prepareVideo(livePocVideo.width, livePocVideo.height, livePocVideo.fps, livePocVideo.bitrate, livePocVideo.iFrameInterval, rotation);
        ref.prepareAudio(livePocAudio.bitrate, livePocAudio.sampleRate, livePocAudio.stereo);
        ref.startPreview('back', livePocVideo.width, livePocVideo.height);
        ref.setAutoReconnect(2, 2000);
        ref.setOnConnectionEvent((event, message) => {
          setStatus(connectionLabel(event, message));
        });
        ref.setOnBitrateChange((bitrateBps) => {
          setBitrate(bitrateBps);
        });
      }),
    [],
  );

  async function enableCamera() {
    const result = await requestRtmpPermissions();
    setReady(result.granted);
    setStatus(result.granted ? 'Cámara lista' : 'Sin permiso de cámara o micrófono');
  }

  function start() {
    if (!url || !publisher.current) {
      setStatus('Falta la dirección o la cámara.');
      return;
    }
    if (secret.trim()) {
      publisher.current.setAuthorization('situ', secret.trim());
    }
    publisher.current.startStream(url);
    setStatus('Conectando…');
  }

  function stop() {
    publisher.current?.stopStream();
    setStatus('Detenida');
    setBitrate(0);
  }

  return (
    <View style={styles.block}>
      {ready ? (
        <RtmpPublisherView
          style={styles.preview}
          forceHardwareCodec
          videoCodec="h264"
          audioCodec="aac"
          aspectRatioMode="adjust"
          mirrorPreview={false}
          mirrorStream={false}
          thermalWarningThreshold="severe"
          audioSource="camcorder"
          noiseSuppression={false}
          autoRotateStream
          streamMode="lowLatency"
          foregroundServiceTitle=""
          foregroundServiceText=""
          foregroundServiceIcon=""
          pictureInPictureEnabled={false}
          hybridRef={hybridRef}
        />
      ) : (
        <View style={styles.preview} />
      )}
      <Text style={styles.body}>{status}</Text>
      <Text style={styles.body}>{`Bitrate de salida: ${Math.round(bitrate / 1000)} kbps`}</Text>
      <View style={styles.row}>
        <Choice label="Activar cámara" onPress={() => void enableCamera()} />
        <Choice label="Iniciar" onPress={start} />
        <Choice label="Detener" onPress={stop} />
      </View>
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

function connectionLabel(event: string, message: string): string {
  const labels: Record<string, string> = {
    connectionStarted: 'Conectando…',
    connectionSuccess: 'Publicando',
    connectionFailed: 'Falló la conexión',
    disconnect: 'Desconectada',
    reconnecting: 'Reconectando…',
    authError: 'La clave fue rechazada',
    authSuccess: 'Clave aceptada',
  };
  const label = labels[event] ?? event;
  return message ? `${label}. ${message}` : label;
}

const styles = StyleSheet.create({
  block: { gap: 8 },
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  body: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 15, lineHeight: 22 },
  button: { minHeight: 44, borderRadius: theme.radius.button, backgroundColor: theme.colors.primary, paddingHorizontal: 14, alignItems: 'center', justifyContent: 'center' },
  buttonLabel: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  preview: { height: 280, borderRadius: theme.radius.card, backgroundColor: theme.colors.surface, overflow: 'hidden' },
});
