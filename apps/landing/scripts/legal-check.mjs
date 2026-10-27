import { readdir, readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const landingRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const directory = process.argv[2] ?? path.join(landingRoot, 'content/legal');
const names = (await readdir(directory)).filter((name) => name.endsWith('.md')).sort();
const marked = [];

for (const name of names) {
  const text = await readFile(path.join(directory, name), 'utf8');
  if (text.includes('LEGAL_PLACEHOLDER')) {
    marked.push(name);
  }
}

if (marked.length > 0) {
  console.error(`LEGAL_PLACEHOLDER en: ${marked.join(', ')}`);
  process.exit(1);
}
