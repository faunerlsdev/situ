import { describe, expect, it } from 'vitest';

import {
  nextQueued,
  mediaLimitsOf,
  panelCopyOf,
  presentAuthor,
  publishBlock,
  reduceUploads,
  rejectedMediaReason,
  relativeTime,
  formatDistance,
  reportStatusLabel,
  resolveAppConfig,
  resolveCatalog,
  retryAfterLabel,
  toMapMarkers,
  validateReportForm,
  withMinorVisibility,
  zoomFromLongitudeDelta,
} from './logic';
import type { AppConfig, MapItem, ReportAuthor, ReportCatalog, SessionProfile } from './types';

const now = Date.parse('2026-10-24T15:12:00Z');

describe('zoomFromLongitudeDelta', () => {
  it('turns the visible longitude span into a map zoom', () => {
    expect(zoomFromLongitudeDelta(360 / 2 ** 12)).toBe(12);
    expect(zoomFromLongitudeDelta(0)).toBe(15);
    expect(zoomFromLongitudeDelta(400)).toBe(1);
  });
});

describe('relative time and distance', () => {
  it('formats a recent moment and a distance', () => {
    expect(relativeTime('2026-10-24T15:00:00Z', now)).toBe('hace 12 min');
    expect(relativeTime('2026-10-23T15:12:00Z', now)).toBe('ayer');
    expect(formatDistance(350)).toBe('a 350 m');
    expect(formatDistance(1500)).toBe('a 1,5 km');
    expect(formatDistance(null)).toBeNull();
  });
});

describe('map markers', () => {
  it('keeps points and clusters without clustering again', () => {
    const markers = toMapMarkers([
      point('a', 'accidente_transito'),
      { ...point('ignored', 'hurto'), kind: 'cluster', id: null, count: 4 },
    ]);
    expect(markers.map((marker) => marker.kind)).toEqual(['point', 'cluster']);
    expect(markers[0]).toMatchObject({ id: 'a', categoryCode: 'accidente_transito' });
    expect(markers[1]).toMatchObject({ kind: 'cluster', count: 4 });
  });
});

describe('anonymous author', () => {
  it('never exposes a profile, a friend action or the user id', () => {
    const author = presentAuthor(anonymousAuthor());
    expect(author.anonymous).toBe(true);
    expect(author.name).toBe('Situero anónimo');
    expect(author.memberSince).toBe('Miembro desde octubre de 2026');
    expect(author.profileUrl).toBeNull();
    expect(author.canAddFriend).toBe(false);
    expect(Object.keys(author)).not.toContain('userId');
    expect(JSON.stringify(author)).not.toContain('user-1');
  });
});

describe('minor visibility', () => {
  it('forces a public report from a minor to stay anonymous', () => {
    expect(withMinorVisibility({ isMinor: true, visibility: 'Public', isAnonymous: false })).toEqual({
      visibility: 'Public',
      isAnonymous: true,
      explain: true,
    });
    expect(withMinorVisibility({ isMinor: true, visibility: 'FriendsOnly', isAnonymous: false }).isAnonymous).toBe(false);
    expect(withMinorVisibility({ isMinor: false, visibility: 'Public', isAnonymous: false }).isAnonymous).toBe(false);
  });
});

describe('catalog cache', () => {
  it('keeps the stored catalog on 304 and replaces it on 200', () => {
    const cached = { etag: '"1"', body: catalog(1) };
    expect(resolveCatalog(cached, { status: 304, etag: '"1"', body: null })).toEqual({
      catalog: cached.body,
      cache: cached,
      unchanged: true,
    });
    const next = resolveCatalog(cached, { status: 200, etag: '"2"', body: catalog(2) });
    expect(next.unchanged).toBe(false);
    expect(next.catalog?.catalogVersion).toBe(2);
    expect(next.cache?.etag).toBe('"2"');
  });
});

describe('upload queue', () => {
  it('reports progress, cancels and retries without losing the queue order', () => {
    let tasks = reduceUploads([], { type: 'enqueue', task: { id: 'a', kind: 'image', status: 'queued', progress: 0 } });
    tasks = reduceUploads(tasks, { type: 'enqueue', task: { id: 'b', kind: 'video', status: 'queued', progress: 0 } });
    expect(nextQueued(tasks)?.id).toBe('a');
    tasks = reduceUploads(tasks, { type: 'start', id: 'a' });
    tasks = reduceUploads(tasks, { type: 'progress', id: 'a', progress: 40 });
    expect(tasks[0]).toMatchObject({ status: 'uploading', progress: 40 });
    expect(nextQueued(tasks)).toBeUndefined();
    tasks = reduceUploads(tasks, { type: 'cancel', id: 'a' });
    tasks = reduceUploads(tasks, { type: 'progress', id: 'a', progress: 90 });
    expect(tasks[0]?.status).toBe('cancelled');
    tasks = reduceUploads(tasks, { type: 'fail', id: 'b', error: 'Sin conexión' });
    tasks = reduceUploads(tasks, { type: 'retry', id: 'b' });
    expect(tasks[1]).toMatchObject({ status: 'queued', progress: 0 });
    expect(nextQueued(tasks)?.id).toBe('b');
  });
});

