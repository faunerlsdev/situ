import { describe, expect, it } from 'vitest';

import { boundsFromRegion, cellId, cellsCovering, diffCells, mapRealtimeOf } from './cells';

describe('map cells', () => {
  it('matches the server grid on edges and both hemispheres', () => {
    expect(cellId(0, 0)).toBe('0:0');
    expect(cellId(0.05, 0.05)).toBe('1:1');
    expect(cellId(-0.0001, 0)).toBe('0:-1');
    expect(cellId(0, -0.05)).toBe('-1:0');
    expect(cellId(4.711, -74.072)).toBe('-1482:94');
    expect(cellId(90, 180)).toBe('3600:1800');
    expect(cellId(-90, -180)).toBe('-3600:-1800');
  });

  it('covers a viewport and refuses one that is too wide or crosses the antimeridian', () => {
    expect(cellsCovering({ minLat: 4.7, minLon: -74.08, maxLat: 4.74, maxLon: -74.04 })).toEqual({
      ids: ['-1482:94', '-1481:94'],
      tooWide: false,
    });
    expect(cellsCovering({ minLat: 0, minLon: -1, maxLat: 1, maxLon: 1 }).tooWide).toBe(true);
    expect(cellsCovering({ minLat: 0, minLon: 170, maxLat: 0, maxLon: -170 }).tooWide).toBe(true);
  });

  it('diffs the cells to subscribe and unsubscribe', () => {
    expect(diffCells(['0:0', '1:0'], ['1:0', '2:0'])).toEqual({
      subscribe: ['2:0'],
      unsubscribe: ['0:0'],
    });
  });

  it('reads the grid from app config and falls back when it is missing', () => {
    expect(mapRealtimeOf({ mapRealtime: { cellDegrees: 0.1, maxCells: 8 } })).toEqual({
      cellDegrees: 0.1,
      maxCells: 8,
    });
    expect(mapRealtimeOf(null)).toEqual({ cellDegrees: 0.05, maxCells: 16 });
    expect(boundsFromRegion({ latitude: 4, longitude: -74, latitudeDelta: 0.02, longitudeDelta: 0.04 })).toEqual({
      minLat: 3.99,
      maxLat: 4.01,
      minLon: -74.02,
      maxLon: -73.98,
    });
  });
});
