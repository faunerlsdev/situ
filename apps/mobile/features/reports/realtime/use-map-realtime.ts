import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useRef, useState } from 'react';
import { AppState } from 'react-native';
import type { HubConnection } from '@microsoft/signalr';
import { HubConnectionState } from '@microsoft/signalr';

import { api } from '@/lib/api';
import type { MapItem, MapResponse } from '@situ/shared';

import { createMapConnection } from './connection';
import {
  mergeMapItems,
  planSubscription,
  planZoneEvents,
  rememberEvents,
  removedBecauseMissing,
  shouldHoldConnection,
  type ZoneEvent,
} from './logic';

export type MapRealtimeMode = 'off' | 'manual' | 'connecting' | 'live' | 'down';

type RegionLike = {
  latitude: number;
  longitude: number;
  latitudeDelta: number;
  longitudeDelta: number;
};

/**
 * One map socket while the app is in the foreground and this screen is visible.
 * The region passed in is already debounced by the map (~500 ms).
 */
export function useMapRealtime(options: {
  mapVisible: boolean;
  region: RegionLike | null;
  cellDegrees: number;
  maxCells: number;
  onResync: () => void;
  onRemove: (ids: string[]) => void;
}): { mode: MapRealtimeMode; freshIds: ReadonlySet<string> } {
  const queryClient = useQueryClient();
  const [appState, setAppState] = useState(AppState.currentState);
  const [phase, setPhase] = useState<'off' | 'connecting' | 'live' | 'down'>('off');
  const [tracking, setTracking] = useState(false);
  const [freshIds, setFreshIds] = useState<ReadonlySet<string>>(new Set());
  const connectionRef = useRef<HubConnection | null>(null);
  const cellsRef = useRef<string[]>([]);
  const seenRef = useRef(new Set<string>());
  const regionRef = useRef(options.region);
  const degreesRef = useRef(options.cellDegrees);
  const maxRef = useRef(options.maxCells);
  const resyncRef = useRef(options.onResync);
  const removeRef = useRef(options.onRemove);
  regionRef.current = options.region;
  degreesRef.current = options.cellDegrees;
  maxRef.current = options.maxCells;
  resyncRef.current = options.onResync;
  removeRef.current = options.onRemove;

  const applyEvents = useCallback(async (events: ZoneEvent[]) => {
    const plan = planZoneEvents(events, seenRef.current);
    if (plan.accepted.length === 0) {
      return;
    }
    let fetched: MapItem[] = [];
    if (plan.fetchIds.length > 0) {
      try {
        fetched = await loadReports(plan.fetchIds);
      } catch {
        if (plan.removeIds.length > 0) {
          dropFromMap(queryClient, plan.removeIds);
          removeRef.current(plan.removeIds);
        }
        resyncRef.current();
        return;
      }
    }
    rememberEvents(seenRef.current, plan.accepted);
    const removeIds = [...plan.removeIds, ...removedBecauseMissing(plan.fetchIds, fetched)];
    const previous = new Set(currentPointIds(queryClient));
    let clustered = false;
    queryClient.setQueriesData<MapResponse>({ queryKey: ['reports-map'] }, (current) => {
      if (!current?.items) {
        return current;
      }
      if (current.items.some((item) => item.kind === 'cluster')) {
        clustered = true;
        return current;
      }
      return { ...current, items: mergeMapItems(current.items, removeIds, fetched) };
    });
    if (clustered) {
      resyncRef.current();
    }
    if (removeIds.length > 0) {
      removeRef.current(removeIds);
    }
    for (const id of plan.fetchIds) {
      void queryClient.invalidateQueries({ queryKey: ['report', id] });
    }
    const appeared = fetched.flatMap((item) => (item.id && !previous.has(item.id) ? [item.id] : []));
    if (appeared.length > 0) {
      setFreshIds(new Set(appeared));
    }
  }, [queryClient]);

  const syncCells = useCallback(async () => {
    const connection = connectionRef.current;
    if (!connection || connection.state !== HubConnectionState.Connected) {
      return;
    }
    const plan = planSubscription(cellsRef.current, regionRef.current, degreesRef.current, maxRef.current);
    setTracking(plan.mode === 'live');
    const previous = cellsRef.current;
    cellsRef.current = plan.cells;
    try {
      if (plan.unsubscribe.length > 0) {
        await connection.invoke('UnsubscribeCells', plan.unsubscribe);
      }
      if (plan.subscribe.length > 0) {
        await connection.invoke('SubscribeCells', plan.subscribe);
      }
    } catch {
      cellsRef.current = previous;
      setPhase('down');
    }
  }, []);

  useEffect(() => {
    const subscription = AppState.addEventListener('change', setAppState);
    return () => subscription.remove();
  }, []);

  const hold = shouldHoldConnection(appState ?? 'background', options.mapVisible);

  useEffect(() => {
    if (!hold) {
      setPhase('off');
      setTracking(false);
      return;
    }

    let stopped = false;
    const connection = createMapConnection((events) => {
      void applyEvents(events);
    });
    connection.onreconnecting(() => {
      if (!stopped) {
        setPhase('down');
      }
    });
    connection.onreconnected(() => {
      cellsRef.current = [];
      if (!stopped) {
        setPhase('live');
        resyncRef.current();
        void syncCells();
      }
    });
    connection.onclose(() => {
      cellsRef.current = [];
      if (!stopped) {
        setPhase('down');
        setTracking(false);
      }
    });
    connectionRef.current = connection;
    setPhase('connecting');
    connection.start().then(() => {
      if (stopped) {
        return;
      }
      setPhase('live');
    }).catch(() => {
      if (!stopped) {
        setPhase('down');
      }
    });

    return () => {
      stopped = true;
      cellsRef.current = [];
      connectionRef.current = null;
      void connection.stop();
    };
  }, [applyEvents, hold, syncCells]);

  useEffect(() => {
    if (phase !== 'live') {
      return;
    }
    void syncCells();
  }, [options.region, options.cellDegrees, options.maxCells, phase, syncCells]);

  useEffect(() => {
    if (freshIds.size === 0) {
      return;
    }
    const timer = setTimeout(() => setFreshIds(new Set()), 400);
    return () => clearTimeout(timer);
  }, [freshIds]);

  const mode: MapRealtimeMode = !hold
    ? 'off'
    : phase === 'live' && !tracking
      ? 'manual'
      : phase;

  return { mode, freshIds };
}

async function loadReports(ids: string[]): Promise<MapItem[]> {
  const items: MapItem[] = [];
  for (let index = 0; index < ids.length; index += 50) {
    const page = await api.batchReports(ids.slice(index, index + 50));
    items.push(...page.items);
  }
  return items;
}

function dropFromMap(queryClient: ReturnType<typeof useQueryClient>, removeIds: readonly string[]) {
  queryClient.setQueriesData<MapResponse>({ queryKey: ['reports-map'] }, (current) => {
    if (!current?.items || current.items.some((item) => item.kind === 'cluster')) {
      return current;
    }
    return { ...current, items: mergeMapItems(current.items, removeIds, []) };
  });
}

function currentPointIds(queryClient: ReturnType<typeof useQueryClient>): string[] {
  const queries = queryClient.getQueriesData<MapResponse>({ queryKey: ['reports-map'] });
  const ids: string[] = [];
  for (const [, data] of queries) {
    for (const item of data?.items ?? []) {
      if (item.kind === 'point' && item.id) {
        ids.push(item.id);
      }
    }
  }
  return ids;
}
