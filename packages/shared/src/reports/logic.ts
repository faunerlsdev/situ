import type { AppConfig, MapItem, MediaLimitsView, ReportAuthor, ReportCatalog, ReportCategory } from './types';

/** Compresión local. El servidor no publica el ancho ni la calidad. */
export const imageUploadDefaults = {
  imageMaxEdge: 2048,
  imageQuality: 0.8,
} as const;

/** Respaldo si /app-config no responde y no hay caché. */
export const reportMediaFallbacks = {
  maxImages: 5,
  maxVideos: 1,
  maxVideoSeconds: 60,
  maxImageBytes: 10 * 1024 * 1024,
  maxVideoBytes: 100 * 1024 * 1024,
  ...imageUploadDefaults,
} as const;

export const panelCopyFallbacks = {
  browse: 'Quiero chismosear',
  report: 'Quiero reportar algo',
} as const;

export function asNumber(value: number | string | null | undefined): number {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value;
  }

  const parsed = typeof value === 'string' ? Number(value) : Number.NaN;
  return Number.isFinite(parsed) ? parsed : 0;
}

export function mediaLimitsOf(config: AppConfig | null | undefined) {
  if (!config) {
    return reportMediaFallbacks;
  }

  return {
    maxImages: asNumber(config.mediaLimits.maxImages),
    maxVideos: asNumber(config.mediaLimits.maxVideos),
    maxVideoSeconds: asNumber(config.mediaLimits.maxVideoSeconds),
    maxImageBytes: asNumber(config.mediaLimits.maxImageBytes),
    maxVideoBytes: asNumber(config.mediaLimits.maxVideoBytes),
    ...imageUploadDefaults,
  };
}

export function panelCopyOf(config: AppConfig | null | undefined) {
  if (!config) {
    return panelCopyFallbacks;
  }

  return {
    browse: config.panelCopy.browse || panelCopyFallbacks.browse,
    report: config.panelCopy.report || panelCopyFallbacks.report,
  };
}

export function descriptionMaxLength(config: AppConfig | null | undefined): number {
  const value = config ? asNumber(config.reportDescriptionMaxLength) : 1000;
  return value > 0 ? value : 1000;
}

export type PublishBlock =
  | { kind: 'allowed' }
  | { kind: 'confirm-email' }
  | { kind: 'guardian' }
  | { kind: 'wait'; message: string };

export function publishBlock(canPublish: boolean, reason: string | null | undefined): PublishBlock {
  if (canPublish) {
    return { kind: 'allowed' };
  }

  if (reason === 'email_not_confirmed') {
    return { kind: 'confirm-email' };
  }

  if (reason === 'guardian_consent_pending') {
    return { kind: 'guardian' };
  }

  if (reason === 'suspended') {
    return { kind: 'wait', message: 'Tu cuenta está suspendida. No puedes publicar todavía.' };
  }

  if (reason === 'banned') {
    return { kind: 'wait', message: 'Tu cuenta está bloqueada. No puedes publicar.' };
  }

  return { kind: 'wait', message: 'No puedes publicar ahora.' };
}

export const legacyLocalProfileKey = 'situ.localProfile';

const months = [
  'enero',
  'febrero',
  'marzo',
  'abril',
  'mayo',
  'junio',
  'julio',
  'agosto',
  'septiembre',
  'octubre',
  'noviembre',
  'diciembre',
] as const;

export function zoomFromLongitudeDelta(longitudeDelta: number): number {
  if (!Number.isFinite(longitudeDelta) || longitudeDelta <= 0) {
    return 15;
  }

  const zoom = Math.log2(360 / longitudeDelta);
  return Math.max(1, Math.min(21, Math.round(zoom)));
}

export function relativeTime(iso: string, now = Date.now()): string {
  const then = Date.parse(iso);
  if (Number.isNaN(then)) {
    return '';
  }

  const minutes = Math.round(Math.max(0, now - then) / 60_000);
  if (minutes < 1) {
    return 'hace un momento';
  }

  if (minutes < 60) {
    return minutes === 1 ? 'hace 1 min' : `hace ${minutes} min`;
  }

  const hours = Math.round(minutes / 60);
  if (hours < 24) {
    return hours === 1 ? 'hace 1 h' : `hace ${hours} h`;
  }

  const days = Math.round(hours / 24);
  return days === 1 ? 'ayer' : `hace ${days} días`;
}

