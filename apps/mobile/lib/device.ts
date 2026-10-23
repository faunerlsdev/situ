import type { DevicePlatform } from '@situ/shared';
import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

const deviceKey = 'situ.deviceId';

export function devicePlatform(): DevicePlatform {
  if (Platform.OS === 'ios') {
    return 'IOS';
  }

  if (Platform.OS === 'android') {
    return 'Android';
  }

  return 'Web';
}

export async function deviceIdentifier(): Promise<string> {
  const existing = await SecureStore.getItemAsync(deviceKey);
  if (existing) {
    return existing;
  }

  const created = globalThis.crypto?.randomUUID?.() ?? fallbackId();
  await SecureStore.setItemAsync(deviceKey, created);
  return created;
}

function fallbackId(): string {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (symbol) => {
    const random = Math.floor(Math.random() * 16);
    const value = symbol === 'x' ? random : (random & 0x3) | 0x8;
    return value.toString(16);
  });
}

export function searchParam(value: string | string[] | undefined): string {
  if (Array.isArray(value)) {
    return value[0] ?? '';
  }

  return value ?? '';
}
