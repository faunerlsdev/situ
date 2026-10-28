import { describe, expect, it } from 'vitest';

import { friendActionLabel, pendingRequestCount } from './logic';

describe('friend actions', () => {
  it('labels the search button from the relationship and counts incoming requests', () => {
    expect(friendActionLabel(undefined)).toEqual({ label: 'Agregar', enabled: true });
    expect(friendActionLabel('none')).toEqual({ label: 'Agregar', enabled: true });
    expect(friendActionLabel('outgoing').label).toBe('Solicitud enviada');
    expect(friendActionLabel('friends')).toEqual({ label: 'Ya son amigos', enabled: false });
    expect(friendActionLabel('incoming').enabled).toBe(false);
    expect(pendingRequestCount(undefined)).toBe(0);
    expect(pendingRequestCount([1, 2])).toBe(2);
  });
});
