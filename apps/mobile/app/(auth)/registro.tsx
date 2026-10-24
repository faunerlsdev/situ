import type { City, Country, LegalDocument, Region } from '@situ/shared';
import { adultAge, ageInYears, composeDateOfBirth, messageFromUnknown, notify, registerSchema } from '@situ/shared';
import type { RegisterValues } from '@situ/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useRouter } from 'expo-router';
import { useEffect, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import {
  Image,
  KeyboardAvoidingView,
  Linking,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import type { TextInputProps } from 'react-native';

import { LogoAction } from '@/components/logo-action';
import { SelectField } from '@/components/select-field';
import { StatusMessage } from '@/components/status-message';
import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

const globe = require('../../assets/images/icons/nav-mapa-globo-dark.png');

export default function RegisterScreen() {
  const [countries, setCountries] = useState<Country[]>([]);
  const [regions, setRegions] = useState<Region[]>([]);
  const [cities, setCities] = useState<City[]>([]);
  const [documents, setDocuments] = useState<LegalDocument[]>([]);
  const [error, setError] = useState<string | null>(null);
  const router = useRouter();
  const form = useForm<RegisterValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      givenNames: '',
      surnames: '',
      phone: '',
      userName: '',
      email: '',
      password: '',
      passwordConfirmation: '',
      day: '',
      month: '',
      year: '',
      countryId: '',
      regionId: '',
      cityId: '',
      accepted: false,
      guardianEmail: '',
      minorNoticeAccepted: false,
    },
  });
  const countryId = form.watch('countryId');
  const regionId = form.watch('regionId');
  const cityId = form.watch('cityId');
  const accepted = form.watch('accepted');
  const day = form.watch('day');
  const month = form.watch('month');
  const year = form.watch('year');
  const selectedCountry = countries.find((country) => country.id === countryId);
  const birth = composeDateOfBirth(year, month, day);
  const age = birth ? ageInYears(birth) : null;
  const minor = age !== null && age < adultAge;
  const belowMinimum = age !== null && selectedCountry !== undefined && age < selectedCountry.minimumAge;

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        const response = await api.listCountries();
        if (cancelled) {
          return;
        }

        setCountries(response.countries.filter((country) => country.isEnabled));
      } catch (cause) {
        if (!cancelled) {
          setError(messageFromUnknown(cause));
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!countryId) {
      return;
    }

    let cancelled = false;
    form.setValue('regionId', '');
    form.setValue('cityId', '');
    setRegions([]);
    setCities([]);
    setDocuments([]);
    form.setValue('accepted', false);

    async function load() {
      try {
        const [regionResponse, documentResponse] = await Promise.all([
          api.listRegions(countryId),
          api.listLegalDocuments(countryId),
        ]);
        if (!cancelled) {
          setRegions(regionResponse.regions);
          setDocuments(documentResponse.documents);
        }
      } catch (cause) {
        if (!cancelled) {
          setError(messageFromUnknown(cause));
        }
      }
    }

    void load();
    return () => {
      cancelled = true;
    };
  }, [countryId, form]);

  useEffect(() => {
    if (!regionId) {
      return;
    }

    let cancelled = false;
    form.setValue('cityId', '');
    setCities([]);

    async function load() {
      try {
        const response = await api.listCities(regionId);
        if (!cancelled) {
          setCities(response.cities);
        }
      } catch (cause) {
        if (!cancelled) {
          setError(messageFromUnknown(cause));
        }
      }
    }

    void load();
    return () => {
      cancelled = true;
    };
  }, [form, regionId]);

  async function onSubmit(values: RegisterValues) {
    const dateOfBirth = composeDateOfBirth(values.year, values.month, values.day);
    if (!dateOfBirth || documents.length === 0) {
      notify.warning('Completa el lugar y acepta los documentos legales.');
      return;
    }

    setError(null);

    try {
      const result = await api.register({
        email: values.email,
        userName: values.userName,
        givenNames: values.givenNames,
        surnames: values.surnames,
        phone: values.phone,
        password: values.password,
        dateOfBirth,
        countryId: values.countryId,
        regionId: values.regionId,
        cityId: values.cityId,
        acceptedDocumentIds: documents.map((document) => document.id),
        guardianEmail: values.guardianEmail.trim() || undefined,
        minorNoticeAccepted: values.minorNoticeAccepted,
      });
      if (result.status === 'PendingGuardianConsent') {
        form.reset();
        notify.success('Creamos la cuenta. Confirma tu correo y luego ingresa para pedir la autorización de tu representante.', { placement: 'bottom' });
        router.replace('/login');
        return;
      }

      form.reset();
      notify.success('Te enviamos un correo para confirmar la cuenta. Ábrelo para activarla.', { placement: 'bottom' });
      router.replace('/login');
    } catch (cause) {
      notify.error(messageFromUnknown(cause));
    }
  }

  return (
    <KeyboardAvoidingView style={styles.screen} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <View style={styles.glow} />
      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        <View style={styles.hero}>
          <Image source={globe} resizeMode="contain" style={styles.globe} />
          <Text style={styles.title}>Registro</Text>
        </View>
        <StatusMessage error={error ?? form.formState.errors.root?.message ?? null} notice={null} />
        <Controller
          control={form.control}
          name="givenNames"
          render={({ field }) => (
            <WideField value={field.value} onChangeText={field.onChange} placeholder="Nombres" accessibilityLabel="Nombres" autoComplete="given-name" textContentType="givenName" autoCapitalize="words" />
          )}
        />
        {form.formState.errors.givenNames?.message ? <Text style={styles.fieldError}>{form.formState.errors.givenNames.message}</Text> : null}
        <Controller
          control={form.control}
          name="surnames"
          render={({ field }) => (
            <WideField value={field.value} onChangeText={field.onChange} placeholder="Apellidos" accessibilityLabel="Apellidos" autoComplete="family-name" textContentType="familyName" autoCapitalize="words" />
          )}
        />
        {form.formState.errors.surnames?.message ? <Text style={styles.fieldError}>{form.formState.errors.surnames.message}</Text> : null}
        <Controller
          control={form.control}
          name="phone"
          render={({ field }) => (
            <WideField value={field.value} onChangeText={field.onChange} placeholder="Teléfono" accessibilityLabel="Teléfono" autoComplete="tel" textContentType="telephoneNumber" keyboardType="phone-pad" />
          )}
        />
        {form.formState.errors.phone?.message ? <Text style={styles.fieldError}>{form.formState.errors.phone.message}</Text> : null}
        <Controller
          control={form.control}
          name="userName"
          render={({ field }) => (
            <WideField value={field.value} onChangeText={field.onChange} placeholder="Nombre de usuario" accessibilityLabel="Nombre de usuario" autoCapitalize="none" autoCorrect={false} />
          )}
        />
        {form.formState.errors.userName?.message ? <Text style={styles.fieldError}>{form.formState.errors.userName.message}</Text> : null}
        <Controller
          control={form.control}
          name="email"
          render={({ field }) => (
            <WideField value={field.value} onChangeText={field.onChange} placeholder="Correo" accessibilityLabel="Correo" autoCapitalize="none" autoCorrect={false} autoComplete="email" textContentType="emailAddress" keyboardType="email-address" inputMode="email" />
          )}
        />
        {form.formState.errors.email?.message ? <Text style={styles.fieldError}>{form.formState.errors.email.message}</Text> : null}
        <Controller
          control={form.control}
          name="password"
          render={({ field }) => (
            <WideField value={field.value} onChangeText={field.onChange} placeholder="Contraseña" accessibilityLabel="Contraseña" secureTextEntry autoComplete="new-password" />
          )}
        />
        {form.formState.errors.password?.message ? <Text style={styles.fieldError}>{form.formState.errors.password.message}</Text> : null}
        <Controller
          control={form.control}
          name="passwordConfirmation"
          render={({ field }) => (
            <WideField value={field.value} onChangeText={field.onChange} placeholder="Confirmar contraseña" accessibilityLabel="Confirmar contraseña" secureTextEntry autoComplete="new-password" textContentType="newPassword" />
          )}
        />
        {form.formState.errors.passwordConfirmation?.message ? <Text style={styles.fieldError}>{form.formState.errors.passwordConfirmation.message}</Text> : null}
        <SelectField
          label="País"
          value={countryId}
          placeholder="Selecciona un país"
          options={countries}
          disabled={countries.length === 0}
          onChange={(id) => form.setValue('countryId', id, { shouldValidate: true })}
        />
        {form.formState.errors.countryId?.message ? <Text style={styles.fieldError}>{form.formState.errors.countryId.message}</Text> : null}
        <SelectField
          label={selectedCountry?.regionLabel ?? 'Departamento'}
          value={regionId}
          placeholder="Selecciona"
          options={regions}
          disabled={!countryId || regions.length === 0}
          onChange={(id) => form.setValue('regionId', id, { shouldValidate: true })}
        />
        {form.formState.errors.regionId?.message ? <Text style={styles.fieldError}>{form.formState.errors.regionId.message}</Text> : null}
        <SelectField
          label="Ciudad"
          value={cityId}
          placeholder="Selecciona una ciudad"
          options={cities}
          disabled={!regionId || cities.length === 0}
          onChange={(id) => form.setValue('cityId', id, { shouldValidate: true })}
        />
        {form.formState.errors.cityId?.message ? <Text style={styles.fieldError}>{form.formState.errors.cityId.message}</Text> : null}
        <Text style={styles.label}>Fecha de nacimiento</Text>
        <View style={styles.dateRow}>
          <Controller control={form.control} name="day" render={({ field }) => (
            <DateBox value={field.value} onChangeText={field.onChange} placeholder="Día" accessibilityLabel="Día" />
          )} />
          <Controller control={form.control} name="month" render={({ field }) => (
            <DateBox value={field.value} onChangeText={field.onChange} placeholder="Mes" accessibilityLabel="Mes" />
          )} />
          <Controller control={form.control} name="year" render={({ field }) => (
            <DateBox value={field.value} onChangeText={field.onChange} placeholder="Año" accessibilityLabel="Año" />
          )} />
        </View>
        {form.formState.errors.day?.message ? <Text style={styles.fieldError}>{form.formState.errors.day.message}</Text> : null}
        {belowMinimum ? (
          <Text style={styles.warning}>En {selectedCountry?.name} la edad mínima para registrarse es {selectedCountry?.minimumAge} años.</Text>
        ) : null}
        {minor && !belowMinimum ? (
          <Text style={styles.notice}>
            Como eres menor de edad, la cuenta queda pendiente. Tu acudiente debe abrir el enlace, verificar su correo y autorizar la cuenta.
          </Text>
        ) : null}
        {documents.map((document) => (
          <Text
            key={document.id}
            style={styles.legal}
            onPress={() => {
              void Linking.openURL(document.url);
            }}
          >
            {document.type === 'PrivacyPolicy' ? 'Política de privacidad' : 'Términos de uso'}
          </Text>
        ))}
        <Pressable accessibilityRole="checkbox" accessibilityState={{ checked: accepted }} onPress={() => form.setValue('accepted', !accepted, { shouldValidate: true })}>
          <Text style={styles.legal}>{accepted ? '☑' : '☐'} Acepto los documentos legales vigentes.</Text>
        </Pressable>
        {form.formState.errors.accepted?.message ? <Text style={styles.fieldError}>{form.formState.errors.accepted.message}</Text> : null}
        <Controller
          control={form.control}
          name="guardianEmail"
          render={({ field }) => (
            <WideField
              value={field.value}
              onChangeText={field.onChange}
              placeholder="Correo de tu representante (opcional)"
              accessibilityLabel="Correo de tu representante legal"
              keyboardType="email-address"
              inputMode="email"
              autoCapitalize="none"
              autoCorrect={false}
              textContentType="emailAddress"
            />
          )}
        />
        {form.formState.errors.guardianEmail?.message ? <Text style={styles.fieldError}>{form.formState.errors.guardianEmail.message}</Text> : null}
        <Pressable accessibilityRole="checkbox" accessibilityState={{ checked: form.watch('minorNoticeAccepted') }} onPress={() => form.setValue('minorNoticeAccepted', !form.watch('minorNoticeAccepted'), { shouldValidate: true })}>
          <Text style={styles.legal}>{form.watch('minorNoticeAccepted') ? '☑' : '☐'} Entendido: un adulto debe autorizar mi cuenta.</Text>
        </Pressable>
        {form.formState.errors.minorNoticeAccepted?.message ? <Text style={styles.fieldError}>{form.formState.errors.minorNoticeAccepted.message}</Text> : null}
        <LogoAction
          label="Registrar Me"
          pendingLabel="Creando cuenta…"
          pending={form.formState.isSubmitting}
          onPress={() => {
            void form.handleSubmit(onSubmit)();
          }}
        />
        <Link href="/login" replace style={styles.back}>
          ¿Ya tienes cuenta? Ingresar
        </Link>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

function WideField(props: TextInputProps) {
  return <TextInput placeholderTextColor={theme.colors.textPlaceholder} keyboardAppearance="dark" {...props} style={styles.wide} />;
}

function DateBox(props: TextInputProps) {
  return (
    <TextInput
      placeholderTextColor={theme.colors.textPlaceholder}
      keyboardAppearance="dark"
      keyboardType="number-pad"
      maxLength={props.accessibilityLabel === 'Año' ? 4 : 2}
      {...props}
      style={styles.date}
    />
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
    top: '10%',
    width: 520,
    height: 520,
    borderRadius: 260,
    backgroundColor: theme.colors.bgGradientCenter,
  },
  content: {
    gap: 14,
    paddingHorizontal: 24,
    paddingTop: 12,
    paddingBottom: 40,
  },
  hero: {
    minHeight: 150,
    justifyContent: 'flex-end',
  },
  globe: {
    position: 'absolute',
    top: -24,
    right: -36,
    width: '55%',
    aspectRatio: 1,
    opacity: 0.25,
  },
  title: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.displayHeavy,
    fontSize: 48,
    lineHeight: 56,
  },
  wide: {
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
  label: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
  },
  dateRow: {
    flexDirection: 'row',
    gap: 10,
  },
  date: {
    width: 84,
    height: 48,
    borderRadius: theme.radius.input,
    borderWidth: 1,
    borderColor: theme.colors.border,
    backgroundColor: theme.colors.input,
    textAlign: 'center',
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
  },
  legal: {
    color: theme.colors.textSecondary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 12,
    lineHeight: 16,
  },
  fieldError: {
    color: theme.colors.danger,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 13,
  },
  warning: {
    color: theme.colors.danger,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
  },
  notice: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
  },
  back: {
    color: theme.colors.primary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
    textAlign: 'center',
  },
});
