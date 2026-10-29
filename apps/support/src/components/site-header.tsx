'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { sites } from '@situ/shared';
import { BrandMark } from './brand-mark';

const links = [
  { href: '/', label: 'Inicio' },
  { href: '/contacto', label: 'Contacto' },
  { href: '/retiro-contenido', label: 'Retiro de contenido' },
] as const;

export function SiteHeader() {
  const pathname = usePathname();

  return (
    <header className="border-b border-gray200 bg-white">
      <div className="mx-auto flex w-full max-w-6xl flex-wrap items-center justify-between gap-4 px-5 py-4">
        <Link href="/" className="rounded-full">
          <BrandMark />
        </Link>
        <nav aria-label="Ayuda">
          <ul className="flex flex-wrap items-center gap-2">
            {links.map((link) => {
              const current = pathname === link.href;
              return (
                <li key={link.href}>
                  <Link
                    href={link.href}
                    aria-current={current ? 'page' : undefined}
                    className={`inline-flex min-h-11 items-center rounded-full px-3 text-sm font-semibold ${
                      current ? 'bg-ink text-white' : 'text-ink'
                    }`}
                  >
                    {link.label}
                  </Link>
                </li>
              );
            })}
            <li>
              <a
                href={sites.web}
                className="inline-flex min-h-11 items-center rounded-full border border-ink px-3 text-sm font-semibold"
              >
                Ir a SITU
              </a>
            </li>
          </ul>
        </nav>
      </div>
    </header>
  );
}
