'use client';

import {
  absoluteMediaUrl,
  defaultApiUrl,
  panelCopyOf,
  readApiUrl,
  toMapMarkers,
} from '@situ/shared';
import type { MapItem, ReportCatalog } from '@situ/shared';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { useEffect, useMemo, useState } from 'react';
import { api } from '@/lib/api';
import { readBrowserSession } from '@/lib/http';
import { classifyStatus } from '@/lib/guard';
import { GoogleMapView } from './google-map';
import type { MapViewport } from './google-map';
import { useMapLive } from './use-map-live';
import { ReportPanel } from '@/features/reports/report-panel';
import { toggleCategory } from '@/features/reports/session-gate';

const apiBase = readApiUrl(process.env.NEXT_PUBLIC_API_URL, defaultApiUrl);
const bogota = { lat: 4.711, lng: -74.072 };

export function MapScreen() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const mapsKey = process.env.NEXT_PUBLIC_GOOGLE_MAPS_KEY ?? '';
  const [viewport, setViewport] = useState<MapViewport | null>(null);
  const [debounced, setDebounced] = useState<MapViewport | null>(null);
  const [categories, setCategories] = useState<string[]>([]);
  const [since, setSince] = useState<'all' | 'hour' | 'today'>('all');
  const [selected, setSelected] = useState<string | null>(null);
  const [guestPin, setGuestPin] = useState(false);
  const [sheetOpen, setSheetOpen] = useState(false);
  const [here, setHere] = useState<{ lat: number; lng: number } | null>(null);
  const [focus, setFocus] = useState<{ lat: number; lng: number; nonce: number } | null>(null);
  const [visible, setVisible] = useState(true);
  const [liveItems, setLiveItems] = useState<MapItem[]>([]);

  const session = useQuery({
    queryKey: ['session'],
    queryFn: () => readBrowserSession(),
  });
  const signedIn = session.data?.signedIn === true;
  const config = useQuery({ queryKey: ['app-config'], queryFn: async () => (await api.appConfig()).data });
  const catalog = useQuery({ queryKey: ['categories'], queryFn: async () => (await api.reportCategories()).data });

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebounced((current) => (sameViewport(current, viewport) ? current : viewport));
    }, 500);
    return () => window.clearTimeout(timer);
  }, [viewport]);

  useEffect(() => {
    const onChange = () => setVisible(document.visibilityState === 'visible');
    document.addEventListener('visibilitychange', onChange);
    onChange();
    return () => document.removeEventListener('visibilitychange', onChange);
  }, []);

  const bounds = useMemo(() => (debounced ? {
    minLat: debounced.south,
    minLon: debounced.west,
    maxLat: debounced.north,
    maxLon: debounced.east,
  } : null), [debounced]);

  const publicQuery = useQuery({
    queryKey: ['public-map', bounds],
    enabled: !signedIn && bounds !== null,
    queryFn: () => api.publicMap({
      minLat: bounds!.minLat,
      minLon: bounds!.minLon,
      maxLat: bounds!.maxLat,
      maxLon: bounds!.maxLon,
    }),
  });

  const mapQuery = useQuery({
    queryKey: ['map', bounds, categories, since, here],
    enabled: signedIn && bounds !== null,
    queryFn: () => api.mapReports({
      minLat: bounds!.minLat,
      minLon: bounds!.minLon,
      maxLat: bounds!.maxLat,
      maxLon: bounds!.maxLon,
      zoom: Math.round(debounced!.zoom),
      categories,
      since: since === 'all' ? null : since,
      friendsOnly: false,
      latitude: here?.lat ?? null,
      longitude: here?.lng ?? null,
    }),
  });

  const markers = useMemo(() => toMapMarkers([...(mapQuery.data?.items ?? []), ...liveItems]), [liveItems, mapQuery.data?.items]);
  const copy = panelCopyOf(config.data ?? null);
  const offline = useMapLive({
    enabled: signedIn,
    visible,
    config: config.data ?? undefined,
    bounds,
    onRemove: (ids) => {
      const drop = new Set(ids);
      setLiveItems((current) => current.filter((item) => !item.id || !drop.has(item.id)));
      if (selected && drop.has(selected)) {
        setSelected(null);
      }
    },
    onFetched: (items) => setLiveItems((current) => merge(current, items)),
    onReconnect: () => void queryClient.invalidateQueries({ queryKey: ['map'] }),
  });

  const icons = new Map((catalog.data?.categories ?? []).map((item) => [item.code, absoluteMediaUrl(apiBase, item.mapIconUrl)]));

  function locate() {
    if (!navigator.geolocation) {
      return;
    }
    navigator.geolocation.getCurrentPosition((position) => {
      const next = { lat: position.coords.latitude, lng: position.coords.longitude };
      setHere(next);
      setFocus({ ...next, nonce: Date.now() });
    });
  }

  function chooseCategory(id: string) {
    if (!signedIn) {
      router.push(`/login?next=/reportar?category=${id}`);
      return;
    }
    router.push(`/reportar?category=${id}`);
  }

  return (
    <div className="relative min-h-dvh bg-app-bg">
      {mapsKey ? (
        <div className="absolute inset-0">
          <GoogleMapView
            apiKey={mapsKey}
            center={bogota}
            zoom={13}
            focus={focus}
            pins={signedIn
              ? markers.map((item) => item.kind === 'cluster'
                ? { id: item.key, lat: item.latitude, lng: item.longitude, kind: 'cluster' as const, title: `${item.count} reportes`, count: item.count }
                : { id: item.id, lat: item.latitude, lng: item.longitude, kind: 'point' as const, title: item.categoryCode ?? 'Reporte', iconUrl: icons.get(item.categoryCode ?? '') })
              : (publicQuery.data?.items ?? []).map((item, index) => ({
                id: `guest-${index}`,
                lat: item.latitude,
                lng: item.longitude,
                kind: 'point' as const,
                title: `${item.categoryName}. ${item.zone}`,
                iconUrl: icons.get(item.categoryCode),
              }))}
            onViewport={setViewport}
            onSelect={(id) => {
              if (!signedIn) {
                setSelected(null);
                setGuestPin(true);
                return;
              }
              setGuestPin(false);
              setSelected(id);
            }}
          />
        </div>
      ) : (
        <div className="grid min-h-dvh place-items-center px-6 text-center">
          <div className="max-w-md">
            <h1 className="font-display text-3xl">Falta la clave del mapa</h1>
            <p className="mt-3 text-app-muted">Configura NEXT_PUBLIC_GOOGLE_MAPS_KEY para ver el mapa. El resto de SITU sigue disponible.</p>
          </div>
        </div>
      )}
      <div className="pointer-events-none absolute inset-x-0 top-0 z-10 flex flex-col gap-2 p-3">
        <div className="pointer-events-auto flex flex-wrap gap-2">
          {signedIn ? (['all', 'hour', 'today'] as const).map((item) => (
            <button key={item} type="button" aria-pressed={since === item} className={`min-h-11 rounded-full px-3 ${since === item ? 'bg-primary text-app-on' : 'bg-app-elevated'}`} onClick={() => setSince(item)}>
              {item === 'hour' ? 'Última hora' : item === 'today' ? 'Hoy' : 'Todo'}
            </button>
          )) : null}
          <button type="button" className="min-h-11 rounded-full bg-app-elevated px-3" onClick={locate}>Mi ubicación</button>
        </div>
        <CategoryFilters catalog={catalog.data} selected={categories} onChange={setCategories} />
        {offline ? <p role="status" className="pointer-events-auto w-fit rounded-full bg-app-elevated px-3 py-1 text-xs text-app-muted">Sin conexión en tiempo real</p> : null}
        {mapQuery.isError ? <p className="pointer-events-auto rounded bg-app-elevated px-3 py-2 text-sm">{mapMessage(mapQuery.error)}</p> : null}
        {!signedIn ? <p className="pointer-events-auto rounded bg-app-elevated px-3 py-2 text-sm">Inicia sesión para ver los detalles</p> : null}
      </div>
      <button type="button" className="absolute bottom-4 left-1/2 z-10 min-h-12 -translate-x-1/2 rounded-full bg-primary px-5 font-bold text-app-on" onClick={() => setSheetOpen(true)}>
        {copy.browse}
      </button>
      {guestPin && !signedIn ? (
        <aside className="absolute bottom-20 left-3 right-3 z-20 rounded-3xl bg-app-elevated p-4 shadow-app sm:left-auto sm:w-[380px]">
          <p className="font-bold">Inicia sesión para ver los detalles</p>
          <div className="mt-3 flex gap-3">
            <button type="button" className="min-h-11 rounded bg-primary px-4 font-bold text-app-on" onClick={() => router.push('/login')}>Iniciar sesión</button>
            <button type="button" className="min-h-11 rounded border border-app-border px-4" onClick={() => router.push('/registro')}>Crear cuenta</button>
          </div>
        </aside>
      ) : null}
      {selected ? (
        <aside className="absolute bottom-20 left-3 right-3 z-20 max-h-[70dvh] overflow-auto rounded-3xl bg-app-elevated p-4 shadow-app sm:left-auto sm:w-[380px]">
          <button type="button" className="min-h-11" aria-label="Cerrar" onClick={() => setSelected(null)}>Cerrar</button>
          <ReportPanel id={selected} signedIn={signedIn} here={here} />
        </aside>
      ) : null}
      {sheetOpen ? (
        <div className="absolute inset-x-0 bottom-0 z-30 max-h-[70dvh] overflow-auto rounded-t-3xl bg-app-surface p-4" role="dialog" aria-label={copy.browse}>
          <div className="mb-3 flex items-center justify-between">
            <h2 className="font-display text-xl">{copy.browse}</h2>
            <button type="button" className="min-h-11" aria-label="Cerrar categorías" onClick={() => setSheetOpen(false)}>Cerrar</button>
          </div>
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
            {(catalog.data?.categories ?? []).filter((item) => !item.parentCategoryId).map((item) => (
              <button key={item.id} type="button" className="grid min-h-28 gap-1 rounded-2xl border border-app-border p-3 text-left" onClick={() => chooseCategory(item.id)}>
                {item.buttonIconUrl ? <img src={absoluteMediaUrl(apiBase, item.buttonIconUrl) ?? ''} alt="" width={36} height={36} className="h-9 w-9 object-contain" /> : null}
                <span className="font-bold">{item.name}</span>
                <span className="line-clamp-2 text-sm text-app-muted">{item.description}</span>
              </button>
            ))}
          </div>
        </div>
      ) : null}
    </div>
  );
}

