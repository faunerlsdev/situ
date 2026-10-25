import { useLocalSearchParams, useRouter } from 'expo-router';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { ReportDetail } from '@/features/reports/report-detail';
import { theme } from '@/theme/theme';

export default function ReporteScreen() {
  const params = useLocalSearchParams<{ id: string; focus?: string }>();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const id = typeof params.id === 'string' ? params.id : '';

  return (
    <View style={[styles.screen, { paddingTop: insets.top }]}>
      <Pressable accessibilityRole="button" accessibilityLabel="Volver" onPress={() => router.back()} style={styles.back}>
        <Text style={styles.backLabel}>Volver</Text>
      </Pressable>
      <ReportDetail id={id} focusComments={params.focus === 'comentarios'} />
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: theme.colors.bg },
  back: { minHeight: 44, justifyContent: 'center', paddingHorizontal: 20 },
  backLabel: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
});
