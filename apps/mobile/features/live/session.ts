/** In-memory publish credentials. They never go in the route, logs, or device storage. */

export type HeldBroadcast = {
  id: string;
  publishUrl: string;
  publishKey: string;
  title: string;
};

let held: HeldBroadcast | null = null;

export function holdBroadcast(value: HeldBroadcast): void {
  held = value;
}

export function readBroadcast(): HeldBroadcast | null {
  return held;
}

export function clearBroadcast(): void {
  if (held) {
    held.publishKey = '';
    held.publishUrl = '';
  }
  held = null;
}
