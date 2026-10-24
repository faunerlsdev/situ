'use client';

import {
  alertModal,
  bindNotify,
  confirm,
  feedbackMessages,
  notify,
  sessionEvents,
} from '@situ/shared';
import type { ToastKind, ToastRecord } from '@situ/shared';
import { useRouter } from 'next/navigation';
import { useEffect, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { createDialogStore } from '@situ/shared';
import { createToastQueue } from '@situ/shared';
import { SituLoader } from './situ-loader';
import { useReducedMotion } from './use-feedback';

const accent: Record<ToastKind, string> = {
  success: 'border-l-app-success',
  error: 'border-l-app-danger',
  warning: 'border-l-primary-light',
  info: 'border-l-primary',
  brand: 'border-l-primary',
};

export function FeedbackProvider({ children }: { children: ReactNode }) {
  const router = useRouter();
  const reduced = useReducedMotion();
  const queue = useMemo(() => createToastQueue(), []);
  const dialogs = useMemo(() => createDialogStore(), []);
  const [toast, setToast] = useState<ToastRecord | null>(null);
  const [dialog, setDialog] = useState(dialogs.current());
  const [online, setOnline] = useState(true);
  const [blocking, setBlocking] = useState(false);
  const wasOffline = useRef(false);
  const dialogRef = useRef<HTMLDivElement>(null);

  useEffect(() => bindNotify({
    success: (message, options) => queue.enqueue({ kind: 'success', message, action: options?.action, placement: options?.placement }),
    error: (message, options) => queue.enqueue({ kind: 'error', message, action: options?.action, placement: options?.placement }),
    warning: (message, options) => queue.enqueue({ kind: 'warning', message, action: options?.action, placement: options?.placement }),
    info: (message, options) => queue.enqueue({ kind: 'info', message, action: options?.action, placement: options?.placement }),
    brand: (message) => queue.enqueue({ kind: 'brand', message }),
    dismiss: (id) => queue.dismiss(id),
    confirm: (input) => dialogs.confirm(input),
    alertModal: (input) => dialogs.alert(input),
  }), [dialogs, queue]);

  useEffect(() => queue.subscribe(() => setToast(queue.current())), [queue]);
  useEffect(() => dialogs.subscribe(() => setDialog(dialogs.current())), [dialogs]);
  useEffect(() => sessionEvents.subscribe(() => {
    void alertModal({
      title: feedbackMessages.sessionExpiredTitle,
      message: feedbackMessages.sessionExpiredBody,
      type: 'warning',
      buttonText: feedbackMessages.understood,
      required: true,
    }).then(() => {
      sessionEvents.reset();
      router.push('/login');
    });
  }), [router]);

  useEffect(() => {
    const apply = () => {
      const next = navigator.onLine;
      setOnline(next);
      if (next && wasOffline.current) {
        notify.success(feedbackMessages.online);
      }
      wasOffline.current = !next;
    };
    window.addEventListener('online', apply);
    window.addEventListener('offline', apply);
    queueMicrotask(apply);
    return () => {
      window.removeEventListener('online', apply);
      window.removeEventListener('offline', apply);
    };
  }, []);

  useEffect(() => {
    if (!dialog) {
      return;
    }

    const node = dialogRef.current;
    const focusable = node?.querySelectorAll<HTMLElement>('button');
    focusable?.[0]?.focus();
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !dialog.request.required) {
        dialogs.answer(false);
      }
      if (event.key !== 'Tab' || !focusable || focusable.length === 0) {
        return;
      }
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (!first || !last) {
        return;
      }
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [dialog, dialogs]);

  return (
    <>
      {children}
      {!online ? (
        <p className="fixed top-[calc(env(safe-area-inset-top)+3.5rem)] right-0 left-0 z-[70] border-b border-primary-light bg-app-elevated px-4 py-2 text-center font-sans text-small font-bold text-app-text">
          {feedbackMessages.offline}
        </p>
      ) : null}
      {toast ? (
        <div
          className={`fixed right-4 left-4 z-[80] lg:left-auto lg:w-[360px] ${toast.placement === 'bottom' ? 'bottom-[calc(env(safe-area-inset-bottom)+1.5rem)]' : 'top-[calc(env(safe-area-inset-top)+4.5rem)]'} ${reduced ? '' : 'situ-toast'}`}
          onPointerEnter={() => queue.pause()}
          onPointerLeave={() => queue.resume()}
          onPointerDown={(event) => {
            const start = event.clientY;
            const move = (next: PointerEvent) => {
              const distance = toast.placement === 'bottom' ? next.clientY - start : start - next.clientY;
              if (distance > 40) {
                queue.dismiss(toast.id);
              }
            };
            const up = () => {
              window.removeEventListener('pointermove', move);
              window.removeEventListener('pointerup', up);
            };
            window.addEventListener('pointermove', move);
            window.addEventListener('pointerup', up);
          }}
        >
          <article className={`flex gap-3 rounded-[12px] p-3 shadow-app ${toast.kind === 'success' ? 'bg-primary text-app-on' : toast.kind === 'error' ? 'bg-app-danger-solid text-white' : `border-l-4 bg-app-elevated text-app-text ${accent[toast.kind]}`}`}>
            {toast.kind === 'brand' ? (
              <img src="/brand/situ-isotipo-naranja-claro.png" alt="" className="size-8 object-contain" />
            ) : (
              <span aria-hidden="true" className="font-bold">{toast.kind === 'error' ? '!' : '•'}</span>
            )}
            <p className="line-clamp-2 flex-1 font-sans text-small">{toast.message}</p>
            {toast.action ? (
              <button type="button" className={`font-sans text-small font-bold ${toast.kind === 'success' ? 'text-app-on' : toast.kind === 'error' ? 'text-white' : 'text-primary'}`} onClick={toast.action.onPress}>
                {toast.action.label}
              </button>
            ) : null}
          </article>
        </div>
      ) : null}
      {dialog ? (
        <div className="fixed inset-0 z-[90] grid place-items-center bg-black/60 p-6" role="presentation">
          <div
            ref={dialogRef}
            role="alertdialog"
            aria-modal="true"
            aria-labelledby="feedback-title"
            aria-describedby="feedback-body"
            className="w-full max-w-[340px] rounded-[20px] bg-app-elevated p-6 text-center"
          >
            <p id="feedback-title" className="font-display text-[20px] font-bold text-app-text">{dialog.request.title}</p>
            <p id="feedback-body" className="mt-2 font-sans text-[15px] text-app-muted">{dialog.request.message}</p>
            <div className="mt-5 grid gap-2">
              <button
                type="button"
                className={`h-12 rounded-[8px] font-sans text-body font-bold ${dialog.kind === 'confirm' && dialog.request.type === 'danger' ? 'bg-app-danger-solid text-white' : 'bg-primary text-app-on'}`}
                onClick={() => dialogs.answer(true)}
              >
                {dialog.kind === 'confirm' ? (dialog.request.confirmText ?? feedbackMessages.understood) : (dialog.request.buttonText ?? feedbackMessages.understood)}
              </button>
              {dialog.kind === 'confirm' ? (
                <button type="button" className="h-12 rounded-[8px] border border-app-border font-sans text-body font-bold text-app-text" onClick={() => dialogs.answer(false)}>
                  {dialog.request.cancelText ?? feedbackMessages.cancel}
                </button>
              ) : null}
            </div>
          </div>
        </div>
      ) : null}
      {blocking ? (
        <div className="fixed inset-0 z-[85] grid place-items-center bg-black/50">
          <SituLoader size="lg" />
        </div>
      ) : null}
      <BlockingBridge setBlocking={setBlocking} />
    </>
  );
}

function BlockingBridge({ setBlocking }: { setBlocking: (value: boolean) => void }) {
  useEffect(() => {
    blockingListeners.add(setBlocking);
    return () => {
      blockingListeners.delete(setBlocking);
    };
  }, [setBlocking]);
  return null;
}

const blockingListeners = new Set<(value: boolean) => void>();

export function setBlockingOverlay(active: boolean) {
  for (const listener of blockingListeners) {
    listener(active);
  }
}

void confirm;
void notify;
