'use client';

import type { AdminMe } from '@/lib/api-contract';
import { panelFetch } from '@/lib/client';
import { visibleSections } from '@/lib/navigation';
import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { useEffect, useState, type ReactNode } from 'react';

export type { AdminMe };

export function Shell({ env, me, children }: { env: string; me: AdminMe; children: ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const [idle, setIdle] = useState(false);
  const [openPath, setOpenPath] = useState<string | null>(null);
  const menuOpen = openPath === pathname;
  const items = visibleSections(me.permissions);

  useEffect(() => {
    let active = true;
    const mark = () => {
      active = true;
    };
    window.addEventListener('pointerdown', mark);
    window.addEventListener('keydown', mark);
    const pulse = window.setInterval(() => {
      if (!active) {
        return;
      }
      active = false;
      void panelFetch('/api/auth/pulse', { method: 'POST' }).catch(() => setIdle(true));
    }, 60_000);
    const refresh = window.setInterval(() => {
      void panelFetch('/api/auth/refresh', { method: 'POST' }).catch(() => setIdle(true));
    }, 4 * 60 * 1000);
    return () => {
      window.clearInterval(pulse);
      window.clearInterval(refresh);
      window.removeEventListener('pointerdown', mark);
      window.removeEventListener('keydown', mark);
    };
  }, []);

  async function logout() {
    await panelFetch('/api/auth/logout', { method: 'POST' });
    router.push('/login');
  }

  return (
    <div className="min-h-screen lg:grid lg:grid-cols-[240px_minmax(0,1fr)] lg:grid-rows-[auto_1fr]">
      <header className="flex flex-wrap items-center justify-between gap-3 border-b border-app-border px-4 py-3 lg:col-start-2 lg:px-6">
        <div className="flex min-w-0 items-center gap-3">
          <button
            type="button"
            className="rounded border border-app-border px-3 py-1 lg:hidden"
            aria-expanded={menuOpen}
            aria-controls="menu-secciones"
            onClick={() => setOpenPath(menuOpen ? null : pathname)}
          >
            {menuOpen ? 'Cerrar' : 'Menú'}
          </button>
          <p className="min-w-0">
            <span className="font-semibold">{me.userName}</span>
            <span className="block truncate text-sm text-app-muted sm:ml-2 sm:inline">{me.roles.map((role) => role.name).join(', ')}</span>
          </p>
        </div>
        <div className="flex items-center gap-2 sm:gap-4">
          <span className="rounded border border-app-border px-2 py-1 text-xs uppercase tracking-wide text-app-muted">{env}</span>
          <button type="button" onClick={() => void logout()} className="rounded border border-app-border px-3 py-1">Cerrar sesión</button>
        </div>
      </header>
      <aside id="menu-secciones" className={`${menuOpen ? 'block' : 'hidden'} border-b border-app-border bg-app-surface p-4 lg:col-start-1 lg:row-span-2 lg:row-start-1 lg:block lg:border-r lg:border-b-0`}>
        <p className="font-display text-2xl text-app-primary">SITU</p>
        <p className="mb-6 text-xs tracking-wide text-app-muted">Panel</p>
        <nav aria-label="Secciones" className="grid gap-1">
          {items.map((item) => (
            <Link key={item.href} href={item.href} aria-current={pathname.startsWith(item.href) ? 'page' : undefined} className={`rounded px-3 py-2 ${pathname.startsWith(item.href) ? 'bg-app-elevated font-semibold' : ''}`}>
              {item.label}
            </Link>
          ))}
        </nav>
      </aside>
      <main id="contenido" className="min-w-0 p-4 lg:col-start-2 lg:p-6">{children}</main>
      {idle ? (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4" role="presentation">
          <div role="alertdialog" aria-modal="true" aria-labelledby="idle-title" className="w-full max-w-md rounded-xl bg-app-elevated p-6">
            <h2 id="idle-title" className="font-display text-xl">Sesión cerrada</h2>
            <p className="mt-2">Pasaron 30 minutos sin actividad. Vuelve a ingresar.</p>
            <button type="button" className="mt-4 rounded bg-app-primary px-4 py-2 font-semibold text-app-on" onClick={() => router.push('/login')}>Ir al ingreso</button>
          </div>
        </div>
      ) : null}
    </div>
  );
}
