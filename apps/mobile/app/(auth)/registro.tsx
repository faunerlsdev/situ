import type { City, Country, LegalDocument, Region } from '@situ/shared';
import { adultAge, ageInYears, composeDateOfBirth, messageFromUnknown, registerSchema } from '@situ/shared';
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
import { StatusMessage } from '@/components/status-message';
import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

const globe = require('../../assets/images/icons/nav-mapa-globo.png');

export default function RegisterScreen() {
  const [countries, setCountries] = useState<Country[]>([]);
  const [regions, setRegions] = useState<Region[]>([]);
  const [cities, setCities] = useState<City[]>([]);
  const [documents, setDocuments] = useState<LegalDocument[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const router = useRouter();
  const form = useForm<RegisterValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      userName: '',
      email: '',
      password: '',
      day: '',
      month: '',
      year: '',
      countryId: '',
      regionId: '',
      cityId: '',
      accepted: false,
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
      setError('Completa el lugar y acepta los documentos legales.');
      setNotice(null);
      return;
    }

    setError(null);
    setNotice(null);

    try {
      const result = await api.register({
        email: values.email,
        userName: values.userName,
        password: values.password,
        dateOfBirth,
        countryId: values.countryId,
        regionId: values.regionId,
        cityId: values.cityId,
        acceptedDocumentIds: documents.map((document) => document.id),
      });
      if (result.status === 'PendingGuardianConsent') {
        router.replace('/esperando-tutor');
        return;
      }

      setNotice('Te enviamos un correo para confirmar la cuenta.');
    } catch (cause) {
      setError(messageFromUnknown(cause));
    }
  }

  return (
    <KeyboardAvoidingView style={styles.screen} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        <View style={styles.hero}>
          <Image source={globe} resizeMode="contain" style={styles.globe} />
          <Text style={styles.title}>Registro</Text>
        </View>
        <StatusMessage error={error ?? form.formState.errors.root?.message ?? null} notice={notice} />
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
            <WideField value={field.value} onChangeText={field.onChange} placeholder="Correo" accessibilityLabel="Correo" autoCapitalize="none" autoComplete="email" keyboardType="email-address" />
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
        <Text style={styles.label}>País</Text>
        {countries.map((country) => {
          const selected = countryId === country.id;
          return (
            <Pressable
              key={country.id}
              accessibilityRole="radio"
              accessibilityState={{ selected }}
              onPress={() => form.setValue('countryId', country.id, { shouldValidate: true })}
              style={[styles.country, selected && styles.countryOn]}
            >
              <Text style={styles.countryLabel}>{country.name}</Text>
            </Pressable>
          );
        })}
        {form.formState.errors.countryId?.message ? <Text style={styles.fieldError}>{form.formState.errors.countryId.message}</Text> : null}
        <Text style={styles.label}>{selectedCountry?.regionLabel ?? 'Departamento'}</Text>
        {regions.map((region) => {
          const selected = regionId === region.id;
          return (
            <Pressable
              key={region.id}
              accessibilityRole="radio"
              accessibilityState={{ selected }}
              onPress={() => form.setValue('regionId', region.id, { shouldValidate: true })}
              style={[styles.country, selected && styles.countryOn]}
            >
              <Text style={styles.countryLabel}>{region.name}</Text>
            </Pressable>
          );
        })}
        {form.formState.errors.regionId?.message ? <Text style={styles.fieldError}>{form.formState.errors.regionId.message}</Text> : null}
        <Text style={styles.label}>Ciudad</Text>
        {cities.map((city) => {
          const selected = cityId === city.id;
          return (
            <Pressable
              key={city.id}
              accessibilityRole="radio"
              accessibilityState={{ selected }}
              onPress={() => form.setValue('cityId', city.id, { shouldValidate: true })}
              style={[styles.country, selected && styles.countryOn]}
            >
              <Text style={styles.countryLabel}>{city.name}</Text>
            </Pressable>
          );
        })}
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
        <LogoAction
          label="Registrar Me"
          pendingLabel="Creando cuenta…"
          pending={form.formState.isSubmitting}
          onPress={() => {
            void form.handleSubmit(onSubmit)();
          }}
        />
        <Link href="/confirmar-correo" style={styles.back}>
          Ya tengo el enlace para confirmar el correo
        </Link>
        <Link href="/login" style={styles.back}>
          ¿Ya tienes cuenta? Ingresar
        </Link>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

function WideField(props: TextInputProps) {
  return <TextInput placeholderTextColor={theme.colors.gray400} {...props} style={styles.wide} />;
}

function DateBox(props: TextInputProps) {
  return (
    <TextInput
      placeholderTextColor={theme.colors.gray400}
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
    backgroundColor: theme.colors.primary,
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
  },
  title: {
    color: theme.colors.white,
    fontFamily: theme.fontFamily.displayHeavyItalic,
    fontSize: 56,
    lineHeight: 64,
  },
  wide: {
    height: 52,
    borderRadius: theme.radius.input,
    backgroundColor: theme.colors.white,
    paddingHorizontal: 14,
    color: theme.colors.ink,
    fontFamily: theme.fontFamily.sansBoldItalic,
    fontSize: 16,
  },
  label: {
    color: theme.colors.white,
    fontFamily: theme.fontFamily.sansBoldItalic,
    fontSize: 16,
  },
  country: {
    minHeight: 48,
    justifyContent: 'center',
    borderRadius: theme.radius.input,
    backgroundColor: theme.colors.white,
    paddingHorizontal: 14,
  },
  countryOn: {
    borderWidth: 2,
    borderColor: theme.colors.black,
  },
  countryLabel: {
    color: theme.colors.ink,
    fontFamily: theme.fontFamily.sansBoldItalic,
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
    backgroundColor: theme.colors.white,
    textAlign: 'center',
    color: theme.colors.ink,
    fontFamily: theme.fontFamily.sansBoldItalic,
    fontSize: 16,
  },
  legal: {
    color: theme.colors.white,
    fontFamily: theme.fontFamily.sansBoldItalic,
    fontSize: 12,
    lineHeight: 16,
  },
  fieldError: {
    color: theme.colors.white,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 13,
  },
  warning: {
    borderRadius: theme.radius.button,
    backgroundColor: theme.colors.white,
    paddingHorizontal: 12,
    paddingVertical: 12,
    color: theme.colors.live,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
  },
  notice: {
    borderRadius: theme.radius.button,
    backgroundColor: theme.colors.white,
    paddingHorizontal: 12,
    paddingVertical: 12,
    color: theme.colors.ink,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
  },
  back: {
    color: theme.colors.white,
    fontFamily: theme.fontFamily.sansBoldItalic,
    fontSize: 16,
    textAlign: 'center',
  },
});
