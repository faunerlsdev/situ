import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');

describe('report screen', () => {
  it('uses a large map, a tap to move the pin, and two media buttons', () => {
    const form = readFileSync(path.join(root, 'features/reports/report-form.tsx'), 'utf8');
    expect(form).toContain('accessibilityLabel="Foto"');
    expect(form).toContain('accessibilityLabel="Video"');
    expect(form).toContain('Abrir la cámara');
    expect(form).toContain('Elegir de la galería');
    expect(form).toContain('Zona aproximada');
    expect(form).toContain('height: 420');
    expect(form).not.toContain('draggable');
    const map = readFileSync(path.join(root, 'features/reports/map-screen.tsx'), 'utf8');
    expect(map).toContain('accessibilityLabel="Transmisión en vivo"');
    expect(map).toContain('<LiveMark');
  });
});
