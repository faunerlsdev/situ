import { StyleSheet, Text } from 'react-native';

import { theme } from '@/theme/theme';

export function StatusMessage({ error, notice }: { error: string | null; notice: string | null }) {
  if (error) {
    return (
      <Text accessibilityRole="alert" style={[styles.box, styles.error]}>
        {error}
      </Text>
    );
  }

  if (notice) {
    return (
      <Text accessibilityRole="text" accessibilityLiveRegion="polite" style={[styles.box, styles.notice]}>
        {notice}
      </Text>
    );
  }

  return null;
}

const styles = StyleSheet.create({
  box: {
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
    lineHeight: 22,
  },
  error: {
    color: theme.colors.danger,
  },
  notice: {
    color: theme.colors.textPrimary,
  },
});
