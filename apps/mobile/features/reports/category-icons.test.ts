import { describe, expect, it } from 'vitest';

import { categoryIconSource, categorySlug, isPlaceholderIcon } from './category-icons';

describe('category icons', () => {
  it('maps the seeded codes onto the drawing slugs', () => {
    expect(categorySlug('accidente_transito')).toBe('accidente');
    expect(categorySlug('manifestacion')).toBe('protesta');
    expect(categorySlug('obra_vial')).toBe('obra_via');
    expect(categorySlug('corte_servicio')).toBe('corte_luz');
    expect(categorySlug('hurto')).toBe('hurto');
    expect(categorySlug('desconocido')).toBeNull();
  });

  it('uses the local drawing when the API still points at the generic pin', () => {
    expect(isPlaceholderIcon('https://api.somossitu.com/assets/categories/accidente_transito.png')).toBe(true);
    expect(isPlaceholderIcon('https://api.somossitu.com/assets/categories/default.png')).toBe(true);
    expect(isPlaceholderIcon('https://api.somossitu.com/assets/categories/button/accidente.png')).toBe(false);
    expect(categoryIconSource({
      remoteUrl: 'https://api.somossitu.com/assets/categories/hurto.png',
      remoteFailed: false,
      hasLocal: true,
    })).toBe('local');
    expect(categoryIconSource({
      remoteUrl: 'https://api.somossitu.com/assets/categories/map/hurto.png',
      remoteFailed: false,
      hasLocal: true,
    })).toBe('remote');
    expect(categoryIconSource({ remoteUrl: null, remoteFailed: false, hasLocal: false })).toBe('generic');
    expect(categoryIconSource({
      remoteUrl: 'https://api.somossitu.com/assets/categories/map/hurto.png',
      remoteFailed: true,
      hasLocal: true,
    })).toBe('local');
  });
});