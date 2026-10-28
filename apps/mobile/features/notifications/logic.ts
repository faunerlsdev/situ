/** Same id the API sends as channelId. */
export const ANDROID_ALERT_CHANNEL_ID = 'zone-alerts';

export const ALERT_MOVE_METERS = 500;
export const ALERT_MIN_INTERVAL_MS = 5 * 60 * 1000;
export const RADIUS_STEPS = [500, 1000, 2000, 3000, 5000] as const;

export type AlertSyncGate = {
  pushEnabled: boolean;
  notifyNearbyReports: boolean;
  locationGranted: boolean;
  appActive: boolean;
  sessionHasDevice: boolean;
  minorBlocked: boolean;
};

export function alertSyncEnabled(gate: AlertSyncGate): boolean {
  return gate.pushEnabled && gate.notifyNearbyReports && gate.locationGranted && gate.appActive && gate.sessionHasDevice && !gate.minorBlocked;
}

export type AlertSendTrigger = 'open' | 'move';

export function shouldSendAlertLocation(input: {
  enabled: boolean;
  now: number;
  lastSentAt: number | null;
  lastLatitude: number | null;
  lastLongitude: number | null;
  latitude: number;
  longitude: number;
  trigger: AlertSendTrigger;
}): boolean {
  if (!input.enabled) {
    return false;
  }

  if (input.lastSentAt !== null && input.now - input.lastSentAt < ALERT_MIN_INTERVAL_MS) {
    return false;
  }

  if (input.trigger === 'open' || input.lastSentAt === null) {
    return true;
  }

  if (input.lastLatitude === null || input.lastLongitude === null) {
    return true;
  }

  return distanceMeters(input.lastLatitude, input.lastLongitude, input.latitude, input.longitude) >= ALERT_MOVE_METERS;
}

export function distanceMeters(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const toRad = (value: number) => (value * Math.PI) / 180;
  const dLat = toRad(lat2 - lat1);
  const dLon = toRad(lon2 - lon1);
  const a = Math.sin(dLat / 2) ** 2 + Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.sin(dLon / 2) ** 2;
  return 2 * 6_371_000 * Math.asin(Math.min(1, Math.sqrt(a)));
}

export type PushPermission = 'undetermined' | 'granted' | 'denied';

export function pushRegistrationPlan(input: {
  physicalDevice: boolean;
  permission: PushPermission;
  projectId: string | null;
  dismissed: boolean;
}): 'simulator' | 'denied' | 'missing-project' | 'ask' | 'later' | 'register' {
  if (!input.physicalDevice) {
    return 'simulator';
  }

  if (!input.projectId) {
    return 'missing-project';
  }

  if (input.permission === 'denied') {
    return 'denied';
  }

  if (input.permission === 'undetermined') {
    return input.dismissed ? 'later' : 'ask';
  }

  return 'register';
}

export type NotificationOpen =
  | { kind: 'report'; reportId: string; focusComments: boolean; notificationId: string | null }
  | { kind: 'live'; publicId: string; notificationId: string | null }
  | { kind: 'unavailable' }
  | { kind: 'inbox'; notificationId: string | null };

export function notificationOpenTarget(input: {
  available: boolean;
  reportId: string | null;
  targetType: string | null;
  liveId?: string | null;
  notificationId?: string | null;
}): NotificationOpen {
  if (!input.available) {
    return { kind: 'unavailable' };
  }

  if (input.targetType === 'LiveStream') {
    if (!input.liveId) {
      return { kind: 'inbox', notificationId: input.notificationId ?? null };
    }
    return { kind: 'live', publicId: input.liveId, notificationId: input.notificationId ?? null };
  }

  if (!input.reportId) {
    return { kind: 'inbox', notificationId: input.notificationId ?? null };
  }

  return {
    kind: 'report',
    reportId: input.reportId,
    focusComments: input.targetType === 'Comment',
    notificationId: input.notificationId ?? null,
  };
}

