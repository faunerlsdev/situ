import { Pressable, StyleSheet, Text } from 'react-native';

import { theme } from '@/theme/theme';

export function PrimaryButton({
  label,
  pendingLabel,
  pending,
  disabled,
  onPress,
}: {
  label: string;
  pendingLabel: string;
  pending?: boolean;
  disabled?: boolean;
  onPress: () => void;
}) {
  const isDisabled = disabled === true || pending === true;

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ disabled: isDisabled, busy: pending === true }}
      disabled={isDisabled}
      onPress={onPress}
      style={({ pressed }) => [styles.button, isDisabled && styles.disabled, pressed && !isDisabled && styles.pressed]}
    >
      <Text style={styles.label}>{pending ? pendingLabel : label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  button: {
    minHeight: 48,
    borderRadius: 999,
    backgroundColor: theme.colors.primary,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 20,
  },
  pressed: {
    backgroundColor: theme.colors.primaryPressed,
  },
  disabled: {
    opacity: 0.6,
  },
  label: {
    color: theme.colors.white,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
  },
});
