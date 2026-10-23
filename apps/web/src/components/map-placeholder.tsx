'use client';

import { notify, presentError } from '@situ/shared';
import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import { useState } from 'react';
import type { FormEvent } from 'react';
import { AreaProgress } from '@/components/feedback/inline';
import { useDelayedFlag } from '@/components/feedback/use-feedback';
import { api } from '@/lib/api';
import { postBff } from '@/lib/http';
import { readField } from './login-form';

const examples = [
  {
    id: 'ejemplo-reten',
    title: 'Retén',
    place: 'Ejemplo · Calle 80',
    detail: 'Un pin de muestra. No es un reporte real.',
    top: '34%',
    left: '28%',
  },
  {
    id: 'ejemplo-basura',
    title: 'Basura en la vía',
    place: 'Ejemplo · Parque central',
    detail: 'Otro pin de muestra para ver cómo se comenta.',
    top: '52%',
    left: '58%',
  },
] as const;

type ExampleId = (typeof examples)[number]['id'];
type Tab = 'mapa' | 'chisme' | 'vivo' | 'mensajes';

const rail = [
  { id: 'mapa', label: 'Mapa', icon: '/icons/nav-mapa-globo.png' },
  { id: 'chisme', label: 'Chisme', icon: '/icons/nav-chisme-megafono.png' },
  { id: 'vivo', label: 'En vivo', icon: '/icons/nav-en-vivo.png' },
  { id: 'mensajes', label: 'Mensajes', icon: '/icons/nav-mensajes.png' },
] as const;

