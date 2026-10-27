'use client';

import {
  absoluteMediaUrl,
  anonymityNotice,
  anonymityNoticeAccepted,
  anonymityStatus,
  commentValidation,
  complaintReasons,
  composerAnonymity,
  defaultApiUrl,
  feedbackMessages,
  notify,
  presentAuthor,
  readApiUrl,
  relativeTime,
} from '@situ/shared';
import type { ReportDetail } from '@situ/shared';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import Link from 'next/link';
import { useState } from 'react';
import { api } from '@/lib/api';
import { classifyStatus } from '@/lib/guard';
import { sessionBlock } from './session-gate';

const apiBase = readApiUrl(process.env.NEXT_PUBLIC_API_URL, defaultApiUrl);

export function ReportPanel({ id, signedIn, here }: { id: string; signedIn: boolean; here: { lat: number; lng: number } | null }) {
  const queryClient = useQueryClient();
  const detail = useQuery({
    queryKey: ['report', id],
    queryFn: () => api.report(id),
    enabled: signedIn,
  });
  const me = useQuery({
    queryKey: ['me'],
    queryFn: () => api.sessionProfile(),
    enabled: signedIn,
  });

  if (!signedIn) {
    return (
      <div className="grid gap-3">
        <p>{sessionBlock(false)}</p>
        <Link href={`/login?next=/r/${id}`} className="font-bold text-primary">Iniciar sesión</Link>
      </div>
    );
  }

  if (detail.isLoading) {
    return <p role="status">Cargando el reporte…</p>;
  }

  if (detail.isError || !detail.data) {
    return <StatusLine error={detail.error} fallback="No pudimos abrir el reporte." />;
  }

  const report = detail.data.report;
  const author = presentAuthor(report.author);
  return (
    <article className="grid gap-3">
      <header>
        <h2 className="font-display text-2xl">{report.categoryCode ?? 'Reporte'}</h2>
        <p className="text-sm text-app-muted">{relativeTime(report.createdAt)}</p>
        <p>{author.name}{author.memberSince ? ` · ${author.memberSince}` : ''}</p>
      </header>
      <p>{report.description}</p>
      <MediaList media={detail.data.media} />
      <p className="text-sm text-app-muted">
        Sigue ahí {report.stillThereCount} · Ya no está {report.goneNowCount} · Comentarios {report.commentCount}
      </p>
      <div className="flex flex-wrap gap-2">
        <button type="button" className="min-h-11 rounded border border-app-border px-3" onClick={() => void confirm(id, 'StillThere', here, queryClient)}>Sigue ahí</button>
        <button type="button" className="min-h-11 rounded border border-app-border px-3" onClick={() => void confirm(id, 'GoneNow', here, queryClient)}>Ya no está</button>
        <button type="button" className="min-h-11 rounded border border-app-border px-3" onClick={() => void share(id)}>Compartir</button>
      </div>
      <Link href={`/retiro-de-contenido?reporte=${id}`} className="text-sm font-bold text-primary">¿Apareces en este chisme?</Link>
      <ComplaintForm targetId={id} />
      <CommentBox reportId={id} detail={detail.data} isMinor={me.data?.isMinor === true} profile={me.data} />
    </article>
  );
}

function MediaList({ media }: { media: ReportDetail['media'] }) {
  if (media.length === 0) {
    return null;
  }
  return (
    <div className="grid gap-2">
      {media.map((item) => {
        const url = absoluteMediaUrl(apiBase, item.url);
        if (!url) {
          return null;
        }
        if (item.type === 'Video') {
          return <video key={item.id} src={url} controls preload="none" className="max-h-72 w-full rounded-2xl bg-black" />;
        }
        return <img key={item.id} src={url} alt="" className="max-h-72 w-full rounded-2xl object-cover" />;
      })}
    </div>
  );
}

