import { createHmac, timingSafeEqual } from 'node:crypto';
import { pathToFileURL } from 'node:url';

/** Short HLS ticket. The phone must not hold the secret; the API will sign these later. */
export const HLS_TOKEN_TTL_SECONDS = 120;
export const HLS_TOKEN_MAX_TTL_SECONDS = 180;

const PATH_PATTERN = /^[A-Za-z0-9_-]{1,64}$/;

export function assertStreamPath(path) {
  if (!PATH_PATTERN.test(path)) {
    throw new Error('El nombre de la transmisión solo admite letras, números, guion y guion bajo.');
  }
  return path;
}

export function signHlsToken({ secret, path, nowSeconds, ttlSeconds = HLS_TOKEN_TTL_SECONDS }) {
  if (!secret) {
    throw new Error('Falta el secreto del token.');
  }
  assertStreamPath(path);
  const ttl = Number(ttlSeconds);
  if (!Number.isInteger(ttl) || ttl < 1 || ttl > HLS_TOKEN_MAX_TTL_SECONDS) {
    throw new Error('La vida del token tiene que estar entre 1 y 180 segundos.');
  }
  const expires = Number(nowSeconds) + ttl;
  const mac = createHmac('sha256', secret).update(`${expires}.${path}`).digest('hex');
  return { expires, token: `${expires}.${mac}` };
}

export function verifyHlsToken({ secret, path, token, nowSeconds }) {
  if (!secret || !PATH_PATTERN.test(path) || typeof token !== 'string') {
    return false;
  }
  const match = /^([0-9]{1,12})\.([0-9a-f]{64})$/.exec(token);
  if (!match) {
    return false;
  }
  const expires = Number(match[1]);
  const now = Number(nowSeconds);
  if (expires < now || expires > now + HLS_TOKEN_MAX_TTL_SECONDS) {
    return false;
  }
  const expected = createHmac('sha256', secret).update(`${expires}.${path}`).digest('hex');
  const left = Buffer.from(match[2], 'hex');
  const right = Buffer.from(expected, 'hex');
  return left.length === right.length && timingSafeEqual(left, right);
}

function readArg(name) {
  const index = process.argv.indexOf(name);
  return index === -1 ? '' : process.argv[index + 1] ?? '';
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const secret = readArg('--secret');
  const path = readArg('--path');
  const ttl = Number(readArg('--ttl') || HLS_TOKEN_TTL_SECONDS);
  const signed = signHlsToken({ secret, path, nowSeconds: Math.floor(Date.now() / 1000), ttlSeconds: ttl });
  process.stdout.write(`token=${signed.token}&expires=${signed.expires}\n`);
}
