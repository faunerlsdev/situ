import type { CachedCatalog } from '@situ/shared';
import { File, Paths } from 'expo-file-system';

const file = new File(Paths.document, 'report-catalog.json');

export async function readCatalogFile(): Promise<CachedCatalog | null> {
  try {
    if (!file.exists) {
      return null;
    }

    const parsed = JSON.parse(await file.text()) as CachedCatalog;
    if (!parsed || typeof parsed.etag !== 'string' || !parsed.body?.categories) {
      return null;
    }

    return parsed;
  } catch {
    return null;
  }
}

export async function writeCatalogFile(cache: CachedCatalog): Promise<void> {
  if (!file.exists) {
    file.create();
  }

  await file.write(JSON.stringify(cache));
}
