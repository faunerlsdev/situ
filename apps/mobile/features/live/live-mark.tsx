import { useEffect, useRef, useState } from 'react';
import { AccessibilityInfo, Animated, Image, StyleSheet, View } from 'react-native';

import { theme } from '@/theme/theme';

const waves = require('../../assets/images/icons/live-mark.png');

/** Round live marker. The S-with-waves asset sits on a light disc so it stays readable on the dark map and on a light tile. */
export function LiveMark({ size = 44, pulse = false, label = 'Transmisión en vivo' }: { size?: number; pulse?: boolean; label?: string }) {
  const [reduceMotion, setReduceMotion] = useState(false);
  const ring = useRef(new Animated.Value(0.35)).current;

  useEffect(() => {
    void AccessibilityInfo.isReduceMotionEnabled().then(setReduceMotion);
  }, []);

  useEffect(() => {
    if (!pulse || reduceMotion) {
      ring.setValue(0.35);
      return;
    }
    const loop = Animated.loop(
      Animated.sequence([
        Animated.timing(ring, { toValue: 0.9, duration: 900, useNativeDriver: true }),
        Animated.timing(ring, { toValue: 0.35, duration: 900, useNativeDriver: true }),
      ]),
    );
    loop.start();
    return () => loop.stop();
  }, [pulse, reduceMotion, ring]);

  const disc = Math.round(size * 0.72);

  return (
    <View accessibilityRole="image" accessibilityLabel={label} style={[styles.hit, { width: size, height: size }]}>
      {pulse && !reduceMotion ? <Animated.View style={[styles.ring, { opacity: ring, borderRadius: size / 2 }]} /> : null}
      <View style={[styles.disc, { width: disc, height: disc, borderRadius: disc / 2 }]}>
        <Image source={waves} resizeMode="contain" style={{ width: disc - 6, height: disc * 0.46 }} />
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  hit: { alignItems: 'center', justifyContent: 'center' },
  ring: { ...StyleSheet.absoluteFill, borderWidth: 2, borderColor: theme.colors.live },
  disc: { alignItems: 'center', justifyContent: 'center', backgroundColor: '#FFFFFF' },
});
