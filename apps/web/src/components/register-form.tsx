'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import type { City, Country, LegalDocument, Region } from '@situ/shared';
import { adultAge, ageInYears, callingCodes, composeDateOfBirth, composePhone, notify, phonePurpose, presentError, registerSchema } from '@situ/shared';
import type { RegisterValues } from '@situ/shared';
import Link from 'next/link';
import { useEffect, useState, type ReactNode } from 'react';
import { useForm, type UseFormRegisterReturn } from 'react-hook-form';
import { api } from '@/lib/api';
import { useRouter } from 'next/navigation';
import { FieldError, FormStatus } from './login-form';
import { QueryError } from './feedback/inline';

export function RegisterForm({
  countries,
  loadError,
}: {
  countries: Country[];
  loadError: string | null;
}) {
  const router = useRouter();
  const [formError, setFormError] = useState<string | null>(null);
  const [regions, setRegions] = useState<Region[]>([]);
  const [cities, setCities] = useState<City[]>([]);
  const [documents, setDocuments] = useState<LegalDocument[]>([]);
  const form = useForm<RegisterValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      givenNames: '',
      surnames: '',
      callingCode: '57',
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
  const day = form.watch('day');
  const month = form.watch('month');
  const year = form.watch('year');
  const selectedCountry = countries.find((country) => country.id === countryId);
  const birth = composeDateOfBirth(year, month, day);
  const age = birth ? ageInYears(birth) : null;
  const minor = age !== null && age < adultAge;
  const belowMinimum = age !== null && selectedCountry !== undefined && age < selectedCountry.minimumAge;

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
          presentError(cause, setFormError);
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
          presentError(cause, setFormError);
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
      setFormError('Completa el lugar y acepta los documentos legales.');
      return;
    }

    setFormError(null);

    try {
      const result = await api.register({
        email: values.email,
        userName: values.userName,
        givenNames: values.givenNames,
        surnames: values.surnames,
        phone: values.phone ? composePhone(values.callingCode, values.phone) : '',
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
        router.push('/login');
        return;
      }

      form.reset();
      notify.success('Te enviamos un correo para confirmar la cuenta. Ábrelo para activarla.', { placement: 'bottom' });
      router.push('/login');
    } catch (cause) {
      presentError(cause, setFormError, () => {
        void onSubmit(values);
      });
    }
  }

  const pending = form.formState.isSubmitting;
  const errors = form.formState.errors;

  if (loadError) {
    return (
      <main className="relative min-h-dvh splash-gradient px-6 py-16">
        <h1 className="font-display text-[56px] leading-none font-extrabold text-app-text italic">Registro</h1>
        <div className="relative mt-6">
          <QueryError message={loadError} onRetry={() => router.refresh()} />
        </div>
      </main>
    );
  }

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="relative min-h-dvh overflow-hidden splash-gradient px-6 py-8" noValidate>
      <img src="/icons/nav-mapa-globo-dark.png" alt="" className="pointer-events-none absolute -top-8 -right-6 w-[55%] max-w-md object-contain opacity-25" />
      <h1 className="relative mt-24 font-display text-[48px] leading-none font-extrabold text-app-text italic">Registro</h1>
      <div className="relative mt-6 flex flex-col gap-3.5">
        <FormStatus error={formError} notice={null} />
        <TextInput id="givenNames" label="Nombres" autoComplete="given-name" error={errors.givenNames?.message} registration={form.register('givenNames')} />
        <TextInput id="surnames" label="Apellidos" autoComplete="family-name" error={errors.surnames?.message} registration={form.register('surnames')} />
        <label className="grid gap-1 text-sm font-bold" htmlFor="calling-code">
          Indicativo
          <select id="calling-code" aria-label="Indicativo" className="min-h-11 rounded border border-app-border bg-app-input px-3" {...form.register('callingCode')}>
            {callingCodes.map((item) => (
              <option key={item.code} value={item.code}>{item.label}</option>
            ))}
          </select>
        </label>
        <TextInput id="phone" label="Teléfono" type="tel" autoComplete="tel" error={errors.phone?.message} registration={form.register('phone')} />
        <p className="text-sm text-app-muted">{phonePurpose}</p>
        <TextInput id="userName" label="Nombre de usuario" autoComplete="username" error={errors.userName?.message} registration={form.register('userName')} />
        <TextInput id="email" label="Correo" type="email" autoComplete="email" error={errors.email?.message} registration={form.register('email')} />
        <TextInput id="password" label="Contraseña" type="password" autoComplete="new-password" error={errors.password?.message} registration={form.register('password')} />
        <TextInput id="passwordConfirmation" label="Confirmar contraseña" type="password" autoComplete="new-password" error={errors.passwordConfirmation?.message} registration={form.register('passwordConfirmation')} />
        <SelectInput id="countryId" label="País" error={errors.countryId?.message} disabled={countries.length === 0} registration={form.register('countryId')}>
          <option value="">Selecciona un país</option>
          {countries.map((country) => (
            <option key={country.id} value={country.id}>{country.name}</option>
          ))}
        </SelectInput>
        <SelectInput id="regionId" label={selectedCountry?.regionLabel ?? 'Departamento'} error={errors.regionId?.message} disabled={regions.length === 0} registration={form.register('regionId')}>
          <option value="">{regions.length === 0 ? 'Selecciona un país primero' : 'Selecciona'}</option>
          {regions.map((region) => (
            <option key={region.id} value={region.id}>{region.name}</option>
          ))}
        </SelectInput>
        <SelectInput id="cityId" label="Ciudad" error={errors.cityId?.message} disabled={cities.length === 0} registration={form.register('cityId')}>
          <option value="">{cities.length === 0 ? 'Selecciona un departamento primero' : 'Selecciona'}</option>
          {cities.map((city) => (
            <option key={city.id} value={city.id}>{city.name}</option>
          ))}
        </SelectInput>
        <fieldset>
          <legend className="font-sans text-body font-bold text-app-text italic">Fecha de nacimiento</legend>
          <div className="mt-2 flex gap-2.5">
            <input aria-label="Día" inputMode="numeric" maxLength={2} placeholder="Día" className={dateClass} {...form.register('day')} />
            <input aria-label="Mes" inputMode="numeric" maxLength={2} placeholder="Mes" className={dateClass} {...form.register('month')} />
            <input aria-label="Año" inputMode="numeric" maxLength={4} placeholder="Año" className={dateClass} {...form.register('year')} />
          </div>
          {errors.day?.message ? <FieldError id="birth-error" message={errors.day.message} /> : null}
        </fieldset>
        {belowMinimum ? (
          <p role="status" className="font-sans text-body font-bold text-app-danger">
            En {selectedCountry?.name} la edad mínima para registrarse es {selectedCountry?.minimumAge} años.
          </p>
        ) : null}
        {minor && !belowMinimum ? (
          <p role="status" className="font-sans text-body font-bold text-app-text">
            Como eres menor de edad, la cuenta queda pendiente. Tu acudiente debe abrir el enlace que le enviamos, verificar su correo y autorizar la cuenta.
          </p>
        ) : null}
        <div className="font-sans text-[12px] leading-4 font-bold text-app-muted italic [&_a]:text-primary">
          {documents.map((document) => (
            <a key={document.id} href={document.url} className="block underline">{legalLabel(document.type)}</a>
          ))}
          <label className="mt-2 flex items-start gap-2">
            <input type="checkbox" className="mt-0.5" {...form.register('accepted')} />
            Acepto los documentos legales vigentes.
          </label>
          {errors.accepted?.message ? <FieldError id="accepted-error" message={errors.accepted.message} /> : null}
          <label className="font-sans text-body font-bold text-app-text italic" htmlFor="guardianEmail">
            Correo de tu representante (opcional)
            <input id="guardianEmail" type="email" inputMode="email" autoComplete="email" className="mt-1 h-[52px] w-full rounded-[8px] border border-app-border bg-app-input px-3 text-app-text outline-none" {...form.register('guardianEmail')} />
          </label>
          {errors.guardianEmail?.message ? <FieldError id="guardian-email-error" message={errors.guardianEmail.message} /> : null}
          <label className="flex items-start gap-2 font-sans text-body font-bold text-app-text italic">
            <input type="checkbox" className="mt-0.5" {...form.register('minorNoticeAccepted')} />
            Entendido: un adulto debe autorizar mi cuenta.
          </label>
          {errors.minorNoticeAccepted?.message ? <FieldError id="notice-error" message={errors.minorNoticeAccepted.message} /> : null}
        </div>
        <button type="submit" disabled={pending} className="mt-2 flex w-full flex-col items-center gap-2 disabled:opacity-60">
          <img src="/brand/situ-isotipo-negro.png" alt="" width={72} height={94} className="h-[94px] w-[72px] object-contain" />
          <span className="flex h-[52px] w-full items-center justify-center rounded-[8px] bg-primary font-sans text-[18px] font-bold text-app-on italic">
            {pending ? 'Creando cuenta…' : 'Registrar Me'}
          </span>
        </button>
        <Link href="/login" className="text-center font-sans text-body font-bold text-primary italic">
          ¿Ya tienes cuenta? Ingresar
        </Link>
      </div>
    </form>
  );
}

