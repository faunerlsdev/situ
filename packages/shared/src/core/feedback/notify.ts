import { AppError } from '../http/errors';
import { messageFor } from '../http/errorMessages';
import type { ToastAction, ToastPlacement } from './toastQueue';

export type NotifyOptions = {
  action?: ToastAction;
  placement?: ToastPlacement;
};

type Bridge = {
  success: (message: string, options?: NotifyOptions) => string | null;
  error: (message: string, options?: NotifyOptions) => string | null;
  warning: (message: string, options?: NotifyOptions) => string | null;
  info: (message: string, options?: NotifyOptions) => string | null;
  brand: (message: string) => string | null;
  dismiss: (id?: string) => void;
  confirm: (input: {
    title: string;
    message: string;
    type: 'info' | 'warning' | 'danger';
    confirmText?: string;
    cancelText?: string;
  }) => Promise<boolean>;
  alertModal: (input: {
    title: string;
    message: string;
    type: 'info' | 'warning' | 'danger';
    buttonText?: string;
    required?: boolean;
  }) => Promise<void>;
};

let bridge: Bridge | null = null;

export function bindNotify(next: Bridge | null) {
  bridge = next;
}

function userText(messageOrError: unknown): string | null {
  if (messageOrError instanceof AppError) {
    if (messageOrError.code === 'CANCELLED') {
      return null;
    }

    return messageOrError.userMessage;
  }

  if (typeof messageOrError === 'string') {
    return messageOrError;
  }

  return messageFor('UNKNOWN');
}

export const notify = {
  success(message: string, options?: NotifyOptions) {
    return bridge?.success(message, options) ?? null;
  },
  error(messageOrError: unknown, options?: NotifyOptions) {
    const message = userText(messageOrError);
    if (!message) {
      return null;
    }

    return bridge?.error(message, options) ?? null;
  },
  warning(message: string, options?: NotifyOptions) {
    return bridge?.warning(message, options) ?? null;
  },
  info(message: string, options?: NotifyOptions) {
    return bridge?.info(message, options) ?? null;
  },
  brand(message: string) {
    return bridge?.brand(message) ?? null;
  },
  dismiss(id?: string) {
    bridge?.dismiss(id);
  },
  promise<T>(promise: Promise<T>, messages: { loading: string; success: string; error?: string }) {
    const id = notify.info(messages.loading);
    return promise.then(
      (value) => {
        notify.dismiss(id ?? undefined);
        notify.success(messages.success);
        return value;
      },
      (cause: unknown) => {
        notify.dismiss(id ?? undefined);
        notify.error(messages.error ?? cause);
        throw cause;
      },
    );
  },
};

export function confirm(input: {
  title: string;
  message: string;
  type: 'info' | 'warning' | 'danger';
  confirmText?: string;
  cancelText?: string;
}): Promise<boolean> {
  return bridge?.confirm(input) ?? Promise.resolve(false);
}

export function presentError(error: unknown, inline: (message: string) => void, retry?: () => void) {
  if (!(error instanceof AppError) || error.code === 'CANCELLED') {
    return;
  }

  if (error.code === 'NETWORK' || error.code === 'TIMEOUT' || error.code === 'SERVER' || error.code === 'RATE_LIMIT') {
    notify.error(error, retry ? { action: { label: 'Reintentar', onPress: retry } } : undefined);
    return;
  }

  inline(error.userMessage);
}

export function alertModal(input: {
  title: string;
  message: string;
  type: 'info' | 'warning' | 'danger';
  buttonText?: string;
  required?: boolean;
}): Promise<void> {
  return bridge?.alertModal(input) ?? Promise.resolve();
}
