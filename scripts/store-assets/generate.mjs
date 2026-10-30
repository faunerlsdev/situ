import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const brandDir = path.join(root, 'situ-brand/brand');
const outDir = path.join(root, 'apps/mobile/store-assets');
const isotipoPath = path.join(brandDir, 'situ-isotipo-naranja-claro.png');
const verticalPath = path.join(brandDir, 'situ-logo-vertical-naranja.png');
const montserratPath = path.join(
  root,
  'node_modules/@expo-google-fonts/montserrat/800ExtraBold/Montserrat_800ExtraBold.ttf',
);
const sourceSansPath = path.join(
  root,
  'node_modules/@expo-google-fonts/source-sans-3/600SemiBold/SourceSans3_600SemiBold.ttf',
);

const background = '#1C1C1C';
const gradientCenter = '#34352B';
const headline = 'Lo que pasa, donde pasa.';
const subtitle = 'Mapa de reportes y transmisiones en vivo';

const cacheDir = path.join(os.tmpdir(), 'situ-store-fontconfig');
fs.mkdirSync(cacheDir, { recursive: true });
process.env.XDG_CACHE_HOME = cacheDir;

function assertFile(filePath) {
  if (!fs.existsSync(filePath)) {
    throw new Error(`No está el archivo ${filePath}`);
  }
}

async function markSized(sourcePath, targetWidth) {
  const source = await sharp(sourcePath).metadata();
  if (!source.width || !source.height) {
    throw new Error(`No se pudo leer ${sourcePath}`);
  }
  const targetHeight = Math.round((targetWidth * source.height) / source.width);
  const buffer = await sharp(sourcePath)
    .resize(targetWidth, targetHeight, {
      fit: 'contain',
      background: { r: 0, g: 0, b: 0, alpha: 0 },
    })
    .png()
    .toBuffer();
  const resized = await sharp(buffer).metadata();
  const ratio = source.width / source.height;
  const resizedRatio = (resized.width ?? 1) / (resized.height ?? 1);
  if (Math.abs(ratio - resizedRatio) > 0.02) {
    throw new Error(`El logo se estiró: ${sourcePath}`);
  }
  return { buffer, width: resized.width ?? targetWidth, height: resized.height ?? targetHeight };
}

async function storeIcon(size) {
  const targetWidth = Math.round(size * 0.62);
  const mark = await markSized(isotipoPath, targetWidth);
  const left = Math.round((size - mark.width) / 2);
  const top = Math.round((size - mark.height) / 2);
  return sharp({
    create: { width: size, height: size, channels: 3, background },
  })
    .composite([{ input: mark.buffer, left, top }])
    .removeAlpha()
    .png({ compressionLevel: 9, palette: false })
    .toBuffer();
}

async function renderText(text, font, fontfile, maxWidth, color) {
  const raw = await sharp({
    text: {
      text,
      font,
      fontfile,
      width: maxWidth,
      align: 'left',
      rgba: true,
      dpi: 72,
    },
  })
    .png()
    .toBuffer();
  const meta = await sharp(raw).metadata();
  const width = meta.width ?? 1;
  const height = meta.height ?? 1;
  const buffer = await sharp({
    create: { width, height, channels: 4, background: color },
  })
    .composite([{ input: raw, blend: 'dest-in' }])
    .png()
    .toBuffer();
  return { buffer, width, height };
}

function inside(box, safe, label) {
  const right = box.left + box.width;
  const bottom = box.top + box.height;
  if (box.left < safe.left || box.top < safe.top || right > safe.right || bottom > safe.bottom) {
    throw new Error(
      `${label} sale del 80% central (${box.left},${box.top} ${box.width}x${box.height})`,
    );
  }
}

