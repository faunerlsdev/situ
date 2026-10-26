import type { Metadata } from 'next';
import type { ReactNode } from 'react';
import { Montserrat, Source_Sans_3 } from 'next/font/google';
import { Providers } from '@/components/providers';
import './globals.css';

const sourceSans = Source_Sans_3({ subsets: ['latin'], weight: ['400', '600', '700'], variable: '--font-source-sans', display: 'swap' });
const montserrat = Montserrat({ subsets: ['latin'], weight: ['700', '800'], variable: '--font-montserrat', display: 'swap' });

export const metadata: Metadata = {
  title: { default: 'Panel SITU', template: '%s · Panel SITU' },
  description: 'Panel de administración de SITU.',
  robots: { index: false, follow: false },
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="es-CO" className={`${sourceSans.variable} ${montserrat.variable}`}>
      <body>
        <a href="#contenido" className="absolute left-4 top-4 -translate-y-24 rounded bg-app-elevated px-3 py-2 focus:translate-y-0">
          Saltar al contenido
        </a>
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
