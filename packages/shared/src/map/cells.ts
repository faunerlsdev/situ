import { asNumber } from '../reports/logic';

/** Same grid as Situ.Domain.Reports.MapCell. Longitude index, then latitude index. */
export const defaultCellDegrees = 0.05;
export const defaultMaxCells = 16;

export type CellBounds = {
  minLat: number;
  minLon: number;
  maxLat: number;
  maxLon: number;
};

export function cellIndex(value: number, degrees: number): number {
  return Math.floor(value / degrees);
}

export function cellId(latitude: number, longitude: number, degrees = defaultCellDegrees): string {
  return `${cellIndex(longitude, degrees)}:${cellIndex(latitude, degrees)}`;
}

export function boundsFromRegion(region: {
  latitude: number;
  longitude: number;
  latitudeDelta: number;
  longitudeDelta: number;
}): CellBounds {
  return {
    minLat: region.latitude - region.latitudeDelta / 2,
    maxLat: region.latitude + region.latitudeDelta / 2,
    minLon: region.longitude - region.longitudeDelta / 2,
    maxLon: region.longitude + region.longitudeDelta / 2,
  };
}

export function cellsCovering(bounds: CellBounds, degrees = defaultCellDegrees, maxCells = defaultMaxCells): {
  ids: string[];
  tooWide: boolean;
} {
  if (
    degrees <= 0
    || maxCells < 1
    || bounds.minLat > bounds.maxLat
    || bounds.minLon > bounds.maxLon
    || bounds.minLat < -90
    || bounds.maxLat > 90
    || bounds.minLon < -180
    || bounds.maxLon > 180
  ) {
    return { ids: [], tooWide: true };
  }

  const x0 = cellIndex(bounds.minLon, degrees);
  const x1 = cellIndex(bounds.maxLon, degrees);
  const y0 = cellIndex(bounds.minLat, degrees);
  const y1 = cellIndex(bounds.maxLat, degrees);
  const count = (x1 - x0 + 1) * (y1 - y0 + 1);
  if (count <= 0 || count > maxCells) {
    return { ids: [], tooWide: true };
  }

  const ids: string[] = [];
  for (let x = x0; x <= x1; x += 1) {
    for (let y = y0; y <= y1; y += 1) {
      ids.push(`${x}:${y}`);
    }
  }
  return { ids, tooWide: false };
}

export function diffCells(current: readonly string[], next: readonly string[]): {
  subscribe: string[];
  unsubscribe: string[];
} {
  const have = new Set(current);
  const want = new Set(next);
  return {
    subscribe: next.filter((id) => !have.has(id)),
    unsubscribe: current.filter((id) => !want.has(id)),
  };
}

export function mapRealtimeOf(
  config: { mapRealtime?: { cellDegrees?: number | string | null; maxCells?: number | string | null } | null } | null | undefined,
): { cellDegrees: number; maxCells: number } {
  const degrees = asNumber(config?.mapRealtime?.cellDegrees);
  const maxCells = asNumber(config?.mapRealtime?.maxCells);
  return {
    cellDegrees: degrees > 0 ? degrees : defaultCellDegrees,
    maxCells: maxCells > 0 ? Math.min(maxCells, defaultMaxCells) : defaultMaxCells,
  };
}