export function MapPlaceholder() {
  const [tab, setTab] = useState<Tab>('mapa');
  const [selected, setSelected] = useState<ExampleId | null>(null);
  const [sheet, setSheet] = useState(false);
  const [search, setSearch] = useState(false);
  const [drawer, setDrawer] = useState(false);
  const area = useQuery({
    queryKey: ['reports', 'area'],
    queryFn: async () => examples,
    staleTime: Infinity,
  });
  const showAreaProgress = useDelayedFlag(area.isFetching);
  const [query, setQuery] = useState('');
  const [pending, setPending] = useState(false);
  const current = examples.find((example) => example.id === selected);
  const visible = examples.filter((example) => example.title.toLowerCase().includes(query.trim().toLowerCase()));

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selected) {
      return;
    }

    const form = new FormData(event.currentTarget);
    setPending(true);

    try {
      await api.commentOnReport(selected, { body: readField(form, 'body') });
      notify.success('Comentario enviado.');
      event.currentTarget.reset();
    } catch (cause) {
      presentError(cause, () => undefined, () => {
        void onSubmit(event);
      });
    } finally {
      setPending(false);
    }
  }

  async function signOut() {
    await postBff('/api/auth/sign-out');
    window.location.assign('/login');
  }

  return (
    <div className="relative min-h-dvh bg-edge text-ink">
      <nav aria-label="Accesos" className="absolute inset-y-0 left-0 z-30 hidden w-16 flex-col items-center gap-3 bg-white py-4 lg:flex">
        <div className="brand-divider absolute top-0" />
        {rail.map((item) => (
          <button
            key={item.id}
            type="button"
            aria-label={item.label}
            aria-current={tab === item.id ? 'page' : undefined}
            onClick={() => {
              setTab(item.id);
              if (item.id === 'chisme') {
                setSheet(true);
              }
            }}
            className="grid size-12 place-items-center"
          >
            <img src={item.icon} alt="" className={`size-8 object-contain ${tab === item.id ? '' : 'opacity-55'}`} />
            {tab === item.id ? <span className="mt-1 size-1 rounded-full bg-primary" /> : null}
          </button>
        ))}
        <button type="button" onClick={() => void signOut()} className="mt-auto font-sans text-small font-bold text-ink">
          Salir
        </button>
      </nav>

      <aside className="absolute top-4 bottom-4 left-20 z-20 hidden w-[360px] flex-col overflow-hidden rounded-[20px] bg-white shadow-sheet lg:flex">
        <label className="flex items-center gap-2 px-4 py-3">
          <img src="/icons/ic-buscar.png" alt="" className="size-8 object-contain" />
          <input
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Buscar"
            aria-label="Buscar"
            className="h-11 w-full rounded-[2px] px-2 font-sans text-body outline-none placeholder:text-gray400"
          />
        </label>
        <div className="brand-divider" />
        <div className="flex-1 overflow-y-auto p-4">
          {tab === 'vivo' || tab === 'mensajes' ? (
            <EmptyCopy title={tab === 'vivo' ? 'En vivo' : 'Mensajes'} />
          ) : visible.length === 0 ? (
            <EmptyCopy title="Sin resultados" />
          ) : (
            <ul className="space-y-2">
              {visible.map((example) => (
                <li key={example.id}>
                  <button
                    type="button"
                    aria-pressed={selected === example.id}
                    onClick={() => setSelected(example.id)}
                    className={`w-full rounded-[4px] border px-3 py-3 text-left ${selected === example.id ? 'border-ink' : 'border-gray200'}`}
                  >
                    <span className="block font-sans text-body font-semibold">{example.title}</span>
                    <span className="mt-1 block font-sans text-small text-gray600">{example.place}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
          <CommentForm current={current} pending={pending} onSubmit={onSubmit} />
        </div>
      </aside>

      <div className="relative min-h-dvh lg:pl-16">
        <header className="bg-white lg:hidden">
          <AreaProgress active={showAreaProgress} />
          <div className="flex h-14 items-center justify-between px-4">
            <div className="flex items-center gap-3">
              <button type="button" aria-label="Abrir menú" onClick={() => setDrawer(true)} className="grid size-9 place-items-center rounded-full bg-gray200 font-sans text-body font-bold">
                S
              </button>
              <button type="button" aria-label="Buscar" onClick={() => setSearch(true)}>
                <img src="/icons/ic-buscar.png" alt="" className="size-8 object-contain" />
              </button>
            </div>
            <img src="/brand/situ-wordmark-naranja.png" alt="Situ" className="h-7 w-auto object-contain" />
          </div>
          <div className="brand-divider" />
        </header>

        <div className="relative min-h-[calc(100dvh-3.5rem-4rem)] bg-edge lg:min-h-dvh">
          <div className="absolute top-[18%] left-1/2 size-64 -translate-x-1/2 rounded-full bg-edge-center" />
          {tab === 'mapa' || tab === 'chisme' ? (
            examples.map((example) => (
              <button
                key={example.id}
                type="button"
                aria-label={example.title}
                aria-pressed={selected === example.id}
                onClick={() => setSelected(example.id)}
                className="absolute"
                style={{ top: example.top, left: example.left }}
              >
                <img src="/icons/situ-pin-mapa.png" alt="" className="h-9 w-7 object-contain" />
              </button>
            ))
          ) : (
            <div className="grid min-h-[50dvh] place-items-center">
              <EmptyCopy title={tab === 'vivo' ? 'En vivo' : 'Mensajes'} />
            </div>
          )}
          <button type="button" aria-label="Centrar en mi ubicación" className="absolute bottom-4 left-4 grid size-12 place-items-center rounded-full bg-white shadow-sheet">
            <span className="size-3.5 rounded-full border-2 border-primary" />
          </button>
          <button type="button" aria-label="Reportar" onClick={() => setSheet(true)} className="absolute right-4 bottom-4 grid size-16 place-items-center rounded-full bg-white shadow-sheet">
            <img src="/brand/situ-isotipo-negro.png" alt="" className="h-[52px] w-10 object-contain" />
          </button>
          {current ? (
            <form onSubmit={onSubmit} className="absolute right-24 bottom-24 left-4 space-y-2 rounded-[20px] bg-white p-3 lg:hidden">
              <p className="font-sans text-body font-semibold">{current.title}</p>
              <p className="font-sans text-small text-gray600">{current.detail}</p>
              <textarea name="body" required disabled={pending} aria-label="Comentario" className="min-h-16 w-full rounded-[2px] border border-gray200 px-2 py-2 font-sans text-body" />
              <button type="submit" disabled={pending} className="h-10 w-full rounded-[4px] bg-primary font-sans text-body font-bold text-white">
                {pending ? 'Enviando…' : 'Comentar'}
              </button>
            </form>
          ) : null}
        </div>

        <nav aria-label="Principal" className="bg-white lg:hidden">
          <div className="brand-divider" />
          <div className="grid h-16 grid-cols-4">
            {rail.map((item) => (
              <button
                key={item.id}
                type="button"
                aria-label={item.label}
                aria-current={tab === item.id ? 'page' : undefined}
                onClick={() => {
                  setTab(item.id);
                  if (item.id === 'chisme') {
                    setSheet(true);
                  }
                }}
                className="grid place-items-center"
              >
                <img src={item.icon} alt="" className={`size-8 object-contain ${tab === item.id ? '' : 'opacity-55'}`} />
                {tab === item.id ? <span className="size-1 rounded-full bg-primary" /> : null}
              </button>
            ))}
          </div>
        </nav>
      </div>

      {sheet ? (
        <div className="fixed inset-0 z-40 flex items-end">
          <button type="button" aria-label="Cerrar" className="absolute inset-0 bg-black/40" onClick={() => setSheet(false)} />
          <div className="relative w-full rounded-t-[20px] bg-white p-4 shadow-sheet">
            <div className="mx-auto mb-3 h-1 w-10 rounded-full bg-gray200" />
            <h2 className="font-display text-h2">Reportar</h2>
            <div className="brand-divider my-3" />
            <p className="font-sans text-body text-gray600">
              El reporte real guarda la foto, el lugar y la categoría. Este adelanto todavía no publica. Desde la web no se inicia una transmisión.
            </p>
            <div className="mt-4 grid grid-cols-3 gap-2">
              {['Retén', 'Basura', 'Hurto'].map((label) => (
                <div key={label} className="grid min-h-24 place-items-center rounded-[4px] border border-gray200 p-2 text-center">
                  <img src="/icons/situ-noticias.png" alt="" className="h-7 w-9 object-contain" />
                  <span className="font-sans text-small">{label}</span>
                </div>
              ))}
            </div>
            <Link href="/registro" className="mt-4 inline-flex font-sans text-body font-bold text-primary">
              Crear cuenta para reportar
            </Link>
          </div>
        </div>
      ) : null}

      {search ? (
        <div className="fixed inset-0 z-40 bg-white p-4 lg:hidden">
          <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Buscar" aria-label="Buscar" className="h-[52px] w-full rounded-[2px] border border-gray200 px-3 font-sans text-body" />
          <button type="button" onClick={() => setSearch(false)} className="mt-3 font-sans text-body font-bold text-primary">
            Cerrar
          </button>
          <EmptyCopy title="Próximamente" />
        </div>
      ) : null}

      {drawer ? (
        <div className="fixed inset-0 z-40 lg:hidden">
          <button type="button" aria-label="Cerrar menú" className="absolute inset-0 bg-black/40" onClick={() => setDrawer(false)} />
          <aside className="relative h-full w-[280px] bg-white p-5 shadow-sheet">
            <h2 className="font-display text-h1">Mi SITU</h2>
            <ul className="mt-4 space-y-3 font-sans text-body">
              <li><Link href="/login">Ingresar</Link></li>
              <li><Link href="/registro">Crear cuenta</Link></li>
              <li>
                <button type="button" onClick={() => void signOut()} className="font-sans text-body">
                  Cerrar sesión
                </button>
              </li>
              <li className="text-gray600">Ajustes · Próximamente</li>
            </ul>
          </aside>
        </div>
      ) : null}
    </div>
  );
}

function EmptyCopy({ title }: { title: string }) {
  return (
    <div className="grid justify-items-center gap-2 py-8 text-center">
      <img src="/icons/situ-noticias.png" alt="" className="h-16 w-24 object-contain" />
      <p className="font-sans text-h2 font-bold">{title}</p>
      <p className="font-sans text-small text-gray600">Próximamente</p>
    </div>
  );
}

function CommentForm({
  current,
  pending,
  onSubmit,
}: {
  current: (typeof examples)[number] | undefined;
  pending: boolean;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
}) {
  return (
    <form onSubmit={onSubmit} className="mt-4 space-y-2">
      <h2 className="font-display text-h2">Comentar</h2>
      <p className="font-sans text-small text-gray600">
        {current ? `${current.title}. ${current.detail}` : 'Elige un reporte de ejemplo para escribir.'}
      </p>
      <textarea
        name="body"
        required
        disabled={current === undefined || pending}
        aria-label="Comentario"
        className="min-h-24 w-full rounded-[2px] border border-gray200 px-3 py-2 font-sans text-body"
      />
      <button type="submit" disabled={current === undefined || pending} className="h-11 rounded-[4px] bg-primary px-4 font-sans text-body font-bold text-white disabled:opacity-60">
        {pending ? 'Enviando…' : 'Comentar'}
      </button>
    </form>
  );
}
