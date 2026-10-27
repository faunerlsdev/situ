import { useQuery } from '@tanstack/react-query';
import * as Location from 'expo-location';
import { useEffect, useRef } from 'react';
import { AppState, Platform } from 'react-native';

import { useAppConfig } from '@/features/reports/use-app-config';
import { api } from '@/lib/api';

import { emitIntervalSeconds } from './logic';

/** Sends a foreground fix while sharing is on. Background never starts a watcher and asks the server to mark last seen. */
export function LocationEmitter() {
  const sharing = useQuery({ queryKey: ['location-sharing'], queryFn: () => api.locationSharing() });
  const config = useAppConfig();
  const interval = sharing.data?.emitIntervalSeconds ?? emitIntervalSeconds(config.data);
  const enabled = sharing.data?.enabled === true && sharing.data.ghost !== true;
  const lastAt = useRef(0);

  useEffect(() => {
    if (!enabled || Platform.OS === 'web') {
      return;
    }

    let watch: Location.LocationSubscription | null = null;
    let stopped = false;

    async function start() {
      if (stopped || watch) {
        return;
      }
      const permission = await Location.requestForegroundPermissionsAsync();
      if (!permission.granted || stopped) {
        return;
      }
      watch = await Location.watchPositionAsync(
        {
          accuracy: Location.Accuracy.Balanced,
          timeInterval: interval * 1000,
          distanceInterval: 25,
        },
        (position) => {
          const now = Date.now();
          if (now - lastAt.current < interval * 1000) {
            return;
          }
          const accuracy = position.coords.accuracy;
          if (accuracy != null && accuracy > 200) {
            return;
          }
          lastAt.current = now;
          void api.postLocation({
            latitude: position.coords.latitude,
            longitude: position.coords.longitude,
            accuracyMeters: accuracy ?? 50,
            capturedAt: new Date(position.timestamp).toISOString(),
          }).catch(() => undefined);
        },
      );
    }

    function pause() {
      watch?.remove();
      watch = null;
      void api.pauseLocation().catch(() => undefined);
    }

    if (AppState.currentState === 'active') {
      void start();
    }
    const subscription = AppState.addEventListener('change', (state) => {
      if (state === 'active') {
        void start();
        return;
      }
      pause();
    });

    return () => {
      stopped = true;
      subscription.remove();
      pause();
    };
  }, [enabled, interval]);

  return null;
}
