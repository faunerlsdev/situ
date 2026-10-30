import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { legalIdentity, type LegalIdentity } from '../../config/legal-identity';
import { parsePrivacyPolicy, type PrivacyDocument } from './privacy-policy';

export const childSafetySourcePath = path.join(process.cwd(), '../../docs/legal/seguridad-infantil-situ.md');

export function splitChildSafety(source: string) {
  const lines = source.replaceAll('\r\n', '\n').split('\n');
  let headings = 0;
  let splitAt = lines.length;
  for (let index = 0; index < lines.length; index += 1) {
    if ((lines[index] ?? '').startsWith('# ')) {
      headings += 1;
      if (headings === 2) {
        splitAt = index;
        break;
      }
    }
  }
  return {
    spanish: lines.slice(0, splitAt).join('\n'),
    english: lines.slice(splitAt).join('\n'),
  };
}

export async function loadChildSafety(identity: LegalIdentity = legalIdentity): Promise<{
  spanish: PrivacyDocument;
  english: PrivacyDocument;
}> {
  try {
    const source = await readFile(childSafetySourcePath, 'utf8');
    const parts = splitChildSafety(source);
    return {
      spanish: parsePrivacyPolicy(parts.spanish, identity),
      english: parsePrivacyPolicy(parts.english, identity),
    };
  } catch (error) {
    const code = (error as NodeJS.ErrnoException).code;
    if (code === 'ENOENT') {
      return {
        spanish: {
          title: 'Estándares de seguridad infantil',
          intro: [{ kind: 'paragraph', text: 'El texto se lee de docs/legal/seguridad-infantil-situ.md.' }],
          sections: [],
          missing: true,
        },
        english: {
          title: 'SITU child safety standards (English)',
          intro: [],
          sections: [],
          missing: true,
        },
      };
    }
    throw error;
  }
}
