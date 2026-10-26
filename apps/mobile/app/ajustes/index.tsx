import { notificationMessages, notify, profileMessages } from '@situ/shared';
import { useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useMe } from '@/features/reports/use-me';
import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

export default function SettingsMenu() {
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const me = useMe();
  const queryClient = useQueryClient();
  const [savingIdentity, setSavingIdentity] = useState(false);
  const preferAnonymous = me.data?.profile.preferAnonymousReports !== false;

  async function toggleIdentity() {
    if (savingIdentity || !me.data) {
      return;
    }
    setSavingIdentity(true);
    try {
      await api.updateMyProfile({ preferAnonymousReports: !preferAnonymous });
      await queryClient.invalidateQueries({ queryKey: ['me'] });
    } catch (error) {
      notify.error(error);
    } finally {
      setSavingIdentity(false);
    }
  }

  return (
    <View style={[styles.screen, { paddingTop: insets.top + 12 }]}>
      <Pressable accessibilityRole="button" accessibilityLabel="Volver" onPress={() => router.back()} style={styles.row}>
        <Text style={styles.link}>Volver</Text>
      </Pressable>
      <Text style={styles.title}>{notificationMessages.settingsMenu}</Text>
      <Pressable accessibilityRole="button" accessibilityLabel={notificationMessages.settingsTitle} onPress={() => router.push('/ajustes/notificaciones')} style={styles.row}>
        <Text style={styles.label}>{notificationMessages.settingsTitle}</Text>
      </Pressable>
      <Pressable
        accessibilityRole="switch"
        accessibilityState={{ checked: preferAnonymous, disabled: !me.data || savingIdentity }}
        accessibilityLabel={profileMessages.reportIdentityTitle}
        disabled={!me.data || savingIdentity}
        onPress={() => void toggleIdentity()}
        style={styles.identity}
      >
        <View style={styles.identityCopy}>
          <Text style={styles.label}>{profileMessages.reportIdentityTitle}</Text>
          <Text style={styles.hint}>{profileMessages.reportIdentityBody}</Text>
        </View>
        <View style={[styles.knob, preferAnonymous && styles.knobOn]} />
      </Pressable>
      <Pressable accessibilityRole="button" accessibilityLabel={notificationMessages.privacy} onPress={() => router.push('/bloqueados')} style={styles.row}>
        <Text style={styles.label}>{notificationMessages.privacy}</Text>
      </Pressable>
      <Pressable accessibilityRole="button" accessibilityLabel={notificationMessages.account} onPress={() => router.push('/cuenta')} style={styles.row}>
        <Text style={styles.label}>{notificationMessages.account}</Text>
      </Pressable>
      <View style={styles.row}>
        <Text style={styles.label}>{notificationMessages.comingSoon}</Text>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: theme.colors.bg, paddingHorizontal: 20, gap: 4 },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 28, lineHeight: 34, marginBottom: 8 },
  row: { minHeight: 44, justifyContent: 'center' },
  label: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 18 },
  link: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  identity: { minHeight: 44, flexDirection: 'row', alignItems: 'center', gap: 12, paddingVertical: 8 },
  identityCopy: { flex: 1, gap: 4 },
  hint: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 13, lineHeight: 18 },
  knob: { width: 44, height: 28, borderRadius: 14, backgroundColor: theme.colors.border },
  knobOn: { backgroundColor: theme.colors.primary },
});
