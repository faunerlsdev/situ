export type ToastKind = 'success' | 'error' | 'warning' | 'info' | 'brand';

export type ToastPlacement = 'top' | 'bottom';

export type ToastAction = {
  label: string;
  onPress: () => void;
};

export type ToastInput = {
  kind: ToastKind;
  message: string;
  action?: ToastAction;
  placement?: ToastPlacement;
};

export type ToastRecord = ToastInput & {
  id: string;
};

type QueueOptions = {
  now?: () => number;
  schedule?: (callback: () => void, ms: number) => ReturnType<typeof setTimeout>;
  cancel?: (id: ReturnType<typeof setTimeout>) => void;
  createId?: () => string;
};

const dedupMs = 2_000;

export function toastDuration(kind: ToastKind, hasAction: boolean): number {
  if (hasAction) {
    return 6_000;
  }

  if (kind === 'warning' || kind === 'error') {
    return 5_000;
  }

  return 5_000;
}

export function createToastQueue(options: QueueOptions = {}) {
  const now = options.now ?? (() => Date.now());
  const schedule = options.schedule ?? ((callback, ms) => setTimeout(callback, ms));
  const cancel = options.cancel ?? ((id) => clearTimeout(id));
  const createId = options.createId ?? (() => Math.random().toString(36).slice(2));
  const waiting: ToastRecord[] = [];
  const recent = new Map<string, number>();
  const listeners = new Set<() => void>();
  let current: ToastRecord | null = null;
  let timer: ReturnType<typeof setTimeout> | null = null;
  let remaining = 0;
  let startedAt = 0;

  function emit() {
    for (const listener of listeners) {
      listener();
    }
  }

  function clearTimer() {
    if (timer !== null) {
      cancel(timer);
      timer = null;
    }
  }

  function arm(ms: number) {
    clearTimer();
    remaining = ms;
    startedAt = now();
    timer = schedule(() => {
      timer = null;
      dismiss();
    }, ms);
  }

  function showNext() {
    if (current || waiting.length === 0) {
      return;
    }

    current = waiting.shift() ?? null;
    if (!current) {
      return;
    }

    arm(toastDuration(current.kind, current.action !== undefined));
    emit();
  }

  function dismiss(id?: string) {
    if (id && current?.id !== id) {
      const index = waiting.findIndex((item) => item.id === id);
      if (index >= 0) {
        waiting.splice(index, 1);
        emit();
      }
      return;
    }

    clearTimer();
    current = null;
    emit();
    showNext();
  }

  return {
    subscribe(listener: () => void) {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    current(): ToastRecord | null {
      return current;
    },
    pending(): number {
      return waiting.length;
    },
    enqueue(input: ToastInput): string | null {
      const key = `${input.kind}:${input.placement ?? 'top'}:${input.message}`;
      const at = now();
      const last = recent.get(key);
      if (last !== undefined && at - last < dedupMs) {
        return null;
      }

      recent.set(key, at);
      const record: ToastRecord = { ...input, id: createId() };
      waiting.push(record);
      showNext();
      return record.id;
    },
    dismiss,
    pause() {
      if (!current || timer === null) {
        return;
      }

      remaining = Math.max(0, remaining - (now() - startedAt));
      clearTimer();
    },
    resume() {
      if (!current || timer !== null) {
        return;
      }

      arm(remaining);
    },
  };
}

export type ToastQueue = ReturnType<typeof createToastQueue>;
