import { sites } from '@situ/shared';
import { BrandMark } from './brand-mark';

const links = [
  { href: '#como-funciona', label: 'Cómo funciona' },
  { href: '#seguridad', label: 'Seguridad' },
  { href: '#planes', label: 'Planes' },
  { href: '#descarga', label: 'Descarga' },
] as const;

export function SiteHeader() {
  return (
    <header className="border-b border-gray200 bg-white">
      <div className="mx-auto flex w-full max-w-6xl flex-wrap items-center justify-between gap-4 px-5 py-4">
        <a href="#inicio" className="rounded-full">
          <BrandMark />
        </a>
        <nav aria-label="Secciones">
          <ul className="flex flex-wrap items-center gap-2">
            {links.map((link) => (
              <li key={link.href}>
                <a href={link.href} className="inline-flex min-h-11 items-center rounded-full px-3 text-sm font-semibold">
                  {link.label}
                </a>
              </li>
            ))}
            <li>
              <a
                href={`${sites.web}/login`}
                className="inline-flex min-h-11 items-center rounded-full bg-ink px-4 text-sm font-semibold text-white"
              >
                Entrar
              </a>
            </li>
          </ul>
        </nav>
      </div>
    </header>
  );
}
