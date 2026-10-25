import { Component, type ReactNode } from 'react';
import { Image, Pressable, StyleSheet, Text, View } from 'react-native';

import { theme } from '@/theme/theme';

const pin = require('../../assets/images/icons/situ-pin-mapa-dark.png');

export function MapUnavailable({ missingVariable, onRetry }: { missingVariable?: string | null; onRetry: () => void }) {
  return (
    <View style={styles.box}>
      <Image source={pin} style={styles.icon} resizeMode="contain" accessibilityIgnoresInvertColors />
      <Text style={styles.title}>El mapa no está disponible en este dispositivo todavía</Text>
      <Pressable accessibilityRole="button" accessibilityLabel="Reintentar" onPress={onRetry} style={styles.button}>
        <Text style={styles.buttonLabel}>Reintentar</Text>
      </Pressable>
      {typeof __DEV__ !== 'undefined' && __DEV__ && missingVariable ? (
        <Text style={styles.detail}>Falta {missingVariable}.</Text>
      ) : null}
    </View>
  );
}

type BoundaryState = { failed: boolean };

export class MapErrorBoundary extends Component<{ children: ReactNode }, BoundaryState> {
  state: BoundaryState = { failed: false };

  static getDerivedStateFromError(): BoundaryState {
    return { failed: true };
  }

  render() {
    if (this.state.failed) {
      return <MapUnavailable onRetry={() => this.setState({ failed: false })} />;
    }
    return this.props.children;
  }
}

const styles = StyleSheet.create({
  box: { flex: 1, justifyContent: 'center', alignItems: 'center', gap: 16, padding: 24, backgroundColor: theme.colors.bg },
  icon: { width: 48, height: 60 },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 16, lineHeight: 24, textAlign: 'center' },
  button: { minHeight: 48, paddingHorizontal: 20, borderRadius: theme.radius.button, backgroundColor: theme.colors.primary, alignItems: 'center', justifyContent: 'center' },
  buttonLabel: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  detail: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 13, textAlign: 'center' },
});
