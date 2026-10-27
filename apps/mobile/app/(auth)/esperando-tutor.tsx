import type { GuardianConsentStatus } from '@situ/shared';
import { AppError, confirm, emailSpamHint, messageFromUnknown, notify } from '@situ/shared';
import { useRouter } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Image, KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';

import { LogoAction } from '@/components/logo-action';
import { api, setAccessToken } from '@/lib/api';
import { theme } from '@/theme/theme';

const logo = require('../../assets/images/brand/situ-logo-vertical-naranja.png');

function remaining(iso: string | null): string {
  if (!iso) {
    return '00:00:00';
  }
  const diff = Math.max(0, Date.parse(iso) - Date.now());
  const total = Math.floor(diff / 1000);
  const hours = Math.floor(total / 3600);
  const minutes = Math.floor((total % 3600) / 60);
  const seconds = total % 60;
  return [hours, minutes, seconds].map((part) => String(part).padStart(2, '0')).join(':');
}

function daysLeft(iso: string | null): string {
  if (!iso) {
    return '7 días';
  }
  const diff = Date.parse(iso) - Date.now();
  const days = Math.max(0, Math.ceil(diff / 86_400_000));
  return days === 1 ? '1 día' : `${days} días`;
}

export default function GuardianAuthorizationScreen() {
  const router = useRouter();
  const [status, setStatus] = useState<GuardianConsentStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [pending, setPending] = useState(false);
  const [email, setEmail] = useState('');
  const [statement, setStatement] = useState('');
  const [heard, setHeard] = useState(false);
  const [changing, setChanging] = useState(false);
  const [tick, setTick] = useState(0);
  const [limitUntil, setLimitUntil] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const next = await api.guardianConsentStatus();
      setStatus(next);
      setHeard(next.minorNoticeRecorded);
      setLimitUntil(next.retryAfter);
    } catch (cause) {
      notify.error(messageFromUnknown(cause), { placement: 'bottom' });
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    const timer = setInterval(() => setTick((value) => value + 1), 1000);
    return () => clearInterval(timer);
  }, []);

  const blocked = limitUntil !== null && Date.parse(limitUntil) > Date.now();
  void tick;

  async function run(action: () => Promise<GuardianConsentStatus | void>) {
    setPending(true);
    try {
      const next = await action();
      if (next) {
        setStatus(next);
        setHeard(next.minorNoticeRecorded);
        setLimitUntil(next.retryAfter);
      }
    } catch (cause) {
      if (cause instanceof AppError && cause.apiCode === 'guardian_consent.send_limited' && cause.retryAfter) {
        setLimitUntil(cause.retryAfter);
        return;
      }
      notify.error(messageFromUnknown(cause), { placement: 'bottom' });
    } finally {
      setPending(false);
    }
  }

  function leave() {
    setAccessToken(null);
    router.replace('/login');
  }

  const waiting = status?.status === 'Requested' || status?.status === 'EmailVerified';
  const canRequest = status?.status === 'NotRequested' || status?.status === 'Expired' || status?.status === 'Revoked';

  return (
    <KeyboardAvoidingView style={styles.screen} behavior={Platform.OS === 'ios' ? undefined : 'height'}>
    <ScrollView
      style={styles.screen}
      contentContainerStyle={styles.content}
      keyboardShouldPersistTaps="handled"
      automaticallyAdjustKeyboardInsets
      keyboardDismissMode="interactive"
    >
      <Image source={logo} resizeMode="contain" style={styles.logo} accessibilityLabel="SITU" />
      <Text style={styles.title}>Necesitamos la autorización de tu mamá, papá o representante legal</Text>
      <Text style={styles.body}>
        Como eres menor de 18 años, la ley nos pide que un adulto responsable autorice que uses SITU. No podrás entrar a la app hasta que lo haga.
      </Text>
      <Text style={styles.steps}>
        1. Escribe el correo de tu representante{'\n'}
        2. Le enviamos un enlace{'\n'}
        3. Él o ella revisa y autoriza{'\n'}
        4. ¡Listo, ya puedes entrar!
      </Text>
      {loading ? <ActivityIndicator color={theme.colors.primary} /> : null}
      {blocked ? (
        <Text style={styles.limit}>Llegaste al límite de envíos. Podrás intentarlo de nuevo en {remaining(limitUntil)}</Text>
      ) : null}
      {!loading && canRequest && !heard ? (
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Antes de enviar</Text>
          <Text style={styles.body}>
            SITU guarda tu usuario, tu edad y tu ciudad para mostrárselos a tu representante, y el correo que escribas solo para enviarle esta autorización. Puedes decirle algo con tus palabras. Si no responde en 7 días, la solicitud se cierra.
          </Text>
          <TextInput
            value={statement}
            onChangeText={setStatement}
            placeholder="Si quieres, escribe algo para tu representante"
            placeholderTextColor={theme.colors.textPlaceholder}
            style={styles.input}
            accessibilityLabel="Mensaje para tu representante"
          />
          <LogoAction
            label="Entendido"
            pendingLabel="Guardando…"
            pending={pending}
            onPress={() => void run(async () => api.acknowledgeMinorNotice())}
          />
        </View>
      ) : null}
      {!loading && canRequest && heard ? (
        <View style={styles.card}>
          <Text style={styles.label}>Correo de tu representante legal</Text>
          <TextInput
            value={email}
            onChangeText={setEmail}
            autoCapitalize="none"
            autoCorrect={false}
            keyboardType="email-address"
            inputMode="email"
            textContentType="emailAddress"
            placeholder="correo@ejemplo.com"
            placeholderTextColor={theme.colors.textPlaceholder}
            style={styles.input}
            accessibilityLabel="Correo de tu representante legal"
          />
          <Text style={styles.help}>Debe ser un correo distinto al tuyo. Solo lo usaremos para esta autorización.</Text>
          <LogoAction
            label="Enviar solicitud"
            pendingLabel="Enviando…"
            pending={pending}
            disabled={blocked}
            onPress={() => void run(() => api.requestGuardianConsent({ guardianEmail: email.trim(), minorNoticeAccepted: true, minorStatement: statement.trim() || undefined }))}
          />
        </View>
      ) : null}
      {!loading && waiting ? (
        <View style={styles.card}>
          <Text style={styles.body}>
            Enviamos la solicitud a {status?.maskedEmail ?? 'tu representante'}. Tu representante tiene {daysLeft(status?.expiresAt ?? null)} para responder.
          </Text>
          <Text style={styles.help}>{emailSpamHint}</Text>
          <LogoAction label="Reenviar correo" pendingLabel="Enviando…" pending={pending} disabled={blocked} onPress={() => void run(() => api.resendGuardianConsent())} />
          {changing ? (
            <TextInput
              value={email}
              onChangeText={setEmail}
              autoCapitalize="none"
              keyboardType="email-address"
              inputMode="email"
              placeholder="Nuevo correo"
              placeholderTextColor={theme.colors.textPlaceholder}
              style={styles.input}
              accessibilityLabel="Nuevo correo del representante"
            />
          ) : null}
          <Pressable disabled={pending || blocked} onPress={() => (changing ? void run(() => api.changeGuardianEmail(email.trim())) : setChanging(true))}>
            <Text style={styles.link}>Cambiar correo del representante</Text>
          </Pressable>
          <Pressable
            disabled={pending}
            onPress={() => void confirm({ title: 'Cancelar registro', message: 'Vamos a borrar los datos de esta cuenta.', type: 'danger', confirmText: 'Cancelar registro', cancelText: 'Volver' }).then((accepted) => {
              if (accepted) {
                void run(async () => {
                  await api.cancelGuardianRegistration();
                  leave();
                });
              }
            })}
          >
            <Text style={styles.link}>Cancelar registro</Text>
          </Pressable>
        </View>
      ) : null}
      {!loading && status?.status === 'Authorized' ? (
        <View style={styles.card}>
          <Text style={styles.body}>Tu representante autorizó la cuenta. Entra de nuevo con tu usuario y contraseña.</Text>
          <LogoAction label="Entrar" pendingLabel="Entrar" onPress={leave} />
        </View>
      ) : null}
      {!loading && status?.status === 'Declined' ? (
        <View style={styles.card}>
          <Text style={styles.body}>Tu representante no autorizó la cuenta.</Text>
          <LogoAction label="Cerrar sesión" pendingLabel="Cerrando…" onPress={leave} />
        </View>
      ) : null}
      <Pressable disabled={loading} onPress={() => void load()}>
        <Text style={styles.link}>Actualizar</Text>
      </Pressable>
    </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: theme.colors.bg },
  content: { flexGrow: 1, gap: 16, padding: 24, backgroundColor: theme.colors.bg },
  logo: { width: 96, height: 96, alignSelf: 'center' },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 28, textAlign: 'center' },
  body: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 16, lineHeight: 24, textAlign: 'center' },
  steps: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 15, lineHeight: 24 },
  help: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 13, lineHeight: 18 },
  card: { gap: 12 },
  cardTitle: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 18, textAlign: 'center' },
  label: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  input: { minHeight: 52, borderRadius: 8, borderWidth: 1, borderColor: theme.colors.border, backgroundColor: theme.colors.input, color: theme.colors.textPrimary, paddingHorizontal: 12, fontFamily: theme.fontFamily.sans },
  link: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16, textAlign: 'center' },
  limit: { color: theme.colors.warning, fontFamily: theme.fontFamily.sansBold, fontSize: 16, textAlign: 'center' },
});
