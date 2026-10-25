import { legacyLocalProfileKey } from '@situ/shared';
import * as SecureStore from 'expo-secure-store';

let started = false;

/** Borra correo, fecha de nacimiento y ciudad que versiones anteriores guardaban en el teléfono. */
export async function clearLegacyProfile(): Promise<void> {
  if (started) {
    return;
  }

  started = true;
  await SecureStore.deleteItemAsync(legacyLocalProfileKey).catch(() => undefined);
}
