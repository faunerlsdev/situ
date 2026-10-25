import { describe, expect, it } from 'vitest';

import {
  ALERT_MIN_INTERVAL_MS,
  alertFailureRecord,
  alertSyncEnabled,
  nearestRadius,
  notificationFailure,
  shouldRetryPush,
  alertCleanupAction,
  notificationOpenTarget,
  pushDataTarget,
  reportOpenFailure,
  pushRegistrationPlan,
  shouldSendAlertLocation,
} from './logic';

const baseGate = {
  pushEnabled: true,
  notifyNearbyReports: true,
  locationGranted: true,
  appActive: true,
  sessionHasDevice: true,
  minorBlocked: false,
};

describe('alert location', () => {
  it('stays off unless every condition is on', () => {
    expect(alertSyncEnabled(baseGate)).toBe(true);
    expect(alertSyncEnabled({ ...baseGate, pushEnabled: false })).toBe(false);
    expect(alertSyncEnabled({ ...baseGate, notifyNearbyReports: false })).toBe(false);
    expect(alertSyncEnabled({ ...baseGate, locationGranted: false })).toBe(false);
    expect(alertSyncEnabled({ ...baseGate, appActive: false })).toBe(false);
    expect(alertSyncEnabled({ ...baseGate, sessionHasDevice: false })).toBe(false);
    expect(alertSyncEnabled({ ...baseGate, minorBlocked: true })).toBe(false);
  });

  it('sends on open, after a move, and never more often than five minutes', () => {
    const here = { latitude: 4.71, longitude: -74.07 };
    expect(shouldSendAlertLocation({ enabled: false, now: 0, lastSentAt: null, lastLatitude: null, lastLongitude: null, ...here, trigger: 'open' })).toBe(false);
    expect(shouldSendAlertLocation({ enabled: true, now: 0, lastSentAt: null, lastLatitude: null, lastLongitude: null, ...here, trigger: 'open' })).toBe(true);
    expect(shouldSendAlertLocation({
      enabled: true,
      now: 1_000,
      lastSentAt: 0,
      lastLatitude: here.latitude,
      lastLongitude: here.longitude,
      latitude: 4.72,
      longitude: -74.07,
      trigger: 'move',
    })).toBe(false);
    expect(shouldSendAlertLocation({
      enabled: true,
      now: ALERT_MIN_INTERVAL_MS,
      lastSentAt: 0,
      lastLatitude: here.latitude,
      lastLongitude: here.longitude,
      latitude: here.latitude + 0.01,
      longitude: here.longitude,
      trigger: 'move',
    })).toBe(true);
    expect(shouldSendAlertLocation({
      enabled: true,
      now: ALERT_MIN_INTERVAL_MS,
      lastSentAt: 0,
      lastLatitude: here.latitude,
      lastLongitude: here.longitude,
      ...here,
      trigger: 'move',
    })).toBe(false);
    expect(shouldSendAlertLocation({
      enabled: true,
      now: ALERT_MIN_INTERVAL_MS,
      lastSentAt: 0,
      lastLatitude: here.latitude,
      lastLongitude: here.longitude,
      ...here,
      trigger: 'open',
    })).toBe(true);
  });

  it('never puts coordinates in the failure record', () => {
    const record = alertFailureRecord('network');
    const text = JSON.stringify(record);
    expect(record).toEqual({ event: 'alert-location', code: 'network' });
    expect(text).not.toMatch(/latitude|longitude|4\.71|-74/);
  });
});

