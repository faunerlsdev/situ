import { spawn } from 'node:child_process';
import { request } from 'node:http';
import { access, readFile } from 'node:fs/promises';
import path from 'node:path';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { legalIdentity, type LegalIdentity } from '../../config/legal-identity';
import { PrivacyArticle } from '@/components/privacy-article';
import { fillTerminos, terminosProblems } from '../../../../scripts/legal/fill-terminos.mjs';
import { parsePrivacyPolicy } from './privacy-policy';
import { termsSectionId, termsSourcePath } from './terms-policy';

const supportApp = path.resolve(__dirname, '../../../support/src/app');
const landingApp = path.resolve(__dirname, '../app');

function getText(url: string) {
  return new Promise<{ status: number; headers: Record<string, string | string[] | undefined>; body: string }>((resolve, reject) => {
    const req = request(url, (res) => {
      const chunks: Buffer[] = [];
      res.on('data', (chunk: Buffer) => chunks.push(chunk));
      res.on('end', () => resolve({ status: res.statusCode ?? 0, headers: res.headers, body: Buffer.concat(chunks).toString('utf8') }));
    });
    req.on('error', reject);
    req.end();
  });
}

function htmlFor(identity: LegalIdentity, source: string) {
  const document = parsePrivacyPolicy(fillTerminos(source, identity), identity, termsSectionId);
  return renderToStaticMarkup(createElement(PrivacyArticle, { document }));
}

describe('términos y condiciones', () => {
  it('responde el HTML público sin corchetes, cédula ni teléfono, y sus enlaces resuelven', async () => {
    expect(legalIdentity.showPrivateIdentity).toBe(false);
    const source = await readFile(termsSourcePath, 'utf8');
    const filled = fillTerminos(source, legalIdentity);
    expect(terminosProblems(filled)).toEqual([]);
    expect(filled).not.toContain('*** Mis Datos ***');
    expect(filled).not.toMatch(/\[[^\]]*\]/);

    const html = htmlFor(legalIdentity, source);
    expect(html).toContain('Contenido');
    expect(html).toContain(legalIdentity.responsibleName);
    expect(html).toContain(legalIdentity.supportEmail);
    expect(html).toContain('id="14"');
    expect(html).toContain('https://ayuda.somossitu.com/contacto');
    expect(html).toContain('https://ayuda.somossitu.com/retiro-contenido');
    expect(html).toContain('https://somossitu.com/privacidad');
    expect(html).toContain('https://somossitu.com/terminos');
    expect(html).toContain('Mi SITU → Mi cuenta');
    expect(html).not.toMatch(/\[[^\]]*\]/);
    expect(html).not.toContain(legalIdentity.nationalId);
    expect(html).not.toContain(legalIdentity.nationalId.replaceAll('.', ''));
    expect(html).not.toContain(legalIdentity.phone);
    expect(html).not.toContain(legalIdentity.phone.replaceAll(' ', ''));
    expect(html).not.toContain('Tacuara');
    expect(html).not.toContain('Apto 610');

    const full = htmlFor({ ...legalIdentity, showPrivateIdentity: true }, source);
    expect(full).toContain(legalIdentity.nationalId);
    expect(full).toContain(legalIdentity.phone);
    expect(full).toContain(legalIdentity.physicalAddress);

    const supportConfig = await readFile(path.resolve(supportApp, '../../next.config.ts'), 'utf8');
    const links = [...html.matchAll(/href="([^"]+)"/g)].map((match) => match[1] ?? '');
    expect(links.length).toBeGreaterThan(0);
    for (const href of new Set(links)) {
      if (href.startsWith('#')) {
        expect(html).toContain(`id="${href.slice(1)}"`);
        continue;
      }
      const url = new URL(href, 'https://somossitu.com');
      if (url.hostname === 'ayuda.somossitu.com') {
        const route = url.pathname.replace(/\/$/, '') || '/';
        if (route === '/solicitudes') {
          expect(supportConfig).toContain('statusCode: 301');
          continue;
        }
        await expect(readFile(path.join(supportApp, route, 'page.tsx'), 'utf8')).resolves.toContain('export');
        continue;
      }
      if (url.hostname === 'somossitu.com') {
        const route = url.pathname.replace(/\/$/, '') || '/';
        if (route === '/privacidad' || route === '/terminos') {
          await expect(readFile(path.join(landingApp, route, 'page.tsx'), 'utf8')).resolves.toContain('export');
          if (url.hash) {
            expect(html).toContain(`id="${url.hash.slice(1)}"`);
          }
        }
      }
    }
  });

  it('GET /terminos responde 200 y las rutas viejas redirigen', async () => {
    const landingRoot = path.resolve(__dirname, '../..');
    try {
      await access(path.join(landingRoot, '.next/BUILD_ID'));
    } catch {
      return;
    }
    const port = 3421;
    const server = spawn(path.resolve(landingRoot, '../../node_modules/.bin/next'), ['start', '--port', String(port), '--hostname', '127.0.0.1'], {
      cwd: landingRoot,
      stdio: 'ignore',
    });
    try {
      let ready = false;
      for (let attempt = 0; attempt < 40 && !ready; attempt += 1) {
        try {
          const probe = await getText(`http://127.0.0.1:${port}/terminos`);
          ready = probe.status > 0;
        } catch {
          await new Promise((resolve) => setTimeout(resolve, 250));
        }
      }
      const terms = await getText(`http://127.0.0.1:${port}/terminos`);
      expect(terms.status).toBe(200);
      const article = terms.body.match(/<article[\s\S]*?<\/article>/)?.[0] ?? '';
      expect(article).not.toMatch(/\[[^\]]*\]/);
      expect(article).not.toContain('Texto en revisión');
      expect(article).not.toContain('El texto se lee de');
      expect(article).not.toContain(legalIdentity.nationalId);
      expect(article).not.toContain(legalIdentity.phone);
      const treatment = await getText(`http://127.0.0.1:${port}/politica-de-tratamiento-de-datos`);
      expect(treatment.status).toBe(301);
      expect(treatment.headers.location).toBe('/privacidad');
      const moderation = await getText(`http://127.0.0.1:${port}/contenido-y-moderacion`);
      expect(moderation.status).toBe(301);
      expect(String(moderation.headers.location)).toContain('/terminos');
    } finally {
      server.kill();
    }
  });
});
