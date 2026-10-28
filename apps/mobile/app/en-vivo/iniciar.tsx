import { AppError, messageFromUnknown } from '@situ/shared';
import * as Location from 'expo-location';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { PrimaryButton } from '@/components/primary-button';
import { StatusMessage } from '@/components/status-message';
import { ANONYMITY_REMINDER, LIVE_TITLE_MAX, liveShareNote, titleError } from '@/features/live/logic';
import { requestLiveCapture } from '@/features/live/publisher';
import { setLiveQuotaFull } from '@/features/live/quota';
import { holdBroadcast } from '@/features/live/session';
import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

export default function StartLiveScreen() {
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const [title, setTitle] = useState('');
  const [visibility, setVisibility] = useState<'Friends' | 'Public'>('Friends');
  const [shareLocation, setShareLocation] = useState(false);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  async function start() {
    const invalid = titleError(title);
    if (invalid) {
      setError(invalid);
      return;
    }

    setPending(true);
    setError(null);
    setNotice(null);
    try {
      const granted = await requestLiveCapture();
      if (!granted) {
        setError('Sin permiso de cámara o micrófono no puedes transmitir.');
        return;
      }

      let latitude: number | null = null;
      let longitude: number | null = null;
      if (shareLocation) {
        const current = await Location.getForegroundPermissionsAsync();
        const permission = current.status === 'granted' ? current : await Location.requestForegroundPermissionsAsync();
        if (permission.status === 'granted') {
          const position = await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.Balanced });
          latitude = position.coords.latitude;
          longitude = position.coords.longitude;
        } else {
          setNotice('Seguimos sin ubicación. La transmisión no aparece en el mapa.');
        }
      }

      const created = await api.createLive({
        visibility,
        title: title.trim(),
        latitude,
        longitude,
      });
      holdBroadcast({
        id: created.id,
        publishUrl: created.publishUrl,
        publishKey: created.publishKey,
        title: title.trim(),
      });
      setLiveQuotaFull(false);
      router.replace('/en-vivo/transmitir');
    } catch (cause) {
      if (cause instanceof AppError && cause.apiCode === 'live.limit_concurrent') {
        setLiveQuotaFull(true);
      }
      setError(messageFromUnknown(cause) || 'No se pudo empezar la transmisión.');
    } finally {
      setPending(false);
    }
  }

  return (
    <KeyboardAvoidingView style={styles.screen} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <ScrollView contentContainerStyle={[styles.content, { paddingTop: insets.top + 16, paddingBottom: insets.bottom + 24 }]} keyboardShouldPersistTaps="handled">
        <Pressable accessibilityRole="button" accessibilityLabel="Volver" onPress={() => router.back()} style={styles.back}>
          <Text style={styles.backLabel}>Volver</Text>
        </Pressable>
        <Text style={styles.title}>En vivo</Text>
        <Text style={styles.body}>Para transmitir, SITU necesita la cámara y el micrófono. El video no se guarda en el teléfono.</Text>
        <Text style={styles.label}>Título</Text>
        <TextInput
          accessibilityLabel="Título de la transmisión"
          value={title}
          onChangeText={setTitle}
          maxLength={LIVE_TITLE_MAX}
          placeholder="Qué está pasando"
          placeholderTextColor={theme.colors.textPlaceholder}
          style={styles.input}
        />
        <Text style={styles.label}>Quién puede verla</Text>
        <View style={styles.row}>
          <Choice label="Solo amigos" selected={visibility === 'Friends'} onPress={() => setVisibility('Friends')} />
          <Choice label="Público" selected={visibility === 'Public'} onPress={() => setVisibility('Public')} />
        </View>
        <Pressable
          accessibilityRole="checkbox"
          accessibilityState={{ checked: shareLocation }}
          accessibilityLabel="Compartir ubicación"
          onPress={() => setShareLocation((value) => !value)}
          style={styles.switchRow}
        >
          <View style={[styles.switchKnob, shareLocation && styles.switchOn]} />
          <Text style={styles.body}>Compartir ubicación</Text>
        </Pressable>
        {liveShareNote(shareLocation, visibility) ? <Text style={styles.body}>{liveShareNote(shareLocation, visibility)}</Text> : null}
        <Text style={styles.reminder}>{ANONYMITY_REMINDER}</Text>
        <StatusMessage error={error} notice={notice} />
        <PrimaryButton label="Empezar transmisión" pendingLabel="Empezando…" pending={pending} onPress={() => void start()} />
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

function Choice({ label, selected, onPress }: { label: string; selected: boolean; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="button" accessibilityState={{ selected }} accessibilityLabel={label} onPress={onPress} style={[styles.choice, selected && styles.choiceOn]}>
      <Text style={styles.choiceLabel}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: theme.colors.bg },
  content: { gap: 12, paddingHorizontal: 20 },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 28, lineHeight: 34 },
  label: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 13 },
  body: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 15, lineHeight: 22 },
  reminder: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16, lineHeight: 22 },
  back: { minHeight: 44, alignSelf: 'flex-start', justifyContent: 'center' },
  backLabel: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
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
  row: { flexDirection: 'row', gap: 8 },
  choice: { flex: 1, minHeight: 44, borderRadius: theme.radius.button, alignItems: 'center', justifyContent: 'center', backgroundColor: theme.colors.surface },
  choiceOn: { backgroundColor: theme.colors.primary },
  choiceLabel: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  switchRow: { flexDirection: 'row', gap: 12, alignItems: 'center', minHeight: 44 },
  switchKnob: { width: 44, height: 28, borderRadius: 14, backgroundColor: theme.colors.border },
  switchOn: { backgroundColor: theme.colors.primary },
});
