'use client';

import { usePathname } from 'next/navigation';
import type { ReactNode } from 'react';
import { SiteFooter } from './site-footer';
import { SiteHeader } from './site-header';

const bareRoutes = new Set([
  '/',
  '/login',
  '/registro',
  '/recuperar',
  '/confirmar-correo',
  '/restablecer',
  '/tutor/autorizar',
  '/mapa',
]);

export function AppChrome({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  if (bareRoutes.has(pathname)) {
    return (
      <main id="contenido" className="min-h-dvh">
        {children}
      </main>
    );
  }

  return (
    <>
      <div className="brand-divider" />
      <SiteHeader />
      <main id="contenido" className="flex-1">
        {children}
      </main>
      <SiteFooter />
    </>
  );
}
