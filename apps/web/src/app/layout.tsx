import type { Metadata, Viewport } from 'next';
import type { ReactNode } from 'react';
import { Montserrat, Source_Sans_3 } from 'next/font/google';
import { AppChrome } from '@/components/app-chrome';
import { AppProviders } from '@/components/app-providers';
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
  description: 'Mira y comenta lo que pasa en tu ciudad. La transmisión en vivo se inicia en la app.',
};

export const viewport: Viewport = {
  themeColor: '#1C1C1C',
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="es-CO" data-scroll-behavior="smooth" className={`${sourceSans.variable} ${montserrat.variable}`}>
      <body data-theme="dark" className="flex min-h-screen flex-col bg-app-bg font-sans text-app-text">
        <a
          href="#contenido"
          className="absolute left-4 top-4 z-50 -translate-y-24 rounded-[8px] bg-app-elevated px-3 py-2 font-semibold text-app-text focus:translate-y-0"
        >
          Saltar al contenido
        </a>
        <AppProviders>
          <AppChrome>{children}</AppChrome>
        </AppProviders>
      </body>
    </html>
  );
}
