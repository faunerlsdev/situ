import { zodResolver } from '@hookform/resolvers/zod';
import { ApiError, loginSchema, notify } from '@situ/shared';
import type { LoginValues } from '@situ/shared';
import { Link, useRouter } from 'expo-router';
import { SymbolView } from 'expo-symbols';
import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, Switch, Text, View } from 'react-native';

import { AuthField } from '@/components/auth-field';
import { LogoAction } from '@/components/logo-action';
import { useLockoutLabel } from '@/hooks/use-lockout';
import { api, setAccessToken } from '@/lib/api';
import { deviceIdentifier, devicePlatform } from '@/lib/device';
import { useSession } from '@/providers/session-provider';
import { theme } from '@/theme/theme';

export default function LoginScreen() {
  const router = useRouter();
  const { signIn, biometricAvailable, canUnlockWithBiometric, unlockWithBiometric } = useSession();
  const [remember, setRemember] = useState(false);
  const [biometric, setBiometric] = useState(false);
  const [retryAfter, setRetryAfter] = useState<string | null>(null);
  const lockout = useLockoutLabel(retryAfter);
  const locked = retryAfter !== null && Date.parse(retryAfter) > Date.now();
  const form = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { login: '', password: '' },
  });

  async function onSubmit(values: LoginValues) {
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
        notify.error('El servidor no inició la sesión.', { placement: 'bottom' });
        return;
      }

      await signIn(session, biometric);
    } catch (cause) {
      if (cause instanceof ApiError && cause.apiCode === 'auth.guardian_consent_required' && cause.accessToken) {
        setAccessToken(cause.accessToken, { guardian: true });
        router.replace('/esperando-tutor');
        return;
      }

      if (cause instanceof ApiError && cause.apiCode === 'auth.device_limit' && cause.accessToken) {
        setAccessToken(cause.accessToken, { deviceLimit: true });
        router.replace('/limite-dispositivos');
        return;
      }

      if (cause instanceof ApiError && cause.apiCode === 'auth.email_unconfirmed') {
        const login = values.login.trim();
        router.replace({
          pathname: '/confirmar-correo',
          params: login.includes('@') ? { email: login } : {},
        });
        return;
      }

      if (cause instanceof ApiError && cause.apiCode === 'auth.locked' && cause.retryAfter) {
        setRetryAfter(cause.retryAfter);
      }

      notify.error(cause, { placement: 'bottom' });
    }
  }

  const loginError = form.formState.errors.login?.message;
  const passwordError = form.formState.errors.password?.message;

  return (
    <KeyboardAvoidingView style={styles.screen} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <View style={styles.glow} />
      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        <View style={styles.brandBlock}>
          <Text style={styles.brand}>SITU</Text>
        </View>
        <Controller
          control={form.control}
          name="login"
          render={({ field }) => (
            <AuthField
              icon={<SymbolView name={{ ios: 'person.fill', android: 'person', web: 'person' }} tintColor={theme.colors.textSecondary} size={20} />}
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
              icon={<SymbolView name={{ ios: 'lock.fill', android: 'lock', web: 'lock' }} tintColor={theme.colors.textSecondary} size={20} />}
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
            <View style={[styles.box, remember && styles.boxOn]}>
              {remember ? <Text style={styles.check}>✓</Text> : null}
            </View>
            <Text style={styles.link}>Recordar</Text>
          </Pressable>
          <Link href="/recuperar" replace style={styles.link}>
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
              trackColor={{ true: theme.colors.primary, false: theme.colors.border }}
              thumbColor={theme.colors.textOnPrimary}
            />
          </View>
        ) : null}
        {canUnlockWithBiometric ? (
          <Pressable accessibilityRole="button" onPress={() => void unlockWithBiometric()} style={styles.biometric}>
            <Text style={styles.link}>Entrar con biometría</Text>
          </Pressable>
        ) : null}
        <Link href="/registro" replace style={styles.register}>
          Soy nuevo y deseo registrarme
        </Link>
        {lockout ? <Text style={styles.lockout}>{lockout}</Text> : null}
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
    backgroundColor: theme.colors.bg,
  },
  glow: {
    position: 'absolute',
    alignSelf: 'center',
    top: '15%',
    width: 520,
    height: 520,
    borderRadius: 260,
    backgroundColor: theme.colors.bgGradientCenter,
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
    color: theme.colors.primary,
    fontFamily: theme.fontFamily.displayHeavy,
    fontSize: 64,
    lineHeight: 72,
  },
  row: {
    width: '100%',
    alignSelf: 'stretch',
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
    borderWidth: 1,
    borderColor: theme.colors.border,
    alignItems: 'center',
    justifyContent: 'center',
  },
  boxOn: {
    backgroundColor: theme.colors.primary,
    borderColor: theme.colors.primary,
  },
  link: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
  },
  check: {
    color: theme.colors.textOnPrimary,
    fontSize: 12,
    lineHeight: 16,
    textAlign: 'center',
  },
  register: {
    width: '100%',
    alignSelf: 'stretch',
    textAlign: 'right',
    color: theme.colors.primary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 13,
  },
  lockout: {
    width: '100%',
    alignSelf: 'stretch',
    color: theme.colors.warning,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 14,
    lineHeight: 20,
    textAlign: 'center',
  },
  fieldError: {
    width: '100%',
    alignSelf: 'stretch',
    color: theme.colors.danger,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 13,
  },
  biometric: {
    alignSelf: 'center',
  },
});