async function featureGraphic() {
  const width = 1024;
  const height = 500;
  const safe = {
    left: Math.round(width * 0.1),
    top: Math.round(height * 0.1),
    right: Math.round(width * 0.9),
    bottom: Math.round(height * 0.9),
  };
  const vertical = await sharp(verticalPath).metadata();
  const logoWidth = Math.round(260 * ((vertical.width ?? 1) / (vertical.height ?? 1)));
  const logo = await markSized(verticalPath, logoWidth);
  const logoTop = Math.round((height - logo.height) / 2);
  const logoLeft = safe.left + 16;
  const textLeft = Math.round(width / 2) + 36;
  const textWidth = safe.right - 16 - textLeft;
  const title = await renderText(headline, 'Montserrat 52', montserratPath, textWidth, '#FFFFFF');
  const detail = await renderText(subtitle, 'Source Sans 3 26', sourceSansPath, textWidth, '#FF9F24');
  const gap = 14;
  const blockHeight = title.height + gap + detail.height;
  const textTop = Math.round((height - blockHeight) / 2);
  const layers = [
    { label: 'logo', left: logoLeft, top: logoTop, width: logo.width, height: logo.height },
    { label: 'título', left: textLeft, top: textTop, width: title.width, height: title.height },
    {
      label: 'subtítulo',
      left: textLeft,
      top: textTop + title.height + gap,
      width: detail.width,
      height: detail.height,
    },
  ];
  for (const layer of layers) {
    inside(layer, safe, layer.label);
  }

  const svg = `<?xml version="1.0" encoding="UTF-8"?>
<svg width="${width}" height="${height}" viewBox="0 0 ${width} ${height}" xmlns="http://www.w3.org/2000/svg">
  <defs>
    <radialGradient id="brand" gradientUnits="userSpaceOnUse" cx="${width / 2}" cy="${height / 2}" r="570">
      <stop offset="0%" stop-color="${gradientCenter}"/>
      <stop offset="100%" stop-color="${background}"/>
    </radialGradient>
  </defs>
  <rect width="${width}" height="${height}" fill="url(#brand)"/>
</svg>`;

  return sharp(Buffer.from(svg))
    .resize(width, height)
    .removeAlpha()
    .composite([
      { input: logo.buffer, left: logoLeft, top: logoTop },
      { input: title.buffer, left: textLeft, top: textTop },
      { input: detail.buffer, left: textLeft, top: textTop + title.height + gap },
    ])
    .removeAlpha()
    .png({ compressionLevel: 9, palette: false })
    .toBuffer();
}

async function writePng(name, buffer) {
  const filePath = path.join(outDir, name);
  await sharp(buffer).png({ compressionLevel: 9, palette: false }).toFile(filePath);
  return filePath;
}

function fail(message) {
  console.error(message);
  process.exitCode = 1;
}

async function verify(filePath, expected) {
  const bytes = fs.statSync(filePath).size;
  const meta = await sharp(filePath).metadata();
  const kb = (bytes / 1024).toFixed(1);
  const alpha = meta.hasAlpha === true;
  console.log(
    `${path.basename(filePath)} ${meta.width}x${meta.height} ${kb} KB alpha=${alpha} channels=${meta.channels}`,
  );
  if (meta.width !== expected.width || meta.height !== expected.height) {
    fail(`${path.basename(filePath)} mide ${meta.width}x${meta.height}`);
  }
  if (bytes > expected.maxBytes) {
    fail(`${path.basename(filePath)} pesa ${kb} KB`);
  }
  if (expected.noAlpha && (alpha || meta.channels !== 3)) {
    fail(`${path.basename(filePath)} tiene canal alfa`);
  }
}

async function main() {
  for (const filePath of [isotipoPath, verticalPath, montserratPath, sourceSansPath]) {
    assertFile(filePath);
  }
  fs.mkdirSync(outDir, { recursive: true });

  const play = await storeIcon(512);
  const apple = await storeIcon(1024);
  const feature = await featureGraphic();

  const playPath = await writePng('play-icon-512.png', play);
  const featurePath = await writePng('play-feature-graphic-1024x500.png', feature);
  const applePath = await writePng('appstore-icon-1024.png', apple);

  await verify(playPath, { width: 512, height: 512, maxBytes: 1024 * 1024, noAlpha: true });
  await verify(featurePath, { width: 1024, height: 500, maxBytes: 15 * 1024 * 1024, noAlpha: true });
  await verify(applePath, { width: 1024, height: 1024, maxBytes: 15 * 1024 * 1024, noAlpha: true });

  if (process.exitCode) {
    process.exit(process.exitCode);
  }
}

await main();
