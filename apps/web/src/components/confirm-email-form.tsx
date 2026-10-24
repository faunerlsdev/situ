'use client';

import { notify, presentError } from '@situ/shared';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { api } from '@/lib/api';
import { FieldError, FormStatus } from './login-form';

export function ConfirmEmailForm() {
  const params = useSearchParams();
  const token = params.get('token') ?? '';
  const [email, setEmail] = useState('');
  const [notice, setNotice] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [confirmed, setConfirmed] = useState(false);

  async function confirm() {
    setPending(true);
    setFormError(null);
    try {
      await api.confirmEmail({ token });
      setConfirmed(true);
      notify.success('Correo confirmado. Ya puedes ingresar.');
    } catch (cause) {
      presentError(cause, setFormError);
    } finally {
      setPending(false);
    }
  }

  async function resend(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPending(true);
    setFormError(null);
    setNotice(null);
    try {
      await api.resendConfirmationEmail({ email });
      notify.success('Si la cuenta existe y falta confirmar el correo, enviaremos un enlace nuevo.');
    } catch (cause) {
      presentError(cause, setFormError);
    } finally {
      setPending(false);
    }
  }

  return (
    <main className="flex min-h-dvh flex-col items-center justify-center gap-4 splash-gradient px-6 py-10">
      <h1 className="font-display text-[42px] leading-none font-extrabold text-app-text italic">Confirmar correo</h1>
      <FormStatus error={formError} notice={notice} />
      {token && !confirmed ? (
        <button type="button" disabled={pending} onClick={() => void confirm()} className="font-sans text-[22px] font-bold text-app-text italic disabled:opacity-60">
          {pending ? 'Confirmando…' : 'Confirmar mi correo'}
        </button>
      ) : null}
      {!token ? (
        <form onSubmit={(event) => void resend(event)} className="flex w-[80%] max-w-[360px] flex-col gap-3" noValidate>
          <label className="font-sans text-body font-bold text-app-text italic" htmlFor="confirm-email">
            Correo
            <input
              id="confirm-email"
              type="email"
              required
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              className="mt-2 h-[52px] w-full rounded-[8px] border border-app-border bg-app-input focus:border-[1.5px] focus:border-primary px-3 font-sans text-body font-bold text-app-text italic outline-none"
            />
          </label>
          {!email.trim() && formError ? <FieldError id="confirm-email-error" message="Escribe tu correo." /> : null}
          <button type="submit" disabled={pending} className="font-sans text-[22px] font-bold text-app-text italic disabled:opacity-60">
            {pending ? 'Enviando…' : 'Reenviar enlace'}
          </button>
        </form>
      ) : null}
      <Link href="/login" className="font-sans text-body font-bold text-app-text italic">Volver a ingresar</Link>
    </main>
  );
}
