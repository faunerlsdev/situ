import { useSyncExternalStore } from 'react';

export type MapPlace = {
  latitude: number;
  longitude: number;
  label: string;
};

let place: MapPlace | null = null;
const listeners = new Set<() => void>();

export function setMapPlace(next: MapPlace | null) {
  place = next;
  for (const listener of listeners) {
    listener();
  }
}

export function useMapPlace(): MapPlace | null {
  return useSyncExternalStore(
    (listener) => {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    () => place,
  );
}
