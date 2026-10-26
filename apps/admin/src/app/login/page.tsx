'use client';

import type { SetupStaffTwoFactorResponse } from '@/lib/api-contract';
import { panelFetch } from '@/lib/client';
import { zodResolver } from '@hookform/resolvers/zod';
import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import QRCode from 'qrcode';
import { z } from 'zod';

const credentials = z.object({
  email: z.string().email('Escribe un correo válido.'),
  password: z.string().min(1, 'Escribe la contraseña.'),
});

export default function LoginPage() {
  const router = useRouter();
  const [step, setStep] = useState<'credentials' | 'mfa' | 'enroll' | 'codes'>('credentials');
  const [message, setMessage] = useState('');
  const [secret, setSecret] = useState('');
  const [qr, setQr] = useState('');
  const [codes, setCodes] = useState<string[]>([]);
  const [saved, setSaved] = useState(false);
  const [recovery, setRecovery] = useState(false);
  const form = useForm<z.infer<typeof credentials>>({ resolver: zodResolver(credentials) });

  async function submitCredentials(values: z.infer<typeof credentials>) {
    setMessage('');
    try {
      const result = await panelFetch<{ step: string }>('/api/auth/login', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify(values),
      });
      if (result.step === 'enroll') {
        const setup = await panelFetch<SetupStaffTwoFactorResponse>('/api/auth/enroll/setup', { method: 'POST' });
        setSecret(setup.secret);
        setQr(await QRCode.toDataURL(setup.otpauthUri, { margin: 1, width: 220 }));
        setStep('enroll');
        return;
      }
      if (result.step === 'mfa') {
        setStep('mfa');
        return;
      }
      router.push('/resumen');
    } catch (cause) {
      setMessage(cause instanceof Error ? cause.message : 'No pudimos completar la solicitud.');
    }
  }

  async function submitCode(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const code = String(data.get('code') ?? '');
    setMessage('');
    try {
      if (step === 'enroll') {
        const enabled = await panelFetch<{ recoveryCodes: string[] }>('/api/auth/enroll/enable', {
          method: 'POST',
          headers: { 'content-type': 'application/json' },
          body: JSON.stringify({ code }),
        });
        setCodes(enabled.recoveryCodes);
        setStep('codes');
        return;
      }
      await panelFetch('/api/auth/mfa', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify(recovery ? { recoveryCode: code } : { code }),
      });
      router.push('/resumen');
    } catch (cause) {
      setMessage(cause instanceof Error ? cause.message : 'No pudimos completar la solicitud.');
    }
  }

  return (
    <main id="contenido" className="mx-auto flex min-h-screen w-full max-w-lg flex-col justify-center px-4 sm:px-6">
      <p className="font-display text-4xl text-app-primary">SITU</p>
      <h1 className="mt-2 font-display text-2xl">Panel de administración</h1>
      {step === 'credentials' ? (
        <form method="post" className="mt-8 grid gap-4" noValidate onSubmit={(event) => { event.preventDefault(); void form.handleSubmit(submitCredentials)(event); }}>
          <label className="grid gap-1">
            Correo
            <input type="email" autoComplete="username" className="w-full rounded border border-app-border bg-app-input px-3 py-2" {...form.register('email')} />
          </label>
          {form.formState.errors.email ? <p role="alert" className="text-app-danger">{form.formState.errors.email.message}</p> : null}
          <label className="grid gap-1">
            Contraseña
            <input type="password" autoComplete="current-password" className="w-full rounded border border-app-border bg-app-input px-3 py-2" {...form.register('password')} />
          </label>
          {form.formState.errors.password ? <p role="alert" className="text-app-danger">{form.formState.errors.password.message}</p> : null}
          <button type="submit" className="rounded bg-app-primary px-4 py-2 font-semibold text-app-on">Ingresar</button>
        </form>
      ) : null}
      {step === 'mfa' || step === 'enroll' ? (
        <form method="post" className="mt-8 grid gap-4" onSubmit={submitCode}>
          {step === 'enroll' ? (
            <>
              <p>Escanea el código con tu aplicación de autenticación y confirma con un código de 6 dígitos. El secreto se muestra una sola vez.</p>
              {qr ? <img src={qr} alt="Código QR para inscribir el autenticador" width={220} height={220} /> : null}
              <p className="break-all font-mono text-sm">{secret}</p>
            </>
          ) : (
            <p>{recovery ? 'Escribe uno de los códigos de recuperación.' : 'Escribe el código de 6 dígitos de tu autenticador.'}</p>
          )}
          <label className="grid gap-1">
            Código
            <input name="code" inputMode="text" autoComplete="one-time-code" required className="w-full rounded border border-app-border bg-app-input px-3 py-2" />
          </label>
          <button type="submit" className="rounded bg-app-primary px-4 py-2 font-semibold text-app-on">Confirmar</button>
          {step === 'mfa' ? (
            <button type="button" className="text-left text-sm text-app-muted" onClick={() => setRecovery((value) => !value)}>
              {recovery ? 'Usar el código del autenticador' : 'Usar un código de recuperación'}
            </button>
          ) : null}
        </form>
      ) : null}
      {step === 'codes' ? (
        <section className="mt-8 grid gap-4">
          <h2 className="font-display text-xl">Guarda estos códigos</h2>
          <p>Se muestran una sola vez. Cada uno sirve para entrar si pierdes el autenticador.</p>
          <ul className="grid gap-1 font-mono">
            {codes.map((code) => <li key={code}>{code}</li>)}
          </ul>
          <button type="button" className="rounded border border-app-border px-4 py-2" onClick={() => void navigator.clipboard.writeText(codes.join('\n'))}>Copiar</button>
          <label className="flex items-center gap-2">
            <input type="checkbox" checked={saved} onChange={(event) => setSaved(event.target.checked)} />
            Ya los guardé
          </label>
          <button type="button" disabled={!saved} className="rounded bg-app-primary px-4 py-2 font-semibold text-app-on disabled:opacity-40" onClick={() => router.push('/resumen')}>
            Entrar al panel
          </button>
        </section>
      ) : null}
      {message ? <p role="alert" className="mt-4 text-app-danger">{message}</p> : null}
    </main>
  );
}
