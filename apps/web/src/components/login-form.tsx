'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { AppError, loginSchema, notify } from '@situ/shared';
import type { LoginValues } from '@situ/shared';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { useState } from 'react';
import { safeReturnPath } from '@/features/account/deletion-copy';
import { useForm } from 'react-hook-form';
import { InlineAlert } from '@/components/feedback/inline';
import { SituLoader } from '@/components/feedback/situ-loader';
import { useDelayedFlag } from '@/components/feedback/use-feedback';
import { postBff } from '@/lib/http';
import { useLockoutLabel } from './use-lockout';

export function LoginForm() {
  const router = useRouter();
  const nextPath = safeReturnPath(useSearchParams().get('next'));
  const [retryAfter, setRetryAfter] = useState<string | null>(null);
  const [remember, setRemember] = useState(false);
  const lockout = useLockoutLabel(retryAfter);
  const locked = lockout !== null && lockout !== 'Ya puedes intentar de nuevo.';
  const form = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { login: '', password: '' },
  });

  async function onSubmit(values: LoginValues) {
    setRetryAfter(null);

    try {
      await postBff('/api/auth/login', { ...values, rememberMe: remember });
      router.push(nextPath);
    } catch (cause) {
      if (cause instanceof AppError && cause.apiCode === 'auth.guardian_consent_required') {
        router.push('/representante');
        return;
      }

      if (cause instanceof AppError && cause.apiCode === 'auth.locked' && cause.retryAfter) {
        setRetryAfter(cause.retryAfter);
      }

      const retry = cause instanceof AppError && (cause.code === 'NETWORK' || cause.code === 'TIMEOUT' || cause.code === 'SERVER' || cause.code === 'RATE_LIMIT')
        ? { action: { label: 'Reintentar', onPress: () => void onSubmit(values) } }
        : undefined;
      notify.error(cause, { placement: 'bottom', ...retry });
    }
  }

  const pending = form.formState.isSubmitting;
  const showLoader = useDelayedFlag(pending);
  const loginError = form.formState.errors.login?.message;
  const passwordError = form.formState.errors.password?.message;

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="flex min-h-dvh flex-col items-center justify-center gap-4 splash-gradient px-6 py-10" noValidate>
      <h1 className="mb-6 font-display text-[64px] leading-none font-extrabold text-primary italic">SITU</h1>
      {lockout ? (
        <p role="status" className="w-[80%] max-w-[360px] font-sans text-body font-bold text-app-text">
          {lockout}
        </p>
      ) : null}
      <div className="w-[80%] max-w-[360px]">
        <label className="flex h-[52px] items-center gap-3 rounded-[8px] border border-app-border bg-app-input focus-within:border-[1.5px] focus-within:border-primary px-3">
          <PersonIcon />
          <input
            type="text"
            placeholder="Usuario"
            autoComplete="username"
            spellCheck={false}
            aria-label="Usuario"
            aria-invalid={loginError ? true : undefined}
            aria-describedby={loginError ? 'login-error' : undefined}
            className="w-full bg-transparent font-sans text-body font-bold text-app-text italic outline-none placeholder:text-app-placeholder"
            {...form.register('login')}
          />
        </label>
        {loginError ? <FieldError id="login-error" message={loginError} /> : null}
      </div>
      <div className="w-[80%] max-w-[360px]">
        <label className="flex h-[52px] items-center gap-3 rounded-[8px] border border-app-border bg-app-input focus-within:border-[1.5px] focus-within:border-primary px-3">
          <LockIcon />
          <input
            type="password"
            placeholder="Contraseña"
            autoComplete="current-password"
            aria-label="Contraseña"
            aria-invalid={passwordError ? true : undefined}
            aria-describedby={passwordError ? 'password-error' : undefined}
            className="w-full bg-transparent font-sans text-body font-bold text-app-text italic outline-none placeholder:text-app-placeholder"
            {...form.register('password')}
          />
        </label>
        {passwordError ? <FieldError id="password-error" message={passwordError} /> : null}
      </div>
      <div className="flex w-[80%] max-w-[360px] items-center justify-between gap-3 text-app-text">
        <label className="flex items-center gap-2 font-sans text-body font-bold italic">
          <input
            type="checkbox"
            checked={remember}
            onChange={(event) => setRemember(event.target.checked)}
            className="size-[18px] accent-primary"
          />
          Recordarme en este dispositivo
        </label>
        <Link href="/recuperar" className="font-sans text-body font-bold italic">
          ¿Olvidó contraseña?
        </Link>
      </div>
      <Link href="/registro" className="w-[80%] max-w-[360px] text-right font-sans text-small font-bold text-primary italic">
        Soy nuevo y deseo registrarme
      </Link>
      <button type="submit" disabled={pending || locked} className="mt-4 flex w-[80%] max-w-[360px] flex-col items-center gap-2 disabled:opacity-60">
        {showLoader ? <SituLoader size="sm" label="Ingresando" /> : <img src="/brand/situ-isotipo-negro.png" alt="" width={72} height={94} className="h-[94px] w-[72px] object-contain" />}
        <span className="flex h-[52px] w-full items-center justify-center rounded-[8px] bg-primary font-sans text-[18px] font-bold text-app-on italic">
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
      <p role="status" className="w-[80%] max-w-[360px] font-sans text-body font-bold text-app-text">
        {notice}
      </p>
    );
  }

  return null;
}

export function FieldError({ id, message }: { id: string; message: string }) {
  return (
    <p id={id} className="mt-1 font-sans text-small font-bold text-app-danger">
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
    <svg aria-hidden="true" viewBox="0 0 24 24" className="size-5 shrink-0 fill-app-muted text-app-muted">
      <circle cx="12" cy="8" r="4" />
      <path d="M4 20c1.5-3.5 4.2-5 8-5s6.5 1.5 8 5" />
    </svg>
  );
}

function LockIcon() {
  return (
    <svg aria-hidden="true" viewBox="0 0 24 24" className="size-5 shrink-0 fill-app-muted text-app-muted">
      <rect x="5" y="10" width="14" height="10" rx="1" />
      <path d="M8 10V7a4 4 0 0 1 8 0v3" fill="none" stroke="currentColor" strokeWidth="2" />
    </svg>
  );
}
