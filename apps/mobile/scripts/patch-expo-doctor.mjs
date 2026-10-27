/**
 * expo-doctor 1.21.5 cannot skip a single schema error. This rewrites the
 * installed bundle so asset errors listed in package.json
 * expo.doctor.ignoreAssetErrorCodes are dropped, and only NOT_SQUARE.
 * The isotipo is 83×110. The brand is not redrawn. Other schema errors still fail.
 * See docs/tiendas/ficha.md.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const candidates = [
  path.resolve(here, '../../../node_modules/expo-doctor/build/index.js'),
  path.resolve(here, '../node_modules/expo-doctor/build/index.js'),
];
const target = candidates.find((file) => fs.existsSync(file));

if (!target) {
  process.exit(0);
}

const marker = 'function filterIgnoredAssetErrors(';
const source = fs.readFileSync(target, 'utf8');
if (source.includes(marker)) {
  process.exit(0);
}

const classMarker =
  'class ExpoConfigSchemaCheck{description="Check Expo config (app.json/ app.config.js) schema"';
const classAt = source.indexOf(classMarker);
const push = 'if(E){g.push(E)}';
const pushAt = classAt === -1 ? -1 : source.indexOf(push, classAt);

if (classAt === -1 || pushAt === -1 || pushAt - classAt > 800) {
  console.error(
    'patch-expo-doctor: expo-doctor 1.21.5 changed and the NOT_SQUARE filter no longer matches. Update apps/mobile/scripts/patch-expo-doctor.mjs.',
  );
  process.exit(1);
}

const filter =
  'function filterIgnoredAssetErrors(projectRoot,message){let codes=[];try{const pkg=require(require("path").join(projectRoot,"package.json"));const configured=pkg&&pkg.expo&&pkg.expo.doctor&&pkg.expo.doctor.ignoreAssetErrorCodes;if(Array.isArray(configured)){codes=configured}}catch{}if(!codes.length){return message}const snippets={NOT_SQUARE:"image should be square"};const ignored=codes.map((code)=>snippets[code]).filter(Boolean);if(!ignored.length){return message}const kept=message.split("\\n").filter((line)=>!ignored.some((snippet)=>line.includes(snippet)));return kept.some((line)=>line.includes("Field:"))?kept.join("\\n"):null}';

const next =
  source.slice(0, classAt) +
  filter +
  source.slice(classAt, pushAt) +
  'if(E){const filtered=filterIgnoredAssetErrors(f,E);if(filtered){g.push(filtered)}}' +
  source.slice(pushAt + push.length);

fs.writeFileSync(target, next);
