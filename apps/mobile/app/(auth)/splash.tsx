import { useRouter } from 'expo-router';
import { useEffect, useRef } from 'react';
import { Animated, Image, StyleSheet, View } from 'react-native';

import { theme } from '@/theme/theme';

const logo = require('../../assets/images/brand/situ-logo-vertical-naranja.png');

export default function SplashRoute() {
  const router = useRouter();
  const opacity = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    const animation = Animated.timing(opacity, {
      toValue: 1,
      duration: 400,
      useNativeDriver: true,
    });

    animation.start(({ finished }) => {
      if (finished) {
        router.replace('/login');
      }
    });

    return () => {
      animation.stop();
    };
  }, [opacity, router]);

  return (
    <View style={styles.screen}>
      <View style={styles.glow} />
      <Animated.View style={{ opacity }}>
        <Image source={logo} resizeMode="contain" accessibilityLabel="SITU" style={styles.logo} />
      </Animated.View>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: theme.colors.bgDarkEdge,
  },
  glow: {
    position: 'absolute',
    width: 520,
    height: 520,
    borderRadius: 260,
    backgroundColor: theme.colors.bgDarkCenter,
  },
  logo: {
    width: 130,
    height: 142,
  },
});