function CommentBox({ reportId, detail, isMinor, profile }: { reportId: string; detail: ReportDetail; isMinor: boolean; profile: unknown }) {
  const queryClient = useQueryClient();
  const [text, setText] = useState('');
  const [chosen, setChosen] = useState(true);
  const rule = composerAnonymity({ isMinor, visibility: detail.report.visibility, chosen });

  async function send() {
    const invalid = commentValidation(text);
    if (invalid) {
      notify.warning(invalid);
      return;
    }
    try {
      await api.addComment(reportId, { text: text.trim(), isAnonymous: rule.anonymous });
      setText('');
      await queryClient.invalidateQueries({ queryKey: ['report', reportId] });
    } catch (cause) {
      notify.error(cause);
    }
  }

  return (
    <section className="grid gap-2">
      <h3 className="font-bold">Comentarios</h3>
      {detail.comments.length === 0 ? <p className="text-sm text-app-muted">{feedbackMessages.commentFirst}</p> : null}
      {detail.comments.map((comment) => {
        const author = presentAuthor(comment.author);
        return (
          <p key={comment.id}><span className="font-bold">{author.name}. </span>{comment.text}</p>
        );
      })}
      <label className="grid gap-1 text-sm">
        Nota
        <textarea value={text} onChange={(event) => setText(event.target.value)} aria-label="Escribe un comentario" className="min-h-20 rounded border border-app-border bg-app-input p-2" />
      </label>
      <label className="flex items-center gap-2 text-sm">
        <input type="checkbox" checked={rule.anonymous} disabled={rule.locked} onChange={(event) => {
          setChosen(event.target.checked);
          if (!rule.locked) {
            void api.updateMyProfile({ preferAnonymousReports: event.target.checked }).catch(() => undefined);
          }
        }} />
        Publicar como anónimo
      </label>
      <p className="text-sm">{anonymityStatus(rule.anonymous)}</p>
      {rule.locked ? <p className="text-sm text-app-muted">{feedbackMessages.commentAnonymousLocked}</p> : null}
      {!anonymityNoticeAccepted(profile) ? (
        <div className="rounded border border-app-border p-3 text-sm">
          <p className="font-bold">{anonymityNotice.title}</p>
          <p>{anonymityNotice.body}</p>
          <button type="button" className="mt-2 min-h-11 font-bold text-primary" onClick={() => void api.acknowledgeAnonymityNotice().then(() => queryClient.invalidateQueries({ queryKey: ['me'] }))}>Entendido</button>
        </div>
      ) : null}
      <button type="button" className="min-h-11 justify-self-start rounded bg-primary px-4 font-bold text-app-on" onClick={() => void send()}>Publicar</button>
    </section>
  );
}

function ComplaintForm({ targetId }: { targetId: string }) {
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState<(typeof complaintReasons)[number]['code']>('Spam');
  const [details, setDetails] = useState('');

  async function send() {
    try {
      await api.submitComplaint({ targetType: 'Report', targetId, reason, details: details || null });
      setOpen(false);
      notify.success(feedbackMessages.complaintThanks);
    } catch (cause) {
      notify.error(cause);
    }
  }

  if (!open) {
    return <button type="button" className="min-h-11 justify-self-start text-sm font-bold text-primary" onClick={() => setOpen(true)}>Denunciar</button>;
  }

  return (
    <form className="grid gap-2" onSubmit={(event) => { event.preventDefault(); void send(); }}>
      <label className="text-sm">Motivo
        <select aria-label="Motivo de la denuncia" value={reason} onChange={(event) => setReason(event.target.value as typeof reason)} className="mt-1 w-full rounded border border-app-border bg-app-input p-2">
          {complaintReasons.map((item) => <option key={item.code} value={item.code}>{item.label}</option>)}
        </select>
      </label>
      <label className="text-sm">Detalle
        <textarea aria-label="Detalle de la denuncia" value={details} onChange={(event) => setDetails(event.target.value)} className="mt-1 min-h-16 w-full rounded border border-app-border bg-app-input p-2" />
      </label>
      <button type="submit" className="min-h-11 justify-self-start rounded border border-app-border px-3">Enviar denuncia</button>
    </form>
  );
}

async function confirm(id: string, type: 'StillThere' | 'GoneNow', here: { lat: number; lng: number } | null, queryClient: ReturnType<typeof useQueryClient>) {
  // eslint-disable-next-line no-alert -- aviso breve antes de pedir la ubicación del navegador
  if (typeof window !== 'undefined' && !window.confirm('Usamos tu ubicación solo para medir la distancia. No la guardamos.')) {
    return;
  }
  const point = here ?? await browserPoint();
  if (!point) {
    notify.warning('Activa tu ubicación para confirmar lo que ves en el lugar.');
    return;
  }
  try {
    await api.confirmReport(id, { type, latitude: point.lat, longitude: point.lng });
    await queryClient.invalidateQueries({ queryKey: ['report', id] });
  } catch (cause) {
    notify.error(cause);
  }
}

function browserPoint(): Promise<{ lat: number; lng: number } | null> {
  if (typeof navigator === 'undefined' || !navigator.geolocation) {
    return Promise.resolve(null);
  }

  return new Promise((resolve) => {
    navigator.geolocation.getCurrentPosition(
      (position) => resolve({ lat: position.coords.latitude, lng: position.coords.longitude }),
      () => resolve(null),
      { enableHighAccuracy: true, maximumAge: 30_000, timeout: 8_000 },
    );
  });
}

async function share(id: string) {
  const url = `${window.location.origin}/r/${id}`;
  if (navigator.share) {
    await navigator.share({ url }).catch(() => undefined);
    return;
  }
  await navigator.clipboard.writeText(url);
  notify.success('Enlace copiado.');
}

function StatusLine({ error, fallback }: { error: unknown; fallback: string }) {
  const status = error && typeof error === 'object' && 'status' in error && typeof error.status === 'number' ? error.status : 0;
  const kind = classifyStatus(status);
  if (kind === 'session') {
    return <p>Tu sesión venció. Vuelve a iniciar sesión.</p>;
  }
  if (kind === 'forbidden') {
    return <p>No puedes ver este reporte.</p>;
  }
  if (kind === 'rate') {
    return <p>Demasiados intentos. Espera un momento.</p>;
  }
  return <p>{fallback}</p>;
}
