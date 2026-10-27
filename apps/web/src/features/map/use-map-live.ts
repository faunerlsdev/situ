'use client';

import { mapRealtimeOf } from '@situ/shared';
import type { AppConfig, MapItem } from '@situ/shared';
import { HttpTransportType, HubConnectionBuilder } from '@microsoft/signalr';
import { useEffect, useRef, useState } from 'react';
import { postBff } from '@/lib/http';
import { api } from '@/lib/api';
import { cellsForViewport, normalizeZoneEvents, planZoneEvents, rememberEvents } from './zone-events';
import type { ZoneEvent } from './zone-events';

export function useMapLive(input: {
  enabled: boolean;
  visible: boolean;
  config: AppConfig | undefined;
  bounds: { minLat: number; minLon: number; maxLat: number; maxLon: number } | null;
  onRemove: (ids: string[]) => void;
  onFetched: (items: MapItem[]) => void;
  onReconnect: () => void;
}) {
  const [offline, setOffline] = useState(false);
  const seen = useRef(new Set<string>());
  const handlers = useRef(input);
  useEffect(() => {
    handlers.current = input;
  });

  useEffect(() => {
    if (!input.enabled || !input.visible || !input.bounds || !input.config) {
      return;
    }

    const grid = mapRealtimeOf(input.config);
    const cover = cellsForViewport(input.bounds, grid.cellDegrees, grid.maxCells);
    let stopped = false;
    let connection: ReturnType<HubConnectionBuilder['build']> | null = null;
    let wait: ReturnType<typeof setTimeout> | undefined;

    async function start() {
      let attempt = 0;
      while (!stopped) {
        try {
          const issued = await postBff<{ ticket: string }>('/api/auth/realtime-ticket');
          if (stopped) {
            return;
          }
          const hub = `${process.env.NEXT_PUBLIC_API_URL ?? 'http://127.0.0.1:5080'}/hubs/map?ticket=${encodeURIComponent(issued.ticket)}`;
          connection = new HubConnectionBuilder()
            .withUrl(hub, { skipNegotiation: true, transport: HttpTransportType.WebSockets })
            .build();
          connection.on('zone', (events: ZoneEvent[] | ZoneEvent) => {
            const plan = planZoneEvents(normalizeZoneEvents(events), seen.current);
            rememberEvents(seen.current, plan.accepted);
            if (plan.removeIds.length > 0) {
              handlers.current.onRemove(plan.removeIds);
            }
            if (plan.fetchIds.length > 0) {
              void api.batchReports(plan.fetchIds).then((page) => handlers.current.onFetched(page.items)).catch(() => undefined);
            }
          });
          await connection.start();
          if (stopped) {
            await connection.stop();
            return;
          }
          setOffline(false);
          if (!cover.tooWide && cover.ids.length > 0) {
            await connection.invoke('SubscribeCells', cover.ids);
          }
          if (attempt > 0) {
            handlers.current.onReconnect();
          }
          attempt = 0;
          await new Promise<void>((resolve) => {
            connection?.onclose(() => resolve());
          });
        } catch {
          if (!stopped) {
            setOffline(true);
          }
        }

        if (stopped) {
          return;
        }

        setOffline(true);
        attempt += 1;
        const delay = Math.min(30_000, 1000 * 2 ** Math.min(attempt, 5));
        await new Promise<void>((resolve) => {
          wait = setTimeout(resolve, delay);
        });
      }
    }

    void start();
    return () => {
      stopped = true;
      if (wait) {
        clearTimeout(wait);
      }
      void connection?.stop();
    };
  }, [input.bounds, input.config, input.enabled, input.visible]);

  return offline;
}
