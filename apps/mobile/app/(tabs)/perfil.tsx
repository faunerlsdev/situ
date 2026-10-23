import { messageFromUnknown } from '@situ/shared';
import { useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';

import { StatusMessage } from '@/components/status-message';
import { useSession } from '@/providers/session-provider';
import { theme } from '@/theme/theme';

export default function ProfileScreen() {
  const { signOut } = useSession();
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function onSignOut() {
    setError(null);
    setPending(true);

    try {
      await signOut();
    } catch (cause) {
      setError(messageFromUnknown(cause));
      setPending(false);
    }
  }

  return (
    <View style={styles.screen}>
      <Text style={styles.title}>Perfil</Text>
      <Text style={styles.body}>La sesión queda en el llavero del teléfono.</Text>
      <StatusMessage error={error} notice={null} />
      <Pressable accessibilityRole="button" disabled={pending} onPress={() => void onSignOut()} style={styles.button}>
        <Text style={styles.buttonLabel}>{pending ? 'Cerrando sesión…' : 'Cerrar sesión'}</Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: {
    flex: 1,
    gap: 12,
    padding: 20,
    backgroundColor: theme.colors.surface,
  },
  title: {
    color: theme.colors.ink,
    fontFamily: theme.fontFamily.display,
    fontSize: 28,
  },
  body: {
    color: theme.colors.gray600,
    fontFamily: theme.fontFamily.sans,
    fontSize: 16,
    lineHeight: 24,
  },
  button: {
    minHeight: 48,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: theme.radius.button,
    backgroundColor: theme.colors.primary,
  },
  buttonLabel: {
    color: theme.colors.white,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
  },
});
