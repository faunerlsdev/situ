'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import type { GuardianConsentReview, GuardianDocumentType } from '@situ/shared';
import { emailSpamHint, guardianDataSchema, messageFromUnknown, notify } from '@situ/shared';
import type { GuardianDataValues } from '@situ/shared';
import { useSearchParams } from 'next/navigation';
import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { SituLoader } from '@/components/feedback/situ-loader';
import { api } from '@/lib/api';

const documents: { value: GuardianDocumentType; label: string }[] = [
  { value: 'CitizenshipCard', label: 'Cédula de ciudadanía' },
  { value: 'ForeignerCard', label: 'Cédula de extranjería' },
  { value: 'Passport', label: 'Pasaporte' },
  { value: 'Other', label: 'Otro' },
];

type Step = 'notice' | 'data' | 'code' | 'decision' | 'done';

export function GuardianForm() {
  const params = useSearchParams();
  const token = params.get('token') ?? '';
  const [step, setStep] = useState<Step>('notice');
  const [review, setReview] = useState<GuardianConsentReview | null>(null);
  const [loading, setLoading] = useState(token.length > 0);
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [code, setCode] = useState('');
  const [reason, setReason] = useState('');
  const [live, setLive] = useState(false);
  const [location, setLocation] = useState(false);
  const [treatment, setTreatment] = useState(false);
  const [readingTreatment, setReadingTreatment] = useState(false);
  const [done, setDone] = useState('');
  const form = useForm<GuardianDataValues>({
    resolver: zodResolver(guardianDataSchema),
    defaultValues: { fullName: '', documentType: 'CitizenshipCard', documentNumber: '', contactEmail: '' },
  });

  useEffect(() => {
    if (!token) {
      return;
    }

    let active = true;
    void api.guardianConsentReview(token).then((value) => {
      if (active) {
        setReview(value);
      }
    }).catch((cause: unknown) => {
      if (active) {
        setError(messageFromUnknown(cause));
      }
    }).finally(() => {
      if (active) {
        setLoading(false);
      }
    });
    return () => {
      active = false;
    };
  }, [token]);

  async function submitData(values: GuardianDataValues) {
    setPending(true);
    setError(null);
    try {
      await api.submitGuardianData({ token, ...values });
      setStep('code');
    } catch (cause) {
      setError(messageFromUnknown(cause));
    } finally {
      setPending(false);
    }
  }

  async function verify() {
    setPending(true);
    setError(null);
    try {
      await api.verifyGuardianEmailCode(token, code.trim());
      setStep('decision');
    } catch (cause) {
      setError(messageFromUnknown(cause));
    } finally {
      setPending(false);
    }
  }

  function openTreatment() {
    setReadingTreatment(true);
  }

  function acceptTreatment() {
    setTreatment(true);
    setReadingTreatment(false);
    setError(null);
  }

  async function authorize() {
    if (!treatment) {
      setError('Para autorizar, acepta el tratamiento de los datos.');
      setReadingTreatment(true);
      return;
    }
    if (review?.requiresLiveAuthorization && !live) {
      setError('Como el menor tiene menos de 16 años, la transmisión en vivo necesita tu autorización específica.');
      return;
    }
    setPending(true);
    setError(null);
    try {
      await api.authorizeGuardianConsent({ token, liveStreamingAuthorized: live, locationSharingAuthorized: location, dataProcessingAccepted: true });
      setDone('Registramos tu autorización. Te enviaremos la constancia al correo que verificaste.');
      setStep('done');
    } catch (cause) {
      setError(messageFromUnknown(cause));
    } finally {
      setPending(false);
    }
  }

  async function decline() {
    setPending(true);
    setError(null);
    try {
      await api.declineGuardianConsent({ token, reason: reason.trim() || undefined });
      setDone('Rechazaste la autorización. El menor verá que la cuenta no fue autorizada.');
      setStep('done');
    } catch (cause) {
      setError(messageFromUnknown(cause));
    } finally {
      setPending(false);
    }
  }

  async function optOut() {
    setPending(true);
    setError(null);
    try {
      await api.optOutGuardianEmail(token);
      setDone('Listo. No volveremos a enviarte solicitudes de autorización.');
      setStep('done');
      notify.info('Dejaremos de enviarte estas solicitudes.');
    } catch (cause) {
      setError(messageFromUnknown(cause));
    } finally {
      setPending(false);
    }
  }

  return (
    <main className="mx-auto flex min-h-dvh w-full max-w-lg flex-col gap-5 px-6 py-10">
      <p className="font-display text-4xl font-extrabold text-[#FF6400]">SITU</p>
      <h1 className="font-display text-3xl font-bold">Autorización de un menor</h1>
      {loading ? <SituLoader label="Cargando la solicitud" /> : null}
      {!token ? <p role="alert" className="rounded-[8px] bg-[#F8E8E6] px-3 py-2 font-sans text-[#8E1B16]">El enlace de autorización no es válido.</p> : null}
      {error ? <p role="alert" className="rounded-[8px] bg-[#F8E8E6] px-3 py-2 font-sans text-[#8E1B16]">{error}</p> : null}
      {!loading && review && step === 'notice' ? (
        <section className="flex flex-col gap-4 font-sans leading-6">
          <p>Un menor pidió usar SITU y escribió tu correo para que decidas. Estos son los únicos datos que te mostramos.</p>
          <dl className="overflow-hidden rounded-2xl border border-[#E4DFD6] bg-white">
            <Fact label="Usuario" value={review.userName} />
            <Fact label="Edad" value={`${review.age} años`} />
            <Fact label="Ciudad" value={review.city} />
            {review.minorStatement ? <Fact label="Mensaje" value={review.minorStatement} /> : null}
          </dl>
          <p className="text-sm text-[#6B675F]">Aviso versión {review.noticeVersion}. La solicitud vence el {new Date(review.expiresAt).toLocaleDateString('es-CO')}.</p>
          <button type="button" onClick={() => setStep('data')} className="rounded-[8px] bg-[#1C1C1C] px-4 py-3 font-bold text-white">Continuar</button>
        </section>
      ) : null}
      {step === 'data' ? (
        <form onSubmit={form.handleSubmit(submitData)} className="flex flex-col gap-3 font-sans" noValidate>
          <label>Nombre completo<input className="mt-1 h-12 w-full rounded-[8px] border border-[#D5D0C8] bg-white px-3" {...form.register('fullName')} /></label>
          {form.formState.errors.fullName ? <p className="text-[#8E1B16]">{form.formState.errors.fullName.message}</p> : null}
          <label>Tipo de documento
            <select className="mt-1 h-12 w-full rounded-[8px] border border-[#D5D0C8] bg-white px-3" {...form.register('documentType')}>
              {documents.map((document) => <option key={document.value} value={document.value}>{document.label}</option>)}
            </select>
          </label>
          <label>Número de documento<input className="mt-1 h-12 w-full rounded-[8px] border border-[#D5D0C8] bg-white px-3" {...form.register('documentNumber')} /></label>
          {form.formState.errors.documentNumber ? <p className="text-[#8E1B16]">{form.formState.errors.documentNumber.message}</p> : null}
          <label>Correo<input type="email" inputMode="email" className="mt-1 h-12 w-full rounded-[8px] border border-[#D5D0C8] bg-white px-3" {...form.register('contactEmail')} /></label>
          {form.formState.errors.contactEmail ? <p className="text-[#8E1B16]">{form.formState.errors.contactEmail.message}</p> : null}
          <button type="submit" disabled={pending} className="rounded-[8px] bg-[#1C1C1C] px-4 py-3 font-bold text-white disabled:opacity-60">Enviar código</button>
        </form>
      ) : null}
      {step === 'code' ? (
        <section className="flex flex-col gap-3 font-sans">
          <p>Te enviamos un código de 6 dígitos. Vence en 10 minutos.</p>
          <p className="text-sm text-[#6B675F]">{emailSpamHint}</p>
          <input inputMode="numeric" maxLength={6} value={code} onChange={(event) => setCode(event.target.value)} aria-label="Código de verificación" className="h-12 rounded-[8px] border border-[#D5D0C8] bg-white px-3 tracking-[0.3em]" />
          <button type="button" disabled={pending} onClick={() => void verify()} className="rounded-[8px] bg-[#1C1C1C] px-4 py-3 font-bold text-white disabled:opacity-60">Verificar correo</button>
          <button type="button" disabled={pending} onClick={() => void api.resendGuardianEmailCode(token).catch((cause: unknown) => setError(messageFromUnknown(cause)))} className="font-bold text-[#FF6400]">Reenviar código</button>
        </section>
      ) : null}
      {readingTreatment ? (
        <TreatmentNotice
          onAccept={acceptTreatment}
          onBack={() => setReadingTreatment(false)}
        />
      ) : null}
      {step === 'decision' && !readingTreatment ? (
        <section className="flex flex-col gap-3 font-sans">
          <div className="rounded-2xl border border-[#E4DFD6] bg-white p-4">
            <label className="flex items-start gap-3">
              <input
                type="checkbox"
                className="mt-1 h-5 w-5 accent-[#FF6400]"
                checked={treatment}
                onChange={(event) => {
                  if (event.target.checked) {
                    openTreatment();
                    return;
                  }
                  setTreatment(false);
                }}
              />
              <span className="font-bold">Acepto el tratamiento de datos</span>
            </label>
            <button type="button" onClick={openTreatment} className="mt-3 ml-8 font-bold text-[#FF6400] underline">Ver tratamiento de datos</button>
          </div>
          <label className="flex items-start gap-3 rounded-2xl border border-[#E4DFD6] bg-white p-4"><input type="checkbox" className="mt-1 h-5 w-5 accent-[#FF6400]" checked={live} onChange={(event) => setLive(event.target.checked)} /> Permitir transmisiones en vivo{review?.requiresLiveAuthorization ? ' (obligatorio: el menor tiene menos de 16 años)' : ''}</label>
          <label className="flex items-start gap-3 rounded-2xl border border-[#E4DFD6] bg-white p-4"><input type="checkbox" className="mt-1 h-5 w-5 accent-[#FF6400]" checked={location} onChange={(event) => setLocation(event.target.checked)} /> Permitir compartir ubicación con amigos</label>
          <button type="button" disabled={pending} onClick={() => void authorize()} className="rounded-[8px] bg-[#1C1C1C] px-4 py-3 font-bold text-white disabled:opacity-60">Autorizar</button>
          <label>Motivo del rechazo, si quieres<textarea value={reason} onChange={(event) => setReason(event.target.value)} className="mt-1 min-h-20 w-full rounded-[8px] border border-[#D5D0C8] bg-white px-3 py-2" /></label>
          <button type="button" disabled={pending} onClick={() => void decline()} className="font-bold underline">Rechazar</button>
          <button type="button" disabled={pending} onClick={() => void optOut()} className="text-left text-sm underline">No conozco a este menor / no quiero recibir más solicitudes de SITU</button>
        </section>
      ) : null}
      {step === 'done' ? <p className="font-sans text-lg">{done}</p> : null}
    </main>
  );
}

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <div className="grid grid-cols-[6.5rem_1fr] gap-3 border-b border-[#E4DFD6] px-4 py-3 last:border-b-0">
      <dt className="text-sm text-[#6B675F]">{label}</dt>
      <dd className="font-bold">{value}</dd>
    </div>
  );
}

function TreatmentNotice({ onAccept, onBack }: { onAccept: () => void; onBack: () => void }) {
  return (
    <section className="flex flex-col gap-4 font-sans leading-6">
      <h2 className="font-display text-2xl font-bold">Tratamiento de datos</h2>
      <p>Para dejar constancia de esta autorización, SITU trata estos datos del adulto responsable:</p>
      <ul className="list-disc space-y-1 pl-5">
        <li>Nombre completo</li>
        <li>Tipo y número de documento</li>
        <li>Correo de contacto</li>
        <li>La decisión de autorizar, rechazar o revocar</li>
        <li>Los permisos de transmisión en vivo y de ubicación</li>
      </ul>
      <p>La finalidad es permitir o negar el uso de la cuenta del menor, según la Ley 1581 de 2012. Puedes conocer, actualizar, rectificar y revocar esta autorización. Para ejercer esos derechos escribe a SITU desde la página de soporte.</p>
      <button type="button" onClick={onAccept} className="rounded-[8px] bg-[#1C1C1C] px-4 py-3 font-bold text-white">Sí, acepto</button>
      <button type="button" onClick={onBack} className="font-bold text-[#FF6400]">Volver</button>
    </section>
  );
}
