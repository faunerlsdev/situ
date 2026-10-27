import { describe, expect, it } from 'vitest';
import { cellsForViewport, normalizeZoneEvents, planZoneEvents, rememberEvents } from './zone-events';

describe('mapa en tiempo real', () => {
  it('usa la misma cuadrícula y se detiene en 16 celdas', () => {
    expect(cellsForViewport({ minLat: 4.7, minLon: -74.08, maxLat: 4.74, maxLon: -74.04 }, 0.05, 16).tooWide).toBe(false);
    expect(cellsForViewport({ minLat: 0, minLon: -1, maxLat: 1, maxLon: 1 }, 0.05, 16).tooWide).toBe(true);
  });

  it('no pide dos veces el mismo aviso y separa altas de retiros', () => {
    const seen = new Set<string>();
    const first = planZoneEvents([
      { type: 'ReportCreated', reportId: 'a', cellId: '1:1', occurredAt: 't' },
      { type: 'ReportCreated', reportId: 'a', cellId: '1:1', occurredAt: 't' },
      { type: 'ReportRemoved', reportId: 'b', cellId: '1:1', occurredAt: 't2' },
    ], seen);
    expect(first.fetchIds).toEqual(['a']);
    expect(first.removeIds).toEqual(['b']);
    rememberEvents(seen, first.accepted);
    const second = planZoneEvents([
      { type: 'ReportCreated', reportId: 'a', cellId: '1:1', occurredAt: 't' },
    ], seen);
    expect(second.fetchIds).toEqual([]);
    expect(second.accepted).toEqual([]);
  });

  it('acepta el lote que manda el hub o un solo aviso', () => {
    const event = { type: 'ReportUpdated', reportId: 'a', cellId: '1:1', occurredAt: 't' };
    expect(normalizeZoneEvents([event, event])).toHaveLength(2);
    expect(normalizeZoneEvents(event)).toEqual([event]);
    expect(normalizeZoneEvents(null)).toEqual([]);
  });
});
