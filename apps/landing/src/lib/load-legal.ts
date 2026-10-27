import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { parseLegalDocument } from './legal-document';

export async function loadLegal(slug: string) {
  const file = path.join(process.cwd(), 'content/legal', `${slug}.md`);
  return parseLegalDocument(await readFile(file, 'utf8'));
}
