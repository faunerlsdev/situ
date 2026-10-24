import type { ReactNode } from 'react';
import { ScrollView, StyleSheet } from 'react-native';

import { theme } from '@/theme/theme';

export function Screen({ children }: { children: ReactNode }) {
  return (
    <ScrollView
      contentInsetAdjustmentBehavior="automatic"
      keyboardShouldPersistTaps="handled"
      contentContainerStyle={styles.content}
    >
      {children}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  content: {
    gap: 16,
    padding: 20,
    backgroundColor: theme.colors.bg,
  },
});
