'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { AppError, loginSchema, presentError } from '@situ/shared';
import type { LoginValues } from '@situ/shared';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { InlineAlert } from '@/components/feedback/inline';
import { SituLoader } from '@/components/feedback/situ-loader';
import { useDelayedFlag } from '@/components/feedback/use-feedback';
import { postBff } from '@/lib/http';
import { webDeviceId } from '@/lib/browser-auth';
import { useLockoutLabel } from './use-lockout';

export function LoginForm() {
  const router = useRouter();
  const [formError, setFormError] = useState<string | null>(null);
  const [retryAfter, setRetryAfter] = useState<string | null>(null);
  const [remember, setRemember] = useState(false);
  const lockout = useLockoutLabel(retryAfter);
  const locked = lockout !== null && lockout !== 'Ya puedes intentar de nuevo.';
  const form = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { login: '', password: '' },
  });

  async function onSubmit(values: LoginValues) {
    setFormError(null);
    setRetryAfter(null);

    try {
      await postBff('/api/auth/login', { ...values, deviceIdentifier: webDeviceId() });
      router.push('/mapa');
    } catch (cause) {
      if (cause instanceof AppError && cause.apiCode === 'auth.locked' && cause.retryAfter) {
        setRetryAfter(cause.retryAfter);
      }

      presentError(cause, setFormError, () => {
        void onSubmit(values);
      });
    }
  }

  const pending = form.formState.isSubmitting;
  const showLoader = useDelayedFlag(pending);
  const loginError = form.formState.errors.login?.message;
  const passwordError = form.formState.errors.password?.message;

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="flex min-h-dvh flex-col items-center justify-center gap-4 bg-primary px-6 py-10" noValidate>
      <h1 className="mb-6 font-display text-[64px] leading-none font-extrabold text-white italic">SITU</h1>
      <FormStatus error={formError} notice={null} />
      {lockout ? (
        <p role="status" className="w-[80%] max-w-[360px] rounded-[4px] bg-white px-3 py-3 font-sans text-body font-bold text-ink">
          {lockout}
        </p>
      ) : null}
      <div className="w-[80%] max-w-[360px]">
        <label className="flex h-[52px] items-center gap-3 rounded-[2px] bg-white px-3">
          <PersonIcon />
          <input
            type="text"
            placeholder="Usuario"
            autoComplete="username"
            spellCheck={false}
            aria-label="Usuario"
            aria-invalid={loginError ? true : undefined}
            aria-describedby={loginError ? 'login-error' : undefined}
            className="w-full bg-transparent font-sans text-body font-bold text-ink italic outline-none placeholder:text-gray400"
            {...form.register('login')}
          />
        </label>
        {loginError ? <FieldError id="login-error" message={loginError} /> : null}
      </div>
      <div className="w-[80%] max-w-[360px]">
        <label className="flex h-[52px] items-center gap-3 rounded-[2px] bg-white px-3">
          <LockIcon />
          <input
            type="password"
            placeholder="Contraseña"
            autoComplete="current-password"
            aria-label="Contraseña"
            aria-invalid={passwordError ? true : undefined}
            aria-describedby={passwordError ? 'password-error' : undefined}
            className="w-full bg-transparent font-sans text-body font-bold text-ink italic outline-none placeholder:text-gray400"
            {...form.register('password')}
          />
        </label>
        {passwordError ? <FieldError id="password-error" message={passwordError} /> : null}
      </div>
      <div className="flex w-[80%] max-w-[360px] items-center justify-between gap-3 text-white">
        <label className="flex items-center gap-2 font-sans text-body font-bold italic">
          <input
            type="checkbox"
            checked={remember}
            onChange={(event) => setRemember(event.target.checked)}
            className="size-[18px] appearance-none rounded-[4px] bg-gray200 checked:border-4 checked:border-ink checked:bg-white"
          />
          Recordar
        </label>
        <Link href="/recuperar" className="font-sans text-body font-bold italic">
          ¿Olvidó contraseña?
        </Link>
      </div>
      <Link href="/registro" className="w-[80%] max-w-[360px] text-right font-sans text-small font-bold text-white italic">
        Soy nuevo y deseo registrarme
      </Link>
      <button type="submit" disabled={pending || locked} className="mt-4 flex flex-col items-center active:scale-95 disabled:opacity-60">
        {showLoader ? <SituLoader size="sm" label="Ingresando" /> : <img src="/brand/situ-isotipo-negro.png" alt="" width={72} height={94} className="h-[94px] w-[72px] object-contain" />}
        <span className="text-stamp font-sans text-[28px] leading-none font-bold text-white italic">
          {pending ? 'Ingresando…' : 'Ingresar'}
        </span>
      </button>
    </form>
  );
}

export function FormStatus({ error, notice }: { error: string | null; notice: string | null }) {
  if (error) {
    return <InlineAlert message={error} />;
  }

  if (notice) {
    return (
      <p role="status" className="w-[80%] max-w-[360px] rounded-[12px] bg-white px-3 py-3 font-sans text-body font-bold text-ink">
        {notice}
      </p>
    );
  }

  return null;
}

export function FieldError({ id, message }: { id: string; message: string }) {
  return (
    <p id={id} className="mt-1 font-sans text-small font-bold text-white">
      {message}
    </p>
  );
}

export function readField(form: FormData, name: string): string {
  const value = form.get(name);
  return typeof value === 'string' ? value.trim() : '';
}

function PersonIcon() {
  return (
    <svg aria-hidden="true" viewBox="0 0 24 24" className="size-5 shrink-0 fill-ink">
      <circle cx="12" cy="8" r="4" />
      <path d="M4 20c1.5-3.5 4.2-5 8-5s6.5 1.5 8 5" />
    </svg>
  );
}

function LockIcon() {
  return (
    <svg aria-hidden="true" viewBox="0 0 24 24" className="size-5 shrink-0 fill-ink">
      <rect x="5" y="10" width="14" height="10" rx="1" />
      <path d="M8 10V7a4 4 0 0 1 8 0v3" fill="none" stroke="currentColor" strokeWidth="2" />
    </svg>
  );
}
