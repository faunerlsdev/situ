import { describe, expect, it } from 'vitest';
import { sessionBlock, toggleCategory } from './session-gate';
import { reportRobots, shareDescription, shareTitle } from './share-meta';

describe('reporte público', () => {
  it('no indexa un reporte que no es público o es de un menor', () => {
    expect(reportRobots(null)).toEqual({ index: false, follow: false });
    expect(reportRobots({ indexable: false, categoryName: 'Hurto', zone: 'Bogotá' })).toEqual({ index: false, follow: false });
    expect(shareTitle({ indexable: false, categoryName: 'Hurto' })).toBe('SITU');
    expect(shareDescription({ indexable: false, zone: 'Bogotá', categoryName: 'Hurto' })).toBeUndefined();
  });

  it('el enlace público solo anuncia categoría y zona', () => {
    const share = { indexable: true, categoryName: 'Hurto', zone: 'Bogotá' };
    expect(reportRobots(share)).toEqual({ index: true, follow: true });
    expect(shareTitle(share)).toBe('Hurto');
    expect(shareDescription(share)).toBe('Bogotá');
  });

  it('bloquea confirmar, comentar y denunciar sin sesión', () => {
    expect(sessionBlock(false)).toMatch(/Inicia sesión/);
    expect(sessionBlock(true)).toBeNull();
  });

  it('activa y quita un filtro de categoría', () => {
    expect(toggleCategory([], 'hurto')).toEqual(['hurto']);
    expect(toggleCategory(['hurto', 'retén'], 'hurto')).toEqual(['retén']);
  });
});
