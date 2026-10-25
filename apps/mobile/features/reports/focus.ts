import { useSyncExternalStore } from 'react';

export type MapFocus = {
  id: string;
  latitude: number;
  longitude: number;
};

let focus: MapFocus | null = null;
const listeners = new Set<() => void>();

export function setMapFocus(next: MapFocus | null) {
  focus = next;
  for (const listener of listeners) {
    listener();
  }
}

export function useMapFocus(): MapFocus | null {
  return useSyncExternalStore(
    (listener) => {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    () => focus,
  );
}