export function formatDistance(meters: number | null): string | null {
  if (meters === null || !Number.isFinite(meters) || meters < 0) {
    return null;
  }

  if (meters < 1000) {
    return `a ${Math.round(meters)} m`;
  }

  const kilometers = meters / 1000;
  const digits = kilometers >= 10 ? 0 : 1;
  return `a ${kilometers.toLocaleString('es-CO', { minimumFractionDigits: digits, maximumFractionDigits: digits })} km`;
}

export function yearOrNull(value: number | string | null | undefined): number | null {
  if (value === null || value === undefined) {
    return null;
  }

  const parsed = asNumber(value);
  return parsed > 0 ? parsed : null;
}

export function memberSinceLabel(year: number | null, month: number | null): string | null {
  if (year === null || month === null || month < 1 || month > 12) {
    return null;
  }

  return `Miembro desde ${months[month - 1]} de ${year}`;
}

export type AuthorPresentation = {
  name: string;
  memberSince: string | null;
  anonymous: boolean;
  profileUrl: null;
  canAddFriend: false;
};

export function presentAuthor(author: ReportAuthor | null): AuthorPresentation {
  if (author === null) {
    return { name: 'Situero', memberSince: null, anonymous: false, profileUrl: null, canAddFriend: false };
  }

  if (author.isAnonymous || author.hideAuthorOnMap || author.userId === null) {
    return {
      name: author.displayName,
      memberSince: memberSinceLabel(yearOrNull(author.memberSinceYear), yearOrNull(author.memberSinceMonth)),
      anonymous: true,
      profileUrl: null,
      canAddFriend: false,
    };
  }

  return { name: author.displayName, memberSince: null, anonymous: false, profileUrl: null, canAddFriend: false };
}

export type MapMarkerModel =
  | {
      kind: 'point';
      key: string;
      id: string;
      latitude: number;
      longitude: number;
      categoryCode: string | null;
      createdAt: string | null;
      distanceMeters: number | null;
      author: AuthorPresentation;
      previewUrl: string | null;
      isAnonymous: boolean;
      isMine: boolean;
    }
  | {
      kind: 'cluster';
      key: string;
      latitude: number;
      longitude: number;
      count: number;
    };

export function toMapMarkers(items: MapItem[]): MapMarkerModel[] {
  const markers: MapMarkerModel[] = [];
  for (const item of items) {
    if (item.kind === 'cluster') {
      markers.push({
        kind: 'cluster',
        key: `cluster:${asNumber(item.latitude).toFixed(5)}:${asNumber(item.longitude).toFixed(5)}:${asNumber(item.count)}`,
        latitude: asNumber(item.latitude),
        longitude: asNumber(item.longitude),
        count: asNumber(item.count),
      });
      continue;
    }

    if (item.kind === 'point' && item.id) {
      markers.push({
        kind: 'point',
        key: item.id,
        id: item.id,
        latitude: asNumber(item.latitude),
        longitude: asNumber(item.longitude),
        categoryCode: item.categoryCode,
        createdAt: item.createdAt,
        distanceMeters: item.distanceMeters === null ? null : asNumber(item.distanceMeters),
        author: presentAuthor(item.author),
        previewUrl: item.previewUrl,
        isAnonymous: item.isAnonymous,
        isMine: item.isMine,
      });
    }
  }

  return markers;
}

export type CachedCatalog = {
  etag: string;
  body: ReportCatalog;
};

export function resolveCatalog(
  cached: CachedCatalog | null,
  response: { status: number; etag: string | null; body: ReportCatalog | null },
): { catalog: ReportCatalog | null; cache: CachedCatalog | null; unchanged: boolean } {
  if (response.status === 304) {
    return cached
      ? { catalog: cached.body, cache: cached, unchanged: true }
      : { catalog: null, cache: null, unchanged: true };
  }

  if (response.status === 200 && response.body) {
    const etag = response.etag ?? `"${response.body.catalogVersion}"`;
    const cache = { etag, body: response.body };
    return { catalog: response.body, cache, unchanged: false };
  }

  return cached
    ? { catalog: cached.body, cache: cached, unchanged: true }
    : { catalog: null, cache: null, unchanged: false };
}

