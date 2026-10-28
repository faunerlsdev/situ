import { useSyncExternalStore } from 'react';
import { Platform, Pressable, StyleSheet, Text, View } from 'react-native';
import { useRouter } from 'expo-router';
import { SymbolView } from 'expo-symbols';

import { liveStartState } from '@/features/live/logic';
import { getLiveQuotaFull, subscribeLiveQuota } from '@/features/live/quota';
import { livePublisherAvailable } from '@/features/live/publisher';
import { useMe } from '@/features/reports/use-me';
import { theme } from '@/theme/theme';

const transmitIcon = { ios: 'video.fill', android: 'videocam', web: 'videocam' } as const;

export function TransmitLiveControl({ label, variant = 'button' }: { label: string; variant?: 'button' | 'icon' }) {
  const router = useRouter();
  const me = useMe();
  const quotaFull = useSyncExternalStore(subscribeLiveQuota, getLiveQuotaFull, getLiveQuotaFull);
  const platform = Platform.OS === 'ios' ? 'ios' : Platform.OS === 'android' ? 'android' : 'web';
  const state = liveStartState({
    profileKnown: me.data !== undefined,
    isMinor: me.data?.isMinor === true,
    canPublish: me.data?.canPublish === true,
    publishBlockedReason: me.data?.publishBlockedReason ?? null,
    platform,
    publisherAvailable: livePublisherAvailable(),
    quotaFull,
  });

  const reason = state.reason;
  return (
    <View style={variant === 'icon' ? styles.iconBlock : styles.block}>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={reason ? `${label}. ${reason}` : label}
        accessibilityState={{ disabled: !state.enabled }}
        disabled={!state.enabled}
        onPress={() => router.push('/en-vivo/iniciar')}
        style={[variant === 'icon' ? styles.iconButton : styles.button, !state.enabled && styles.disabled]}
      >
        {variant === 'icon' ? (
          <SymbolView name={transmitIcon} tintColor={theme.colors.live} size={26} />
        ) : (
          <Text style={styles.label}>{label}</Text>
        )}
      </Pressable>
      {reason && variant === 'button' ? <Text style={styles.reason}>{reason}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  block: { flex: 1, gap: 6 },
  button: {
    minHeight: 48,
    borderRadius: theme.radius.button,
    backgroundColor: theme.colors.primary,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 12,
  },
  disabled: { opacity: 0.45 },
  label: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16, textAlign: 'center' },
  reason: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 13, lineHeight: 18 },
  iconBlock: { width: 44, height: 44 },
  iconButton: { width: 44, height: 44, alignItems: 'center', justifyContent: 'center' },
});
