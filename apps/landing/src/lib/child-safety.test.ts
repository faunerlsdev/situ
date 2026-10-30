import { spawn } from 'node:child_process';
import { request } from 'node:http';
import { access, readFile } from 'node:fs/promises';
import path from 'node:path';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { legalIdentity } from '../../config/legal-identity';
import { PrivacyArticle } from '@/components/privacy-article';
import { childSafetySourcePath, loadChildSafety, splitChildSafety } from './child-safety';

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

function htmlFor(document: Awaited<ReturnType<typeof loadChildSafety>>) {
  return renderToStaticMarkup(
    createElement(
      'div',
      null,
      createElement(PrivacyArticle, {
        document: document.spanish,
        extraLinks: [{ href: '#english', label: document.english.title }],
      }),
      createElement(PrivacyArticle, { id: 'english', document: document.english }),
    ),
  );
}

describe('seguridad infantil', () => {
  it('publica español y luego inglés, sin cédula, teléfono ni dirección', async () => {
    expect(legalIdentity.showPrivateIdentity).toBe(false);
    const source = await readFile(childSafetySourcePath, 'utf8');
    const parts = splitChildSafety(source);
    expect(parts.spanish.startsWith('# Estándares de seguridad infantil')).toBe(true);
    expect(parts.english.startsWith('# SITU child safety standards')).toBe(true);

    const document = await loadChildSafety();
    const html = htmlFor(document);
    expect(html).toContain('Estándares de seguridad infantil de SITU');
    expect(html).toContain('Contenido');
    expect(html).toContain('id="english"');
    expect(html).toContain('SITU child safety standards (English)');
    expect(html.indexOf('Estándares de seguridad infantil de SITU')).toBeLessThan(html.indexOf('id="english"'));
    expect(html).toContain('https://ayuda.somossitu.com/retiro-contenido');
    expect(html).toContain('soporte@somossitu.com');
    expect(html).not.toContain(legalIdentity.nationalId);
    expect(html).not.toContain(legalIdentity.nationalId.replaceAll('.', ''));
    expect(html).not.toContain(legalIdentity.phone);
    expect(html).not.toContain(legalIdentity.phone.replaceAll(' ', ''));
    expect(html).not.toContain(legalIdentity.physicalAddress);
    expect(html).not.toContain('Tacuara');
    expect(html).not.toContain('Apto 610');
  });

  it('GET /seguridad-infantil responde 200', async () => {
    const landingRoot = path.resolve(__dirname, '../..');
    const buildId = path.join(landingRoot, '.next/BUILD_ID');
    try {
      await access(buildId);
    } catch {
      return;
    }
    const port = 3423;
    const server = spawn(path.resolve(landingRoot, '../../node_modules/.bin/next'), ['start', '--port', String(port), '--hostname', '127.0.0.1'], {
      cwd: landingRoot,
      stdio: 'ignore',
    });
    try {
      let response: { status: number; body: string } | null = null;
      const started = Date.now();
      while (Date.now() - started < 20_000) {
        try {
          response = await getText(`http://127.0.0.1:${port}/seguridad-infantil`);
          break;
        } catch {
          await new Promise((resolve) => setTimeout(resolve, 250));
        }
      }
      expect(response?.status).toBe(200);
      const body = response?.body ?? '';
      const article = body.slice(body.indexOf('<article'), body.lastIndexOf('</article>') + '</article>'.length);
      expect(article).toContain('Estándares de seguridad infantil de SITU');
      expect(article).toContain('id="english"');
      expect(article).not.toContain(legalIdentity.nationalId);
      expect(article).not.toContain(legalIdentity.phone);
      expect(article).not.toContain(legalIdentity.physicalAddress);
      expect(article).not.toContain('Tacuara');
    } finally {
      server.kill('SIGTERM');
    }
  }, 30_000);
});
