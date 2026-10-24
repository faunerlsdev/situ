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
      {hint ? <span className="mt-1 block text-sm text-app-muted">{hint}</span> : null}
    </label>
  );
}

export const controlClassName =
  'mt-2 w-full rounded-[8px] border border-app-border bg-app-input px-3 py-3 text-app-text focus:border-[1.5px] focus:border-primary';
