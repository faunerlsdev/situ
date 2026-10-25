import type { CachedAppConfig } from '@situ/shared';
import { File, Paths } from 'expo-file-system';

const file = new File(Paths.document, 'app-config.json');

export async function readAppConfigFile(): Promise<CachedAppConfig | null> {
  try {
    if (!file.exists) {
      return null;
    }

    const parsed = JSON.parse(await file.text()) as CachedAppConfig;
    if (!parsed || typeof parsed.etag !== 'string' || !parsed.body?.panelCopy || !parsed.body.mediaLimits) {
      return null;
    }

    return parsed;
  } catch {
    return null;
  }
}

export async function writeAppConfigFile(cache: CachedAppConfig): Promise<void> {
  if (!file.exists) {
    file.create();
  }

  await file.write(JSON.stringify(cache));
}
