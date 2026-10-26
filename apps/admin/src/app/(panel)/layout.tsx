import { Frame } from '@/components/frame';
import type { ReactNode } from 'react';

export default function PanelLayout({ children }: { children: ReactNode }) {
  return <Frame env={process.env.APP_ENV ?? 'Development'}>{children}</Frame>;
}
