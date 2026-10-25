import { alertModal, AppError, notificationMessages } from '@situ/shared';
import * as Location from 'expo-location';
import { useEffect, useRef } from 'react';

import { api } from '@/lib/api';

import {
  alertCleanupAction,
  alertFailureRecord,
  alertLocationPaused,
  isMinorProximityBlocked,
  markMinorProximityBlocked,
  notificationFailure,
  shouldSendAlertLocation,
} from './logic';

export async function forgetAlertLocation(): Promise<void> {
  try {
    await api.clearAlertLocation();
  } catch (error) {
    if (__DEV__) {
      console.warn(alertFailureRecord(error instanceof AppError ? error.code : 'unknown'));
    }
  }
}

type Fix = { at: number; latitude: number; longitude: number };

/**
 * Foreground-only alert location.
 * A later version can add an "always" background task here. Do not start one now:
 * the product only sends a point while the app is open, and the server drops it after 30 minutes.
 */
export function useAlertLocationSync(enabled: boolean) {
  const last = useRef<Fix | null>(null);

  useEffect(() => {
    if (!enabled) {
      return;
    }

    let cancelled = false;
    let watch: Location.LocationSubscription | null = null;

    async function send(position: Location.LocationObject, trigger: 'open' | 'move') {
      if (cancelled || alertLocationPaused() || isMinorProximityBlocked()) {
        return;
      }

      const previous = last.current;
      const latitude = position.coords.latitude;
      const longitude = position.coords.longitude;
      const now = Date.now();
      if (!shouldSendAlertLocation({
        enabled: true,
        now,
        lastSentAt: previous?.at ?? null,
        lastLatitude: previous?.latitude ?? null,
        lastLongitude: previous?.longitude ?? null,
        latitude,
        longitude,
        trigger,
      })) {
        return;
      }

      try {
        await api.setAlertLocation({
          latitude,
          longitude,
          accuracyMeters: position.coords.accuracy,
        });
        last.current = { at: now, latitude, longitude };
      } catch (error) {
        const apiCode = error instanceof AppError ? error.apiCode : undefined;
        const kind = notificationFailure(apiCode);
        if (error instanceof AppError && (error.status === 429 || error.code === 'NETWORK' || error.code === 'TIMEOUT' || kind === 'silent')) {
          return;
        }

        if (kind === 'minor') {
          markMinorProximityBlocked();
          if (alertCleanupAction({ signingOut: false, nearbyEnabled: true, locationGranted: true, minorBlocked: true }) === 'alert-location') {
            void forgetAlertLocation();
          }
          void alertModal({
            title: notificationMessages.minorTitle,
            message: notificationMessages.minorBody,
            type: 'info',
          });
          return;
        }

        if (kind === 'device-required') {
          void alertModal({
            title: notificationMessages.deviceRequiredTitle,
            message: notificationMessages.deviceRequiredBody,
            type: 'warning',
          });
          return;
        }

        if (__DEV__) {
          console.warn(alertFailureRecord(error instanceof AppError ? error.code : 'unknown'));
        }
      }
    }

    void (async () => {
      try {
        const current = await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.Balanced });
        if (cancelled) {
          return;
        }
        await send(current, 'open');
        watch = await Location.watchPositionAsync(
          { accuracy: Location.Accuracy.Balanced, distanceInterval: 500, timeInterval: 300_000 },
          (position) => { void send(position, 'move'); },
        );
      } catch (error) {
        if (__DEV__) {
          console.warn(alertFailureRecord(error instanceof AppError ? error.code : 'unavailable'));
        }
      }
    })();

    return () => {
      cancelled = true;
      watch?.remove();
    };
  }, [enabled]);
}
