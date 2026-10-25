import { useSyncExternalStore } from 'react';

export type MapFilters = {
  categoryCodes: string[];
  since: 'all' | 'hour' | 'today';
  friendsOnly: boolean;
};

const initial: MapFilters = { categoryCodes: [], since: 'all', friendsOnly: false };
let filters = initial;
const listeners = new Set<() => void>();

function emit() {
  for (const listener of listeners) {
    listener();
  }
}

export function setMapFilters(next: MapFilters) {
  filters = next;
  emit();
}

export function useMapFilters(): MapFilters {
  return useSyncExternalStore(
    (listener) => {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    () => filters,
  );
}
