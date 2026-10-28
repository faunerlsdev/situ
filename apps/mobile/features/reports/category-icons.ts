const aliases: Record<string, string> = {
  accidente_transito: 'accidente',
  manifestacion: 'protesta',
  obra_vial: 'obra_via',
  corte_servicio: 'corte_luz',
};

const slugs = new Set([
  'accidente',
  'trafico',
  'vehiculo_varado',
  'hurto',
  'puesto_control',
  'basuras',
  'hueco',
  'derrumbe',
  'inundacion',
  'incendio',
  'protesta',
  'obra_via',
  'corte_luz',
  'corte_agua',
  'peligro',
  'policia',
  'emergencia',
  'animal',
  'evento',
  'ruido',
  'semaforo',
  'via_cerrada',
  'otro',
]);

export function categorySlug(code: string | null): string | null {
  if (!code) {
    return null;
  }
  return aliases[code] ?? (slugs.has(code) ? code : null);
}

/** Seed files in /assets/categories/<code>.png are the same generic pin. Real drawings live under button/ and map/. */
export function isPlaceholderIcon(url: string | null): boolean {
  if (!url) {
    return true;
  }
  const path = (url.split('?')[0] ?? url).split('#')[0]?.toLowerCase() ?? '';
  if (path.endsWith('/default.png')) {
    return true;
  }
  return /\/assets\/categories\/[^/]+\.png$/.test(path);
}

export function categoryIconSource(input: {
  remoteUrl: string | null;
  remoteFailed: boolean;
  hasLocal: boolean;
}): 'remote' | 'local' | 'generic' {
  if (input.remoteUrl && !input.remoteFailed && !isPlaceholderIcon(input.remoteUrl)) {
    return 'remote';
  }
  if (input.hasLocal) {
    return 'local';
  }
  return 'generic';
}
