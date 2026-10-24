'use client';

import { bindNotify, createDialogStore, createToastQueue, feedbackMessages } from '@situ/shared';
import type { ToastKind, ToastRecord } from '@situ/shared';
import { useEffect, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';

const accent: Record<ToastKind, string> = {
  success: 'border-l-[#1FA35B]',
  error: 'border-l-live',
  warning: 'border-l-primary-light',
  info: 'border-l-edge bg-edge text-white',
  brand: 'border-l-primary',
};

export function SupportProviders({ children }: { children: ReactNode }) {
  const queue = useMemo(() => createToastQueue(), []);
  const dialogs = useMemo(() => createDialogStore(), []);
  const [toast, setToast] = useState<ToastRecord | null>(null);
  const [dialog, setDialog] = useState(dialogs.current());
  const [online, setOnline] = useState(true);
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

  useEffect(() => {
    const apply = () => {
      const next = navigator.onLine;
      setOnline(next);
      if (next && wasOffline.current) {
        queue.enqueue({ kind: 'success', message: feedbackMessages.online });
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
  }, [queue]);

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
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [dialog, dialogs]);

  return (
    <>
      {children}
      {!online ? (
        <p className="fixed top-14 right-0 left-0 z-[70] bg-edge px-4 py-2 text-center font-sans text-small font-bold text-white">
          {feedbackMessages.offline}
        </p>
      ) : null}
      {toast ? (
        <div className={`situ-toast fixed right-4 left-4 z-[80] lg:left-auto lg:w-[360px] ${toast.placement === 'bottom' ? 'bottom-6' : 'top-20'}`}>
          <article className={`flex gap-3 rounded-[12px] border-l-4 bg-white p-3 text-ink shadow-[0_4px_12px_rgba(0,0,0,.15)] ${accent[toast.kind]}`}>
            <p className="line-clamp-2 flex-1 font-sans text-small">{toast.message}</p>
          </article>
        </div>
      ) : null}
      {dialog ? (
        <div className="fixed inset-0 z-[90] grid place-items-center bg-black/55 p-6">
          <div ref={dialogRef} role="alertdialog" aria-modal="true" className="w-full max-w-[340px] rounded-[20px] bg-white p-6 text-center">
            <p className="font-display text-[20px] font-bold text-ink">{dialog.request.title}</p>
            <p className="mt-2 font-sans text-[15px] text-ink">{dialog.request.message}</p>
            <button type="button" className="mt-5 h-12 w-full rounded-[4px] bg-primary font-sans text-body font-bold text-white" onClick={() => dialogs.answer(true)}>
              {dialog.kind === 'confirm' ? (dialog.request.confirmText ?? feedbackMessages.understood) : (dialog.request.buttonText ?? feedbackMessages.understood)}
            </button>
            {dialog.kind === 'confirm' ? (
              <button type="button" className="mt-2 h-12 w-full rounded-[4px] border border-ink font-sans text-body font-bold text-ink" onClick={() => dialogs.answer(false)}>
                {dialog.request.cancelText ?? feedbackMessages.cancel}
              </button>
            ) : null}
          </div>
        </div>
      ) : null}
    </>
  );
}
