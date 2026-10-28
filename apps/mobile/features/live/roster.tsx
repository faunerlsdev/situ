import type { LiveCard } from '@situ/shared';
import { useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query';
import { useEffect, useState, useSyncExternalStore } from 'react';
import { AppState } from 'react-native';

import { api } from '@/lib/api';

import { isVisibleLive, ROSTER_DEBOUNCE_MS, ROSTER_POLL_MS, shouldPollLiveRoster, type LiveNotice } from './logic';

export const LIVE_ROSTER_KEY = ['live', 'list'] as const;

type LiveList = { streams: LiveCard[] };

const noticeListeners = new Set<(notice: LiveNotice) => void>();
const hubListeners = new Set<() => void>();
let hubConnected = false;
let rosterClient: QueryClient | null = null;
let refreshTimer: ReturnType<typeof setTimeout> | null = null;

export function publishLiveNotice(notice: LiveNotice) {
  for (const listener of noticeListeners) {
    listener(notice);
  }
}

export function subscribeLiveNotice(listener: (notice: LiveNotice) => void) {
  noticeListeners.add(listener);
  return () => {
    noticeListeners.delete(listener);
  };
}

export function setHubConnected(connected: boolean) {
  if (hubConnected === connected) {
    return;
  }
  hubConnected = connected;
  for (const listener of hubListeners) {
    listener();
  }
}

export function useHubConnected() {
  return useSyncExternalStore(
    (listener) => {
      hubListeners.add(listener);
      return () => hubListeners.delete(listener);
    },
    () => hubConnected,
    () => false,
  );
}

export function bindLiveRoster(client: QueryClient) {
  rosterClient = client;
  return () => {
    if (rosterClient === client) {
      rosterClient = null;
    }
  };
}

/** Refetch the one live list. Hub, foreground, and map focus share this debounce. */
export function refreshLiveRoster(immediate = false) {
  if (refreshTimer) {
    clearTimeout(refreshTimer);
    refreshTimer = null;
  }
  const run = () => {
    refreshTimer = null;
    void rosterClient?.invalidateQueries({ queryKey: LIVE_ROSTER_KEY });
  };
  if (immediate) {
    run();
    return;
  }
  refreshTimer = setTimeout(run, ROSTER_DEBOUNCE_MS);
}

export function useLiveRoster() {
  const client = useQueryClient();
  const state = useSyncExternalStore(
    (listener) => client.getQueryCache().subscribe(listener),
    () => client.getQueryState<LiveList>(LIVE_ROSTER_KEY),
    () => client.getQueryState<LiveList>(LIVE_ROSTER_KEY),
  );
  const streams = (state?.data?.streams ?? []).filter((item) => isVisibleLive(item));
  return {
    streams,
    count: streams.length,
    isPending: state?.status !== 'success' && state?.status !== 'error',
    isError: state?.status === 'error',
    error: state?.error ?? null,
    refetch: () => refreshLiveRoster(true),
  };
}

/** The only component that calls GET /live. Toolbar, map, and menu read the same cache. */
export function LiveRoster() {
  const client = useQueryClient();
  const connected = useHubConnected();
  const [foreground, setForeground] = useState(AppState.currentState === 'active');

  useEffect(() => bindLiveRoster(client), [client]);

  useEffect(() => {
    const subscription = AppState.addEventListener('change', (state) => {
      const active = state === 'active';
      setForeground(active);
      if (active) {
        refreshLiveRoster();
      }
    });
    return () => subscription.remove();
  }, []);

  useQuery({
    queryKey: LIVE_ROSTER_KEY,
    queryFn: () => api.listLive(),
    refetchInterval: shouldPollLiveRoster(foreground, connected) ? ROSTER_POLL_MS : false,
  });

  return null;
}