function CategoryFilters({ catalog, selected, onChange }: { catalog: ReportCatalog | null | undefined; selected: string[]; onChange: (codes: string[]) => void }) {
  const roots = (catalog?.categories ?? []).filter((item) => !item.parentCategoryId);
  return (
    <div className="pointer-events-auto flex gap-2 overflow-x-auto">
      {roots.map((item) => {
        const active = selected.includes(item.code);
        return (
          <button
            key={item.id}
            type="button"
            aria-pressed={active}
            className={`min-h-11 shrink-0 rounded-full px-3 ${active ? 'bg-primary text-app-on' : 'bg-app-elevated'}`}
            onClick={() => onChange(toggleCategory(selected, item.code))}
          >
            {item.name}
          </button>
        );
      })}
    </div>
  );
}

function sameViewport(current: MapViewport | null, next: MapViewport | null) {
  if (current === next) return true;
  if (!current || !next) return false;
  return current.north === next.north
    && current.south === next.south
    && current.east === next.east
    && current.west === next.west
    && current.zoom === next.zoom;
}

function merge(current: MapItem[], fetched: MapItem[]) {
  const byId = new Map(fetched.filter((item) => item.id).map((item) => [item.id as string, item]));
  const next = current.map((item) => (item.id && byId.has(item.id) ? byId.get(item.id)! : item));
  for (const item of fetched) {
    if (item.id && !next.some((existing) => existing.id === item.id)) {
      next.push(item);
    }
  }
  return next;
}

function mapMessage(error: unknown) {
  const status = error && typeof error === 'object' && 'status' in error && typeof error.status === 'number' ? error.status : 0;
  const kind = classifyStatus(status);
  if (kind === 'session') return 'Tu sesión venció. Vuelve a iniciar sesión.';
  if (kind === 'forbidden') return 'No puedes ver los reportes de esta zona.';
  if (kind === 'rate') return 'Demasiadas consultas. Espera un momento.';
  return 'No pudimos cargar el mapa.';
}
