import { useState } from 'react';
import { Image, Pressable, StyleSheet, Text, View } from 'react-native';

import { theme } from '@/theme/theme';

const isotipo = require('../assets/images/brand/situ-isotipo-negro.png');

export function LogoAction({
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
  const [pressed, setPressed] = useState(false);
  const isDisabled = disabled === true || pending === true;

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ disabled: isDisabled, busy: pending === true }}
      disabled={isDisabled}
      onPress={onPress}
      onPressIn={() => setPressed(true)}
      onPressOut={() => setPressed(false)}
      style={[styles.hit, pressed && !isDisabled && styles.pressed, isDisabled && styles.disabled]}
    >
      <View style={styles.button}>
        <Image source={isotipo} resizeMode="contain" style={styles.mark} />
        <Text style={styles.label}>{pending ? pendingLabel : label}</Text>
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  hit: {
    width: '80%',
    maxWidth: 360,
    alignSelf: 'center',
    alignItems: 'center',
    gap: 8,
  },
  button: {
    height: 52,
    width: '100%',
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 10,
    borderRadius: theme.radius.button,
    backgroundColor: theme.colors.primary,
  },
  pressed: {
    transform: [{ scale: 0.95 }],
  },
  disabled: {
    opacity: 0.6,
  },
  mark: {
    width: 22,
    height: 28,
  },
  label: {
    color: theme.colors.textOnPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 18,
    lineHeight: 22,
  },
});
