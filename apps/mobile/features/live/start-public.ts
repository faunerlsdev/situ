import { confirm, messageFromUnknown, notify } from '@situ/shared';
import * as Location from 'expo-location';
import { Platform } from 'react-native';

import { liveStartState } from '@/features/live/logic';
import { requestLiveCapture, livePublisherAvailable } from '@/features/live/publisher';
import { getLiveQuotaFull } from '@/features/live/quota';
import { holdBroadcast } from '@/features/live/session';
import { api } from '@/lib/api';

export const PUBLIC_LIVE_TITLE = 'En vivo';

type ReplaceRouter = {
  replace: (href: '/en-vivo/transmitir') => void;
};

type LiveProfile = {
  isMinor?: boolean;
  canPublish?: boolean;
  publishBlockedReason?: string | null;
} | null | undefined;

/** Starts a public stream at once. Title and audience change after the camera is open. */
export async function beginPublicLive(router: ReplaceRouter, profile: LiveProfile): Promise<boolean> {
  const platform = Platform.OS === 'ios' ? 'ios' : Platform.OS === 'android' ? 'android' : 'web';
  const state = liveStartState({
    profileKnown: profile !== undefined && profile !== null,
    isMinor: profile?.isMinor === true,
    canPublish: profile?.canPublish === true,
    publishBlockedReason: profile?.publishBlockedReason ?? null,
    platform,
    publisherAvailable: livePublisherAvailable(),
    quotaFull: getLiveQuotaFull(),
  });
  if (!state.enabled) {
    notify.warning(state.reason ?? 'No puedes transmitir.');
    return false;
  }

  const accepted = await confirm({
    title: 'Transmisión pública',
    message: 'Sale en público, en el lugar donde estás. Ya en vivo puedes pasarla a amigos o ponerle un título.',
    type: 'warning',
    confirmText: 'Transmitir',
    cancelText: 'Cancelar',
  });
  if (!accepted) {
    return false;
  }

  try {
    const granted = await requestLiveCapture();
    if (!granted) {
      notify.warning('Sin permiso de cámara o micrófono no puedes transmitir.');
      return false;
    }

    let latitude: number | null = null;
    let longitude: number | null = null;
    const current = await Location.getForegroundPermissionsAsync();
    if (current.status === 'granted') {
      const position = await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.Balanced });
      latitude = position.coords.latitude;
      longitude = position.coords.longitude;
    }

    const created = await api.createLive({
      visibility: 'Public',
      title: PUBLIC_LIVE_TITLE,
      latitude,
      longitude,
    });
    holdBroadcast({
      id: created.id,
      publishUrl: created.publishUrl,
      publishKey: created.publishKey,
      title: PUBLIC_LIVE_TITLE,
    });
    router.replace('/en-vivo/transmitir');
    return true;
  } catch (cause) {
    notify.error(messageFromUnknown(cause) || 'No se pudo empezar la transmisión.');
    return false;
  }
}
