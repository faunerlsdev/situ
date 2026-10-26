'use client';

import { useId, useState } from 'react';

export function ReasonDialog({
  title,
  confirmLabel,
  danger,
  extra,
  onClose,
  onConfirm,
}: {
  title: string;
  confirmLabel: string;
  danger?: boolean;
  extra?: React.ReactNode;
  onClose: () => void;
  onConfirm: (reason: string) => Promise<void>;
}) {
  const reasonId = useId();
  const [reason, setReason] = useState('');
  const [error, setError] = useState('');
  const [pending, setPending] = useState(false);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (reason.trim().length < 3) {
      setError('Escribe el motivo.');
      return;
    }

    setPending(true);
    setError('');
    try {
      await onConfirm(reason.trim());
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'No pudimos completar la solicitud.');
      setPending(false);
    }
  }

  return (
    <div className="fixed inset-0 z-40 flex items-center justify-center bg-black/60 p-6" role="presentation">
      <form role="dialog" aria-modal="true" aria-labelledby={`${reasonId}-title`} className="w-full max-w-lg rounded-xl border border-app-border bg-app-elevated p-6" onSubmit={submit}>
        <h2 id={`${reasonId}-title`} className="font-display text-xl">{title}</h2>
        {extra}
        <label htmlFor={reasonId} className="mt-4 block text-sm text-app-muted">Motivo</label>
        <textarea id={reasonId} value={reason} aria-invalid={error ? true : undefined} onChange={(event) => setReason(event.target.value)} className="mt-1 min-h-24 w-full rounded border border-app-border bg-app-input p-3" />
        {error ? <p role="alert" className="mt-2 text-app-danger">{error}</p> : null}
        <div className="mt-4 flex flex-wrap justify-end gap-2">
          <button type="button" onClick={onClose} className="rounded border border-app-border px-4 py-2">Cancelar</button>
          <button type="submit" disabled={pending} className={`rounded px-4 py-2 font-semibold ${danger ? 'bg-app-danger text-white' : 'bg-app-primary text-app-on'}`}>
            {pending ? 'Guardando…' : confirmLabel}
          </button>
        </div>
      </form>
    </div>
  );
}
