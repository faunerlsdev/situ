'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { notify, presentError, resetPasswordSchema } from '@situ/shared';
import type { ResetPasswordValues } from '@situ/shared';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { useEffect, useState } from 'react';
import { useForm, type UseFormRegisterReturn } from 'react-hook-form';
import { api } from '@/lib/api';
import { FieldError, FormStatus } from './login-form';

export function NewPasswordForm() {
  const params = useSearchParams();
  const token = params.get('token') ?? '';
  const [notice, setNotice] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(token ? null : 'El enlace no incluye el código de recuperación.');
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
          presentError(cause, setFormError);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [token]);

  async function onSubmit(values: ResetPasswordValues) {
    setFormError(null);
    setNotice(null);
    try {
      await api.resetPassword({ token, password: values.password });
      notify.success('Contraseña actualizada. Ya puedes ingresar.');
      setReady(false);
    } catch (cause) {
      presentError(cause, setFormError);
    }
  }

  const errors = form.formState.errors;

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="flex min-h-dvh flex-col items-center justify-center gap-4 bg-primary px-6 py-10" noValidate>
      <h1 className="font-display text-[42px] leading-none font-extrabold text-white italic">Nueva contraseña</h1>
      <FormStatus error={formError} notice={notice} />
      {ready ? (
        <>
          <PasswordField id="password" label="Contraseña nueva" error={errors.password?.message} registration={form.register('password')} />
          <PasswordField id="confirmation" label="Repite la contraseña" error={errors.confirmation?.message} registration={form.register('confirmation')} />
          <button type="submit" disabled={form.formState.isSubmitting} className="font-sans text-[22px] font-bold text-white italic disabled:opacity-60">
            {form.formState.isSubmitting ? 'Guardando…' : 'Guardar contraseña'}
          </button>
        </>
      ) : null}
      <Link href="/login" className="font-sans text-body font-bold text-white italic">Volver a ingresar</Link>
    </form>
  );
}

function PasswordField({
  id,
  label,
  error,
  registration,
}: {
  id: string;
  label: string;
  error?: string;
  registration: UseFormRegisterReturn;
}) {
  return (
    <div className="w-[80%] max-w-[360px]">
      <input
        id={id}
        type="password"
        placeholder={label}
        autoComplete="new-password"
        aria-label={label}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${id}-error` : undefined}
        className="h-[52px] w-full rounded-[2px] bg-white px-3 font-sans text-body font-bold text-ink italic outline-none placeholder:text-gray400"
        {...registration}
      />
      {error ? <FieldError id={`${id}-error`} message={error} /> : null}
    </div>
  );
}
