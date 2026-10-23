'use client';

import { useState } from 'react';
import type { FormEvent } from 'react';
import { notify, presentError } from '@situ/shared';
import { api } from '@/lib/api';
import { FormStatus, controlClassName, readField } from './form-status';

export function ContactForm() {
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    setError(null);
    setNotice(null);
    setPending(true);

    try {
      await api.submitContact({
        name: readField(form, 'name'),
        email: readField(form, 'email'),
        message: readField(form, 'message'),
      });
      notify.success('Recibimos tu mensaje. Te respondemos al correo que escribiste.');
      event.currentTarget.reset();
    } catch (cause) {
      presentError(cause, setError);
    } finally {
      setPending(false);
    }
  }

  return (
    <form onSubmit={onSubmit} className="space-y-5">
      <FormStatus error={error} notice={notice} />
      <label className="block text-sm font-semibold">
        Nombre
        <input className={controlClassName} name="name" autoComplete="name" required />
      </label>
      <label className="block text-sm font-semibold">
        Correo
        <input className={controlClassName} name="email" type="email" autoComplete="email" required spellCheck={false} />
      </label>
      <label className="block text-sm font-semibold">
        Mensaje
        <textarea className={`${controlClassName} min-h-32`} name="message" required />
      </label>
      <button
        type="submit"
        disabled={pending}
        className="inline-flex min-h-11 items-center rounded-full bg-primary px-5 font-semibold text-white hover:bg-primary-pressed disabled:opacity-60"
      >
        {pending ? 'Enviando…' : 'Enviar mensaje'}
      </button>
    </form>
  );
}
