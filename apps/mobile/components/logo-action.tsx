import { useState } from 'react';
import { Image, Pressable, StyleSheet, Text } from 'react-native';

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
      <Image source={isotipo} resizeMode="contain" style={styles.mark} />
      <Text style={styles.label}>{pending ? pendingLabel : label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  hit: {
    alignItems: 'center',
    gap: 4,
  },
  pressed: {
    transform: [{ scale: 0.95 }],
  },
  disabled: {
    opacity: 0.6,
  },
  mark: {
    width: 72,
    height: 94,
  },
  label: {
    color: theme.colors.white,
    fontFamily: theme.fontFamily.sansBoldItalic,
    fontSize: 28,
    lineHeight: 34,
    textShadowColor: theme.colors.black,
    textShadowOffset: { width: 2, height: 2 },
    textShadowRadius: 0,
  },
});
