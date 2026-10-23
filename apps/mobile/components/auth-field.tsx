import type { ReactNode } from 'react';
import { StyleSheet, TextInput, View } from 'react-native';
import type { TextInputProps } from 'react-native';

import { theme } from '@/theme/theme';

export function AuthField({ icon, ...input }: { icon: ReactNode } & TextInputProps) {
  return (
    <View style={styles.field}>
      <View style={styles.icon}>{icon}</View>
      <TextInput
        placeholderTextColor={theme.colors.gray400}
        {...input}
        style={[styles.input, input.style]}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  field: {
    height: 52,
    width: '80%',
    maxWidth: 360,
    alignSelf: 'center',
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    borderRadius: theme.radius.input,
    backgroundColor: theme.colors.white,
    paddingHorizontal: 12,
  },
  icon: {
    width: 22,
    alignItems: 'center',
  },
  input: {
    flex: 1,
    color: theme.colors.ink,
    fontFamily: theme.fontFamily.sansBoldItalic,
    fontSize: 16,
    paddingVertical: 0,
  },
});
