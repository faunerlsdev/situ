import { useLocalSearchParams } from 'expo-router';
import { StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { ReportForm } from '@/features/reports/report-form';
import { theme } from '@/theme/theme';

export default function ReportarScreen() {
  const params = useLocalSearchParams<{ categoryId?: string }>();
  const insets = useSafeAreaInsets();
  const categoryId = typeof params.categoryId === 'string' ? params.categoryId : '';

  return (
    <View style={[styles.screen, { paddingTop: insets.top }]}>
      <ReportForm categoryId={categoryId} />
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: theme.colors.bg },
});
