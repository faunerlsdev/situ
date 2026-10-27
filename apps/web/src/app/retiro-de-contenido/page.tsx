'use client';

import { notify } from '@situ/shared';
import { useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'next/navigation';
import { Suspense, useEffect, useRef, useState, type FormEvent } from 'react';
import { api } from '@/lib/api';

type CaptchaConfig = { captcha?: { provider?: string; siteKey?: string } };

export default function TakedownPage() {
  return (
    <Suspense fallback={<main className="mx-auto max-w-xl px-5 py-10">Cargando el formulario…</main>}>
      <TakedownForm />
    </Suspense>
  );
}

function TakedownForm() {
  const params = useSearchParams();
  const config = useQuery({ queryKey: ['app-config'], queryFn: async () => (await api.appConfig()).data as CaptchaConfig });
  const siteKey = config.data?.captcha?.siteKey ?? '';
  const development = config.data?.captcha?.provider === 'Development' || (!siteKey && process.env.NODE_ENV !== 'production');
  const [target, setTarget] = useState(params.get('reporte') ?? '');
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [reason, setReason] = useState('Aparezco en el contenido');
  const [description, setDescription] = useState('');
  const [token, setToken] = useState(development ? 'dev-ok' : '');
  const [tracking, setTracking] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const widget = useRef<HTMLDivElement>(null);

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

  async function submit(event: FormEvent | { preventDefault(): void }) {
    event.preventDefault();
    const reportId = target.match(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i)?.[0];
    if (!reportId) {
      notify.warning('Pega el enlace del reporte o su identificador.');
      return;
    }
    if (!token) {
      notify.warning('Confirma que eres una persona.');
      return;
    }
    setPending(true);
    try {
      const result = await api.submitTakedown({
        requesterName: name.trim(),
        requesterEmail: email.trim(),
        targetType: 'Report',
        targetId: reportId,
        description: `${reason}. ${description}`.trim(),
        captchaToken: token,
      });
      setTracking(result.trackingCode);
    } catch (cause) {
      notify.error(cause);
    } finally {
      setPending(false);
    }
  }

  if (tracking) {
    return (
      <main className="mx-auto grid max-w-xl gap-3 px-5 py-10">
        <h1 className="font-display text-3xl">Recibimos tu solicitud</h1>
        <p>El número de seguimiento es {tracking}. Te escribimos al correo y respondemos en un plazo de 15 días. La respuesta no indica si el contenido existe.</p>
      </main>
    );
  }

  return (
    <main className="mx-auto grid max-w-xl gap-4 px-5 py-10">
      <h1 className="font-display text-3xl">Retiro de contenido</h1>
      <p>Si apareces en un chisme y quieres pedir que lo revisemos, deja tus datos. Respondemos en un plazo de 15 días.</p>
      <form className="grid gap-3" method="post" onSubmit={(event) => { event.preventDefault(); void submit(event); }}>
        <label className="grid gap-1 text-sm">Nombre
          <input required value={name} onChange={(event) => setName(event.target.value)} className="min-h-11 rounded border border-app-border bg-app-input px-3" />
        </label>
        <label className="grid gap-1 text-sm">Correo
          <input required type="email" value={email} onChange={(event) => setEmail(event.target.value)} className="min-h-11 rounded border border-app-border bg-app-input px-3" />
        </label>
        <label className="grid gap-1 text-sm">Enlace o identificador del reporte
          <input required value={target} onChange={(event) => setTarget(event.target.value)} className="min-h-11 rounded border border-app-border bg-app-input px-3" />
        </label>
        <label className="grid gap-1 text-sm">Motivo
          <input required value={reason} onChange={(event) => setReason(event.target.value)} className="min-h-11 rounded border border-app-border bg-app-input px-3" />
        </label>
        <label className="grid gap-1 text-sm">Descripción
          <textarea required value={description} onChange={(event) => setDescription(event.target.value)} className="min-h-24 rounded border border-app-border bg-app-input p-3" />
        </label>
        {siteKey ? <div ref={widget} /> : null}
        <button type="button" disabled={pending || (!development && !token)} onClick={(event) => { event.preventDefault(); void submit(event); }} className="min-h-11 rounded bg-primary font-bold text-app-on disabled:opacity-60">
          {pending ? 'Enviando…' : 'Enviar solicitud'}
        </button>
      </form>
    </main>
  );
}
