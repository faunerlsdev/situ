import { feedbackMessages } from '@situ/shared';
import { useLocalSearchParams } from 'expo-router';
import { StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { theme } from '@/theme/theme';

export default function AdiosScreen() {
  const params = useLocalSearchParams<{ date?: string }>();
  const insets = useSafeAreaInsets();
  const date = typeof params.date === 'string' && params.date.length > 0 ? params.date : feedbackMessages.deletionPendingUnknown;

  return (
    <View style={[styles.screen, { paddingTop: insets.top + 48, paddingBottom: insets.bottom + 24 }]}>
      <Text style={styles.title}>{feedbackMessages.goodbyeTitle}</Text>
      <Text style={styles.body}>La fecha programada es el {date}. Si ingresas antes, puedes cancelar la eliminación.</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, gap: 16, paddingHorizontal: 24, backgroundColor: theme.colors.bg },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 28, lineHeight: 34 },
  body: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 16, lineHeight: 24 },
});
