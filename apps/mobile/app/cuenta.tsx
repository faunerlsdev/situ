import { confirm, deletionDateLabel, feedbackMessages, notify, profileMessages } from '@situ/shared';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { Linking, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { displayEmail } from '@/features/account/logic';
import { responseCommitment } from '@/features/interaction/support-copy';
import { useAppConfig } from '@/features/reports/use-app-config';
import { useMe } from '@/features/reports/use-me';
import { api } from '@/lib/api';
import { dataDeletionUrl, supportFallbackUrl } from '@/lib/store-links';
import { useSession } from '@/providers/session-provider';
import { theme } from '@/theme/theme';

const fallbackLegal = {
  termsUrl: 'https://somossitu.com/terminos',
  privacyPolicyUrl: 'https://somossitu.com/privacidad',
  supportUrl: 'https://ayuda.somossitu.com/contacto',
  responseCommitment: '',
};

export default function CuentaScreen() {
  const router = useRouter();
  const { signOut } = useSession();
  const insets = useSafeAreaInsets();
  const me = useMe();
  const config = useAppConfig();
  const legal = config.data?.legal ?? fallbackLegal;
  const supportUrl = legal.supportUrl || supportFallbackUrl();
  const deletionUrl = dataDeletionUrl();
  const [password, setPassword] = useState('');
  const [pending, setPending] = useState(false);

  async function remove() {
    if (password.trim().length === 0) {
      notify.warning('Escribe tu contraseña para continuar.');
      return;
    }
    const accepted = await confirm({
      title: feedbackMessages.deleteAccountTitle,
      message: feedbackMessages.deleteAccountConfirm,
      type: 'danger',
      confirmText: 'Eliminar mi cuenta',
      cancelText: 'Cancelar',
    });
    if (!accepted) {
      return;
    }
    setPending(true);
    try {
      const result = await api.requestAccountDeletion(password);
      const date = deletionDateLabel(result.scheduledFor);
      router.replace({ pathname: '/adios', params: { date } });
      await signOut();
    } catch (error) {
      notify.error(error, { action: { label: feedbackMessages.retry, onPress: () => void remove() } });
    } finally {
      setPending(false);
    }
  }

  return (
    <ScrollView style={styles.screen} contentContainerStyle={[styles.content, { paddingTop: insets.top + 12, paddingBottom: insets.bottom + 24 }]}>
      <Pressable accessibilityRole="button" accessibilityLabel={profileMessages.back} onPress={() => router.back()} style={styles.row}>
        <Text style={styles.link}>{profileMessages.back}</Text>
      </Pressable>
      <Text style={styles.title}>Mi cuenta</Text>
      {me.isError ? (
        <Pressable accessibilityRole="button" accessibilityLabel={profileMessages.retry} onPress={() => void me.refetch()} style={styles.row}>
          <Text style={styles.body}>{profileMessages.loadError}</Text>
          <Text style={styles.link}>{profileMessages.retry}</Text>
        </Pressable>
      ) : (
        <Text style={styles.body}>{`${profileMessages.accountEmail}: ${displayEmail(me.data?.maskedEmail)}`}</Text>
      )}
      <Pressable accessibilityRole="button" accessibilityLabel={profileMessages.changePassword} onPress={() => router.push('/cambiar-contrasena')} style={styles.row}>
        <Text style={styles.label}>{profileMessages.changePassword}</Text>
      </Pressable>
      <Pressable accessibilityRole="button" accessibilityLabel={profileMessages.devices} onPress={() => router.push('/dispositivos')} style={styles.row}>
        <Text style={styles.label}>{profileMessages.devices}</Text>
      </Pressable>
      <Pressable accessibilityRole="button" accessibilityLabel="Amigos" onPress={() => router.push('/amigos')} style={styles.row}>
        <Text style={styles.label}>Amigos</Text>
      </Pressable>
      <Pressable accessibilityRole="button" accessibilityLabel="Visibilidad de mi perfil" onPress={() => router.push('/visibilidad')} style={styles.row}>
        <Text style={styles.label}>Visibilidad de mi perfil</Text>
      </Pressable>
      <Pressable accessibilityRole="button" accessibilityLabel="Compartir mi ubicación" onPress={() => router.push('/ubicacion')} style={styles.row}>
        <Text style={styles.label}>Compartir mi ubicación</Text>
      </Pressable>
      {__DEV__ ? (
        <Pressable accessibilityRole="button" accessibilityLabel="Prueba de transmisión" onPress={() => router.push('/vivo-poc')} style={styles.row}>
          <Text style={styles.label}>Prueba de transmisión</Text>
        </Pressable>
      ) : null}
      <Pressable accessibilityRole="button" accessibilityLabel={profileMessages.terms} onPress={() => void Linking.openURL(legal.termsUrl)} style={styles.row}>
        <Text style={styles.label}>{profileMessages.terms}</Text>
      </Pressable>
      <Pressable accessibilityRole="button" accessibilityLabel={profileMessages.privacyPolicy} onPress={() => void Linking.openURL(legal.privacyPolicyUrl)} style={styles.row}>
        <Text style={styles.label}>{profileMessages.privacyPolicy}</Text>
      </Pressable>
      <Pressable accessibilityRole="link" accessibilityLabel="Contactar soporte" onPress={() => void Linking.openURL(supportUrl)} style={styles.row}>
        <Text style={styles.label}>Contactar soporte</Text>
      </Pressable>
      <Text style={styles.body}>{responseCommitment(legal.responseCommitment)}</Text>
      <Pressable accessibilityRole="button" accessibilityLabel={profileMessages.signOut} onPress={() => void signOut()} style={styles.row}>
        <Text style={styles.label}>{profileMessages.signOut}</Text>
      </Pressable>
      <Text style={styles.heading}>{feedbackMessages.deleteAccountTitle}</Text>
      <Text style={styles.body}>{feedbackMessages.deleteAccountBody}</Text>
      {deletionUrl ? <Text style={styles.body}>{`También puedes pedir la eliminación en ${deletionUrl}`}</Text> : null}
      <TextInput
        value={password}
        onChangeText={setPassword}
        secureTextEntry
        accessibilityLabel={feedbackMessages.deleteAccountPassword}
        placeholder={feedbackMessages.deleteAccountPassword}
        placeholderTextColor={theme.colors.textSecondary}
        keyboardAppearance="dark"
        style={styles.input}
      />
      <Pressable accessibilityRole="button" accessibilityLabel={feedbackMessages.deleteAccountTitle} disabled={pending} onPress={() => void remove()} style={styles.danger}>
        <Text style={styles.dangerLabel}>{pending ? 'Eliminando…' : feedbackMessages.deleteAccountTitle}</Text>
      </Pressable>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: theme.colors.bg },
  content: { gap: 8, paddingHorizontal: 24 },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 28, lineHeight: 34, marginBottom: 8 },
  heading: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 20, marginTop: 16 },
  label: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 18 },
  body: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 16, lineHeight: 24 },
  row: { minHeight: 44, justifyContent: 'center' },
  link: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  input: {
    minHeight: 48,
    borderRadius: theme.radius.input,
    borderWidth: 1,
    borderColor: theme.colors.border,
    paddingHorizontal: 12,
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sans,
    fontSize: 16,
  },
  danger: { minHeight: 44, borderRadius: theme.radius.button, backgroundColor: theme.colors.dangerSolid, alignItems: 'center', justifyContent: 'center' },
  dangerLabel: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
});
