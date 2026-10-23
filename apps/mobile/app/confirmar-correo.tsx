import { messageFromUnknown } from '@situ/shared';
import { Link, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';

import { StatusMessage } from '@/components/status-message';
import { api } from '@/lib/api';
import { searchParam } from '@/lib/device';
import { theme } from '@/theme/theme';

export default function ConfirmEmailScreen() {
  const params = useLocalSearchParams<{ token?: string | string[] }>();
  const token = searchParam(params.token);
  const [email, setEmail] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function confirm() {
    setPending(true);
    setError(null);
    try {
      await api.confirmEmail({ token });
      setNotice('Correo confirmado. Ya puedes ingresar.');
    } catch (cause) {
      setError(messageFromUnknown(cause));
    } finally {
      setPending(false);
    }
  }

  async function resend() {
    setPending(true);
    setError(null);
    setNotice(null);
    try {
      await api.resendConfirmationEmail({ email: email.trim() });
      setNotice('Si la cuenta existe y falta confirmar el correo, enviaremos un enlace nuevo.');
    } catch (cause) {
      setError(messageFromUnknown(cause));
    } finally {
      setPending(false);
    }
  }

  return (
    <ScrollView contentContainerStyle={styles.content}>
      <Text style={styles.title}>Confirmar correo</Text>
      <StatusMessage error={error} notice={notice} />
      {token ? (
        <Pressable accessibilityRole="button" disabled={pending} onPress={() => void confirm()}>
          <Text style={styles.action}>{pending ? 'Confirmando…' : 'Confirmar mi correo'}</Text>
        </Pressable>
      ) : (
        <View style={styles.form}>
          <TextInput
            value={email}
            onChangeText={setEmail}
            placeholder="Correo"
            placeholderTextColor={theme.colors.gray400}
            autoCapitalize="none"
            keyboardType="email-address"
            accessibilityLabel="Correo"
            style={styles.input}
          />
          <Pressable accessibilityRole="button" disabled={pending} onPress={() => void resend()}>
            <Text style={styles.action}>{pending ? 'Enviando…' : 'Reenviar enlace'}</Text>
          </Pressable>
        </View>
      )}
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
    fontSize: 42,
    textAlign: 'center',
  },
  form: {
    gap: 12,
  },
  input: {
    height: 52,
    borderRadius: theme.radius.input,
    backgroundColor: theme.colors.white,
    paddingHorizontal: 14,
    color: theme.colors.ink,
    fontFamily: theme.fontFamily.sansBoldItalic,
    fontSize: 16,
  },
  action: {
    color: theme.colors.white,
    fontFamily: theme.fontFamily.sansBoldItalic,
    fontSize: 22,
    textAlign: 'center',
  },
  link: {
    color: theme.colors.white,
    fontFamily: theme.fontFamily.sansBoldItalic,
    fontSize: 16,
    textAlign: 'center',
  },
});
