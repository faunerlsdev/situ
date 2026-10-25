import type { SessionDevice } from '@situ/shared';
import { profileMessages } from '@situ/shared';
import { Pressable, StyleSheet, Text, View } from 'react-native';

import { theme } from '@/theme/theme';

import { canRevokeDevice } from './logic';

function platformLabel(platform: SessionDevice['platform']): string {
  if (platform === 'IOS') {
    return 'iPhone';
  }
  if (platform === 'Android') {
    return 'Android';
  }
  return 'Navegador';
}

function seenLabel(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return 'Sin actividad reciente';
  }
  return `${profileMessages.lastSeen}: ${date.toLocaleString('es-CO', { dateStyle: 'medium', timeStyle: 'short' })}`;
}

export function DeviceList({
  devices,
  pendingId,
  onRevoke,
}: {
  devices: SessionDevice[];
  pendingId: string | null;
  onRevoke: (device: SessionDevice) => void;
}) {
  const active = devices.filter((device) => device.isActive);
  if (active.length === 0) {
    return <Text style={styles.meta}>{profileMessages.devicesEmpty}</Text>;
  }

  return (
    <View style={styles.list}>
      {active.map((device) => {
        const current = device.isCurrent === true;
        const name = device.deviceName?.trim() || platformLabel(device.platform);
        return (
          <View key={device.id} style={styles.card}>
            <Text style={styles.name}>{name}</Text>
            <Text style={styles.meta}>{platformLabel(device.platform)}</Text>
            <Text style={styles.meta}>{seenLabel(device.lastSeenAt)}</Text>
            {current ? <Text style={styles.current}>{profileMessages.thisDevice}</Text> : null}
            {canRevokeDevice(current) ? (
              <Pressable
                accessibilityRole="button"
                accessibilityLabel={`${profileMessages.revoke} ${name}`}
                disabled={pendingId !== null}
                onPress={() => onRevoke(device)}
                style={styles.action}
              >
                <Text style={styles.actionLabel}>{pendingId === device.id ? 'Cerrando sesión…' : profileMessages.revoke}</Text>
              </Pressable>
            ) : null}
          </View>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  list: { gap: 12 },
  card: { gap: 4, padding: 16, borderRadius: 12, borderWidth: 1, borderColor: theme.colors.border },
  name: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 18 },
  meta: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 14, lineHeight: 20 },
  current: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 14 },
  action: { minHeight: 44, justifyContent: 'center' },
  actionLabel: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
});
