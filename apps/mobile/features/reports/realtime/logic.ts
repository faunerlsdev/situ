import { diffCells, cellsCovering, boundsFromRegion } from '@situ/shared';
import type { CellBounds, MapItem } from '@situ/shared';

export const zoneCreated = 'ReportCreated';
export const zoneUpdated = 'ReportUpdated';
export const zoneResolved = 'ReportResolved';
export const zoneExpired = 'ReportExpired';
export const zoneRemoved = 'ReportRemoved';

const terminal = new Set([zoneRemoved, zoneExpired, zoneResolved]);
const content = new Set([zoneCreated, zoneUpdated]);

export type ZoneEvent = {
  type: string;
  reportId: string;
  cellId: string;
  occurredAt: string;
};

export function shouldHoldConnection(appState: string, mapVisible: boolean): boolean {
  return mapVisible && appState === 'active';
}

/** Backoff for SignalR automatic reconnect. Eight attempts, then the caller starts again on the next foreground. */
export function nextRetryDelayMs(attempt: number): number | null {
  if (attempt >= 8) {
    return null;
  }
  return Math.min(30_000, 1000 * 2 ** attempt);
}

export function planSubscription(
  current: readonly string[],
  region: { latitude: number; longitude: number; latitudeDelta: number; longitudeDelta: number } | null,
  degrees: number,
  maxCells: number,
): { mode: 'manual' | 'live'; cells: string[]; subscribe: string[]; unsubscribe: string[] } {
  if (!region) {
    return { mode: 'manual', cells: [], subscribe: [], unsubscribe: [...current] };
  }
  const cover = cellsCovering(boundsFromRegion(region), degrees, maxCells);
  if (cover.tooWide) {
    return { mode: 'manual', cells: [], subscribe: [], unsubscribe: [...current] };
  }
  const diff = diffCells(current, cover.ids);
  return { mode: 'live', cells: cover.ids, subscribe: diff.subscribe, unsubscribe: diff.unsubscribe };
}

export function planZoneEvents(events: readonly ZoneEvent[], seen: ReadonlySet<string>): {
  fetchIds: string[];
  removeIds: string[];
  accepted: string[];
} {
  const fetchIds: string[] = [];
  const removeIds: string[] = [];
  const accepted: string[] = [];
  const batch = new Set(seen);
  for (const event of events) {
    if (!event.reportId || !event.type || !event.occurredAt) {
      continue;
    }
    const key = `${event.type}:${event.reportId}:${event.occurredAt}`;
    if (batch.has(key)) {
      continue;
    }
    batch.add(key);
    accepted.push(key);
    if (terminal.has(event.type)) {
      removeIds.push(event.reportId);
    } else if (content.has(event.type)) {
      fetchIds.push(event.reportId);
    }
  }
  return { fetchIds: unique(fetchIds), removeIds: unique(removeIds), accepted };
}

export function mergeMapItems(items: readonly MapItem[], removeIds: readonly string[], fetched: readonly MapItem[]): MapItem[] {
  const drop = new Set(removeIds);
  const byId = new Map(fetched.filter((item) => item.id).map((item) => [item.id as string, item]));
  const next: MapItem[] = [];
  const placed = new Set<string>();
  for (const item of items) {
    if (item.kind !== 'point' || !item.id) {
      next.push(item);
      continue;
    }
    if (drop.has(item.id)) {
      continue;
    }
    const replacement = byId.get(item.id);
    if (replacement) {
      next.push(replacement);
      placed.add(item.id);
      continue;
    }
    next.push(item);
  }
  for (const item of fetched) {
    if (item.id && !placed.has(item.id) && !drop.has(item.id)) {
      next.push(item);
    }
  }
  return next;
}

export function removedBecauseMissing(fetchIds: readonly string[], fetched: readonly MapItem[]): string[] {
  const got = new Set(fetched.map((item) => item.id).filter((id): id is string => Boolean(id)));
  return fetchIds.filter((id) => !got.has(id));
}

export function rememberEvents(seen: Set<string>, accepted: readonly string[], cap = 500): void {
  for (const key of accepted) {
    seen.add(key);
  }
  if (seen.size <= cap) {
    return;
  }
  const extra = seen.size - cap;
  const iterator = seen.values();
  for (let index = 0; index < extra; index += 1) {
    const next = iterator.next();
    if (next.done) {
      break;
    }
    seen.delete(next.value);
  }
}

function unique(values: string[]): string[] {
  return [...new Set(values)];
}

export function regionBounds(region: {
  latitude: number;
  longitude: number;
  latitudeDelta: number;
  longitudeDelta: number;
}): CellBounds {
  return boundsFromRegion(region);
}
