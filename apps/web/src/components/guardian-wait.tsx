'use client';

import type { GuardianConsentStatus } from '@situ/shared';
import { AppError, confirm, messageFromUnknown, notify } from '@situ/shared';
import { useRouter } from 'next/navigation';
import { useCallback, useEffect, useState } from 'react';
import { SituLoader } from '@/components/feedback/situ-loader';
import { api, postBff } from '@/lib/http';

function remaining(iso: string | null, now: number): string {
  if (!iso) {
    return '00:00:00';
  }
  const total = Math.max(0, Math.floor((Date.parse(iso) - now) / 1000));
  const hours = Math.floor(total / 3600);
  const minutes = Math.floor((total % 3600) / 60);
  const seconds = total % 60;
  return [hours, minutes, seconds].map((part) => String(part).padStart(2, '0')).join(':');
}

function daysLeft(iso: string | null, now: number): string {
  if (!iso) {
    return '7 días';
  }
  const days = Math.max(0, Math.ceil((Date.parse(iso) - now) / 86_400_000));
  return days === 1 ? '1 día' : `${days} días`;
}

export function GuardianWait() {
  const router = useRouter();
  const [status, setStatus] = useState<GuardianConsentStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [pending, setPending] = useState(false);
  const [email, setEmail] = useState('');
  const [statement, setStatement] = useState('');
  const [heard, setHeard] = useState(false);
  const [changing, setChanging] = useState(false);
  const [limitUntil, setLimitUntil] = useState<string | null>(null);
  const [now, setNow] = useState(() => Date.now());

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const next = await api.guardianConsentStatus();
      setStatus(next);
      setHeard(next.minorNoticeRecorded);
      setLimitUntil(next.retryAfter);
    } catch (cause) {
      notify.error(messageFromUnknown(cause), { placement: 'bottom' });
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    let active = true;
    void api.guardianConsentStatus().then((next) => {
      if (!active) {
        return;
      }
      setStatus(next);
      setHeard(next.minorNoticeRecorded);
      setLimitUntil(next.retryAfter);
    }).catch((cause: unknown) => {
      if (active) {
        notify.error(messageFromUnknown(cause), { placement: 'bottom' });
      }
    }).finally(() => {
      if (active) {
        setLoading(false);
      }
    });
    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, []);

  const blocked = limitUntil !== null && Date.parse(limitUntil) > now;
  const waiting = status?.status === 'Requested' || status?.status === 'EmailVerified';
  const canRequest = status?.status === 'NotRequested' || status?.status === 'Expired' || status?.status === 'Revoked';

  async function run(action: () => Promise<GuardianConsentStatus | void>) {
    setPending(true);
    try {
      const next = await action();
      if (next) {
        setStatus(next);
        setHeard(next.minorNoticeRecorded);
        setLimitUntil(next.retryAfter);
      }
    } catch (cause) {
      if (cause instanceof AppError && cause.apiCode === 'guardian_consent.send_limited' && cause.retryAfter) {
        setLimitUntil(cause.retryAfter);
        return;
      }
      notify.error(messageFromUnknown(cause), { placement: 'bottom' });
    } finally {
      setPending(false);
    }
  }

  function leave() {
    void postBff('/api/auth/sign-out').finally(() => router.push('/login'));
  }

  return (
    <main className="mx-auto flex min-h-dvh w-full max-w-md flex-col gap-4 px-6 py-10">
      <p className="text-center font-display text-5xl font-extrabold text-primary italic">SITU</p>
      <h1 className="text-center font-display text-3xl font-bold text-app-text">Necesitamos la autorización de tu mamá, papá o representante legal</h1>
      <p className="text-center font-sans text-body text-app-text">
        Como eres menor de 18 años, la ley nos pide que un adulto responsable autorice que uses SITU. No podrás entrar a la app hasta que lo haga.
      </p>
      <ol className="list-decimal space-y-1 pl-5 font-sans text-body text-app-muted">
        <li>Escribe el correo de tu representante</li>
        <li>Le enviamos un enlace</li>
        <li>Él o ella revisa y autoriza</li>
        <li>¡Listo, ya puedes entrar!</li>
      </ol>
      {loading ? <SituLoader label="Cargando la autorización" /> : null}
      {blocked ? (
        <p role="status" className="text-center font-sans text-body font-bold text-[#FF9F24]">
          Llegaste al límite de envíos. Podrás intentarlo de nuevo en {remaining(limitUntil, now)}
        </p>
      ) : null}
      {!loading && canRequest && !heard ? (
        <section className="flex flex-col gap-3">
          <h2 className="font-sans text-lg font-bold text-app-text">Antes de enviar</h2>
          <p className="font-sans text-body text-app-text">
            SITU muestra a tu representante tu usuario, tu edad y tu ciudad. El correo que escribas se usa solo para esta autorización. Si quieres, deja un mensaje. Si no hay respuesta en 7 días, la solicitud se cierra.
          </p>
          <textarea value={statement} onChange={(event) => setStatement(event.target.value)} placeholder="Si quieres, escribe algo para tu representante" className="min-h-24 rounded-[8px] border border-app-border bg-app-input px-3 py-2 text-app-text" />
          <button type="button" disabled={pending} onClick={() => void run(() => api.acknowledgeMinorNotice())} className="rounded-[8px] bg-primary px-4 py-3 font-sans font-bold text-app-on disabled:opacity-60">Entendido</button>
        </section>
      ) : null}
      {!loading && canRequest && heard ? (
        <section className="flex flex-col gap-3">
          <label className="font-sans font-bold text-app-text" htmlFor="guardian-email">
            Correo de tu representante legal
            <input id="guardian-email" type="email" inputMode="email" value={email} onChange={(event) => setEmail(event.target.value)} className="mt-1 h-[52px] w-full rounded-[8px] border border-app-border bg-app-input px-3 font-normal text-app-text" />
          </label>
          <p className="font-sans text-sm text-app-muted">Debe ser un correo distinto al tuyo. Solo lo usaremos para esta autorización.</p>
          <button type="button" disabled={pending || blocked} onClick={() => void run(() => api.requestGuardianConsent({ guardianEmail: email.trim(), minorNoticeAccepted: true, minorStatement: statement.trim() || undefined }))} className="rounded-[8px] bg-primary px-4 py-3 font-sans font-bold text-app-on disabled:opacity-60">Enviar solicitud</button>
        </section>
      ) : null}
      {!loading && waiting ? (
        <section className="flex flex-col gap-3 text-center">
          <p className="font-sans text-body text-app-text">Enviamos la solicitud a {status?.maskedEmail ?? 'tu representante'}. Tu representante tiene {daysLeft(status?.expiresAt ?? null, now)} para responder.</p>
          <button type="button" disabled={pending || blocked} onClick={() => void run(() => api.resendGuardianConsent())} className="rounded-[8px] bg-primary px-4 py-3 font-sans font-bold text-app-on disabled:opacity-60">Reenviar correo</button>
          {changing ? <input type="email" value={email} onChange={(event) => setEmail(event.target.value)} placeholder="Nuevo correo" className="h-[52px] rounded-[8px] border border-app-border bg-app-input px-3 text-app-text" /> : null}
          <button type="button" disabled={pending || blocked} onClick={() => (changing ? void run(() => api.changeGuardianEmail(email.trim())) : setChanging(true))} className="font-sans font-bold text-primary">Cambiar correo del representante</button>
          <button type="button" disabled={pending} onClick={() => void confirm({ title: 'Cancelar registro', message: 'Vamos a borrar los datos de esta cuenta.', type: 'danger', confirmText: 'Cancelar registro', cancelText: 'Volver' }).then((accepted) => { if (accepted) void run(async () => { await api.cancelGuardianRegistration(); leave(); }); })} className="font-sans font-bold text-primary">Cancelar registro</button>
        </section>
      ) : null}
      {!loading && status?.status === 'Authorized' ? (
        <section className="flex flex-col gap-3 text-center">
          <p className="font-sans text-body text-app-text">Tu representante autorizó la cuenta. Entra de nuevo con tu usuario y contraseña.</p>
          <button type="button" onClick={leave} className="rounded-[8px] bg-primary px-4 py-3 font-sans font-bold text-app-on">Entrar</button>
        </section>
      ) : null}
      {!loading && status?.status === 'Declined' ? (
        <section className="flex flex-col gap-3 text-center">
          <p className="font-sans text-body text-app-text">Tu representante no autorizó la cuenta.</p>
          <button type="button" onClick={leave} className="rounded-[8px] bg-primary px-4 py-3 font-sans font-bold text-app-on">Cerrar sesión</button>
        </section>
      ) : null}
      <button type="button" onClick={() => void load()} className="font-sans font-bold text-primary">Actualizar</button>
    </main>
  );
}
