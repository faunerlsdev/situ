'use client';

import { lockoutRemainingLabel } from '@situ/shared';
import { useEffect, useState } from 'react';

export function useLockoutLabel(retryAfter: string | null): string | null {
  const [label, setLabel] = useState<string | null>(null);

  useEffect(() => {
    if (!retryAfter) {
      return;
    }

    const tick = () => setLabel(lockoutRemainingLabel(retryAfter));
    const id = window.setInterval(tick, 1000);
    queueMicrotask(tick);
    return () => window.clearInterval(id);
  }, [retryAfter]);

  return retryAfter ? label : null;
}
