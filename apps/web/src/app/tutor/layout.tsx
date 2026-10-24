import type { ReactNode } from 'react';

export default function TutorLayout({ children }: { children: ReactNode }) {
  return <div className="min-h-dvh bg-[#F7F5F2] text-[#1C1C1C]">{children}</div>;
}
