import { notificationMessages, relativeTime } from '@situ/shared';
import type { InboxNotification } from '@situ/shared';
import { Pressable, StyleSheet, Text, View } from 'react-native';

import { LiveMark } from '@/features/live/live-mark';
import { theme } from '@/theme/theme';

export function NotificationRow({ item, onPress }: { item: InboxNotification; onPress: (item: InboxNotification) => void }) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`${item.title}. ${item.body}`}
      accessibilityState={{ disabled: !item.available }}
      onPress={() => onPress(item)}
      style={[styles.row, !item.available && styles.dim]}
    >
      {item.type === 'FriendLiveStarted' || item.type === 'FriendLiveEnded' || item.type === 'NearbyLiveStarted' || item.targetType === 'LiveStream' ? (
        <LiveMark size={36} label="Transmisión en vivo" />
      ) : (
        <View style={styles.icon}>
          <Text style={styles.iconLabel}>{iconFor(item.type)}</Text>
        </View>
      )}
      <View style={styles.copy}>
        <Text style={styles.title}>{item.title}</Text>
        <Text style={styles.body}>{item.body}</Text>
        <Text style={styles.time}>{relativeTime(item.createdAt)}</Text>
        {!item.available ? <Text style={styles.gone}>{notificationMessages.unavailable}</Text> : null}
      </View>
      {!item.isRead && item.available ? <View accessibilityLabel="No leída" style={styles.dot} /> : null}
    </Pressable>
  );
}

function iconFor(type: string): string {
  if (type === 'Comment') {
    return '💬';
  }

  if (type === 'FriendReport') {
    return '👤';
  }

  if (type === 'Digest') {
    return '•';
  }

  return '📍';
}

const styles = StyleSheet.create({
  row: { minHeight: 44, flexDirection: 'row', gap: 12, paddingVertical: 12, paddingHorizontal: 16, alignItems: 'flex-start' },
  dim: { opacity: 0.45 },
  icon: { width: 36, height: 36, borderRadius: 18, alignItems: 'center', justifyContent: 'center', backgroundColor: theme.colors.surface },
  iconLabel: { fontSize: 16 },
  copy: { flex: 1, gap: 2 },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  body: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 15, lineHeight: 20 },
  time: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 13 },
  gone: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sansBold, fontSize: 13 },
  dot: { width: 8, height: 8, borderRadius: 4, marginTop: 8, backgroundColor: theme.colors.primary },
});
