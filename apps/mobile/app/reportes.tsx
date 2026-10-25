import { useRouter } from 'expo-router';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { MyReports } from '@/features/reports/my-reports';
import { theme } from '@/theme/theme';

export default function ReportesScreen() {
  const router = useRouter();
  const insets = useSafeAreaInsets();

  return (
    <View style={[styles.screen, { paddingTop: insets.top }]}>
      <Pressable accessibilityRole="button" accessibilityLabel="Volver" onPress={() => router.back()} style={styles.back}>
        <Text style={styles.backLabel}>Volver</Text>
      </Pressable>
      <Text style={styles.title}>Mis reportes</Text>
      <MyReports />
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: theme.colors.bg },
  back: { minHeight: 44, justifyContent: 'center', paddingHorizontal: 20 },
  backLabel: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 28, paddingHorizontal: 20, marginBottom: 8 },
});