function domainRoot(): string {
  return (process.env.EXPO_PUBLIC_DOMAIN_ROOT || '').trim().replace(/^https?:\/\//, '').replace(/\/$/, '');
}

/** Public page another surface already serves. Null until the store build has a domain. */
export function dataDeletionUrl(): string | null {
  const domain = domainRoot();
  return domain ? `https://app.${domain}/cuenta/eliminar` : null;
}

export function supportFallbackUrl(): string {
  const domain = domainRoot();
  return domain ? `https://ayuda.${domain}/contacto` : 'https://ayuda.somossitu.com/contacto';
}
