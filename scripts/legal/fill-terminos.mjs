import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const removalUrl = 'https://ayuda.somossitu.com/retiro-contenido';
const privacyUrl = 'https://somossitu.com/privacidad';
const termsUrl = 'https://somossitu.com/terminos';
const supportUrl = 'https://ayuda.somossitu.com/contacto';
const domicile = 'Yopal, Casanare, Colombia';

export function readLegalIdentity(source) {
  const read = (key) => {
    const match = source.match(new RegExp(`${key}:\\s*'([^']*)'`));
    if (!match?.[1]) {
      throw new Error(`Falta ${key} en legal-identity.ts`);
    }
    return match[1];
  };
  return {
    responsibleName: read('responsibleName'),
    supportEmail: read('supportEmail'),
    nationalId: read('nationalId'),
    phone: read('phone'),
    physicalAddress: read('physicalAddress'),
  };
}

function replaceExact(source, from, to) {
  if (!source.includes(from)) {
    throw new Error(`No está el texto a reemplazar: ${from}`);
  }
  return source.replaceAll(from, to);
}

function identityList(identity) {
  return [
    `- Nombre completo: ${identity.responsibleName}`,
    `- Identificación: C.C. ${identity.nationalId} de Yopal`,
    `- Domicilio: ${domicile}`,
    `- Dirección para notificaciones: ${identity.physicalAddress}`,
    `- Correo: ${identity.supportEmail}`,
    `- Teléfono: ${identity.phone}`,
  ].join('\n');
}

function deletionCopy(identity) {
  return [
    `El usuario podrá eliminar su cuenta directamente desde la aplicación, en **Mi SITU → Mi cuenta**, con el botón **Eliminar mi cuenta**. En esta etapa inicial no se ofrece un mecanismo web independiente para iniciar la eliminación.`,
    '',
    `Si el usuario no puede acceder a la aplicación, podrá solicitar la eliminación escribiendo a ${identity.supportEmail} desde el correo asociado a la cuenta, y SITU verificará razonablemente su identidad antes de tramitarla.`,
  ].join('\n');
}

function replaceSection23(source, identity) {
  const lines = source.split('\n');
  const start = lines.findIndex((line) => /^##\s+23(?:\.|\s)/.test(line));
  if (start < 0) {
    throw new Error('No está la sección 23');
  }
  let end = lines.findIndex((line, index) => index > start && /^##\s+/.test(line));
  if (end < 0) {
    end = lines.length;
  }
  const section = lines.slice(start + 1, end);
  const marker = section.findIndex((line) => line.includes('URL pública para eliminar la cuenta') || line.includes('[URL DE ELIMINACIÓN DE CUENTA]'));
  if (marker < 0) {
    throw new Error('No está la línea de la URL de eliminación de cuenta');
  }
  const kept = section.slice(marker + 1);
  while (kept[0]?.trim() === '') {
    kept.shift();
  }
  return [...lines.slice(0, start + 1), '', deletionCopy(identity), '', ...kept, ...lines.slice(end)].join('\n');
}

function valueForSection30(line, identity) {
  const label = line.toLowerCase();
  if (label.includes('eliminación de cuenta') || label.includes('eliminacion de cuenta')) {
    return 'Desde la app, en Mi SITU → Mi cuenta, botón Eliminar mi cuenta (ver sección 23)';
  }
  if (label.includes('retiro')) {
    return removalUrl;
  }
  if (label.includes('privacidad') || label.includes('datos personales')) {
    return supportUrl;
  }
  if (label.includes('soporte')) {
    return supportUrl;
  }
  if (label.includes('operador') || label.includes('nombre completo')) {
    return identity.responsibleName;
  }
  if (label.includes('identific')) {
    return `C.C. ${identity.nationalId} de Yopal`;
  }
  if (label.includes('domicilio')) {
    return domicile;
  }
  if (label.includes('dirección') || label.includes('direccion')) {
    return identity.physicalAddress;
  }
  if (label.includes('teléfono') || label.includes('telefono')) {
    return identity.phone;
  }
  if (label.includes('correo')) {
    return identity.supportEmail;
  }
  return null;
}

function fillSection30(source, identity) {
  const lines = source.split('\n');
  const start = lines.findIndex((line) => /^##\s+30(?:\.|\s)/.test(line));
  if (start < 0) {
    throw new Error('No está la sección 30');
  }
  let end = lines.findIndex((line, index) => index > start && /^##\s+/.test(line));
  if (end < 0) {
    end = lines.length;
  }
  for (let index = start + 1; index < end; index += 1) {
    const line = lines[index] ?? '';
    if (!line.includes('[')) {
      continue;
    }
    const value = valueForSection30(line, identity);
    if (!value) {
      continue;
    }
    lines[index] = line.replace(/\[[^\]]*\]/g, value);
  }
  return lines.join('\n');
}

export function fillTerminos(source, identity) {
  let text = source.replaceAll('\r\n', '\n');
  text = replaceExact(text, '**Fecha de publicación:** [28/10/2026]', '**Fecha de publicación:** 28/10/2026');
  text = replaceExact(text, '**Fecha de entrada en vigor:** [29/10/2026]', '**Fecha de entrada en vigor:** 29/10/2026');
  text = replaceExact(text, '*** Mis Datos ***', identityList(identity));
  text = replaceExact(text, '[URL PÚBLICA DE RETIRO DE CONTENIDO]', removalUrl);
  text = replaceExact(text, '[URL PÚBLICA DE LA POLÍTICA]', privacyUrl);
  text = replaceExact(text, '[URL PÚBLICA DE LOS TÉRMINOS]', termsUrl);
  text = replaceSection23(text, identity);
  text = fillSection30(text, identity);
  return text;
}

export function terminosProblems(filled) {
  const problems = [];
  if (filled.includes('*** Mis Datos ***')) {
    problems.push('Sigue *** Mis Datos ***');
  }
  if (/\[[^\]]*\]/.test(filled)) {
    problems.push('Siguen corchetes');
  }
  return problems;
}

async function main() {
  const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
  const sourcePath = path.join(root, 'docs/legal/terminos-situ-v1.0.md');
  const identityPath = path.join(root, 'apps/landing/config/legal-identity.ts');
  const source = await readFile(sourcePath, 'utf8');
  const identity = readLegalIdentity(await readFile(identityPath, 'utf8'));
  const filled = fillTerminos(source, identity);
  const problems = terminosProblems(filled);
  if (problems.length > 0) {
    const pending = filled.split('\n').filter((line) => line.includes('[') || line.includes(']') || line.includes('*** Mis Datos ***'));
    console.error(problems.join('\n'));
    console.error(pending.join('\n'));
    process.exit(1);
  }
}

const entry = process.argv[1] ? path.resolve(process.argv[1]) : '';
if (entry === fileURLToPath(import.meta.url)) {
  await main();
}
