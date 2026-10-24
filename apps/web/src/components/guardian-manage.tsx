'use client';

import { messageFromUnknown } from '@situ/shared';
import { useSearchParams } from 'next/navigation';
import { useState } from 'react';
import { api } from '@/lib/api';

export function GuardianManage() {
  const token = useSearchParams().get('token') ?? '';
  const [live, setLive] = useState(false);
  const [location, setLocation] = useState(false);
  const [message, setMessage] = useState<string | null>(token ? null : 'El enlace no es válido.');
  const [pending, setPending] = useState(false);

  async function save() {
    setPending(true);
    try {
      await api.updateGuardianPermissions({ token, liveStreamingAuthorized: live, locationSharingAuthorized: location });
      setMessage('Guardamos los permisos. La constancia original sigue vigente.');
    } catch (cause) {
      setMessage(messageFromUnknown(cause));
    } finally {
      setPending(false);
    }
  }

  async function revoke() {
    setPending(true);
    try {
      await api.revokeGuardianConsent(token);
      setMessage('Revocamos la autorización. El menor ya no puede usar la cuenta.');
    } catch (cause) {
      setMessage(messageFromUnknown(cause));
    } finally {
      setPending(false);
    }
  }

  return (
    <main className="mx-auto flex min-h-dvh w-full max-w-lg flex-col gap-4 px-6 py-10 font-sans">
      <p className="font-display text-4xl font-extrabold text-[#FF6400]">SITU</p>
      <h1 className="font-display text-3xl font-bold">Gestionar la autorización</h1>
      {message ? <p role="status">{message}</p> : null}
      <label className="flex items-start gap-2"><input type="checkbox" checked={live} onChange={(event) => setLive(event.target.checked)} /> Permitir transmisiones en vivo</label>
      <label className="flex items-start gap-2"><input type="checkbox" checked={location} onChange={(event) => setLocation(event.target.checked)} /> Permitir compartir ubicación con amigos</label>
      <button type="button" disabled={pending || !token} onClick={() => void save()} className="rounded-[8px] bg-[#1C1C1C] px-4 py-3 font-bold text-white disabled:opacity-60">Guardar permisos</button>
      <button type="button" disabled={pending || !token} onClick={() => void revoke()} className="font-bold underline">Revocar la autorización</button>
    </main>
  );
}