function TextInput({
  id,
  label,
  error,
  registration,
  type = 'text',
  autoComplete,
}: {
  id: string;
  label: string;
  error?: string;
  registration: UseFormRegisterReturn;
  type?: string;
  autoComplete?: string;
}) {
  return (
    <div>
      <input
        id={id}
        placeholder={label}
        spellCheck={false}
        aria-label={label}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${id}-error` : undefined}
        className={fieldClass}
        {...registration}
        type={type}
        autoComplete={autoComplete}
        inputMode={type === 'email' ? 'email' : type === 'tel' ? 'tel' : undefined}
      />
      {error ? <FieldError id={`${id}-error`} message={error} /> : null}
    </div>
  );
}

function SelectInput({
  id,
  label,
  error,
  disabled,
  registration,
  children,
}: {
  id: string;
  label: string;
  error?: string;
  disabled: boolean;
  registration: UseFormRegisterReturn;
  children: ReactNode;
}) {
  return (
    <label className="font-sans text-body font-bold text-app-text italic" htmlFor={id}>
      {label}
      <select id={id} disabled={disabled} aria-invalid={error ? true : undefined} className={`${fieldClass} mt-2`} {...registration}>
        {children}
      </select>
      {error ? <FieldError id={`${id}-error`} message={error} /> : null}
    </label>
  );
}

function legalLabel(type: LegalDocument['type']) {
  switch (type) {
    case 'TermsOfUse':
      return 'Términos de uso';
    case 'PrivacyPolicy':
      return 'Política de privacidad';
    case 'PublicProfileConsent':
      return 'Consentimiento de perfil público';
    case 'LocationSharingConsent':
      return 'Consentimiento de ubicación';
    case 'PersonalizedAdsConsent':
      return 'Consentimiento de publicidad';
    case 'GuardianNotice':
      return 'Aviso para acudientes';
    default:
      return type;
  }
}

const fieldClass =
  'h-[52px] w-full rounded-[8px] border border-app-border bg-app-input focus:border-[1.5px] focus:border-primary px-3 font-sans text-body font-bold text-app-text italic outline-none placeholder:text-app-placeholder';

const dateClass =
  'h-12 w-[84px] rounded-[8px] border border-app-border bg-app-input text-center font-sans text-body font-bold text-app-text italic outline-none placeholder:text-app-placeholder';
