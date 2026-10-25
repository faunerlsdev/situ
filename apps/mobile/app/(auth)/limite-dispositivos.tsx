import type { SessionDevice } from '@situ/shared';
import { AppError, loginErrorMessages, notify } from '@situ/shared';

import { DeviceList } from '@/features/account/device-list';
import { useRouter } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text } from 'react-native';

import { api, isDeviceLimitSession, setAccessToken } from '@/lib/api';
import { theme } from '@/theme/theme';

export default function DeviceLimitScreen() {
  const router = useRouter();
  const [devices, setDevices] = useState<SessionDevice[]>([]);
  const [loading, setLoading] = useState(true);
  const [pendingId, setPendingId] = useState<string | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!isDeviceLimitSession()) {
      setDevices([]);
      setLoadError('Vuelve a ingresar para ver los dispositivos de esta cuenta.');
      setLoading(false);
      return;
    }

    setLoading(true);
    setLoadError(null);
    try {
      const page = await api.listMyDevices();
      setDevices(page.devices);
    } catch (cause) {
      setLoadError(cause instanceof AppError ? cause.userMessage : 'No pudimos cargar tus dispositivos.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  function leave() {
    setAccessToken(null);
    router.replace('/login');
  }

  async function revoke(device: SessionDevice) {
    setPendingId(device.id);
    try {
      await api.revokeDevice(device.id);
      setAccessToken(null);
      notify.success('Listo. Ya puedes ingresar en este dispositivo.');
      router.replace('/login');
    } catch (cause) {
      notify.error(cause instanceof AppError ? cause : 'No pudimos cerrar la sesión en ese dispositivo.');
      setPendingId(null);
    }
  }

  const active = devices.filter((device) => device.isActive);

  return (
    <ScrollView style={styles.screen} contentContainerStyle={styles.content}>
      <Text style={styles.title}>Llegaste al límite de dispositivos</Text>
      <Text style={styles.body}>{loginErrorMessages['auth.device_limit']} Cierra sesión en uno para entrar en este.</Text>
      {loading ? <ActivityIndicator color={theme.colors.primary} /> : null}
      {loadError ? <Text style={styles.error}>{loadError}</Text> : null}
      {!loading && !loadError && active.length === 0 ? (
        <Text style={styles.body}>No hay otros dispositivos con sesión. Vuelve a intentar ingresar.</Text>
      ) : null}
      {!loading && !loadError && active.length > 0 ? (
        <DeviceList devices={active} pendingId={pendingId} onRevoke={(device) => void revoke(device)} />
      ) : null}
      <Pressable accessibilityRole="button" onPress={leave}>
        <Text style={styles.link}>Volver al ingreso</Text>
      </Pressable>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: theme.colors.bg },
  content: { flexGrow: 1, gap: 16, padding: 24 },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 28, textAlign: 'center' },
  body: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 16, lineHeight: 24, textAlign: 'center' },
  error: { color: theme.colors.danger, fontFamily: theme.fontFamily.sansBold, fontSize: 15, textAlign: 'center' },
  link: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sansBold, fontSize: 16, textAlign: 'center' },
});
