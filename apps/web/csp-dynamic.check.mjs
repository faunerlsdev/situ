import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const appRoot = dirname(fileURLToPath(import.meta.url));

test('the root layout stays dynamic so each response gets its own nonce', () => {
  const layout = readFileSync(join(appRoot, 'src/app/layout.tsx'), 'utf8');
  assert.match(layout, /export const dynamic = 'force-dynamic'/);
  assert.match(layout, /headers\(/);
});

test('a production build does not prerender HTML routes that carry a nonce CSP', () => {
  const manifestPath = join(appRoot, '.next/prerender-manifest.json');
  if (!existsSync(manifestPath)) {
    return;
  }
  const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
  const staticRoutes = Object.keys(manifest.routes ?? {}).filter((route) => !route.startsWith('/_') && !/\.[a-z0-9]+$/i.test(route));
  assert.deepEqual(staticRoutes, [], `rutas estáticas: ${staticRoutes.join(', ')}`);
});
