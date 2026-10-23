import type { ReactNode } from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { theme } from '@/theme/theme';

export function Field({
  label,
  hint,
  children,
}: {
  label: string;
  hint?: string;
  children: ReactNode;
}) {
  return (
    <View style={styles.field}>
      <Text style={styles.label}>{label}</Text>
      {hint ? <Text style={styles.hint}>{hint}</Text> : null}
      {children}
    </View>
  );
}

export const inputStyle = StyleSheet.create({
  control: {
    minHeight: 48,
    borderWidth: 1,
    borderColor: theme.colors.gray200,
    borderRadius: 14,
    backgroundColor: theme.colors.white,
    paddingHorizontal: 14,
    paddingVertical: 12,
    color: theme.colors.ink,
    fontFamily: theme.fontFamily.sans,
    fontSize: 16,
  },
}).control;

const styles = StyleSheet.create({
  field: {
    gap: 6,
  },
  label: {
    color: theme.colors.ink,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 14,
  },
  hint: {
    color: theme.colors.gray600,
    fontFamily: theme.fontFamily.sans,
    fontSize: 13,
    lineHeight: 18,
  },
});
