'use client';

import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { notify, presentError } from '@situ/shared';
import { api } from '@/lib/api';
import { FormStatus, controlClassName, readField } from './form-status';

const reportIdPattern = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i;
const responseDays = 15;

type CaptchaConfig = { captcha?: { provider?: string; siteKey?: string } };

export function TakedownForm() {
  const [siteKey, setSiteKey] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [tracking, setTracking] = useState<string | null>(null);
  const [token, setToken] = useState(process.env.NODE_ENV === 'production' ? '' : 'dev-ok');
  const widget = useRef<HTMLDivElement>(null);

  useEffect(() => {
    let cancelled = false;
    api.appConfig().then((response) => {
      if (cancelled) {
        return;
      }
      const config = response.data as CaptchaConfig | undefined;
      const key = config?.captcha?.siteKey ?? '';
      const development = config?.captcha?.provider === 'Development' || (!key && process.env.NODE_ENV !== 'production');
      setSiteKey(key);
      if (development) {
        setToken('dev-ok');
      }
    }).catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!siteKey || !widget.current) {
      return;
    }
    const script = document.createElement('script');
    script.src = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';
    script.async = true;
    script.onload = () => {
      const turnstile = (window as unknown as { turnstile?: { render: (node: HTMLElement, options: { sitekey: string; callback: (value: string) => void }) => void } }).turnstile;
      if (widget.current) {
        turnstile?.render(widget.current, { sitekey: siteKey, callback: setToken });
      }
    };
    document.body.appendChild(script);
  }, [siteKey]);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const content = readField(form, 'content');
    const reason = readField(form, 'reason');
    const reportId = content.match(reportIdPattern)?.[0];
    setError(null);
    if (!reportId) {
      setError('Pega el enlace del contenido o su identificador, y puedes añadir la descripción en el mismo campo.');
      return;
    }
    if (!token) {
      setError('Confirma que eres una persona.');
      return;
    }
    setPending(true);
    try {
      const result = await api.submitTakedown({
        requesterName: readField(form, 'name'),
        requesterEmail: readField(form, 'email'),
        targetType: 'Report',
        targetId: reportId,
        description: `${reason}. ${content}`.trim(),
        captchaToken: token,
      });
      setTracking(result.trackingCode);
      notify.success('Recibimos la solicitud.');
    } catch (cause) {
      presentError(cause, setError);
    } finally {
      setPending(false);
    }
  }

  if (tracking) {
    return (
      <div role="status" className="rounded-3xl border border-gray200 bg-white p-5">
        <h2 className="font-display text-3xl">Recibimos tu solicitud</h2>
        <p className="mt-3 leading-7 text-gray600">El número de seguimiento es {tracking}.</p>
        <p className="mt-2 leading-7 text-gray600">Respondemos en un plazo de {responseDays} días. La respuesta no indica si el contenido existe.</p>
      </div>
    );
  }

  return (
    <form onSubmit={onSubmit} className="space-y-5">
      <FormStatus error={error} notice={null} />
      <p className="leading-7 text-gray600">Respondemos en un plazo de {responseDays} días.</p>
      <label className="block text-sm font-semibold">
        Nombre
        <input className={controlClassName} name="name" autoComplete="name" required />
      </label>
      <label className="block text-sm font-semibold">
        Correo
        <input className={controlClassName} name="email" type="email" autoComplete="email" required spellCheck={false} />
      </label>
      <label className="block text-sm font-semibold">
        Enlace o descripción del contenido
        <textarea className={`${controlClassName} min-h-28`} name="content" required />
        <span className="mt-1 block font-normal text-gray600">Pega el enlace de la publicación o describe el contenido. Si tienes el identificador, inclúyelo.</span>
      </label>
      <label className="block text-sm font-semibold">
        Motivo
        <textarea className={`${controlClassName} min-h-24`} name="reason" required />
      </label>
      {siteKey ? <div ref={widget} /> : null}
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
