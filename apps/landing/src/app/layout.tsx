import type { Metadata } from 'next';
import type { ReactNode } from 'react';
import { Montserrat, Source_Sans_3 } from 'next/font/google';
import { SiteFooter } from '@/components/site-footer';
import { SiteHeader } from '@/components/site-header';
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
  title: { default: 'SITU', template: '%s · SITU' },
  description: 'La red para ver qué está pasando en tu ciudad: reportes en el mapa, amigos y avisos a un kilómetro.',
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="es-CO" data-scroll-behavior="smooth" className={`${sourceSans.variable} ${montserrat.variable}`}>
      <body>
        <a
          href="#contenido"
          className="absolute left-4 top-4 z-50 -translate-y-24 rounded-full bg-white px-3 py-2 font-semibold text-ink focus:translate-y-0"
        >
          Saltar al contenido
        </a>
        <div className="brand-divider" aria-hidden="true" />
        <SiteHeader />
        <main id="contenido">{children}</main>
        <SiteFooter />
      </body>
    </html>
  );
}