describe('opening a notification', () => {
  it('opens an available report, comments, or nothing', () => {
    expect(notificationOpenTarget({ available: true, reportId: 'r1', targetType: 'Report' })).toEqual({
      kind: 'report',
      reportId: 'r1',
      focusComments: false,
      notificationId: null,
    });
    expect(notificationOpenTarget({ available: true, reportId: 'r1', targetType: 'Comment', notificationId: 'n1' })).toEqual({
      kind: 'report',
      reportId: 'r1',
      focusComments: true,
      notificationId: 'n1',
    });
    expect(notificationOpenTarget({ available: false, reportId: 'r1', targetType: 'Report' })).toEqual({ kind: 'unavailable' });
    expect(notificationOpenTarget({ available: true, reportId: null, targetType: 'Report' })).toEqual({ kind: 'inbox', notificationId: null });
  });

  it('resolves a system push from its data', () => {
    expect(pushDataTarget({
      notificationId: 'n1',
      type: 'NearbyReport',
      targetType: 'Report',
      targetId: 'r1',
      reportId: 'r1',
    })).toEqual({ kind: 'report', reportId: 'r1', focusComments: false, notificationId: 'n1' });
    expect(pushDataTarget({
      notificationId: 'n2',
      type: 'Comment',
      targetType: 'Comment',
      targetId: 'c1',
      reportId: 'r1',
    })).toEqual({ kind: 'report', reportId: 'r1', focusComments: true, notificationId: 'n2' });
    expect(pushDataTarget({ notificationId: 'n3', type: 'Summary' })).toEqual({ kind: 'inbox', notificationId: 'n3' });
    expect(pushDataTarget(null)).toEqual({ kind: 'inbox', notificationId: null });
    expect(pushDataTarget({ notification: true })).toEqual({ kind: 'inbox', notificationId: null });
    expect(pushDataTarget({ type: 'NearbyReport', notificationId: 'n4' })).toEqual({ kind: 'inbox', notificationId: 'n4' });
    expect(pushDataTarget({ reportId: '', type: 'Comment' })).toEqual({ kind: 'inbox', notificationId: null });
  });

  it('treats report.not_available as a closed report', () => {
    expect(reportOpenFailure('report.not_available')).toBe('unavailable');
    expect(reportOpenFailure('report.not_found')).toBe('other');
    expect(reportOpenFailure(undefined)).toBe('other');
  });
});

describe('alert location cleanup', () => {
  const open = { signingOut: false, nearbyEnabled: true, locationGranted: true, minorBlocked: false };

  it('deletes only the location, or the push token when signing out', () => {
    expect(alertCleanupAction(open)).toBe('none');
    expect(alertCleanupAction({ ...open, nearbyEnabled: false })).toBe('alert-location');
    expect(alertCleanupAction({ ...open, locationGranted: false })).toBe('alert-location');
    expect(alertCleanupAction({ ...open, minorBlocked: true })).toBe('alert-location');
    expect(alertCleanupAction({ ...open, signingOut: true, nearbyEnabled: false })).toBe('push-token');
  });
});

describe('push registration and API codes', () => {
  it('registers only on a physical device with permission and a project id', () => {
    expect(pushRegistrationPlan({ physicalDevice: false, permission: 'granted', projectId: 'p', dismissed: false })).toBe('simulator');
    expect(pushRegistrationPlan({ physicalDevice: true, permission: 'granted', projectId: null, dismissed: false })).toBe('missing-project');
    expect(pushRegistrationPlan({ physicalDevice: true, permission: 'denied', projectId: 'p', dismissed: false })).toBe('denied');
    expect(pushRegistrationPlan({ physicalDevice: true, permission: 'undetermined', projectId: 'p', dismissed: false })).toBe('ask');
    expect(pushRegistrationPlan({ physicalDevice: true, permission: 'undetermined', projectId: 'p', dismissed: true })).toBe('later');
    expect(pushRegistrationPlan({ physicalDevice: true, permission: 'granted', projectId: 'p', dismissed: false })).toBe('register');
  });

  it('classifies device and minor failures', () => {
    expect(notificationFailure('notification.device_required')).toBe('device-required');
    expect(notificationFailure('notification.proximity_not_allowed_minor')).toBe('minor');
    expect(notificationFailure('notification.alert_location_limited')).toBe('silent');
    expect(notificationFailure(undefined)).toBe('show');
  });

  it('snaps the radius to the published steps', () => {
    expect(nearestRadius(1000)).toBe(1000);
    expect(nearestRadius(1400)).toBe(1000);
    expect(nearestRadius(1800)).toBe(2000);
  });

  it('retries only network failures, and only twice more', () => {
    expect(shouldRetryPush('NETWORK', 1)).toBe(true);
    expect(shouldRetryPush('TIMEOUT', 2)).toBe(true);
    expect(shouldRetryPush('NETWORK', 3)).toBe(false);
    expect(shouldRetryPush('FORBIDDEN', 1)).toBe(false);
  });
});
