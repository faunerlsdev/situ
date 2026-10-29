import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { legalIdentity, type LegalIdentity } from '../../config/legal-identity';
import { fillTerminos } from '../../../../scripts/legal/fill-terminos.mjs';
import { headingId, parsePrivacyPolicy, type PrivacyDocument } from './privacy-policy';

export const termsSourcePath = path.join(process.cwd(), '../../docs/legal/terminos-situ-v1.0.md');

export function termsSectionId(title: string) {
  const match = title.match(/^(\d+)(?:\.(?!\d)|\s)/);
  return match?.[1] ?? headingId(title);
}

export async function loadTerms(identity: LegalIdentity = legalIdentity): Promise<PrivacyDocument> {
  try {
    const source = await readFile(termsSourcePath, 'utf8');
    const filled = fillTerminos(source, identity);
    return parsePrivacyPolicy(filled, identity, termsSectionId);
  } catch (error) {
    const code = (error as NodeJS.ErrnoException).code;
    if (code === 'ENOENT') {
      return {
        title: 'Términos y condiciones',
        intro: [{ kind: 'paragraph', text: 'El texto se lee de docs/legal/terminos-situ-v1.0.md.' }],
        sections: [],
        missing: true,
      };
    }
    throw error;
  }
}
