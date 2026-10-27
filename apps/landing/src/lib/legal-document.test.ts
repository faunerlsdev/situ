import { execFile } from 'node:child_process';
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { promisify } from 'node:util';
import { describe, expect, it } from 'vitest';
import { legalPlaceholder, legalReviewBanner, parseLegalDocument, publicLegalPages, publicSiteUrls, renderLegalText } from './legal-document';

const exec = promisify(execFile);
const landingRoot = path.resolve(__dirname, '../..');

describe('documentos legales', () => {
  it('renderiza versión, fecha y el aviso de revisión, sin el marcador', async () => {
    for (const page of publicLegalPages) {
      const source = await readFile(path.join(landingRoot, 'content/legal', `${page.slug}.md`), 'utf8');
      const document = parseLegalDocument(source);
      const rendered = renderLegalText(document);
      expect(document.version).toBe('2026-10-27-borrador');
      expect(document.updated).toBe('2026-10-27');
      expect(document.placeholder).toBe(true);
      expect(rendered).toContain(legalReviewBanner);
      expect(rendered).toContain('Versión 2026-10-27-borrador');
      expect(rendered).not.toContain(legalPlaceholder);
      expect(rendered).not.toMatch(/\+57\d{10}/);
    }
  });

  it('arma las URLs públicas con DOMAIN_ROOT', () => {
    const sites = publicSiteUrls('ejemplo.com');
    expect(sites.termsUrl).toBe('https://ejemplo.com/terminos');
    expect(sites.privacyUrl).toBe('https://ejemplo.com/privacidad');
    expect(sites.treatmentUrl).toBe('https://ejemplo.com/politica-de-tratamiento-de-datos');
    expect(sites.moderationUrl).toBe('https://ejemplo.com/contenido-y-moderacion');
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
