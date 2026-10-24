import { zodResolver } from '@hookform/resolvers/zod';
import { messageFromUnknown, notify, resetPasswordSchema } from '@situ/shared';
import type { ResetPasswordValues } from '@situ/shared';
import { Link, useLocalSearchParams } from 'expo-router';
import { useEffect, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { ScrollView, StyleSheet, Text, TextInput } from 'react-native';

import { LogoAction } from '@/components/logo-action';
import { StatusMessage } from '@/components/status-message';
import { api } from '@/lib/api';
import { searchParam } from '@/lib/device';
import { theme } from '@/theme/theme';

export default function ResetPasswordScreen() {
  const params = useLocalSearchParams<{ token?: string | string[] }>();
  const token = searchParam(params.token);
  const [error, setError] = useState<string | null>(token ? null : 'El enlace no incluye el código de recuperación.');
  const [ready, setReady] = useState(false);
  const form = useForm<ResetPasswordValues>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { password: '', confirmation: '' },
  });

  useEffect(() => {
    if (!token) {
      return;
    }

    let cancelled = false;
    api.validateResetToken(token)
      .then(() => {
        if (!cancelled) {
          setReady(true);
        }
      })
      .catch((cause: unknown) => {
        if (!cancelled) {
          setError(messageFromUnknown(cause));
        }
      });

    return () => {
      cancelled = true;
    };
  }, [token]);

  async function onSubmit(values: ResetPasswordValues) {
    setError(null);
    try {
      await api.resetPassword({ token, password: values.password });
      notify.success('Contraseña actualizada. Ya puedes ingresar.');
      setReady(false);
    } catch (cause) {
      notify.error(messageFromUnknown(cause));
    }
  }

  return (
    <ScrollView contentContainerStyle={styles.content}>
      <Text style={styles.title}>Nueva contraseña</Text>
      <StatusMessage error={error} notice={null} />
      {ready ? (
        <>
          <Controller
            control={form.control}
            name="password"
            render={({ field }) => (
              <TextInput keyboardAppearance="dark"
                value={field.value}
                onChangeText={field.onChange}
                placeholder="Contraseña nueva"
                placeholderTextColor={theme.colors.textPlaceholder}
                secureTextEntry
                accessibilityLabel="Contraseña nueva"
                style={styles.input}
              />
            )}
          />
          {form.formState.errors.password?.message ? <Text style={styles.fieldError}>{form.formState.errors.password.message}</Text> : null}
          <Controller
            control={form.control}
            name="confirmation"
            render={({ field }) => (
              <TextInput keyboardAppearance="dark"
                value={field.value}
                onChangeText={field.onChange}
                placeholder="Repite la contraseña"
                placeholderTextColor={theme.colors.textPlaceholder}
                secureTextEntry
                accessibilityLabel="Repite la contraseña"
                style={styles.input}
              />
            )}
          />
          {form.formState.errors.confirmation?.message ? <Text style={styles.fieldError}>{form.formState.errors.confirmation.message}</Text> : null}
          <LogoAction
            label="Guardar contraseña"
            pendingLabel="Guardando…"
            pending={form.formState.isSubmitting}
            onPress={() => {
              void form.handleSubmit(onSubmit)();
            }}
          />
        </>
      ) : null}
      <Link href="/login" replace style={styles.link}>Volver a ingresar</Link>
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
  title: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.displayHeavy,
    fontSize: 42,
    textAlign: 'center',
  },
  input: {
    height: 52,
    borderRadius: theme.radius.input,
    backgroundColor: theme.colors.input,
    paddingHorizontal: 14,
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
  },
  fieldError: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 13,
  },
  link: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
    textAlign: 'center',
  },
});
