'use client';

import { useReducedMotion } from './use-feedback';

const sizeClass = {
  sm: 'size-6',
  md: 'size-12',
  lg: 'size-20',
} as const;

export function SituLoader({ size = 'md', label = 'Cargando' }: { size?: 'sm' | 'md' | 'lg'; label?: string }) {
  const reduced = useReducedMotion();

  return (
    <img
      src="/brand/situ-isotipo-naranja-claro.png"
      alt=""
      role="status"
      aria-label={label}
      className={`${sizeClass[size]} object-contain ${reduced ? '' : 'situ-loader'}`}
    />
  );
}
