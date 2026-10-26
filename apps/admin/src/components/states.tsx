'use client';

import { PanelError } from '@/lib/problem';
import type { ReactNode } from 'react';

export function PanelState({
  loading,
  error,
  empty,
  children,
}: {
  loading: boolean;
  error: unknown;
  empty?: boolean;
  children: ReactNode;
}) {
  if (loading) {
    return <p className="text-app-muted">Cargando…</p>;
  }

  if (error instanceof PanelError && error.status === 403) {
    return (
      <section className="rounded-xl border border-app-border bg-app-surface p-8">
        <h1 className="font-display text-2xl">Sin permiso</h1>
        <p className="mt-2 text-app-muted">No tienes permiso para ver esta sección. Si crees que es un error, pídeselo a un superadministrador.</p>
      </section>
    );
  }

  if (error) {
    return (
      <p role="alert" className="text-app-danger">
        {error instanceof Error ? error.message : 'No pudimos cargar la información.'}
      </p>
    );
  }

  if (empty) {
    return <p className="text-app-muted">No hay nada para mostrar.</p>;
  }

  return children;
}
