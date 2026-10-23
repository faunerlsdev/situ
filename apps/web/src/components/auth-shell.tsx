import type { ReactNode } from 'react';

export function AuthShell({
  title,
  lede,
  children,
}: {
  title: string;
  lede: string;
  children: ReactNode;
}) {
  return (
    <section className="mx-auto w-full max-w-md px-5 py-16">
      <h1 className="font-display text-4xl">{title}</h1>
      <p className="mt-3 text-gray600">{lede}</p>
      <div className="mt-8">{children}</div>
    </section>
  );
}