export type CachedAppConfig = {
  etag: string;
  body: AppConfig;
};

export function resolveAppConfig(
  cached: CachedAppConfig | null,
  outcome:
    | { ok: true; status: number; etag: string | null; body: AppConfig | null }
    | { ok: false },
): { config: AppConfig | null; cache: CachedAppConfig | null } {
  if (!outcome.ok) {
    return { config: cached?.body ?? null, cache: cached };
  }

  if (outcome.status === 304) {
    return { config: cached?.body ?? null, cache: cached };
  }

  if (outcome.status === 200 && outcome.body) {
    const cache = { etag: outcome.etag ?? '"app-config"', body: outcome.body };
    return { config: outcome.body, cache };
  }

  return { config: cached?.body ?? null, cache: cached };
}

export function categoriesUnder(categories: ReportCategory[], parentId: string | null): ReportCategory[] {
  return categories
    .filter((category) => category.parentCategoryId === parentId)
    .sort((left, right) => asNumber(left.sortOrder) - asNumber(right.sortOrder) || left.name.localeCompare(right.name, 'es'));
}

export function categoryHasChildren(categories: ReportCategory[], categoryId: string): boolean {
  return categories.some((category) => category.parentCategoryId === categoryId);
}

export function withMinorVisibility(input: {
  isMinor: boolean;
  visibility: 'Public' | 'FriendsOnly';
  isAnonymous: boolean;
}): { visibility: 'Public' | 'FriendsOnly'; isAnonymous: boolean; explain: boolean } {
  if (input.isMinor && input.visibility === 'Public') {
    return { visibility: 'Public', isAnonymous: true, explain: true };
  }

  return { visibility: input.visibility, isAnonymous: input.isAnonymous, explain: false };
}

export function validateReportForm(input: {
  description: string;
  mediaRequirement: string;
  readyImages: number;
  readyVideos: number;
  maxLength?: number;
}): { description?: string; media?: string } {
  const errors: { description?: string; media?: string } = {};
  const text = input.description.trim();
  const maxLength = input.maxLength && input.maxLength > 0 ? input.maxLength : 1000;
  if (text.length === 0) {
    errors.description = 'Cuéntanos qué está pasando.';
  } else if (text.length > maxLength) {
    errors.description = `La descripción acepta hasta ${maxLength} caracteres.`;
  }

  if (input.mediaRequirement === 'PhotoRequired' && input.readyImages < 1) {
    errors.media = 'Esta categoría pide al menos una foto.';
  } else if (input.mediaRequirement === 'VideoRequired' && input.readyVideos < 1) {
    errors.media = 'Esta categoría pide un video.';
  } else if (input.mediaRequirement === 'PhotoOrVideoRequired' && input.readyImages + input.readyVideos < 1) {
    errors.media = 'Esta categoría pide al menos una foto o un video.';
  }

  return errors;
}

