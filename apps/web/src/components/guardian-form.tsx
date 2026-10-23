'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import type { GuardianDocumentType } from '@situ/shared';
import { guardianDataSchema, notify, presentError } from '@situ/shared';
import type { GuardianDataValues } from '@situ/shared';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { useState } from 'react';
import { useForm, type UseFormRegisterReturn } from 'react-hook-form';
import { api } from '@/lib/api';
import { FieldError, FormStatus } from './login-form';

const documents: { value: GuardianDocumentType; label: string }[] = [
  { value: 'CitizenshipCard', label: 'Cédula de ciudadanía' },
  { value: 'ForeignerCard', label: 'Cédula de extranjería' },
  { value: 'Passport', label: 'Pasaporte' },
  { value: 'Other', label: 'Otro documento' },
];

export function GuardianForm() {
  const params = useSearchParams();
  const token = params.get('token') ?? '';
  const [step, setStep] = useState<'data' | 'authorize'>('data');
  const [notice, setNotice] = useState<string | null>(token ? null : 'El enlace de autorización no incluye el código.');
  const [formError, setFormError] = useState<string | null>(null);
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
    setFormError(null);
    setNotice(null);
    try {
      await api.submitGuardianData({ token, ...values });
      setStep('authorize');
      notify.info('Te enviamos un correo para verificar tu identidad. Ábrelo y luego vuelve aquí para autorizar.');
    } catch (cause) {
      presentError(cause, setFormError);
    }
  }

  async function authorize() {
    setPending(true);
    setFormError(null);
    try {
      await api.authorizeGuardianConsent({
        token,
        liveStreamingAuthorized: live,
        locationSharingAuthorized: location,
      });
      notify.success('Autorización registrada. El menor puede continuar cuando su correo esté confirmado.');
      setStep('data');
    } catch (cause) {
      presentError(cause, setFormError);
    } finally {
      setPending(false);
    }
  }

  async function decline() {
    setPending(true);
    setFormError(null);
    try {
      await api.declineGuardianConsent({ token });
      notify.info('Rechazaste la autorización. La cuenta del menor sigue pendiente.');
    } catch (cause) {
      presentError(cause, setFormError);
    } finally {
      setPending(false);
    }
  }

  const errors = form.formState.errors;

  return (
    <main className="flex min-h-dvh flex-col items-center justify-center gap-4 bg-primary px-6 py-10">
      <h1 className="text-center font-display text-[40px] leading-none font-extrabold text-white italic">Autorización del acudiente</h1>
      <FormStatus error={formError} notice={notice} />
      {token && step === 'data' ? (
        <form onSubmit={form.handleSubmit(onSubmit)} className="flex w-[80%] max-w-[360px] flex-col gap-3" noValidate>
          <TextField id="fullName" label="Nombre completo" error={errors.fullName?.message} registration={form.register('fullName')} />
          <label className="font-sans text-body font-bold text-white italic" htmlFor="documentType">
            Documento
            <select id="documentType" className="mt-1 h-[52px] w-full rounded-[2px] bg-white px-3 text-ink" {...form.register('documentType')}>
              {documents.map((document) => (
                <option key={document.value} value={document.value}>{document.label}</option>
              ))}
            </select>
          </label>
          {errors.documentType?.message ? <FieldError id="document-type-error" message={errors.documentType.message} /> : null}
          <TextField id="documentNumber" label="Número de documento" error={errors.documentNumber?.message} registration={form.register('documentNumber')} />
          <TextField id="contactEmail" label="Correo de contacto" type="email" error={errors.contactEmail?.message} registration={form.register('contactEmail')} />
          <button type="submit" disabled={form.formState.isSubmitting} className="font-sans text-[22px] font-bold text-white italic disabled:opacity-60">
            {form.formState.isSubmitting ? 'Enviando…' : 'Continuar'}
          </button>
        </form>
      ) : null}
      {token && step === 'authorize' ? (
        <div className="flex w-[80%] max-w-[360px] flex-col gap-3 font-sans text-body font-bold text-white italic">
          <label className="flex items-start gap-2">
            <input type="checkbox" checked={live} onChange={(event) => setLive(event.target.checked)} />
            Autorizo que el menor transmita en vivo.
          </label>
          <label className="flex items-start gap-2">
            <input type="checkbox" checked={location} onChange={(event) => setLocation(event.target.checked)} />
            Autorizo que el menor comparta su ubicación.
          </label>
          <button type="button" disabled={pending} onClick={() => void authorize()} className="font-sans text-[22px] font-bold text-white italic disabled:opacity-60">
            {pending ? 'Guardando…' : 'Autorizar'}
          </button>
          <button type="button" disabled={pending} onClick={() => void decline()} className="font-sans text-body font-bold text-white underline disabled:opacity-60">
            Rechazar
          </button>
        </div>
      ) : null}
      <Link href="/login" className="font-sans text-body font-bold text-white italic">Volver a ingresar</Link>
    </main>
  );
}

function TextField({
  id,
  label,
  error,
  registration,
  type = 'text',
}: {
  id: string;
  label: string;
  error?: string;
  registration: UseFormRegisterReturn;
  type?: string;
}) {
  return (
    <div>
      <label className="font-sans text-body font-bold text-white italic" htmlFor={id}>
        {label}
        <input
          id={id}
          type={type}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? `${id}-error` : undefined}
          className="mt-1 h-[52px] w-full rounded-[2px] bg-white px-3 text-ink outline-none"
          {...registration}
        />
      </label>
      {error ? <FieldError id={`${id}-error`} message={error} /> : null}
    </div>
  );
}
