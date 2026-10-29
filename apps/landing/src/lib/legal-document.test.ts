import { execFile } from 'node:child_process';
import { mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { promisify } from 'node:util';
import { describe, expect, it } from 'vitest';
import { legalPlaceholder, publicLegalPages, publicSiteUrls } from './legal-document';

const exec = promisify(execFile);
const landingRoot = path.resolve(__dirname, '../..');

describe('documentos legales', () => {
  it('publica términos y privacidad, sin páginas en revisión', () => {
    expect(publicLegalPages.map((page) => page.path)).toEqual(['/terminos', '/privacidad']);
  });

  it('arma las URLs públicas con DOMAIN_ROOT', () => {
    const sites = publicSiteUrls('ejemplo.com');
    expect(sites.termsUrl).toBe('https://ejemplo.com/terminos');
    expect(sites.privacyUrl).toBe('https://ejemplo.com/privacidad');
    expect(sites.supportUrl).toBe('https://ayuda.ejemplo.com/contacto');
    expect(sites.deleteAccountUrl).toBe('https://app.ejemplo.com/cuenta/eliminar');
  });
});

describe('legal:check', () => {
  it('falla si un archivo contiene el marcador', async () => {
    const directory = await mkdtemp(path.join(tmpdir(), 'situ-legal-'));
    await writeFile(path.join(directory, 'terminos.md'), `texto\n${legalPlaceholder}\n`, 'utf8');
    await expect(exec('node', ['scripts/legal-check.mjs', directory], { cwd: landingRoot })).rejects.toMatchObject({ code: 1 });
    await rm(directory, { recursive: true, force: true });
  });

  it('pasa cuando ningún archivo tiene el marcador', async () => {
    const directory = await mkdtemp(path.join(tmpdir(), 'situ-legal-'));
    await writeFile(path.join(directory, 'terminos.md'), 'Texto definitivo del abogado.\n', 'utf8');
    await expect(exec('node', ['scripts/legal-check.mjs', directory], { cwd: landingRoot })).resolves.toMatchObject({ stderr: '' });
    await rm(directory, { recursive: true, force: true });
  });
});
