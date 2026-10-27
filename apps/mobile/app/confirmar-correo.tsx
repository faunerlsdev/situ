import { emailSpamHint, messageFromUnknown, notify } from '@situ/shared';
import { useQueryClient } from '@tanstack/react-query';
import { Link, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { Image, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';

import { PrimaryButton } from '@/components/primary-button';
import { api } from '@/lib/api';
import { searchParam } from '@/lib/device';
import { theme } from '@/theme/theme';

const logo = require('../assets/images/brand/situ-logo-vertical-naranja.png');

export default function ConfirmEmailScreen() {
  const params = useLocalSearchParams<{ token?: string | string[]; email?: string | string[] }>();
  const token = searchParam(params.token);
  const queryClient = useQueryClient();
  const [email, setEmail] = useState(searchParam(params.email));
  const [pending, setPending] = useState(false);

  async function confirm() {
    setPending(true);
    try {
      await api.confirmEmail({ token });
      await queryClient.invalidateQueries({ queryKey: ['me'] });
      notify.success('Correo confirmado. Ya puedes ingresar.');
    } catch (cause) {
      notify.error(messageFromUnknown(cause));
    } finally {
      setPending(false);
    }
  }

  async function resend() {
    setPending(true);
    try {
      await api.resendConfirmationEmail({ email: email.trim() });
      notify.success('Si la cuenta existe y falta confirmar el correo, enviaremos un enlace nuevo.');
    } catch (cause) {
      notify.error(messageFromUnknown(cause));
    } finally {
      setPending(false);
    }
  }

  return (
    <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
      <Image source={logo} resizeMode="contain" accessibilityLabel="SITU" style={styles.logo} />
      <Text style={styles.title}>{token ? 'Confirmar correo' : 'Reenviar enlace'}</Text>
      {token ? null : (
        <Text style={styles.lede}>
          Tu correo todavía no está confirmado. Escribe el correo de la cuenta y te enviamos el enlace de nuevo.
        </Text>
      )}
      {token ? null : <Text style={styles.hint}>{emailSpamHint}</Text>}
      {token ? (
        <PrimaryButton label="Confirmar mi correo" pendingLabel="Confirmando…" pending={pending} onPress={() => void confirm()} />
      ) : (
        <View style={styles.form}>
          <TextInput
            keyboardAppearance="dark"
            value={email}
            onChangeText={setEmail}
            placeholder="Correo"
            placeholderTextColor={theme.colors.textPlaceholder}
            autoCapitalize="none"
            autoComplete="email"
            keyboardType="email-address"
            accessibilityLabel="Correo"
            style={styles.input}
          />
          <PrimaryButton
            label="Reenviar enlace"
            pendingLabel="Enviando…"
            pending={pending}
            disabled={email.trim().length === 0}
            onPress={() => void resend()}
          />
        </View>
      )}
      <Link href="/login" replace style={styles.link}>
        Volver a ingresar
      </Link>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  content: {
    flexGrow: 1,
    justifyContent: 'center',
    gap: 16,
    padding: 24,
    backgroundColor: theme.colors.bg,
  },
  logo: {
    alignSelf: 'center',
    width: 96,
    height: 104,
  },
  title: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.displayHeavy,
    fontSize: 36,
    lineHeight: 42,
    textAlign: 'center',
  },
  lede: {
    color: theme.colors.textSecondary,
    fontFamily: theme.fontFamily.sans,
    fontSize: 16,
    lineHeight: 22,
    textAlign: 'center',
  },
  hint: {
    color: theme.colors.textSecondary,
    fontFamily: theme.fontFamily.sans,
    fontSize: 13,
    lineHeight: 18,
    textAlign: 'center',
  },
  form: {
    gap: 12,
  },
  input: {
    height: 52,
    borderRadius: theme.radius.input,
    borderWidth: 1,
    borderColor: theme.colors.border,
    backgroundColor: theme.colors.input,
    paddingHorizontal: 14,
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
  },
  link: {
    color: theme.colors.primary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
    textAlign: 'center',
  },
});
