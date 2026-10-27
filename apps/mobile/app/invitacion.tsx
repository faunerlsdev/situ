import { notify } from '@situ/shared';
import { Link, useLocalSearchParams, useRouter } from 'expo-router';
import { useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { PrimaryButton } from '@/components/primary-button';
import { api } from '@/lib/api';
import { searchParam } from '@/lib/device';
import { useSession } from '@/providers/session-provider';
import { theme } from '@/theme/theme';

export default function InvitationScreen() {
  const params = useLocalSearchParams<{ token?: string | string[] }>();
  const token = searchParam(params.token);
  const { isSignedIn } = useSession();
  const router = useRouter();
  const [pending, setPending] = useState(false);

  async function accept() {
    if (token.length === 0) {
      notify.warning('Este enlace no trae una invitación.');
      return;
    }
    setPending(true);
    try {
      const result = await api.acceptFriendInvitation(token);
      notify.success(result.status === 'friends' ? 'Ya eran amigos. La invitación quedó usada.' : 'Listo. La solicitud quedó en Amigos.');
      router.replace('/amigos');
    } catch (error) {
      notify.error(error);
    } finally {
      setPending(false);
    }
  }

  return (
    <View style={styles.screen}>
      <Text style={styles.title}>Invitación de amistad</Text>
      {isSignedIn ? (
        <>
          <Text style={styles.body}>El enlace se usa una sola vez y tiene que abrirse con el mismo correo al que llegó.</Text>
          <PrimaryButton label="Aceptar invitación" pendingLabel="Aceptando…" pending={pending} onPress={() => void accept()} />
        </>
      ) : (
        <>
          <Text style={styles.body}>Inicia sesión con el correo de la invitación y vuelve a abrir este enlace. Si aún no tienes cuenta, créala con ese mismo correo.</Text>
          <Link href="/(auth)/login" style={styles.link}>Iniciar sesión</Link>
        </>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, gap: 16, justifyContent: 'center', padding: 24, backgroundColor: theme.colors.bg },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 28 },
  body: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 16, lineHeight: 24 },
  link: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
});