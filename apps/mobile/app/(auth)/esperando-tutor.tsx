import { Link } from 'expo-router';
import { ScrollView, StyleSheet, Text } from 'react-native';

import { theme } from '@/theme/theme';

export default function WaitingGuardianScreen() {
  return (
    <ScrollView contentContainerStyle={styles.content}>
      <Text style={styles.title}>Esperando autorización del tutor</Text>
      <Text style={styles.body}>
        Creamos la cuenta. Confirma tu correo cuando te llegue el enlace. Tu acudiente debe abrir el suyo, verificar su identidad y autorizar la cuenta. Hasta entonces no puedes ingresar.
      </Text>
      <Link href="/confirmar-correo" style={styles.link}>Confirmar mi correo</Link>
      <Link href="/login" style={styles.link}>Volver a ingresar</Link>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  content: {
    flexGrow: 1,
    justifyContent: 'center',
    gap: 16,
    padding: 24,
    backgroundColor: theme.colors.primary,
  },
  title: {
    color: theme.colors.white,
    fontFamily: theme.fontFamily.displayHeavyItalic,
    fontSize: 40,
    textAlign: 'center',
  },
  body: {
    color: theme.colors.white,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
    lineHeight: 22,
    textAlign: 'center',
  },
  link: {
    color: theme.colors.white,
    fontFamily: theme.fontFamily.sansBoldItalic,
    fontSize: 16,
    textAlign: 'center',
  },
});
