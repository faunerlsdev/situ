'use client';

import { useEffect, useState } from 'react';

export function useDelayedFlag(active: boolean, delayMs = 300): boolean {
  const [shown, setShown] = useState(false);

  useEffect(() => {
    if (!active) {
      return;
    }

    const id = window.setTimeout(() => setShown(true), delayMs);
    return () => {
      window.clearTimeout(id);
      setShown(false);
    };
  }, [active, delayMs]);

  return active && shown;
}

export function useReducedMotion(): boolean {
  const [reduced, setReduced] = useState(false);

  useEffect(() => {
    const media = window.matchMedia('(prefers-reduced-motion: reduce)');
    const apply = () => setReduced(media.matches);
    apply();
    media.addEventListener('change', apply);
    return () => media.removeEventListener('change', apply);
  }, []);

  return reduced;
}
