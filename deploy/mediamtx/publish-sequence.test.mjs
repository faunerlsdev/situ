// MediaMTX 1.21.3 accepts the HaishinKit and RootEncoder publish sequences.
// HaishinKit 2.2.5 awaits releaseStream and FCPublish before createStream.
// MediaMTX never replies to those commands, so the connection hits readTimeout.
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
import { publishSequence } from './rtmp-client.mjs';

const exec = promisify(execFile);
const here = dirname(fileURLToPath(import.meta.url));
const version = 'v1.21.3';
const id = '0123456789abcdef0123456789abcdef';
const key = '0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef';

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
  return text
    .replace(/pass=[^&\s"]+/g, 'pass=[redacted]')
    .replace(/"(password|token)":"[^"]*"/g, '"$1":"[redacted]"');
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

function summarize(result) {
  return {
    mode: result.mode,
    failed: result.failed,
    sent: result.sent.map((entry) => `${entry.name} payload=${entry.payload} wire=${entry.wire}`),
    received: result.received.map((entry) => `${entry.name || 'type-' + entry.type}${entry.after ? ' after ' + entry.after : ''}`),
  };
}

test('HaishinKit and RootEncoder publish sequences against MediaMTX 1.21.3', { timeout: 90_000 }, async () => {
  const mediamtx = await binary();
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
      const allowed = body.action === 'publish'
        && body.path === id
        && body.user === 'situ'
        && body.password === key
        && String(body.query).includes('user=situ')
        && String(body.query).includes(`pass=${key}`);
      response.writeHead(allowed ? 200 : 401);
      response.end();
    });
  });
  const authPort = await listen(auth);
  const work = await mkdtemp(join(tmpdir(), 'situ-mtx-seq-'));
  const rtmpPort = await freePort();
  const config = join(work, 'mediamtx.yml');
  await writeFile(config, `logLevel: debug
authMethod: http
authHTTPAddress: http://127.0.0.1:${authPort}/internal/live/authorize?secret=seq-test
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
readTimeout: 6s
writeTimeout: 6s
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
  const results = [];
  try {
    await waitForPort(rtmpPort);
    for (const mode of ['root', 'haishin-fragmented', 'haishin', 'haishin-unfragmented', 'haishin-fmle-wait']) {
      const before = serverLog.length;
      const authBefore = seen.length;
      const result = await publishSequence({
        port: rtmpPort,
        mode,
        id,
        key,
        timeoutMs: mode === 'haishin-fmle-wait' ? 8_000 : 5_000,
      });
      if (mode === 'haishin-unfragmented' || mode === 'haishin-fmle-wait') {
        await new Promise((resolve) => setTimeout(resolve, 400));
      }
      const log = redact(serverLog.slice(before));
      results.push({
        ...summarize(result),
        auth: seen.slice(authBefore).map((entry) => ({
          action: entry.action,
          path: entry.path,
          user: entry.user,
          passwordMatched: entry.password === key,
        })),
        closed: log.split('\n').filter((line) => line.includes('closed:')).map((line) => line.replace(/.*closed: /, 'closed: ')),
      });
    }
    const byMode = Object.fromEntries(results.map((result) => [result.mode, result]));
    for (const mode of ['root', 'haishin-fragmented', 'haishin']) {
      const result = byMode[mode];
      assert.equal(result.failed, '', JSON.stringify(result));
      assert.equal(result.auth.some((entry) => entry.passwordMatched && entry.path === id && entry.user === 'situ'), true, JSON.stringify(result));
      assert.equal(result.received.some((entry) => entry.includes('onStatus')), true, JSON.stringify(result));
    }
    const broken = byMode['haishin-unfragmented'];
    assert.equal(broken.auth.length, 0, JSON.stringify(broken));
    assert.equal(broken.closed.some((line) => line.includes('received type 1 chunk without previous chunk')), true, JSON.stringify(broken));
    const blocked = byMode['haishin-fmle-wait'];
    assert.equal(blocked.sent.some((entry) => entry.startsWith('createStream') || entry.startsWith('publish')), false, JSON.stringify(blocked.sent));
    assert.equal(blocked.sent.some((entry) => entry.startsWith('releaseStream') && entry.includes('wire=153')), true, JSON.stringify(blocked.sent));
    assert.equal(blocked.sent.some((entry) => entry.startsWith('FCPublish') && entry.includes('wire=149')), true, JSON.stringify(blocked.sent));
    assert.equal(blocked.auth.length, 0, JSON.stringify(blocked));
    assert.equal(blocked.closed.some((line) => line.includes('i/o timeout')), true, JSON.stringify(blocked));
  } finally {
    server.kill('SIGTERM');
    auth.close();
    await rm(work, { recursive: true, force: true });
  }
});

test('HaishinKit sequence over RTMPS reaches authorize', { timeout: 60_000 }, async () => {
  const mediamtx = await binary();
  const seen = [];
  const auth = createServer((request, response) => {
    const chunks = [];
    request.on('data', (chunk) => chunks.push(chunk));
    request.on('end', () => {
      const body = JSON.parse(Buffer.concat(chunks).toString('utf8') || '{}');
      seen.push(body);
      const allowed = body.action === 'publish' && body.path === id && body.user === 'situ' && body.password === key;
      response.writeHead(allowed ? 200 : 401);
      response.end();
    });
  });
  const authPort = await listen(auth);
  const work = await mkdtemp(join(tmpdir(), 'situ-mtx-rtmps-'));
  const rtmpPort = await freePort();
  const cert = join(work, 'cert.pem');
  const certKey = join(work, 'key.pem');
  await exec('openssl', [
    'req', '-x509', '-newkey', 'ec', '-pkeyopt', 'ec_paramgen_curve:prime256v1',
    '-keyout', certKey, '-out', cert, '-days', '1', '-nodes', '-subj', '/CN=127.0.0.1',
  ]);
  await writeFile(join(work, 'mediamtx.yml'), `logLevel: info
authMethod: http
authHTTPAddress: http://127.0.0.1:${authPort}/internal/live/authorize?secret=seq-test
authHTTPExclude:
  - action: api
rtsp: false
srt: false
webrtc: false
moq: false
hls: false
api: false
rtmp: true
rtmpEncryption: strict
rtmpsAddress: 127.0.0.1:${rtmpPort}
rtmpServerCert: ${cert}
rtmpServerKey: ${certKey}
readTimeout: 6s
writeTimeout: 6s
pathDefaults:
  record: false
paths:
  "~^[a-f0-9]{32}$":
    source: publisher
`);
  const server = spawn(mediamtx, [join(work, 'mediamtx.yml')], { stdio: ['ignore', 'pipe', 'pipe'] });
  let serverLog = '';
  server.stdout.on('data', (chunk) => {
    serverLog += chunk.toString('utf8');
  });
  server.stderr.on('data', (chunk) => {
    serverLog += chunk.toString('utf8');
  });
  try {
    await waitForPort(rtmpPort);
    const result = await publishSequence({
      port: rtmpPort,
      mode: 'haishin',
      id,
      key,
      tls: true,
      timeoutMs: 5_000,
    });
    assert.equal(result.failed, '', redact(serverLog));
    assert.equal(result.received.some((entry) => entry.name === 'onStatus'), true, JSON.stringify(summarize(result)));
    assert.equal(seen.some((entry) => entry.path === id && entry.password === key), true);
  } finally {
    server.kill('SIGTERM');
    auth.close();
    await rm(work, { recursive: true, force: true });
  }
});
