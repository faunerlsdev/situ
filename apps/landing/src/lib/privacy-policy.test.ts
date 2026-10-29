import { spawn } from 'node:child_process';
import { request } from 'node:http';
import { access, readFile } from 'node:fs/promises';
import path from 'node:path';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { legalIdentity, type LegalIdentity } from '../../config/legal-identity';
import { PrivacyArticle } from '@/components/privacy-article';
import { parsePrivacyPolicy, privacySourcePath } from './privacy-policy';

const supportApp = path.resolve(__dirname, '../../../support/src/app');

function getText(url: string) {
  return new Promise<{ status: number; body: string }>((resolve, reject) => {
    const req = request(url, (res) => {
      const chunks: Buffer[] = [];
      res.on('data', (chunk: Buffer) => chunks.push(chunk));
      res.on('end', () => resolve({ status: res.statusCode ?? 0, body: Buffer.concat(chunks).toString('utf8') }));
    });
    req.on('error', reject);
    req.end();
  });
}

function htmlFor(identity: LegalIdentity) {
  return readFile(privacySourcePath, 'utf8').then((source) => {
    const document = parsePrivacyPolicy(source, identity);
    return renderToStaticMarkup(createElement(PrivacyArticle, { document }));
  });
}

describe('política de privacidad', () => {
  it('responde el HTML público sin cédula, teléfono ni dirección, y enlaza rutas de ayuda que existen', async () => {
    expect(legalIdentity.showPrivateIdentity).toBe(false);
    const html = await htmlFor(legalIdentity);

    expect(html).toContain('POLÍTICA DE TRATAMIENTO');
    expect(html).toContain('Contenido');
    expect(html).toContain(legalIdentity.responsibleName);
    expect(html).toContain(legalIdentity.supportEmail);
    expect(html).toContain('Un año desde su creación');
    expect(html).toContain('https://ayuda.somossitu.com/contacto');
    expect(html).toContain('https://ayuda.somossitu.com/retiro-contenido');
    expect(html).not.toContain(legalIdentity.nationalId);
    expect(html).not.toContain(legalIdentity.nationalId.replaceAll('.', ''));
    expect(html).not.toContain(legalIdentity.phone);
    expect(html).not.toContain(legalIdentity.phone.replaceAll(' ', ''));
    expect(html).not.toContain('Tacuara');
    expect(html).not.toContain('Apto 610');

    const full = await htmlFor({ ...legalIdentity, showPrivateIdentity: true });
    expect(full).toContain(legalIdentity.nationalId);
    expect(full).toContain(legalIdentity.phone);
    expect(full).toContain(legalIdentity.physicalAddress);

    const links = [...html.matchAll(/https:\/\/ayuda\.somossitu\.com([^"<\s]*)/g)].map((match) => match[1] ?? '/');
    expect(links.length).toBeGreaterThan(0);
    const supportConfig = await readFile(path.resolve(supportApp, '../../next.config.ts'), 'utf8');
    for (const pathname of new Set(links)) {
      const route = pathname.replace(/[.,;:]+$/, '') || '/';
      if (route === '/solicitudes') {
        expect(supportConfig).toContain("source: '/solicitudes'");
        expect(supportConfig).toContain("destination: '/retiro-contenido'");
        expect(supportConfig).toContain('statusCode: 301');
        continue;
      }
      await expect(readFile(path.join(supportApp, route, 'page.tsx'), 'utf8')).resolves.toContain('export');
    }
  });

  it('GET /privacidad responde 200', async () => {
    const landingRoot = path.resolve(__dirname, '../..');
    const buildId = path.join(landingRoot, '.next/BUILD_ID');
    try {
      await access(buildId);
    } catch {
      return;
    }
    const port = 3417;
    const server = spawn(path.resolve(landingRoot, '../../node_modules/.bin/next'), ['start', '--port', String(port), '--hostname', '127.0.0.1'], {
      cwd: landingRoot,
      stdio: 'ignore',
    });
    try {
      let response: { status: number; body: string } | null = null;
      const started = Date.now();
      while (Date.now() - started < 20_000) {
        try {
          response = await getText(`http://127.0.0.1:${port}/privacidad`);
          break;
        } catch {
          await new Promise((resolve) => setTimeout(resolve, 250));
        }
      }
      expect(response?.status).toBe(200);
      const body = response?.body;
      expect(body).toContain('POLÍTICA DE TRATAMIENTO');
      expect(body).not.toContain(legalIdentity.nationalId);
      expect(body).not.toContain(legalIdentity.phone);
      expect(body).not.toContain('Tacuara');
    } finally {
      server.kill('SIGTERM');
    }
  }, 30_000);
});
