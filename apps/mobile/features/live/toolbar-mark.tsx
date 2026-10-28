import { useEffect, useRef, useState } from 'react';
import { AccessibilityInfo, Animated, Image, Pressable, StyleSheet, Text, View } from 'react-native';

import { theme } from '@/theme/theme';

const mark = require('../../assets/images/icons/nav-en-vivo-dark.png');

/** Transparent S-with-waves mark. The app chrome uses the dark surface, and this asset stays readable on it. */
export function LiveToolbarMark({ count, onPress }: { count: number; onPress: () => void }) {
  const [reduceMotion, setReduceMotion] = useState(false);
  const pulse = useRef(new Animated.Value(1)).current;
  const active = count > 0;

  useEffect(() => {
    void AccessibilityInfo.isReduceMotionEnabled().then(setReduceMotion);
  }, []);

  useEffect(() => {
    if (!active || reduceMotion) {
      pulse.setValue(1);
      return;
    }
    const loop = Animated.loop(
      Animated.sequence([
        Animated.timing(pulse, { toValue: 0.72, duration: 900, useNativeDriver: true }),
        Animated.timing(pulse, { toValue: 1, duration: 900, useNativeDriver: true }),
      ]),
    );
    loop.start();
    return () => loop.stop();
  }, [active, pulse, reduceMotion]);

  const badge = count > 9 ? '9+' : String(count);

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel="Ver transmisiones en vivo"
      accessibilityHint={active ? `${count} transmisiones activas` : 'Nadie está en vivo'}
      onPress={onPress}
      style={styles.hit}
    >
      <Animated.View style={{ opacity: pulse }}>
        <Image source={mark} resizeMode="contain" style={styles.mark} />
      </Animated.View>
      {active ? (
        <View style={styles.badge}>
          <Text style={styles.badgeText}>{badge}</Text>
        </View>
      ) : null}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  hit: { minWidth: 44, minHeight: 44, alignItems: 'center', justifyContent: 'center' },
  mark: { width: 72, height: 32 },
  badge: {
    position: 'absolute',
    top: 2,
    right: 0,
    minWidth: 16,
    height: 16,
    paddingHorizontal: 4,
    borderRadius: 8,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: theme.colors.live,
  },
  badgeText: { color: theme.colors.textOnDanger, fontFamily: theme.fontFamily.sansBold, fontSize: 10 },
});