describe('session profile', () => {
  it('forces a public report from a minor to stay anonymous using isMinor from /me', () => {
    const me = {
      isMinor: true,
      canPublish: false,
      publishBlockedReason: 'guardian_consent_pending',
    } satisfies Pick<SessionProfile, 'isMinor' | 'canPublish' | 'publishBlockedReason'>;

    expect(withMinorVisibility({ isMinor: me.isMinor, visibility: 'Public', isAnonymous: false })).toEqual({
      visibility: 'Public',
      isAnonymous: true,
      explain: true,
    });
    expect(JSON.stringify(me)).not.toContain('dateOfBirth');
    expect(publishBlock(me.canPublish, me.publishBlockedReason).kind).toBe('guardian');
  });

  it('maps each publish block to the matching next step', () => {
    expect(publishBlock(true, null)).toEqual({ kind: 'allowed' });
    expect(publishBlock(false, 'email_not_confirmed')).toEqual({ kind: 'confirm-email' });
    expect(publishBlock(false, 'suspended').kind).toBe('wait');
    expect(publishBlock(false, 'banned').kind).toBe('wait');
  });
});

describe('app config', () => {
  const config = {
    panelCopy: { browse: 'Ver el mapa', report: 'Contar algo' },
    mediaLimits: { maxImages: 2, maxVideos: 1, maxVideoSeconds: 30, maxImageBytes: 1, maxVideoBytes: 2 },
    reportDescriptionMaxLength: 800,
    confirmationMaxDistanceMeters: 500,
    anonymityDisplayName: 'Anónimo',
    versions: {
      ios: { minSupported: '0.1.0', latest: '0.1.0' },
      android: { minSupported: '0.1.0', latest: '0.1.0' },
      web: { minSupported: '0.1.0', latest: '0.1.0' },
    },
    legal: {
      termsUrl: 'https://somossitu.com/terminos',
      privacyPolicyUrl: 'https://somossitu.com/privacidad',
      supportUrl: 'https://somossitu.com/soporte',
    },
    panelCopyByCountry: [],
    mapRealtime: { cellDegrees: 0.05, maxCells: 16, coalesceIntervalMilliseconds: 1000, subscribeCallsPerMinute: 20 },
    features: { socialTabs: false },
    webSession: { refreshDays: 30, rememberDays: 90 },
    phoneRequiredForMinors: true,
    captcha: { provider: 'Turnstile', siteKey: '' },
  } satisfies AppConfig;

  it('uses the response when the network answers and the cache when it does not', () => {
    const online = resolveAppConfig(null, { ok: true, status: 200, etag: '"1"', body: config });
    expect(panelCopyOf(online.config)).toEqual({ browse: 'Ver el mapa', report: 'Contar algo' });
    expect(mediaLimitsOf(online.config).maxImages).toBe(2);
    expect(mediaLimitsOf(online.config).maxVideoSeconds).toBe(30);

    const cached = { etag: '"1"', body: config };
    expect(resolveAppConfig(cached, { ok: false }).config).toBe(config);
    expect(resolveAppConfig(cached, { ok: true, status: 304, etag: '"1"', body: null }).config).toBe(config);
    expect(panelCopyOf(null).browse).toBe('Quiero chismosear');
    expect(mediaLimitsOf(null).maxImages).toBe(5);
  });
});

describe('report form', () => {
  it('requires a description and media when the category asks for it', () => {
    expect(validateReportForm({ description: '  ', mediaRequirement: 'None', readyImages: 0, readyVideos: 0 }).description).toBeTruthy();
    expect(validateReportForm({ description: 'Hay un choque.', mediaRequirement: 'PhotoOrVideoRequired', readyImages: 0, readyVideos: 0 }).media).toBeTruthy();
    expect(validateReportForm({ description: 'Hay un choque.', mediaRequirement: 'PhotoOrVideoRequired', readyImages: 1, readyVideos: 0 })).toEqual({});
    expect(rejectedMediaReason({ kind: 'video', mime: 'video/webm', name: 'clip.webm', imageCount: 0, videoCount: 0 })).toMatch(/MP4/);
  });
});

describe('report status', () => {
  it('uses the Spanish labels', () => {
    expect(reportStatusLabel('Active')).toBe('Activo');
    expect(reportStatusLabel('Expired')).toBe('Vencido');
    expect(reportStatusLabel('Removed')).toBe('Retirado');
    expect(reportStatusLabel('UnderReview')).toBe('En revisión');
  });
});

describe('retry after', () => {
  it('includes the moment the limit lifts', () => {
    const when = new Date('2026-10-25T18:00:00Z');
    expect(retryAfterLabel(when.toISOString())).toContain(String(when.getDate()));
    expect(retryAfterLabel(undefined)).toContain('límite');
  });
});

function anonymousAuthor(): ReportAuthor {
  return {
    userId: null,
    displayName: 'Situero anónimo',
    avatarUrl: null,
    memberSinceYear: 2026,
    memberSinceMonth: 10,
    isAnonymous: true,
    hideAuthorOnMap: true,
  };
}

function point(id: string, code: string): MapItem {
  return {
    kind: 'point',
    id,
    count: 1,
    latitude: 4.71,
    longitude: -74.07,
    categoryId: 'cat',
    categoryCode: code,
    createdAt: '2026-10-24T15:00:00Z',
    distanceMeters: 120,
    author: anonymousAuthor(),
    stillThereCount: 0,
    goneNowCount: 0,
    commentCount: 0,
    previewUrl: null,
    isAnonymous: true,
    hideAuthorOnMap: true,
    isMine: false,
  };
}

function catalog(version: number): ReportCatalog {
  return { catalogVersion: version, locale: 'es-CO', adultCatalog: true, cityId: null, categories: [] };
}
