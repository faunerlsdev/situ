import { Platform } from 'react-native';

export const androidMapsKeyVariable = 'EXPO_PUBLIC_GOOGLE_MAPS_API_KEY_ANDROID';
export const iosMapsKeyVariable = 'EXPO_PUBLIC_GOOGLE_MAPS_API_KEY_IOS';

export type MapProviderChoice =
  | { status: 'apple'; userInterfaceStyle: 'dark' }
  | { status: 'google' }
  | { status: 'unavailable'; missingVariable: typeof androidMapsKeyVariable };

export function resolveMapProvider(
  platform: string,
  keys: { ios?: string | null; android?: string | null },
): MapProviderChoice {
  if (platform === 'android') {
    if (!keys.android?.trim()) {
      return { status: 'unavailable', missingVariable: androidMapsKeyVariable };
    }
    return { status: 'google' };
  }

  if (!keys.ios?.trim()) {
    return { status: 'apple', userInterfaceStyle: 'dark' };
  }

  return { status: 'google' };
}

export function useMapProvider(): MapProviderChoice {
  return resolveMapProvider(Platform.OS, {
    ios: process.env.EXPO_PUBLIC_GOOGLE_MAPS_API_KEY_IOS,
    android: process.env.EXPO_PUBLIC_GOOGLE_MAPS_API_KEY_ANDROID,
  });
}
