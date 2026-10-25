import { confirm, notify, profileMessages } from '@situ/shared';
import type { SessionDevice } from '@situ/shared';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { DeviceList } from '@/features/account/device-list';
import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

export default function DevicesScreen() {
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const queryClient = useQueryClient();
  const devices = useQuery({ queryKey: ['devices'], queryFn: () => api.listMyDevices() });
  const [pendingId, setPendingId] = useState<string | null>(null);

  async function revoke(device: SessionDevice) {
    const accepted = await confirm({
      title: profileMessages.revokeTitle,
      message: profileMessages.revokeBody,
      type: 'danger',
      confirmText: profileMessages.revoke,
      cancelText: 'Cancelar',
    });
    if (!accepted) {
      return;
    }
    setPendingId(device.id);
    try {
      await api.revokeDevice(device.id);
      await queryClient.invalidateQueries({ queryKey: ['devices'] });
      notify.success(profileMessages.revoked);
    } catch (error) {
      notify.error(error, { action: { label: profileMessages.retry, onPress: () => void revoke(device) } });
    } finally {
      setPendingId(null);
    }
  }

  return (
    <ScrollView style={styles.screen} contentContainerStyle={[styles.content, { paddingTop: insets.top + 12, paddingBottom: insets.bottom + 24 }]}>
      <Pressable accessibilityRole="button" accessibilityLabel={profileMessages.back} onPress={() => router.back()} style={styles.row}>
        <Text style={styles.link}>{profileMessages.back}</Text>
      </Pressable>
      <Text style={styles.title}>{profileMessages.devices}</Text>
      {devices.isError ? (
        <View style={styles.block}>
          <Text style={styles.body}>{profileMessages.devicesError}</Text>
          <Pressable accessibilityRole="button" accessibilityLabel={profileMessages.retry} onPress={() => void devices.refetch()} style={styles.row}>
            <Text style={styles.link}>{profileMessages.retry}</Text>
          </Pressable>
        </View>
      ) : null}
      {devices.isLoading ? <View style={styles.skeleton} /> : null}
      {devices.data ? <DeviceList devices={devices.data.devices} pendingId={pendingId} onRevoke={(device) => void revoke(device)} /> : null}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: theme.colors.bg },
  content: { gap: 12, paddingHorizontal: 24 },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 28, lineHeight: 34 },
  body: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 16, lineHeight: 24 },
  block: { gap: 8 },
  row: { minHeight: 44, justifyContent: 'center' },
  link: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  skeleton: { height: 88, borderRadius: 12, backgroundColor: theme.colors.skeleton },
});