export function rejectedMediaReason(input: {
  kind: 'image' | 'video';
  mime: string;
  name: string;
  durationSeconds?: number | null;
  imageCount: number;
  videoCount: number;
  limits?: Pick<MediaLimitsView, 'maxImages' | 'maxVideos' | 'maxVideoSeconds'>;
}): string | null {
  const limits = {
    maxImages: asNumber(input.limits?.maxImages ?? reportMediaFallbacks.maxImages),
    maxVideos: asNumber(input.limits?.maxVideos ?? reportMediaFallbacks.maxVideos),
    maxVideoSeconds: asNumber(input.limits?.maxVideoSeconds ?? reportMediaFallbacks.maxVideoSeconds),
  };
  const mime = input.mime.toLowerCase();
  const name = input.name.toLowerCase();
  if (mime.includes('webm') || name.endsWith('.webm')) {
    return 'Ese formato no se puede subir. Usa una foto JPG, PNG o WebP, o un video MP4.';
  }

  if (input.kind === 'image') {
    const allowed = mime.includes('jpeg') || mime.includes('jpg') || mime.includes('png') || mime.includes('webp') || name.endsWith('.jpg') || name.endsWith('.jpeg') || name.endsWith('.png') || name.endsWith('.webp');
    if (!allowed) {
      return 'Esa foto tiene que ser JPG, PNG o WebP.';
    }

    if (input.imageCount >= limits.maxImages) {
      return 'Puedes subir hasta 5 fotos.';
    }
  }

  if (input.kind === 'video') {
    const allowed = mime.includes('mp4') || mime.includes('quicktime') || name.endsWith('.mp4') || name.endsWith('.mov');
    if (!allowed) {
      return 'El video tiene que ser MP4 o MOV.';
    }

    if (input.videoCount >= limits.maxVideos) {
      return 'Puedes subir un solo video.';
    }

    if (input.durationSeconds !== undefined && input.durationSeconds !== null && input.durationSeconds > limits.maxVideoSeconds) {
      return 'El video puede durar hasta 60 segundos.';
    }
  }

  return null;
}

export type UploadStatus = 'queued' | 'uploading' | 'ready' | 'failed' | 'cancelled';

export type UploadTask = {
  id: string;
  kind: 'image' | 'video';
  status: UploadStatus;
  progress: number;
  mediaId?: string;
  error?: string;
};

export type UploadAction =
  | { type: 'enqueue'; task: UploadTask }
  | { type: 'start'; id: string }
  | { type: 'progress'; id: string; progress: number }
  | { type: 'ready'; id: string; mediaId: string }
  | { type: 'fail'; id: string; error: string }
  | { type: 'cancel'; id: string }
  | { type: 'remove'; id: string }
  | { type: 'retry'; id: string };

export function reduceUploads(tasks: UploadTask[], action: UploadAction): UploadTask[] {
  if (action.type === 'enqueue') {
    return [...tasks, action.task];
  }

  if (action.type === 'remove') {
    return tasks.filter((task) => task.id !== action.id);
  }

  return tasks.map((task) => {
    if (task.id !== action.id) {
      return task;
    }

    if (action.type === 'start') {
      return { ...task, status: 'uploading', progress: 0, error: undefined };
    }

    if (action.type === 'progress') {
      return task.status === 'cancelled' ? task : { ...task, status: 'uploading', progress: action.progress };
    }

    if (action.type === 'ready') {
      return { ...task, status: 'ready', progress: 100, mediaId: action.mediaId, error: undefined };
    }

    if (action.type === 'fail') {
      return task.status === 'cancelled' ? task : { ...task, status: 'failed', error: action.error };
    }

    if (action.type === 'cancel') {
      return { ...task, status: 'cancelled' };
    }

    if (task.status === 'failed' || task.status === 'cancelled') {
      return { ...task, status: 'queued', progress: 0, error: undefined, mediaId: undefined };
    }

    return task;
  });
}

export function nextQueued(tasks: UploadTask[]): UploadTask | undefined {
  if (tasks.some((task) => task.status === 'uploading')) {
    return undefined;
  }

  return tasks.find((task) => task.status === 'queued');
}

export function retryAfterLabel(iso: string | undefined): string {
  if (!iso) {
    return 'Llegaste al límite de hoy. Inténtalo más tarde.';
  }

  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return 'Llegaste al límite de hoy. Inténtalo más tarde.';
  }

  const when = date.toLocaleString('es-CO', { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' });
  return `Llegaste al límite. Podrás volver a publicar el ${when}`;
}

export function reportStatusLabel(status: string): string {
  if (status === 'Active') {
    return 'Activo';
  }

  if (status === 'Expired') {
    return 'Vencido';
  }

  if (status === 'Removed') {
    return 'Retirado';
  }

  if (status === 'UnderReview') {
    return 'En revisión';
  }

  return status;
}

export function absoluteMediaUrl(baseUrl: string, path: string | null): string | null {
  if (!path) {
    return null;
  }

  if (path.startsWith('http://') || path.startsWith('https://')) {
    return path;
  }

  return `${baseUrl.replace(/\/$/, '')}${path.startsWith('/') ? path : `/${path}`}`;
}
