'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { notify, passwordResetRequestSchema, presentError } from '@situ/shared';
import type { PasswordResetRequestValues } from '@situ/shared';
import Link from 'next/link';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { api } from '@/lib/api';
import { FieldError, FormStatus } from './login-form';

export function ResetForm() {
  const [notice, setNotice] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const form = useForm<PasswordResetRequestValues>({
    resolver: zodResolver(passwordResetRequestSchema),
    defaultValues: { email: '' },
  });

  async function onSubmit(values: PasswordResetRequestValues) {
    setNotice(null);
    setFormError(null);

    try {
      await api.requestPasswordReset(values);
      notify.success('Si el correo está registrado, enviaremos el enlace para elegir una contraseña nueva.');
    } catch (cause) {
      presentError(cause, setFormError);
    }
  }

  const error = form.formState.errors.email?.message;
  const pending = form.formState.isSubmitting;

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="flex min-h-dvh flex-col items-center justify-center gap-4 bg-primary px-6 py-10" noValidate>
      <h1 className="mb-4 font-display text-[48px] leading-none font-extrabold text-white italic">Recuperar</h1>
      <p className="w-[80%] max-w-[360px] text-center font-sans text-body font-bold text-white">
        Escribe el correo de tu cuenta. Si está registrado, te enviaremos un enlace.
      </p>
      <FormStatus error={formError} notice={notice} />
      <div className="w-[80%] max-w-[360px]">
        <input
          type="email"
          placeholder="Correo"
          autoComplete="email"
          aria-label="Correo"
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? 'reset-email-error' : undefined}
          className="h-[52px] w-full rounded-[2px] bg-white px-3 font-sans text-body font-bold text-ink italic outline-none placeholder:text-gray400"
          {...form.register('email')}
        />
        {error ? <FieldError id="reset-email-error" message={error} /> : null}
      </div>
      <button type="submit" disabled={pending} className="mt-2 font-sans text-[22px] font-bold text-white italic disabled:opacity-60">
        {pending ? 'Enviando…' : 'Enviar enlace'}
      </button>
      <Link href="/login" className="font-sans text-body font-bold text-white italic">
        Volver a ingresar
      </Link>
    </form>
  );
}
