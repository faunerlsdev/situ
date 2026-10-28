import { scrubLiveDetail } from '@/features/live/logic';

const hidden = /key|token|password|url|authorization/i;

/** Dev-only. Drops secrets and any URL before it reaches the console. */
export function liveDebug(step: string, detail?: Record<string, string | number | boolean | null>): void {
  if (!__DEV__) {
    return;
  }
  const safe: Record<string, string | number | boolean | null> = {};
  for (const [name, value] of Object.entries(detail ?? {})) {
    if (hidden.test(name)) {
      continue;
    }
    safe[name] = typeof value === 'string' ? scrubLiveDetail(value) : value;
  }
  console.debug('[situ-live]', step, safe);
}
