'use client';

import { alertModal, confirm, feedbackMessages, notify } from '@situ/shared';
import { notFound } from 'next/navigation';
import { useState } from 'react';
import { AreaProgress, SkeletonCards, UploadProgress } from '@/components/feedback/inline';
import { SituLoader } from '@/components/feedback/situ-loader';

export default function FeedbackDemoPage() {
  if (process.env.NODE_ENV === 'production') {
    notFound();
  }

  return <Demo />;
}

function Demo() {
  const [percent, setPercent] = useState(40);
  const [blocking, setBlocking] = useState(false);

  return (
    <main className="mx-auto grid max-w-xl gap-6 px-4 py-8">
      <h1 className="font-display text-h1">Feedback</h1>
      <div className="flex flex-wrap gap-2">
        <button type="button" className="rounded-[4px] bg-primary px-3 py-2 text-white" onClick={() => notify.success('Listo')}>Éxito</button>
        <button type="button" className="rounded-[4px] bg-live px-3 py-2 text-white" onClick={() => notify.error('No pudimos publicar', { action: { label: 'Reintentar', onPress: () => notify.info('Reintentando') } })}>Error</button>
        <button type="button" className="rounded-[4px] bg-primary-light px-3 py-2 text-ink" onClick={() => notify.warning('Revisa tu conexión')}>Aviso</button>
        <button type="button" className="rounded-[4px] bg-edge px-3 py-2 text-white" onClick={() => notify.info('Guardamos el borrador')}>Info</button>
        <button type="button" className="rounded-[4px] border border-ink px-3 py-2" onClick={() => notify.brand(feedbackMessages.published)}>Chisme</button>
      </div>
      <SituLoader size="lg" />
      <SkeletonCards />
      <AreaProgress active />
      <UploadProgress percent={percent} onCancel={() => setPercent(0)} />
      <button type="button" className="h-12 rounded-[4px] bg-primary font-bold text-white" onClick={() => void alertModal({ title: feedbackMessages.sessionExpiredTitle, message: feedbackMessages.sessionExpiredBody, type: 'warning', buttonText: feedbackMessages.understood })}>
        Modal
      </button>
      <button type="button" className="h-12 rounded-[4px] bg-live font-bold text-white" onClick={() => void confirm({ title: 'Eliminar publicación', message: 'Esta acción no se puede deshacer.', type: 'danger', confirmText: 'Eliminar', cancelText: 'Cancelar' })}>
        Confirmar
      </button>
      <button type="button" className="h-12 rounded-[4px] border border-ink" onClick={() => setBlocking((value) => !value)}>
        {blocking ? 'Ocultar overlay' : 'Overlay'}
      </button>
      {blocking ? <div className="grid h-40 place-items-center rounded-[20px] bg-black/50"><SituLoader size="lg" /></div> : null}
    </main>
  );
}
