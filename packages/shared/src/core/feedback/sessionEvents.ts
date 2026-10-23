type Listener = () => void;

const listeners = new Set<Listener>();
let expired = false;

export const sessionEvents = {
  subscribe(listener: Listener): () => void {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  },
  emitExpired(): void {
    if (expired) {
      return;
    }

    expired = true;
    for (const listener of listeners) {
      listener();
    }
  },
  reset(): void {
    expired = false;
  },
};
