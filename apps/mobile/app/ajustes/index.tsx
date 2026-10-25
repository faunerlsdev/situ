import { notificationMessages } from '@situ/shared';
import { useRouter } from 'expo-router';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { theme } from '@/theme/theme';

export default function SettingsMenu() {
  const router = useRouter();
  const insets = useSafeAreaInsets();

  return (
    <View style={[styles.screen, { paddingTop: insets.top + 12 }]}>
      <Pressable accessibilityRole="button" accessibilityLabel="Volver" onPress={() => router.back()} style={styles.row}>
        <Text style={styles.link}>Volver</Text>
      </Pressable>
      <Text style={styles.title}>{notificationMessages.settingsMenu}</Text>
      <Pressable accessibilityRole="button" accessibilityLabel={notificationMessages.settingsTitle} onPress={() => router.push('/ajustes/notificaciones')} style={styles.row}>
        <Text style={styles.label}>{notificationMessages.settingsTitle}</Text>
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
});
