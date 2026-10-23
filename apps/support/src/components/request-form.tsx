'use client';

import { useState } from 'react';
import type { FormEvent } from 'react';
import type { DataRequestKind } from '@situ/shared';
import { notify, presentError } from '@situ/shared';
import { api } from '@/lib/api';
import { FormStatus, controlClassName, readField } from './form-status';

const kinds: { value: DataRequestKind; label: string }[] = [
  { value: 'access', label: 'Conocer mis datos' },
  { value: 'correction', label: 'Corregir mis datos' },
  { value: 'deletion', label: 'Eliminar mis datos' },
  { value: 'contentRemoval', label: 'Retirar contenido' },
];

export function RequestForm() {
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const kind = readField(form, 'kind');
    if (!isKind(kind)) {
      setError('Elige el tipo de solicitud.');
      return;
    }

    const contentUrl = readField(form, 'contentUrl');
    setError(null);
    setNotice(null);
    setPending(true);

    try {
      await api.submitDataRequest({
        name: readField(form, 'name'),
        email: readField(form, 'email'),
        kind,
        message: readField(form, 'message'),
        ...(contentUrl.length > 0 ? { contentUrl } : {}),
      });
      notify.success('Recibimos la solicitud. Te escribimos al correo que dejaste.');
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
        Tipo de solicitud
        <select className={controlClassName} name="kind" required defaultValue="">
          <option value="">Selecciona una opción</option>
          {kinds.map((kind) => (
            <option key={kind.value} value={kind.value}>
              {kind.label}
            </option>
          ))}
        </select>
      </label>
      <label className="block text-sm font-semibold">
        Cuéntanos qué necesitas
        <textarea className={`${controlClassName} min-h-32`} name="message" required />
      </label>
      <label className="block text-sm font-semibold">
        Enlace del contenido
        <input className={controlClassName} name="contentUrl" type="url" inputMode="url" placeholder="https://" />
        <span className="mt-1 block font-normal text-gray600">Solo si pides un retiro. Si no aplica, déjalo vacío.</span>
      </label>
      <button
        type="submit"
        disabled={pending}
        className="inline-flex min-h-11 items-center rounded-full bg-primary px-5 font-semibold text-white hover:bg-primary-pressed disabled:opacity-60"
      >
        {pending ? 'Enviando…' : 'Enviar solicitud'}
      </button>
    </form>
  );
}

function isKind(value: string): value is DataRequestKind {
  return value === 'access' || value === 'correction' || value === 'deletion' || value === 'contentRemoval';
}
