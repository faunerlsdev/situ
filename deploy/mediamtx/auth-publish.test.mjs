import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { createServer } from 'node:http';
import { spawn } from 'node:child_process';
import { mkdtemp, readFile, rm, writeFile, access } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { createConnection, createServer as createNetServer } from 'node:net';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';

const exec = promisify(execFile);
const here = dirname(fileURLToPath(import.meta.url));
const version = 'v1.21.3';

function assetName() {
  const arch = process.arch === 'arm64' ? 'arm64' : 'amd64';
  const os = process.platform === 'darwin' ? 'darwin' : 'linux';
  return `mediamtx_${version}_${os}_${arch}.tar.gz`;
}

async function binary() {
  const dir = join(tmpdir(), `situ-mediamtx-${version}-${process.platform}-${process.arch}`);
  const bin = join(dir, 'mediamtx');
  try {
    await access(bin);
    return bin;
  } catch {
    // Download the pinned release once.
  }
  const name = assetName();
  const pinned = await readFile(join(here, 'checksums.sha256'), 'utf8');
  const line = pinned.split('\n').find((entry) => entry.endsWith(name));
  assert.ok(line, `missing checksum for ${name}`);
  const expected = line.split(' ')[0];
  const { mkdir } = await import('node:fs/promises');
  await mkdir(dir, { recursive: true });
  const archive = join(dir, name);
  const response = await fetch(`https://github.com/bluenviron/mediamtx/releases/download/${version}/${name}`);
  assert.equal(response.ok, true);
  await writeFile(archive, Buffer.from(await response.arrayBuffer()));
  const hash = createHash('sha256').update(await readFile(archive)).digest('hex');
  assert.equal(hash, expected);
  await exec('tar', ['-xzf', archive, '-C', dir, 'mediamtx']);
  return bin;
}

function listen(server) {
  return new Promise((resolve) => {
    server.listen(0, '127.0.0.1', () => resolve(server.address().port));
  });
}

test('MediaMTX forwards the RTMP query password to the authorize API', { timeout: 120_000 }, async () => {
  const ffmpeg = await exec('which', ['ffmpeg']).then((result) => result.stdout.trim());
  const mediamtx = await binary();
  const id = '0123456789abcdef0123456789abcdef';
  const key = 'abcdefabcdefabcdefabcdefabcdefab';
  const ingest = 'ingest-for-mediamtx-auth-test';
  const seen = [];
  const auth = createServer((request, response) => {
    const chunks = [];
    request.on('data', (chunk) => chunks.push(chunk));
    request.on('end', () => {
      const body = JSON.parse(Buffer.concat(chunks).toString('utf8') || '{}');
      seen.push({
        action: body.action,
        path: body.path,
        user: body.user,
        password: body.password,
        query: body.query,
      });
      const allowed = body.action === 'publish' && body.path === id && body.user === 'situ' && body.password === key;
      response.writeHead(allowed ? 200 : 401);
      response.end();
    });
  });
  const authPort = await listen(auth);
  const work = await mkdtemp(join(tmpdir(), 'situ-mtx-auth-'));
  const rtmpPort = await freePort();
  const config = join(work, 'mediamtx.yml');
  await writeFile(config, `logLevel: error
authMethod: http
authHTTPAddress: http://127.0.0.1:${authPort}/internal/live/authorize?secret=${ingest}
authHTTPExclude:
  - action: api
rtsp: false
srt: false
webrtc: false
moq: false
hls: false
api: false
rtmp: true
rtmpEncryption: "no"
rtmpAddress: 127.0.0.1:${rtmpPort}
pathDefaults:
  record: false
paths:
  "~^[a-f0-9]{32}$":
    source: publisher
`);
  const server = spawn(mediamtx, [config], { stdio: ['ignore', 'pipe', 'pipe'] });
  let serverLog = '';
  server.stdout.on('data', (chunk) => {
    serverLog += chunk.toString('utf8');
  });
  server.stderr.on('data', (chunk) => {
    serverLog += chunk.toString('utf8');
  });
  try {
    await waitForPort(rtmpPort);
    const wrong = await runFfmpeg(ffmpeg, `rtmp://127.0.0.1:${rtmpPort}/${id}?user=situ&pass=wrong-key`);
    assert.notEqual(wrong, 0);
    const published = await runFfmpeg(ffmpeg, `rtmp://127.0.0.1:${rtmpPort}/${id}?user=situ&pass=${key}`);
    assert.equal(published, 0, redact(serverLog));
    assert.equal(seen.length >= 2, true);
    assert.equal(seen[0].password === key, false);
    assert.equal(seen.at(-1).action, 'publish');
    assert.equal(seen.at(-1).path, id);
    assert.equal(seen.at(-1).user, 'situ');
    assert.equal(seen.at(-1).password === key, true);
    assert.equal(String(seen.at(-1).query).includes('user=situ'), true);
    assert.equal(String(seen.at(-1).query).includes('pass='), true);
  } finally {
    server.kill('SIGTERM');
    auth.close();
    await rm(work, { recursive: true, force: true });
  }
});

function freePort() {
  return new Promise((resolve) => {
    const server = createNetServer();
    server.listen(0, '127.0.0.1', () => {
      const { port } = server.address();
      server.close(() => resolve(port));
    });
  });
}

function redact(text) {
  return text.replace(/pass=[^&\s]+/g, 'pass=[redacted]');
}

function waitForPort(port) {
  return new Promise((resolve, reject) => {
    const started = Date.now();
    const attempt = () => {
      const socket = createConnection({ host: '127.0.0.1', port }, () => {
        socket.end();
        resolve();
      });
      socket.on('error', () => {
        socket.destroy();
        if (Date.now() - started > 15_000) {
          reject(new Error(`MediaMTX did not listen on ${port}`));
          return;
        }
        setTimeout(attempt, 150);
      });
    };
    attempt();
  });
}

function runFfmpeg(ffmpeg, url) {
  return new Promise((resolve) => {
    const child = spawn(ffmpeg, [
      '-hide_banner',
      '-loglevel',
      'error',
      '-f',
      'lavfi',
      '-i',
      'testsrc=size=160x120:rate=10',
      '-t',
      '1',
      '-c:v',
      'libx264',
      '-pix_fmt',
      'yuv420p',
      '-preset',
      'ultrafast',
      '-an',
      '-f',
      'flv',
      url,
    ], { stdio: 'ignore' });
    child.on('exit', (code) => resolve(code ?? 1));
  });
}
