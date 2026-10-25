import { deletionDateLabel, feedbackMessages, notify } from '@situ/shared';
import type { SessionProfile } from '@situ/shared';
import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useMe } from '@/features/reports/use-me';
import { api } from '@/lib/api';
import { useSession } from '@/providers/session-provider';
import { theme } from '@/theme/theme';

export function DeletionPending() {
  const me = useMe();
  const { signOut } = useSession();
  const queryClient = useQueryClient();
  const insets = useSafeAreaInsets();
  const [pending, setPending] = useState(false);
  const date = deletionDateLabel(scheduledFor(me.data));

  async function cancel() {
    setPending(true);
    try {
      await api.cancelAccountDeletion();
      await queryClient.invalidateQueries({ queryKey: ['me'] });
      notify.success(feedbackMessages.deletionCancelled);
    } catch (error) {
      notify.error(error, { action: { label: feedbackMessages.retry, onPress: () => void cancel() } });
    } finally {
      setPending(false);
    }
  }

  return (
    <View style={[styles.screen, { paddingTop: insets.top + 32, paddingBottom: insets.bottom + 24 }]}>
      <Text style={styles.title}>{feedbackMessages.deletionPendingTitle} {date}</Text>
      <Text style={styles.body}>Mientras tanto no puedes publicar. Si cambias de opinión, cancela la eliminación.</Text>
      <Pressable accessibilityRole="button" accessibilityLabel={feedbackMessages.cancelDeletion} disabled={pending} onPress={() => void cancel()} style={styles.button}>
        <Text style={styles.buttonLabel}>{pending ? 'Cancelando…' : feedbackMessages.cancelDeletion}</Text>
      </Pressable>
      <Pressable accessibilityRole="button" accessibilityLabel="Cerrar sesión" onPress={() => void signOut()} style={styles.secondary}>
        <Text style={styles.secondaryLabel}>Cerrar sesión</Text>
      </Pressable>
    </View>
  );
}

function scheduledFor(profile: SessionProfile | undefined): string | null {
  if (!profile) {
    return null;
  }
  const value = (profile as SessionProfile & { deletionScheduledFor?: string | null }).deletionScheduledFor;
  return typeof value === 'string' ? value : null;
}

const styles = StyleSheet.create({
  screen: { flex: 1, gap: 16, paddingHorizontal: 24, backgroundColor: theme.colors.bg },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 28, lineHeight: 34 },
  body: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 16, lineHeight: 24 },
  button: { minHeight: 44, borderRadius: theme.radius.button, backgroundColor: theme.colors.primary, alignItems: 'center', justifyContent: 'center' },
  buttonLabel: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  secondary: { minHeight: 44, alignItems: 'center', justifyContent: 'center' },
  secondaryLabel: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
});
