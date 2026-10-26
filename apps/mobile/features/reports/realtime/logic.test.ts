import { describe, expect, it } from 'vitest';

import type { MapItem } from '@situ/shared';

import {
  mergeMapItems,
  nextRetryDelayMs,
  planSubscription,
  planZoneEvents,
  removedBecauseMissing,
  shouldHoldConnection,
  zoneCreated,
  zoneExpired,
  zoneRemoved,
  zoneResolved,
  zoneUpdated,
} from './logic';

const point = (id: string): MapItem => ({
  kind: 'point',
  id,
  count: 1,
  latitude: 4.71,
  longitude: -74.07,
  categoryId: 'cat',
  categoryCode: 'accidente',
  createdAt: '2026-10-26T15:00:00Z',
  distanceMeters: null,
  author: null,
  stillThereCount: 0,
  goneNowCount: 0,
  commentCount: 0,
  previewUrl: null,
  isAnonymous: true,
  hideAuthorOnMap: true,
  isMine: false,
});

describe('map realtime', () => {
  it('subscribes only the cells that entered and drops the ones that left', () => {
    const region = { latitude: 4.71, longitude: -74.07, latitudeDelta: 0.02, longitudeDelta: 0.02 };
    const first = planSubscription([], region, 0.05, 16);
    expect(first.mode).toBe('live');
    expect(first.cells.length).toBeGreaterThan(0);
    const moved = planSubscription(first.cells, { ...region, longitude: -74.02 }, 0.05, 16);
    expect(moved.unsubscribe.length + moved.subscribe.length).toBeGreaterThan(0);
    expect(moved.cells).not.toEqual(first.cells);
  });

  it('stays on the manual load when the zoom covers more than 16 cells', () => {
    const wide = planSubscription(['0:0'], { latitude: 4, longitude: -74, latitudeDelta: 2, longitudeDelta: 2 }, 0.05, 16);
    expect(wide).toEqual({ mode: 'manual', cells: [], subscribe: [], unsubscribe: ['0:0'] });
  });

  it('fetches content only for created and updated events and removes the rest without a fetch', () => {
    const seen = new Set<string>();
    const plan = planZoneEvents([
      { type: zoneCreated, reportId: 'a', cellId: '0:0', occurredAt: 't1' },
      { type: zoneUpdated, reportId: 'b', cellId: '0:0', occurredAt: 't2' },
      { type: zoneRemoved, reportId: 'c', cellId: '0:0', occurredAt: 't3' },
      { type: zoneExpired, reportId: 'd', cellId: '0:0', occurredAt: 't4' },
      { type: zoneResolved, reportId: 'e', cellId: '0:0', occurredAt: 't5' },
      { type: zoneCreated, reportId: 'a', cellId: '0:0', occurredAt: 't1' },
    ], seen);
    expect(plan.fetchIds).toEqual(['a', 'b']);
    expect(plan.removeIds).toEqual(['c', 'd', 'e']);
    expect(plan.accepted).toHaveLength(5);

    const again = planZoneEvents([{ type: zoneCreated, reportId: 'a', cellId: '0:0', occurredAt: 't1' }], new Set(plan.accepted));
    expect(again.fetchIds).toEqual([]);
    expect(again.removeIds).toEqual([]);
  });

  it('merges fetched points once and drops removed ones without asking again', () => {
    const current = [point('a'), point('c')];
    const fetched = [point('a'), point('b')];
    const missing = removedBecauseMissing(['a', 'b', 'gone'], fetched);
    const merged = mergeMapItems(current, ['c', ...missing], fetched);
    expect(merged.map((item) => item.id)).toEqual(['a', 'b']);
  });

  it('reconnects with backoff and disconnects in the background', () => {
    expect(nextRetryDelayMs(0)).toBe(1000);
    expect(nextRetryDelayMs(1)).toBe(2000);
    expect(nextRetryDelayMs(5)).toBe(30000);
    expect(nextRetryDelayMs(8)).toBeNull();
    expect(shouldHoldConnection('active', true)).toBe(true);
    expect(shouldHoldConnection('background', true)).toBe(false);
    expect(shouldHoldConnection('inactive', true)).toBe(false);
    expect(shouldHoldConnection('active', false)).toBe(false);
  });
});
