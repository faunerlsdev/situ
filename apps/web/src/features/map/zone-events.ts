import { cellsCovering } from '@situ/shared';
import type { CellBounds } from '@situ/shared';

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

export function normalizeZoneEvents(value: ZoneEvent[] | ZoneEvent | null | undefined): ZoneEvent[] {
  if (Array.isArray(value)) {
    return value;
  }

  return value ? [value] : [];
}

export function cellsForViewport(bounds: CellBounds, degrees: number, maxCells: number) {
  return cellsCovering(bounds, degrees, maxCells);
}

export function planZoneEvents(events: readonly ZoneEvent[], seen: ReadonlySet<string>) {
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

export function rememberEvents(seen: Set<string>, accepted: readonly string[], cap = 500) {
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

function unique(values: string[]) {
  return [...new Set(values)];
}
