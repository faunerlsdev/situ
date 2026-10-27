import { describe, expect, it } from 'vitest';

import { clusterPeople, emitIntervalSeconds, mergePeople, seenLabel } from './logic';
import type { MapPerson } from '@situ/shared';

const person = (userId: string, latitude: number): MapPerson => ({
  userId,
  userName: userId,
  displayName: userId,
  avatarUrl: null,
  latitude,
  longitude: -74,
  live: true,
  lastSeenAt: null,
});

describe('location people', () => {
  it('prefers the friend pin and hides the viewer', () => {
    const merged = mergePeople(
      [person('friend', 4.71)],
      [person('friend', 4.7), person('me', 4.6), person('public', 4.5)],
      'me',
    );
    expect(merged.map((item) => item.userId).sort()).toEqual(['friend', 'public']);
    expect(merged.find((item) => item.userId === 'friend')?.latitude).toBe(4.71);
  });

  it('clusters only when the map is zoomed out', () => {
    const near = clusterPeople([person('a', 4.71), person('b', 4.712)], 0.05);
    expect(near.every((item) => item.kind === 'person')).toBe(true);
    const far = clusterPeople([person('a', 4.71), person('b', 4.712)], 0.4);
    expect(far).toHaveLength(1);
    expect(far[0]?.kind).toBe('cluster');
  });

  it('rounds the last-seen label and keeps a foreground interval', () => {
    const now = Date.parse('2026-10-27T15:10:00Z');
    expect(seenLabel(true, null, now)).toBe('Ahora');
    expect(seenLabel(false, '2026-10-27T15:05:00Z', now)).toBe('Hace 5 min');
    expect(emitIntervalSeconds(null)).toBe(20);
    expect(emitIntervalSeconds({ locationEmitIntervalSeconds: 45 })).toBe(45);
    expect(emitIntervalSeconds({ locationEmitIntervalSeconds: 1 })).toBe(20);
  });
});
