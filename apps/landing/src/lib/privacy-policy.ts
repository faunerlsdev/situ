import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { legalIdentity, type LegalIdentity } from '../../config/legal-identity';

export const privacySourcePath = path.join(process.cwd(), '../../docs/legal/politica-privacidad-situ-v1.1.md');

export type PrivacyBlock =
  | { kind: 'paragraph'; text: string }
  | { kind: 'list'; ordered: boolean; items: string[] }
  | { kind: 'table'; headers: string[]; rows: string[][] }
  | { kind: 'subheading'; id: string; text: string };

export type PrivacySection = {
  id: string;
  title: string;
  blocks: PrivacyBlock[];
};

export type PrivacyDocument = {
  title: string;
  intro: PrivacyBlock[];
  sections: PrivacySection[];
  missing: boolean;
};

export function redactPrivacySource(source: string, identity: LegalIdentity) {
  if (identity.showPrivateIdentity) {
    return source;
  }
  const hidden = [identity.nationalId, identity.nationalId.replaceAll('.', ''), identity.phone, identity.phone.replaceAll(' ', ''), identity.physicalAddress];
  return source
    .split('\n')
    .filter((line) => !hidden.some((value) => value.length > 0 && line.includes(value)))
    .join('\n');
}

export function parsePrivacyPolicy(
  source: string,
  identity: LegalIdentity,
  sectionId: (title: string) => string = headingId,
): PrivacyDocument {
  const lines = redactPrivacySource(source, identity).replaceAll('\r\n', '\n').split('\n');
  let title = 'Política de privacidad';
  const intro: PrivacyBlock[] = [];
  const sections: PrivacySection[] = [];
  let section: PrivacySection | null = null;
  let index = 0;

  const target = () => (section ? section.blocks : intro);

  while (index < lines.length) {
    const line = lines[index] ?? '';
    if (line.trim() === '' || line.trim() === '---') {
      index += 1;
      continue;
    }
    if (line.startsWith('# ')) {
      title = line.slice(2).trim();
      index += 1;
      continue;
    }
    if (line.startsWith('## ')) {
      const text = line.slice(3).trim();
      section = { id: sectionId(text), title: text, blocks: [] };
      sections.push(section);
      index += 1;
      continue;
    }
    if (line.startsWith('### ')) {
      const text = line.slice(4).trim();
      target().push({ kind: 'subheading', id: headingId(text), text });
      index += 1;
      continue;
    }
    if (line.startsWith('|')) {
      const tableLines: string[] = [];
      while (index < lines.length && (lines[index] ?? '').startsWith('|')) {
        tableLines.push(lines[index] ?? '');
        index += 1;
      }
      const table = parseTable(tableLines);
      if (table) {
        target().push(table);
      }
      continue;
    }
    if (line.startsWith('**')) {
      target().push({ kind: 'paragraph', text: line.trim() });
      index += 1;
      continue;
    }
    if (line.startsWith('- ') || /^\d+\. /.test(line)) {
      const ordered = /^\d+\. /.test(line);
      const items: string[] = [];
      while (index < lines.length) {
        const current = lines[index] ?? '';
        const marker = ordered ? /^\d+\. / : /^- /;
        if (!marker.test(current)) {
          break;
        }
        items.push(current.replace(marker, '').trim());
        index += 1;
      }
      target().push({ kind: 'list', ordered, items });
      continue;
    }
    const paragraph: string[] = [];
    while (index < lines.length) {
      const current = lines[index] ?? '';
      if (current.trim() === '' || current.trim() === '---' || current.startsWith('#') || current.startsWith('|') || current.startsWith('- ') || current.startsWith('**') || /^\d+\. /.test(current)) {
        break;
      }
      paragraph.push(current.trim());
      index += 1;
    }
    const text = paragraph.join(' ').trim();
    if (text.length > 0) {
      target().push({ kind: 'paragraph', text });
    }
  }

  return { title, intro, sections, missing: false };
}

export async function loadPrivacyPolicy(identity: LegalIdentity = legalIdentity): Promise<PrivacyDocument> {
  try {
    const source = await readFile(privacySourcePath, 'utf8');
    return parsePrivacyPolicy(source, identity);
  } catch (error) {
    const code = (error as NodeJS.ErrnoException).code;
    if (code === 'ENOENT') {
      return {
        title: 'Política de privacidad',
        intro: [{ kind: 'paragraph', text: 'El texto se lee de docs/legal/politica-privacidad-situ-v1.1.md.' }],
        sections: [],
        missing: true,
      };
    }
    throw error;
  }
}

export function headingId(text: string) {
  return text
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '');
}

function parseTable(lines: string[]): PrivacyBlock | null {
  const rows = lines
    .map((line) => line.split('|').slice(1, -1).map((cell) => cell.trim()))
    .filter((cells) => cells.length > 0 && !cells.every((cell) => /^:?-{3,}:?$/.test(cell)));
  const headers = rows[0];
  if (!headers) {
    return null;
  }
  return { kind: 'table', headers, rows: rows.slice(1) };
}
