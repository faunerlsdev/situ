'use client';

import { deletionDateLabel, notify, presentError } from '@situ/shared';
import Link from 'next/link';
import { useState } from 'react';
import type { FormEvent } from 'react';
import {
  deletionCancelLabel,
  deletionGraceDays,
  deletionLoginLabel,
  deletionRequestLabel,
  deletionSteps,
  reportRetentionDays,
  type DeletionView,
} from './deletion-copy';

export function DeleteAccountView({
  state,
  supportUrl,
  supportEmail,
  onRequest,
  onCancel,
}: {
  state: DeletionView;
  supportUrl: string;
  supportEmail: string;
  onRequest: (password: string) => Promise<void>;
  onCancel: () => Promise<void>;
}) {
  const [password, setPassword] = useState('');
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function request(event: FormEvent) {
    event.preventDefault();
    setPending(true);
    setError(null);
    try {
      await onRequest(password);
      setPassword('');
      notify.success('Recibimos la solicitud. Te escribimos con la fecha en que se elimina la cuenta.');
    } catch (cause) {
      presentError(cause, setError);
    } finally {
      setPending(false);
    }
  }

  async function cancel() {
    setPending(true);
    setError(null);
    try {
      await onCancel();
      notify.success('Listo. Tu cuenta sigue activa.');
    } catch (cause) {
      presentError(cause, setError);
    } finally {
      setPending(false);
    }
  }

  return (
    <article className="mx-auto grid w-full max-w-xl gap-4 px-5 py-10">
      <h1 className="font-display text-4xl">Eliminar cuenta</h1>
      <p>Puedes pedir que borremos tu cuenta desde la app o, si ya ingresaste en la web, desde esta página. La solicitud usa tu contraseña para confirmar que eres tú.</p>
      <h2 className="font-display text-2xl">Qué se borra</h2>
      <p>Correo, teléfono, nombre, contraseña, fecha de nacimiento, perfil, reportes, comentarios, reacciones, amigos, ubicación y sesiones. El teléfono y el correo dejan de mostrarse.</p>
      <h2 className="font-display text-2xl">Qué se conserva</h2>
      <p>Se guarda un identificador anonimizado para auditoría y moderación, las constancias de autorización y de aceptación, las denuncias, los retiros de contenido y los registros de pago sin el número de la tarjeta. Si hay una retención legal, la cuenta no se borra hasta que esa retención termine.</p>
      <h2 className="font-display text-2xl">Plazos</h2>
      <p>Hay {deletionGraceDays} días de gracia. Durante esos días puedes cancelar la solicitud. Los reportes se conservan hasta {reportRetentionDays} días si no pides antes la eliminación de la cuenta. Esos plazos se configuran en el servidor.</p>
      <h2 className="font-display text-2xl">Desde la app</h2>
      <ol className="list-decimal space-y-1 pl-5">
        {deletionSteps.map((step) => (
          <li key={step}>{step}</li>
        ))}
      </ol>
      {error ? <p role="alert">{error}</p> : null}
      {state.signedIn && state.scheduledFor ? (
        <section className="grid gap-3">
          <p>La eliminación está programada para el {deletionDateLabel(state.scheduledFor)}. Puedes cancelarla durante la gracia.</p>
          <button type="button" disabled={pending} onClick={() => void cancel()} className="min-h-11 rounded bg-primary font-bold text-app-on disabled:opacity-60">
            {pending ? 'Cancelando…' : deletionCancelLabel}
          </button>
        </section>
      ) : null}
      {state.signedIn && !state.scheduledFor ? (
        <form onSubmit={(event) => void request(event)} className="grid gap-3">
          <label className="grid gap-1 text-sm">
            Contraseña
            <input
              type="password"
              required
              autoComplete="current-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              className="min-h-11 rounded border border-app-border bg-app-input px-3"
            />
          </label>
          <button type="submit" disabled={pending} className="min-h-11 rounded bg-primary font-bold text-app-on disabled:opacity-60">
            {pending ? 'Enviando…' : deletionRequestLabel}
          </button>
        </form>
      ) : null}
      {!state.signedIn ? (
        <p>
          <Link href={state.loginHref} className="font-bold text-primary">
            {deletionLoginLabel}
          </Link>
        </p>
      ) : null}
      <p>
        Si no puedes ingresar, escribe a <a href={`mailto:${supportEmail}`} className="font-bold text-primary">{supportEmail}</a> o abre el{' '}
        <a href={supportUrl} className="font-bold text-primary">centro de ayuda</a>.
      </p>
    </article>
  );
}
