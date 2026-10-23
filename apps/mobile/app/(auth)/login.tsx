import { zodResolver } from '@hookform/resolvers/zod';
import { ApiError, loginSchema, messageFromUnknown } from '@situ/shared';
import type { LoginValues } from '@situ/shared';
import { Link } from 'expo-router';
import { SymbolView } from 'expo-symbols';
import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, Switch, Text, View } from 'react-native';

import { AuthField } from '@/components/auth-field';
import { LogoAction } from '@/components/logo-action';
import { StatusMessage } from '@/components/status-message';
import { useLockoutLabel } from '@/hooks/use-lockout';
import { api } from '@/lib/api';
import { deviceIdentifier, devicePlatform } from '@/lib/device';
import { useSession } from '@/providers/session-provider';
import { theme } from '@/theme/theme';

export default function LoginScreen() {
  const { signIn, biometricAvailable, canUnlockWithBiometric, unlockWithBiometric } = useSession();
  const [remember, setRemember] = useState(false);
  const [biometric, setBiometric] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [retryAfter, setRetryAfter] = useState<string | null>(null);
  const lockout = useLockoutLabel(retryAfter);
  const locked = retryAfter !== null && Date.parse(retryAfter) > Date.now();
  const form = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { login: '', password: '' },
  });

  async function onSubmit(values: LoginValues) {
    setError(null);
    setRetryAfter(null);

    try {
      const session = await api.login({
        login: values.login,
        password: values.password,
        deviceIdentifier: await deviceIdentifier(),
        platform: devicePlatform(),
        deviceName: Platform.OS,
      });
      if (!session) {
        setError('El servidor no inició la sesión.');
        return;
      }

      await signIn(session, biometric);
    } catch (cause) {
      if (cause instanceof ApiError && cause.apiCode === 'auth.locked' && cause.retryAfter) {
        setRetryAfter(cause.retryAfter);
      }

      setError(messageFromUnknown(cause));
    }
  }

  const loginError = form.formState.errors.login?.message;
  const passwordError = form.formState.errors.password?.message;

  return (
    <KeyboardAvoidingView style={styles.screen} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        <View style={styles.brandBlock}>
          <Text style={styles.brand}>SITU</Text>
        </View>
        <StatusMessage error={error} notice={lockout} />
        <Controller
          control={form.control}
          name="login"
          render={({ field }) => (
            <AuthField
              icon={<SymbolView name={{ ios: 'person.fill', android: 'person', web: 'person' }} tintColor={theme.colors.ink} size={20} />}
              value={field.value}
              onChangeText={field.onChange}
              onBlur={field.onBlur}
              placeholder="Usuario"
              autoCapitalize="none"
              autoComplete="username"
              autoCorrect={false}
              textContentType="username"
              accessibilityLabel="Usuario"
              accessibilityHint={loginError}
            />
          )}
        />
        {loginError ? <Text style={styles.fieldError}>{loginError}</Text> : null}
        <Controller
          control={form.control}
          name="password"
          render={({ field }) => (
            <AuthField
              icon={<SymbolView name={{ ios: 'lock.fill', android: 'lock', web: 'lock' }} tintColor={theme.colors.ink} size={20} />}
              value={field.value}
              onChangeText={field.onChange}
              onBlur={field.onBlur}
              placeholder="Contraseña"
              secureTextEntry
              autoComplete="current-password"
              textContentType="password"
              accessibilityLabel="Contraseña"
              accessibilityHint={passwordError}
            />
          )}
        />
        {passwordError ? <Text style={styles.fieldError}>{passwordError}</Text> : null}
        <View style={styles.row}>
          <Pressable
            accessibilityRole="checkbox"
            accessibilityState={{ checked: remember }}
            onPress={() => setRemember((value) => !value)}
            style={styles.remember}
          >
            <View style={[styles.box, remember && styles.boxOn]} />
            <Text style={styles.link}>Recordar</Text>
          </Pressable>
          <Link href="/recuperar" style={styles.link}>
            ¿Olvidó contraseña?
          </Link>
        </View>
        {biometricAvailable ? (
          <View style={styles.row}>
            <Text style={styles.link}>Desbloquear con biometría</Text>
            <Switch
              accessibilityLabel="Desbloquear con biometría"
              value={biometric}
              onValueChange={setBiometric}
              trackColor={{ true: theme.colors.white, false: theme.colors.gray200 }}
            />
          </View>
        ) : null}
        {canUnlockWithBiometric ? (
          <Pressable accessibilityRole="button" onPress={() => void unlockWithBiometric()} style={styles.biometric}>
            <Text style={styles.link}>Entrar con biometría</Text>
          </Pressable>
        ) : null}
        <Link href="/registro" style={styles.register}>
          Soy nuevo y deseo registrarme
        </Link>
        <LogoAction
          label="Ingresar"
          pendingLabel="Ingresando…"
          pending={form.formState.isSubmitting}
          disabled={locked}
          onPress={() => {
            void form.handleSubmit(onSubmit)();
          }}
        />
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  screen: {
    flex: 1,
    backgroundColor: theme.colors.primary,
  },
  content: {
    flexGrow: 1,
    justifyContent: 'center',
    gap: 14,
    paddingHorizontal: 24,
    paddingVertical: 32,
  },
  brandBlock: {
    alignItems: 'center',
    marginBottom: 28,
  },
  brand: {
    color: theme.colors.white,
    fontFamily: theme.fontFamily.displayHeavyItalic,
    fontSize: 64,
    lineHeight: 72,
  },
  row: {
    width: '80%',
    maxWidth: 360,
    alignSelf: 'center',
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 12,
  },
  remember: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  box: {
    width: 18,
    height: 18,
    borderRadius: 4,
    backgroundColor: theme.colors.gray200,
  },
  boxOn: {
    backgroundColor: theme.colors.white,
    borderWidth: 4,
    borderColor: theme.colors.ink,
  },
  link: {
    color: theme.colors.white,
    fontFamily: theme.fontFamily.sansBoldItalic,
    fontSize: 16,
  },
  register: {
    width: '80%',
    maxWidth: 360,
    alignSelf: 'center',
    textAlign: 'right',
    color: theme.colors.white,
    fontFamily: theme.fontFamily.sansBoldItalic,
    fontSize: 13,
  },
  fieldError: {
    width: '80%',
    maxWidth: 360,
    alignSelf: 'center',
    color: theme.colors.white,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 13,
  },
  biometric: {
    alignSelf: 'center',
  },
});
