import type { Metadata } from 'next';
import type { ReactNode } from 'react';
import { Montserrat, Source_Sans_3 } from 'next/font/google';
import { SiteFooter } from '@/components/site-footer';
import { SiteHeader } from '@/components/site-header';
import { SupportProviders } from '@/components/app-providers';
import './globals.css';

const sourceSans = Source_Sans_3({
  subsets: ['latin'],
  weight: ['400', '600', '700'],
  style: ['normal', 'italic'],
  variable: '--font-source-sans',
  display: 'swap',
});
const montserrat = Montserrat({
  subsets: ['latin'],
  weight: ['700', '800'],
  style: ['normal', 'italic'],
  variable: '--font-montserrat',
  display: 'swap',
});

export const metadata: Metadata = {
  title: { default: 'Ayuda SITU', template: '%s · Ayuda SITU' },
  description: 'Centro de ayuda de SITU: cuenta, mapa, privacidad y planes.',
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="es-CO" data-scroll-behavior="smooth" className={`${sourceSans.variable} ${montserrat.variable}`}>
      <body className="flex min-h-screen flex-col">
        <a
          href="#contenido"
          className="absolute left-4 top-4 z-50 -translate-y-24 rounded-full bg-white px-3 py-2 font-semibold focus:translate-y-0"
        >
          Saltar al contenido
        </a>
        <div className="brand-divider" aria-hidden="true" />
        <SiteHeader />
        <main id="contenido" className="flex-1">
          <SupportProviders>{children}</SupportProviders>
        </main>
        <SiteFooter />
      </body>
    </html>
  );
}
