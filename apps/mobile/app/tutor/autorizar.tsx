import { zodResolver } from '@hookform/resolvers/zod';
import type { GuardianDocumentType } from '@situ/shared';
import { guardianDataSchema, messageFromUnknown, notify } from '@situ/shared';
import type { GuardianDataValues } from '@situ/shared';
import { useQueryClient } from '@tanstack/react-query';
import { Link, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';

import { StatusMessage } from '@/components/status-message';
import { api } from '@/lib/api';
import { searchParam } from '@/lib/device';
import { theme } from '@/theme/theme';

const documents: { value: GuardianDocumentType; label: string }[] = [
  { value: 'CitizenshipCard', label: 'Cédula de ciudadanía' },
  { value: 'ForeignerCard', label: 'Cédula de extranjería' },
  { value: 'Passport', label: 'Pasaporte' },
  { value: 'Other', label: 'Otro documento' },
];

export default function GuardianScreen() {
  const params = useLocalSearchParams<{ token?: string | string[] }>();
  const token = searchParam(params.token);
  const queryClient = useQueryClient();
  const [step, setStep] = useState<'data' | 'authorize'>('data');
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(token ? null : 'El enlace de autorización no incluye el código.');
  const [live, setLive] = useState(false);
  const [location, setLocation] = useState(false);
  const [pending, setPending] = useState(false);
  const form = useForm<GuardianDataValues>({
    resolver: zodResolver(guardianDataSchema),
    defaultValues: {
      fullName: '',
      documentType: 'CitizenshipCard',
      documentNumber: '',
      contactEmail: '',
    },
  });

  async function onSubmit(values: GuardianDataValues) {
    setError(null);
    setNotice(null);
    try {
      await api.submitGuardianData({ token, ...values });
      setStep('authorize');
      setNotice(null);
      notify.success('Te enviamos un correo para verificar tu identidad. Ábrelo y luego vuelve aquí para autorizar.');
    } catch (cause) {
      notify.error(messageFromUnknown(cause));
    }
  }

  async function authorize() {
    setPending(true);
    setError(null);
    try {
      await api.authorizeGuardianConsent({
        token,
        liveStreamingAuthorized: live,
        locationSharingAuthorized: location,
      });
      setNotice(null);
      setStep('data');
      await queryClient.invalidateQueries({ queryKey: ['me'] });
      notify.success('Autorización registrada. El menor puede continuar cuando su correo esté confirmado.');
    } catch (cause) {
      notify.error(messageFromUnknown(cause));
    } finally {
      setPending(false);
    }
  }

  async function decline() {
    setPending(true);
    setError(null);
    try {
      await api.declineGuardianConsent({ token });
      setNotice(null);
      notify.info('Rechazaste la autorización. La cuenta del menor sigue pendiente.');
    } catch (cause) {
      notify.error(messageFromUnknown(cause));
    } finally {
      setPending(false);
    }
  }

  const documentType = form.watch('documentType');

  return (
    <ScrollView contentContainerStyle={styles.content}>
      <Text style={styles.title}>Autorización del acudiente</Text>
      <StatusMessage error={error} notice={notice} />
      {token && step === 'data' ? (
        <View style={styles.form}>
          <Field control={form.control} name="fullName" label="Nombre completo" error={form.formState.errors.fullName?.message} />
          <Text style={styles.label}>Documento</Text>
          {documents.map((document) => (
            <Pressable
              key={document.value}
              accessibilityRole="radio"
              accessibilityState={{ selected: documentType === document.value }}
              onPress={() => form.setValue('documentType', document.value)}
              style={[styles.choice, documentType === document.value && styles.choiceOn]}
            >
              <Text style={styles.choiceLabel}>{document.label}</Text>
            </Pressable>
          ))}
          <Field control={form.control} name="documentNumber" label="Número de documento" error={form.formState.errors.documentNumber?.message} />
          <Field control={form.control} name="contactEmail" label="Correo de contacto" error={form.formState.errors.contactEmail?.message} keyboardType="email-address" />
          <Pressable accessibilityRole="button" disabled={form.formState.isSubmitting} onPress={() => void form.handleSubmit(onSubmit)()}>
            <Text style={styles.action}>{form.formState.isSubmitting ? 'Enviando…' : 'Continuar'}</Text>
          </Pressable>
        </View>
      ) : null}
      {token && step === 'authorize' ? (
        <View style={styles.form}>
          <Pressable accessibilityRole="checkbox" accessibilityState={{ checked: live }} onPress={() => setLive((value) => !value)}>
            <Text style={styles.label}>{live ? '☑' : '☐'} Autorizo que el menor transmita en vivo.</Text>
          </Pressable>
          <Pressable accessibilityRole="checkbox" accessibilityState={{ checked: location }} onPress={() => setLocation((value) => !value)}>
            <Text style={styles.label}>{location ? '☑' : '☐'} Autorizo que el menor comparta su ubicación.</Text>
          </Pressable>
          <Pressable accessibilityRole="button" disabled={pending} onPress={() => void authorize()}>
            <Text style={styles.action}>{pending ? 'Guardando…' : 'Autorizar'}</Text>
          </Pressable>
          <Pressable accessibilityRole="button" disabled={pending} onPress={() => void decline()}>
            <Text style={styles.link}>Rechazar</Text>
          </Pressable>
        </View>
      ) : null}
      <Link href="/login" replace style={styles.link}>Volver a ingresar</Link>
    </ScrollView>
  );
}

function Field({
  control,
  name,
  label,
  error,
  keyboardType,
}: {
  control: ReturnType<typeof useForm<GuardianDataValues>>['control'];
  name: 'fullName' | 'documentNumber' | 'contactEmail';
  label: string;
  error?: string;
  keyboardType?: 'email-address';
}) {
  return (
    <View>
      <Controller
        control={control}
        name={name}
        render={({ field }) => (
          <TextInput keyboardAppearance="dark"
            value={field.value}
            onChangeText={field.onChange}
            placeholder={label}
            placeholderTextColor={theme.colors.textPlaceholder}
            accessibilityLabel={label}
            autoCapitalize="none"
            keyboardType={keyboardType}
            style={styles.input}
          />
        )}
      />
      {error ? <Text style={styles.fieldError}>{error}</Text> : null}
    </View>
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
    fontSize: 36,
    textAlign: 'center',
  },
  form: {
    gap: 12,
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
  label: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
  },
  choice: {
    minHeight: 48,
    justifyContent: 'center',
    borderRadius: theme.radius.input,
    backgroundColor: theme.colors.input,
    paddingHorizontal: 14,
  },
  choiceOn: {
    borderWidth: 2,
    borderColor: theme.colors.overlay,
  },
  choiceLabel: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
  },
  action: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 22,
    textAlign: 'center',
  },
  link: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
    textAlign: 'center',
  },
  fieldError: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 13,
  },
});
