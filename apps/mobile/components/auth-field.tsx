import { useState } from 'react';
import type { ReactNode } from 'react';
import { StyleSheet, TextInput, View } from 'react-native';
import type { TextInputProps } from 'react-native';

import { theme } from '@/theme/theme';

export function AuthField({ icon, ...input }: { icon: ReactNode } & TextInputProps) {
  const [focused, setFocused] = useState(false);

  return (
    <View style={[styles.field, focused && styles.focused]}>
      <View style={styles.icon}>{icon}</View>
      <TextInput
        placeholderTextColor={theme.colors.textPlaceholder}
        keyboardAppearance="dark"
        {...input}
        onFocus={(event) => {
          setFocused(true);
          input.onFocus?.(event);
        }}
        onBlur={(event) => {
          setFocused(false);
          input.onBlur?.(event);
        }}
        style={[styles.input, input.style]}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  field: {
    height: 52,
    width: '100%',
    alignSelf: 'stretch',
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    borderRadius: theme.radius.input,
    borderWidth: 1,
    borderColor: theme.colors.border,
    backgroundColor: theme.colors.input,
    paddingHorizontal: 12,
  },
  focused: {
    borderWidth: 1.5,
    borderColor: theme.colors.primary,
  },
  icon: {
    width: 22,
    alignItems: 'center',
  },
  input: {
    flex: 1,
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
    paddingVertical: 0,
  },
});
