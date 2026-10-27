'use client';

import Link from 'next/link';
import { useEffect, useId, useState, type ReactNode } from 'react';

export const fieldClass = 'mt-1 w-full rounded-lg border border-app-border bg-app-input px-3 py-2 text-sm';

export function PageHeader({
  title,
  hint,
  action,
}: {
  title: string;
  hint?: ReactNode;
  action?: ReactNode;
}) {
  return (
    <div className="flex flex-wrap items-end justify-between gap-4">
      <div className="min-w-0">
        <h1 className="font-display text-3xl tracking-tight">{title}</h1>
        {hint ? <p className="mt-1 text-sm text-app-muted">{hint}</p> : null}
      </div>
      {action}
    </div>
  );
}

export function PrimaryButton({
  children,
  onClick,
  type = 'button',
}: {
  children: ReactNode;
  onClick?: () => void;
  type?: 'button' | 'submit';
}) {
  return (
    <button type={type} onClick={onClick} className="inline-flex items-center gap-2 rounded-lg bg-app-primary px-4 py-2 text-sm font-semibold text-app-on">
      {children}
    </button>
  );
}

export function SearchField({
  value,
  onChange,
  placeholder,
  label = 'Buscar',
}: {
  value: string;
  onChange: (value: string) => void;
  placeholder: string;
  label?: string;
}) {
  return (
    <label className="relative block min-w-0 flex-1">
      <span className="sr-only">{label}</span>
      <SearchIcon />
      <input
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
        className="w-full rounded-lg border border-app-border bg-app-input py-2 pr-3 pl-10 text-sm"
      />
    </label>
  );
}

export function StatusPill({ on, onLabel, offLabel }: { on: boolean; onLabel: string; offLabel: string }) {
  return (
    <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-semibold ${on ? 'bg-app-success/15 text-app-success' : 'bg-white/10 text-app-muted'}`}>
      {on ? onLabel : offLabel}
    </span>
  );
}

export function Modal({ title, onClose, children }: { title: string; onClose: () => void; children: ReactNode }) {
  const titleId = useId();

  useEffect(() => {
    function onKey(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onClose();
      }
    }

    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [onClose]);

  return (
    <div
      className="fixed inset-0 z-40 flex items-end justify-center bg-black/70 p-3 backdrop-blur-sm sm:items-center sm:p-6"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) {
          onClose();
        }
      }}
    >
      <div role="dialog" aria-modal="true" aria-labelledby={titleId} className="flex max-h-[min(92vh,44rem)] w-full max-w-2xl flex-col overflow-hidden rounded-2xl border border-app-border bg-app-elevated shadow-[0_24px_80px_rgba(0,0,0,0.45)]">
        <div className="flex items-start justify-between gap-4 border-b border-app-border px-5 py-4">
          <h2 id={titleId} className="font-display text-xl">{title}</h2>
          <IconButton label="Cerrar" onClick={onClose}>
            <CloseIcon />
          </IconButton>
        </div>
        <div className="overflow-y-auto px-5 py-4">{children}</div>
      </div>
    </div>
  );
}

export function IconButton({
  label,
  onClick,
  href,
  children,
}: {
  label: string;
  onClick?: () => void;
  href?: string;
  children: ReactNode;
}) {
  const className = 'group relative inline-flex h-9 w-9 items-center justify-center rounded-lg border border-app-border bg-app-surface text-app-text transition hover:border-app-primary hover:text-app-primary';
  const tip = (
    <span role="tooltip" className="pointer-events-none absolute top-[calc(100%+0.35rem)] left-1/2 z-20 -translate-x-1/2 rounded-md bg-black px-2 py-1 text-xs font-medium whitespace-nowrap text-white opacity-0 shadow-lg transition group-hover:opacity-100 group-focus-visible:opacity-100">
      {label}
    </span>
  );
  if (href) {
    return (
      <Link href={href} aria-label={label} title={label} className={className}>
        {children}
        {tip}
      </Link>
    );
  }

  return (
    <button type="button" aria-label={label} title={label} onClick={onClick} className={className}>
      {children}
      {tip}
    </button>
  );
}

export function DataView({
  columns,
  rows,
  emptyLabel,
}: {
  columns: string[];
  rows: { id: string; cells: ReactNode[]; actions: ReactNode }[];
  emptyLabel: string;
}) {
  const compact = useCompact();
  if (rows.length === 0) {
    return <p className="rounded-2xl border border-dashed border-app-border px-4 py-10 text-center text-sm text-app-muted">{emptyLabel}</p>;
  }

  if (compact) {
    return (
      <ul className="grid gap-3">
        {rows.map((row) => (
          <li key={row.id} className="rounded-2xl border border-app-border bg-app-surface p-4">
            <div className="font-semibold">{row.cells[0]}</div>
            <dl className="mt-3 grid grid-cols-2 gap-3 text-sm">
              {columns.slice(1).map((header, index) => (
                <div key={header}>
                  <dt className="text-xs tracking-wide text-app-muted uppercase">{header}</dt>
                  <dd className="mt-0.5">{row.cells[index + 1]}</dd>
                </div>
              ))}
            </dl>
            <div className="mt-4 flex justify-end gap-2">{row.actions}</div>
          </li>
        ))}
      </ul>
    );
  }

  return (
    <div className="overflow-hidden rounded-2xl border border-app-border bg-app-surface">
      <div className="overflow-x-auto">
        <table className="w-full min-w-[40rem] border-collapse text-left text-sm">
          <thead className="bg-app-elevated text-xs tracking-wide text-app-muted uppercase">
            <tr>
              {columns.map((column) => (
                <th key={column} className="px-4 py-3 font-semibold">{column}</th>
              ))}
              <th className="px-4 py-3 text-right font-semibold">Acciones</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.id} className="border-t border-app-border transition hover:bg-white/5">
                {row.cells.map((cell, index) => (
                  <td key={columns[index]} className="px-4 py-3 align-middle">{cell}</td>
                ))}
                <td className="px-4 py-3">
                  <div className="flex justify-end gap-2">{row.actions}</div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function useCompact() {
  const [compact, setCompact] = useState(false);

  useEffect(() => {
    if (typeof window.matchMedia !== 'function') {
      return;
    }

    const media = window.matchMedia('(max-width: 767px)');
    const apply = () => setCompact(media.matches);
    apply();
    media.addEventListener('change', apply);
    return () => media.removeEventListener('change', apply);
  }, []);

  return compact;
}

export function EyeIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Z" stroke="currentColor" strokeWidth="1.8" />
      <circle cx="12" cy="12" r="3" stroke="currentColor" strokeWidth="1.8" />
    </svg>
  );
}

export function PencilIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M4 20h4l10.5-10.5a2.1 2.1 0 0 0 0-3L16.5 4.5a2.1 2.1 0 0 0-3 0L3 15v5Z" stroke="currentColor" strokeWidth="1.8" strokeLinejoin="round" />
      <path d="M12.5 6.5l5 5" stroke="currentColor" strokeWidth="1.8" />
    </svg>
  );
}

export function PlusIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M12 5v14M5 12h14" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
    </svg>
  );
}

function CloseIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M6 6l12 12M18 6 6 18" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
    </svg>
  );
}

function SearchIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true" className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-app-muted">
      <circle cx="11" cy="11" r="6.5" stroke="currentColor" strokeWidth="1.8" />
      <path d="M16 16l4 4" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
    </svg>
  );
}
