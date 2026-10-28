/** Last answer from POST /live when the server reports the concurrent cap. */

let quotaFull = false;
const listeners = new Set<() => void>();

export function getLiveQuotaFull(): boolean {
  return quotaFull;
}

export function setLiveQuotaFull(value: boolean): void {
  if (quotaFull === value) {
    return;
  }
  quotaFull = value;
  listeners.forEach((listener) => listener());
}

export function subscribeLiveQuota(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}