export function pushDataTarget(data: Record<string, unknown> | null | undefined): NotificationOpen {
  if (!data) {
    return { kind: 'inbox', notificationId: null };
  }

  const notificationId = text(data.notificationId);
  const type = text(data.type);
  const reportId = text(data.reportId);
  const targetType = text(data.targetType);
  if (type === 'FriendLiveStarted' || type === 'FriendLiveEnded' || type === 'NearbyLiveStarted' || targetType === 'LiveStream') {
    const publicId = text(data.targetId);
    if (!publicId) {
      return { kind: 'inbox', notificationId };
    }
    return { kind: 'live', publicId, notificationId };
  }
  if (type === 'Summary' || !reportId) {
    return { kind: 'inbox', notificationId };
  }

  return {
    kind: 'report',
    reportId,
    focusComments: type === 'Comment' || targetType === 'Comment',
    notificationId,
  };
}

function text(value: unknown): string | null {
  return typeof value === 'string' && value.length > 0 ? value : null;
}

export function reportOpenFailure(apiCode: string | undefined): 'unavailable' | 'other' {
  return apiCode === 'report.not_available' ? 'unavailable' : 'other';
}

export function alertCleanupAction(input: {
  signingOut: boolean;
  nearbyEnabled: boolean;
  locationGranted: boolean;
  minorBlocked: boolean;
}): 'push-token' | 'alert-location' | 'none' {
  if (input.signingOut) {
    return 'push-token';
  }

  if (!input.nearbyEnabled || !input.locationGranted || input.minorBlocked) {
    return 'alert-location';
  }

  return 'none';
}

export function notificationFailure(apiCode: string | undefined): 'device-required' | 'minor' | 'silent' | 'show' {
  if (apiCode === 'notification.device_required') {
    return 'device-required';
  }

  if (apiCode === 'notification.proximity_not_allowed_minor') {
    return 'minor';
  }

  if (apiCode === 'notification.alert_location_limited' || apiCode === 'notification.proximity_disabled') {
    return 'silent';
  }

  return 'show';
}

/** What may be written when an alert location fails. Coordinates never belong here. */
export function alertFailureRecord(code: string): { event: 'alert-location'; code: string } {
  return { event: 'alert-location', code };
}

export function radiusLabel(meters: number): string {
  if (meters < 1000) {
    return `${meters} m`;
  }

  const km = meters / 1000;
  return Number.isInteger(km) ? `${km} km` : `${km} km`;
}

export function nearestRadius(meters: number): (typeof RADIUS_STEPS)[number] {
  return RADIUS_STEPS.reduce((best, step) => (Math.abs(step - meters) < Math.abs(best - meters) ? step : best));
}

export function quietClock(value: string | null): { hour: number; minute: number } | null {
  if (!value) {
    return null;
  }

  const match = /^(\d{2}):(\d{2})/.exec(value);
  if (!match) {
    return null;
  }

  const hour = Number(match[1]);
  const minute = Number(match[2]);
  if (hour > 23 || minute > 59) {
    return null;
  }

  return { hour, minute };
}

export function formatQuiet(hour: number, minute: number): string {
  return `${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}`;
}

export function shouldRetryPush(code: string, attempt: number): boolean {
  return attempt < 3 && (code === 'NETWORK' || code === 'TIMEOUT');
}

let alertPaused = false;
let minorProximityBlocked = false;

export function pauseAlertLocation(): void {
  alertPaused = true;
}

export function resumeAlertLocation(): void {
  alertPaused = false;
  minorProximityBlocked = false;
}

export function alertLocationPaused(): boolean {
  return alertPaused;
}

export function markMinorProximityBlocked(): void {
  minorProximityBlocked = true;
}

export function isMinorProximityBlocked(): boolean {
  return minorProximityBlocked;
}

export type PushAvailability = 'unknown' | 'ready' | 'denied' | 'unavailable';

let pushAvailability: PushAvailability = 'unknown';
const pushListeners = new Set<() => void>();

export function readPushAvailability(): PushAvailability {
  return pushAvailability;
}

export function setPushAvailability(next: PushAvailability): void {
  pushAvailability = next;
  pushListeners.forEach((listener) => listener());
}

export function subscribePushAvailability(listener: () => void): () => void {
  pushListeners.add(listener);
  return () => {
    pushListeners.delete(listener);
  };
}

let refreshPush: (() => void) | null = null;

export function registerPushRefresh(refresh: () => void): () => void {
  refreshPush = refresh;
  return () => {
    if (refreshPush === refresh) {
      refreshPush = null;
    }
  };
}

export function refreshPushRegistration(): void {
  refreshPush?.();
}
