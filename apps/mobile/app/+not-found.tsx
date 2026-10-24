import { Link, Stack } from 'expo-router';
import { StyleSheet, Text, View } from 'react-native';

import { theme } from '@/theme/theme';

export default function NotFoundScreen() {
  return (
    <>
      <Stack.Screen options={{ title: 'No encontrada', gestureEnabled: false }} />
      <View style={styles.screen}>
        <Text style={styles.body}>Esta pantalla no existe.</Text>
        <Link href="/login" replace style={styles.link}>
          Volver a ingresar
        </Link>
      </View>
    </>
  );
}

const styles = StyleSheet.create({
  screen: {
    flex: 1,
    justifyContent: 'center',
    gap: 12,
    backgroundColor: theme.colors.bg,
    padding: 24,
  },
  body: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sans,
    fontSize: 16,
  },
  link: {
    color: theme.colors.primary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
    paddingHorizontal: 20,
  },
});
