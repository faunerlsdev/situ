import type { ReactNode } from 'react';

export function Field({
  label,
  hint,
  children,
}: {
  label: string;
  hint?: string;
  children: ReactNode;
}) {
  return (
    <label className="block">
      <span className="text-sm font-semibold">{label}</span>
      {children}
      {hint ? <span className="mt-1 block text-sm text-gray600">{hint}</span> : null}
    </label>
  );
}

export const controlClassName =
  'mt-2 w-full rounded-xl border border-gray200 bg-white px-3 py-3 text-ink';
