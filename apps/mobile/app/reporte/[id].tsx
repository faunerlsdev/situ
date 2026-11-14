import { useLocalSearchParams, useRouter } from 'expo-router';
import { SymbolView } from 'expo-symbols';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { ReportDetail } from '@/features/reports/report-detail';
import { theme } from '@/theme/theme';

export default function ReporteScreen() {
  const params = useLocalSearchParams<{ id: string; focus?: string; foto?: string }>();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const id = typeof params.id === 'string' ? params.id : '';

  return (
    <View style={[styles.screen, { paddingTop: insets.top }]}>
      <Pressable accessibilityRole="button" accessibilityLabel="Volver" onPress={() => router.back()} style={styles.back}>
        <SymbolView name={{ ios: 'chevron.left', android: 'arrow_back', web: 'arrow_back' }} tintColor={theme.colors.textSecondary} size={18} />
        <Text style={styles.backLabel}>Volver</Text>
      </Pressable>
      <ReportDetail id={id} focusComments={params.focus === 'comentarios'} openPhoto={params.foto === '1'} />
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: theme.colors.bg },
  back: { minHeight: 44, flexDirection: 'row', alignItems: 'center', gap: 4, paddingHorizontal: 16 },
  backLabel: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 15 },
});
