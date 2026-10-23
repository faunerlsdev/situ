'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import type { City, Country, LegalDocument, Region } from '@situ/shared';
import { adultAge, ageInYears, composeDateOfBirth, notify, presentError, registerSchema } from '@situ/shared';
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
        password: values.password,
        dateOfBirth,
        countryId: values.countryId,
        regionId: values.regionId,
        cityId: values.cityId,
        acceptedDocumentIds: documents.map((document) => document.id),
      });
      notify.success(
        result.status === 'PendingGuardianConsent'
          ? 'Creamos la cuenta. Confirma tu correo. Como eres menor de edad, la cuenta sigue pendiente hasta que tu acudiente autorice el enlace que le enviamos.'
          : 'Te enviamos un correo para confirmar la cuenta.',
      );
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
      <main className="relative min-h-dvh bg-primary px-6 py-16">
        <h1 className="font-display text-[56px] leading-none font-extrabold text-white italic">Registro</h1>
        <div className="relative mt-6">
          <QueryError message={loadError} onRetry={() => router.refresh()} />
        </div>
      </main>
    );
  }

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="relative min-h-dvh overflow-hidden bg-primary px-6 py-8" noValidate>
      <img src="/icons/nav-mapa-globo.png" alt="" className="pointer-events-none absolute -top-8 -right-6 w-[55%] max-w-md object-contain mix-blend-multiply" />
      <h1 className="relative mt-24 font-display text-[56px] leading-none font-extrabold text-white italic">Registro</h1>
      <div className="relative mt-6 flex flex-col gap-3.5">
        <FormStatus error={formError} notice={null} />
        <TextInput id="userName" label="Nombre de usuario" autoComplete="username" error={errors.userName?.message} registration={form.register('userName')} />
        <TextInput id="email" label="Correo" type="email" autoComplete="email" error={errors.email?.message} registration={form.register('email')} />
        <TextInput id="password" label="Contraseña" type="password" autoComplete="new-password" error={errors.password?.message} registration={form.register('password')} />
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
          <legend className="font-sans text-body font-bold text-white italic">Fecha de nacimiento</legend>
          <div className="mt-2 flex gap-2.5">
            <input aria-label="Día" inputMode="numeric" maxLength={2} placeholder="Día" className={dateClass} {...form.register('day')} />
            <input aria-label="Mes" inputMode="numeric" maxLength={2} placeholder="Mes" className={dateClass} {...form.register('month')} />
            <input aria-label="Año" inputMode="numeric" maxLength={4} placeholder="Año" className={dateClass} {...form.register('year')} />
          </div>
          {errors.day?.message ? <FieldError id="birth-error" message={errors.day.message} /> : null}
        </fieldset>
        {belowMinimum ? (
          <p role="status" className="rounded-[4px] bg-white px-3 py-3 font-sans text-body font-bold text-live">
            En {selectedCountry?.name} la edad mínima para registrarse es {selectedCountry?.minimumAge} años.
          </p>
        ) : null}
        {minor && !belowMinimum ? (
          <p role="status" className="rounded-[4px] bg-white px-3 py-3 font-sans text-body font-bold text-ink">
            Como eres menor de edad, la cuenta queda pendiente. Tu acudiente debe abrir el enlace que le enviamos, verificar su correo y autorizar la cuenta.
          </p>
        ) : null}
        <div className="font-sans text-[12px] leading-4 font-bold text-white italic">
          {documents.map((document) => (
            <a key={document.id} href={document.url} className="block underline">{legalLabel(document.type)}</a>
          ))}
          <label className="mt-2 flex items-start gap-2">
            <input type="checkbox" className="mt-0.5" {...form.register('accepted')} />
            Acepto los documentos legales vigentes.
          </label>
          {errors.accepted?.message ? <FieldError id="accepted-error" message={errors.accepted.message} /> : null}
        </div>
        <button type="submit" disabled={pending} className="mt-2 flex flex-col items-center active:scale-95 disabled:opacity-60">
          <img src="/brand/situ-isotipo-negro.png" alt="" width={72} height={94} className="h-[94px] w-[72px] object-contain" />
          <span className="text-stamp font-sans text-[28px] leading-none font-bold text-white italic">
            {pending ? 'Creando cuenta…' : 'Registrar Me'}
          </span>
        </button>
        <Link href="/login" className="text-center font-sans text-body font-bold text-white italic">
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
        type={type}
        placeholder={label}
        autoComplete={autoComplete}
        spellCheck={false}
        aria-label={label}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${id}-error` : undefined}
        className={fieldClass}
        {...registration}
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
    <label className="font-sans text-body font-bold text-white italic" htmlFor={id}>
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
  'h-[52px] w-full rounded-[2px] bg-white px-3 font-sans text-body font-bold text-ink italic outline-none placeholder:text-gray400';

const dateClass =
  'h-12 w-[84px] rounded-[2px] bg-white text-center font-sans text-body font-bold text-ink italic outline-none placeholder:text-gray400';
