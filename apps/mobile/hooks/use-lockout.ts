import { lockoutRemainingLabel } from '@situ/shared';
import { useEffect, useState } from 'react';

export function useLockoutLabel(retryAfter: string | null): string | null {
  const [label, setLabel] = useState<string | null>(null);

  useEffect(() => {
    if (!retryAfter) {
      setLabel(null);
      return;
    }

    const tick = () => setLabel(lockoutRemainingLabel(retryAfter));
    tick();
    const id = setInterval(tick, 1000);
    return () => clearInterval(id);
  }, [retryAfter]);

  return label;
}
