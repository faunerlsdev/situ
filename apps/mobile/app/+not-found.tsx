import { Link, Stack } from 'expo-router';
import { StyleSheet, Text } from 'react-native';

import { theme } from '@/theme/theme';

export default function NotFoundScreen() {
  return (
    <>
      <Stack.Screen options={{ title: 'No encontrada' }} />
      <Text style={styles.body}>Esta pantalla no existe.</Text>
      <Link href="/login" style={styles.link}>
        Volver al inicio
      </Link>
    </>
  );
}

const styles = StyleSheet.create({
  body: {
    color: theme.colors.ink,
    fontFamily: theme.fontFamily.sans,
    fontSize: 16,
    padding: 20,
  },
  link: {
    color: theme.colors.primary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
    paddingHorizontal: 20,
  },
});
