import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

const mobileRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');

function screenTag(layout: string, name: string) {
  const marker = `name="${name}"`;
  const start = layout.indexOf(marker);
  const end = layout.indexOf('/>', start);
  return layout.slice(start, end);
}

describe('friends navigation', () => {
  it('opens Amigos from the menu instead of bouncing back to the map', () => {
    const layout = readFileSync(path.join(mobileRoot, 'app/(tabs)/_layout.tsx'), 'utf8');
    const amigos = screenTag(layout, 'amigos');
    const perfil = screenTag(layout, 'perfil');
    expect(amigos).not.toContain('href: null');
    expect(perfil).not.toContain('href: null');
    expect(amigos).toContain("title: 'Amigos'");

    const shell = readFileSync(path.join(mobileRoot, 'components/shell.tsx'), 'utf8');
    const cuenta = readFileSync(path.join(mobileRoot, 'app/cuenta.tsx'), 'utf8');
    expect(shell).toContain("go('/amigos')");
    expect(cuenta).toContain("router.push('/amigos')");
    const friends = readFileSync(path.join(mobileRoot, 'app/(tabs)/amigos.tsx'), 'utf8');
    expect(friends).toContain('messageFromUnknown');
    expect(friends).toContain('Solicitudes');
    expect(friends).toContain('Buscar');
    expect(friends).toContain('Invitar por correo');
    expect(shell).toContain('solicitudes');
  });
});
