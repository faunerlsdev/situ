import { describe, expect, it } from 'vitest';

import {
  anonymousBlockLeaks,
  blockEndpoint,
  blockRowLabel,
  classifyWriteError,
  composerAnonymity,
  rollbackReaction,
  toggleReaction,
} from './interaction';

describe('composer anonymity for minors', () => {
  it('locks the switch on a public report', () => {
    expect(composerAnonymity({ isMinor: true, visibility: 'Public', chosen: false })).toEqual({
      anonymous: true,
      locked: true,
    });
  });

  it('lets a minor choose on a friends-only report', () => {
    expect(composerAnonymity({ isMinor: true, visibility: 'FriendsOnly', chosen: false })).toEqual({
      anonymous: false,
      locked: false,
    });
  });

  it('lets an adult choose on a public report', () => {
    expect(composerAnonymity({ isMinor: false, visibility: 'Public', chosen: true })).toEqual({
      anonymous: true,
      locked: false,
    });
  });
});

describe('write error mapping', () => {
  it('keeps the daily anonymous limit next to the composer', () => {
    expect(classifyWriteError({ apiCode: 'comment.anonymous_daily_limit' })).toEqual({
      kind: 'inline',
      message: 'Llegaste al límite de comentarios anónimos de hoy.',
    });
  });

  it('counts down a rate limit', () => {
    const failure = classifyWriteError(
      { code: 'RATE_LIMIT', apiCode: 'auth.rate_limited', retryAfter: '2026-10-24T15:00:30.000Z' },
      Date.parse('2026-10-24T15:00:00.000Z'),
    );
    expect(failure).toEqual({ kind: 'countdown', message: 'Demasiados intentos. Espera 30 s.' });
  });

  it('explains a duplicate complaint without asking to retry', () => {
    const failure = classifyWriteError({ apiCode: 'complaint.duplicate' });
    expect(failure.kind).toBe('duplicate');
    if (failure.kind === 'duplicate') {
      expect(failure.message).toContain('Ya enviaste una denuncia');
    }
  });

  it('treats a network failure as retryable without a new message', () => {
    expect(classifyWriteError({ code: 'NETWORK' })).toEqual({ kind: 'network' });
  });
});

describe('optimistic reactions', () => {
  it('adds a like and rolls it back', () => {
    const previous = { counts: [], mine: null };
    const next = toggleReaction(previous, 'Like');
    expect(next).toEqual({ counts: [{ type: 'Like', count: 1 }], mine: 'Like' });
    expect(rollbackReaction(previous)).toEqual(previous);
    expect(toggleReaction(next, 'Like')).toEqual({ counts: [], mine: null });
  });

  it('replaces the previous reaction', () => {
    const next = toggleReaction({ counts: [{ type: 'Like', count: 2 }], mine: 'Like' }, 'Love');
    expect(next.mine).toBe('Love');
    expect(next.counts).toEqual([
      { type: 'Like', count: 1 },
      { type: 'Love', count: 1 },
    ]);
  });
});

describe('block privacy', () => {
  it('uses block-author only for an anonymous author', () => {
    expect(blockEndpoint(true)).toBe('block-author');
    expect(blockEndpoint(false)).toBe('blocks');
  });

  it('formats the origin without a name', () => {
    expect(blockRowLabel({
      scope: 'AnonymousContentOnly',
      displayName: 'Usuario anónimo',
      originLabel: 'desde un comentario del 12 oct',
    })).toBe('Usuario anónimo · desde un comentario del 12 oct');
  });

  it('treats a name or user id on an anonymous block as a leak', () => {
    expect(anonymousBlockLeaks({ scope: 'AnonymousContentOnly', userId: null, displayName: 'Usuario anónimo' })).toBe(false);
    expect(anonymousBlockLeaks({ scope: 'AnonymousContentOnly', userId: '018f', displayName: 'Usuario anónimo' })).toBe(true);
    expect(anonymousBlockLeaks({ scope: 'AnonymousContentOnly', userId: null, displayName: 'Ana' })).toBe(true);
    expect(anonymousBlockLeaks({ scope: 'Full', userId: '018f', displayName: 'Ana' })).toBe(false);
  });
});
