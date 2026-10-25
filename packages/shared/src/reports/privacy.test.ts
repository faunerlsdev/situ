import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

import { legacyLocalProfileKey } from './logic';

const mobileRoot = join(import.meta.dirname, '../../../../apps/mobile');

function sourceFiles(directory: string): string[] {
  const found: string[] = [];
  for (const entry of readdirSync(directory)) {
    const path = join(directory, entry);
    if (entry === 'node_modules' || entry === '.expo') {
      continue;
    }
    const info = statSync(path);
    if (info.isDirectory()) {
      found.push(...sourceFiles(path));
      continue;
    }
    if (path.endsWith('.ts') || path.endsWith('.tsx')) {
      found.push(path);
    }
  }
  return found;
}

describe('device privacy', () => {
  it('deletes the legacy profile and does not store a birth date, email or city', () => {
    expect(legacyLocalProfileKey).toBe('situ.localProfile');
    const files = sourceFiles(mobileRoot);
    const migration = files.find((path) => path.endsWith('lib/local-profile.ts'));
    expect(migration).toBeTruthy();
    const migrationSource = readFileSync(migration ?? '', 'utf8');
    expect(migrationSource).toContain('deleteItemAsync');
    expect(migrationSource).toContain('legacyLocalProfileKey');
    expect(migrationSource).not.toContain('setItemAsync');
    expect(migrationSource).not.toContain('dateOfBirth');

    for (const path of files) {
      const source = readFileSync(path, 'utf8');
      expect(source, path).not.toContain('situ.localProfile');
      expect(source, path).not.toContain('saveLocalProfile');
      expect(source, path).not.toContain('readLocalProfile');
      expect(source, path).not.toContain('profileIsMinor');
      if (source.includes('setItemAsync') && source.includes('dateOfBirth')) {
        throw new Error(`${path} writes a birth date to the device`);
      }
    }
  });
});
