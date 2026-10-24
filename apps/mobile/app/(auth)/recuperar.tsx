import { zodResolver } from '@hookform/resolvers/zod';
import { messageFromUnknown, notify, passwordResetRequestSchema } from '@situ/shared';
import type { PasswordResetRequestValues } from '@situ/shared';
import { Link } from 'expo-router';
import { SymbolView } from 'expo-symbols';
import { Controller, useForm } from 'react-hook-form';
import { KeyboardAvoidingView, Platform, ScrollView, StyleSheet, Text, View } from 'react-native';

import { AuthField } from '@/components/auth-field';
import { LogoAction } from '@/components/logo-action';
import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

export default function RecoverScreen() {
  const form = useForm<PasswordResetRequestValues>({
    resolver: zodResolver(passwordResetRequestSchema),
    defaultValues: { email: '' },
  });

  async function onSubmit(values: PasswordResetRequestValues) {
    try {
      await api.requestPasswordReset(values);
      notify.success('Si el correo está registrado, enviaremos el enlace para elegir una contraseña nueva.');
    } catch (cause) {
      notify.error(messageFromUnknown(cause));
    }
  }

  return (
    <KeyboardAvoidingView style={styles.screen} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        <Text style={styles.title}>Recuperar</Text>
        <Text style={styles.lede}>Escribe el correo de tu cuenta. Si está registrado, te enviaremos un enlace.</Text>
        <Controller
          control={form.control}
          name="email"
          render={({ field }) => (
            <AuthField
              icon={<SymbolView name={{ ios: 'person.fill', android: 'person', web: 'person' }} tintColor={theme.colors.textPrimary} size={20} />}
              value={field.value}
              onChangeText={field.onChange}
              placeholder="Correo"
              autoCapitalize="none"
              autoComplete="email"
              keyboardType="email-address"
              accessibilityLabel="Correo"
            />
          )}
        />
        {form.formState.errors.email?.message ? <Text style={styles.fieldError}>{form.formState.errors.email.message}</Text> : null}
        <LogoAction
          label="Enviar enlace"
          pendingLabel="Enviando…"
          pending={form.formState.isSubmitting}
          onPress={() => {
            void form.handleSubmit(onSubmit)();
          }}
        />
        <View style={styles.back}>
          <Link href="/login" replace style={styles.link}>
            Volver a ingresar
          </Link>
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  screen: {
    flex: 1,
    backgroundColor: theme.colors.bg,
  },
  content: {
    flexGrow: 1,
    justifyContent: 'center',
    gap: 16,
    paddingHorizontal: 24,
    paddingVertical: 32,
  },
  title: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.displayHeavy,
    fontSize: 48,
    lineHeight: 56,
    textAlign: 'center',
  },
  lede: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
    textAlign: 'center',
  },
  link: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
  },
  back: {
    alignItems: 'center',
  },
  fieldError: {
    width: '80%',
    maxWidth: 360,
    alignSelf: 'center',
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 13,
  },
});
