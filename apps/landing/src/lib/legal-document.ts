export const legalPlaceholder = 'LEGAL_PLACEHOLDER';
export const legalReviewBanner = 'Texto en revisión';

export type LegalBlock = { kind: 'heading' | 'paragraph' | 'item'; text: string };

export type LegalDocument = {
  title: string;
  version: string;
  updated: string;
  description: string;
  placeholder: boolean;
  blocks: LegalBlock[];
};

export const publicLegalPages = [
  { slug: 'terminos', path: '/terminos' },
  { slug: 'privacidad', path: '/privacidad' },
  { slug: 'politica-de-tratamiento-de-datos', path: '/politica-de-tratamiento-de-datos' },
  { slug: 'contenido-y-moderacion', path: '/contenido-y-moderacion' },
] as const;

export function domainRoot(value = process.env.DOMAIN_ROOT) {
  const raw = (value ?? 'somossitu.com').trim().replace(/^https?:\/\//, '').replace(/\/$/, '');
  return raw.length > 0 ? raw : 'somossitu.com';
}

export function publicSiteUrls(root = domainRoot()) {
  return {
    landing: `https://${root}`,
    app: `https://app.${root}`,
    help: `https://ayuda.${root}`,
    termsUrl: `https://${root}/terminos`,
    privacyUrl: `https://${root}/privacidad`,
    treatmentUrl: `https://${root}/politica-de-tratamiento-de-datos`,
    moderationUrl: `https://${root}/contenido-y-moderacion`,
    supportUrl: `https://ayuda.${root}/contacto`,
    deleteAccountUrl: `https://app.${root}/cuenta/eliminar`,
    supportEmail: `soporte@${root}`,
  };
}

export function parseLegalDocument(source: string): LegalDocument {
  const match = source.match(/^---\r?\n([\s\S]*?)\r?\n---\r?\n?([\s\S]*)$/);
  const meta = match?.[1] ?? '';
  const body = match?.[2] ?? source;
  const fields = new Map<string, string>();
  for (const line of meta.split('\n')) {
    const index = line.indexOf(':');
    if (index > 0) {
      fields.set(line.slice(0, index).trim(), line.slice(index + 1).trim());
    }
  }

  const blocks: LegalBlock[] = [];
  for (const chunk of body.split(/\n{2,}/)) {
    const text = chunk.replaceAll(legalPlaceholder, '').trim();
    if (text.length === 0) {
      continue;
    }
    if (text.startsWith('## ')) {
      blocks.push({ kind: 'heading', text: text.slice(3).trim() });
      continue;
    }
    const lines = text.split('\n');
    if (lines.every((line) => line.startsWith('- '))) {
      for (const line of lines) {
        blocks.push({ kind: 'item', text: line.slice(2).trim() });
      }
      continue;
    }
    blocks.push({ kind: 'paragraph', text: lines.join(' ').trim() });
  }

  return {
    title: fields.get('title') ?? '',
    version: fields.get('version') ?? '',
    updated: fields.get('updated') ?? '',
    description: fields.get('description') ?? '',
    placeholder: source.includes(legalPlaceholder),
    blocks: blocks.filter((block) => block.text.length > 0),
  };
}

export function renderLegalText(document: LegalDocument) {
  const banner = document.placeholder ? legalReviewBanner : '';
  const body = document.blocks.map((block) => block.text).join('\n');
  return [`Versión ${document.version}`, document.updated, banner, document.title, body].filter(Boolean).join('\n');
}
