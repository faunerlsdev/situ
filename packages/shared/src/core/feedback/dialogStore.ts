export type ConfirmType = 'info' | 'warning' | 'danger';

export type ConfirmRequest = {
  title: string;
  message: string;
  type: ConfirmType;
  confirmText?: string;
  cancelText?: string;
  required?: boolean;
};

export type AlertRequest = {
  title: string;
  message: string;
  type: ConfirmType;
  buttonText?: string;
  required?: boolean;
};

type Dialog =
  | { kind: 'confirm'; request: ConfirmRequest; resolve: (value: boolean) => void }
  | { kind: 'alert'; request: AlertRequest; resolve: () => void };

export function createDialogStore() {
  const waiting: Dialog[] = [];
  const listeners = new Set<() => void>();
  let current: Dialog | null = null;

  function emit() {
    for (const listener of listeners) {
      listener();
    }
  }

  function showNext() {
    if (current || waiting.length === 0) {
      return;
    }

    current = waiting.shift() ?? null;
    emit();
  }

  return {
    subscribe(listener: () => void) {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    current(): Dialog | null {
      return current;
    },
    confirm(request: ConfirmRequest): Promise<boolean> {
      return new Promise((resolve) => {
        waiting.push({ kind: 'confirm', request, resolve });
        showNext();
      });
    },
    alert(request: AlertRequest): Promise<void> {
      return new Promise((resolve) => {
        waiting.push({ kind: 'alert', request, resolve });
        showNext();
      });
    },
    answer(value: boolean) {
      if (!current) {
        return;
      }

      if (current.kind === 'confirm') {
        current.resolve(value);
      } else if (value) {
        current.resolve();
      } else if (!current.request.required) {
        current.resolve();
      } else {
        return;
      }

      current = null;
      emit();
      showNext();
    },
  };
}

export type DialogStore = ReturnType<typeof createDialogStore>;
