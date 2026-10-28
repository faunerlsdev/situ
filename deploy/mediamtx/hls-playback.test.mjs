import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { spawn } from 'node:child_process';
import { createServer } from 'node:http';
import { access, mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
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
const id = '0123456789abcdef0123456789abcdef';
const otherId = 'fedcba9876543210fedcba9876543210';

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

function freePort() {
  return new Promise((resolve, reject) => {
    const server = createNetServer();
    server.listen(0, '127.0.0.1', () => {
      const { port } = server.address();
      server.close(() => resolve(port));
    });
    server.on('error', reject);
  });
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
          reject(new Error(`nothing listened on ${port}`));
          return;
        }
        setTimeout(attempt, 150);
      });
    };
    attempt();
  });
}

function uris(playlist) {
  return playlist.split('\n').flatMap((line) => {
    const mapped = /URI="([^"]+)"/.exec(line);
    if (mapped) {
      return [mapped[1]];
    }
    if (line && !line.startsWith('#')) {
      return [line.trim()];
    }
    return [];
  });
}

test('an iPhone-shaped HLS request follows relative URLs and rejects a bad token', { timeout: 120_000 }, async () => {
  const yml = await readFile(join(here, 'mediamtx.yml'), 'utf8');
  assert.match(yml, /hlsVariant: mpegts/);
  assert.match(yml, /hlsSegmentDuration: 2s/);
  const ffmpeg = (await exec('which', ['ffmpeg'])).stdout.trim();
  const ffprobe = (await exec('which', ['ffprobe'])).stdout.trim();
  const nginxBin = (await exec('which', ['nginx'])).stdout.trim();
  const mediamtx = await binary();
  const valid = `${Math.floor(Date.now() / 1000) + 600}.${'ab'.repeat(32)}`;
  const expired = `1.${'cd'.repeat(32)}`;
  const authPort = await freePort();
  const rtmpPort = await freePort();
  const hlsPort = await freePort();
  const nginxPort = await freePort();
  const cdn = 'cdn-secret-for-hls-playback-test';
  const auth = createServer((request, response) => {
    const chunks = [];
    request.on('data', (chunk) => chunks.push(chunk));
    request.on('end', () => {
      if ((request.url ?? '').startsWith('/internal/live/hls')) {
        const token = String(request.headers['x-live-token'] ?? '');
        const uri = String(request.headers['x-original-uri'] ?? '');
        const path = /\/hls\/([a-f0-9]{32})/.exec(uri)?.[1] ?? '';
        const expires = Number(token.split('.')[0]);
        const shaped = /^[0-9]+\.[0-9a-f]{64}$/.test(token);
        const allowed = shaped && expires > Math.floor(Date.now() / 1000) && path === id && token === valid;
        response.writeHead(allowed ? 200 : 401);
        response.end();
        return;
      }
      const body = JSON.parse(Buffer.concat(chunks).toString('utf8') || '{}');
      const allowed = body.action === 'publish' && body.path === id && body.user === 'situ' && body.password === 'secret';
      response.writeHead(allowed ? 200 : 401);
      response.end();
    });
  });
  await new Promise((resolve) => auth.listen(authPort, '127.0.0.1', resolve));
  const work = await mkdtemp(join(tmpdir(), 'situ-hls-play-'));
  await writeFile(join(work, 'mediamtx.yml'), `logLevel: error
authMethod: http
authHTTPAddress: http://127.0.0.1:${authPort}/internal/live/authorize
authHTTPExclude:
  - action: api
rtsp: false
srt: false
webrtc: false
moq: false
hls: true
hlsAddress: 127.0.0.1:${hlsPort}
hlsEncryption: false
hlsAllowOrigins: []
hlsTrustedProxies: []
hlsAlwaysRemux: false
hlsVariant: mpegts
hlsSegmentCount: 7
hlsSegmentDuration: 2s
hlsPartDuration: 200ms
hlsCDNSecret: "${cdn}"
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
  const template = await readFile(join(here, '../nginx/situ-live.conf'), 'utf8');
  const site = template
    .replaceAll('__HLS_CDN_SECRET__', cdn)
    .replaceAll('live.somossitu.com', '127.0.0.1')
    .replaceAll('listen 443 ssl http2;', `listen 127.0.0.1:${nginxPort};`)
    .replaceAll('listen [::]:443 ssl http2;', '')
    .replaceAll('ssl_certificate     /etc/letsencrypt/live/somossitu.com/fullchain.pem;', '')
    .replaceAll('ssl_certificate_key /etc/letsencrypt/live/somossitu.com/privkey.pem;', '')
    .replaceAll('http://127.0.0.1:15080/internal/live/hls', `http://127.0.0.1:${authPort}/internal/live/hls`)
    .replaceAll('http://127.0.0.1:18890;', `http://127.0.0.1:${hlsPort};`)
    .replaceAll('/var/log/nginx/situ-live.access.log', `${work}/access.log`);
  const prefix = join(work, 'nginx');
  await mkdir(join(prefix, 'logs'), { recursive: true });
  await mkdir(join(prefix, 'conf'), { recursive: true });
  await writeFile(join(prefix, 'conf', 'nginx.conf'), `worker_processes 1;
error_log ${prefix}/logs/error.log info;
pid ${prefix}/nginx.pid;
events { worker_connections 64; }
http {
  default_type application/octet-stream;
  access_log ${prefix}/logs/access.log;
  ${site}
}
`);
  const mtx = spawn(mediamtx, [join(work, 'mediamtx.yml')], { stdio: ['ignore', 'pipe', 'pipe'] });
  const nginx = spawn(nginxBin, ['-p', `${prefix}/`, '-c', `${prefix}/conf/nginx.conf`, '-g', 'daemon off;'], {
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  let nginxLog = '';
  nginx.stderr.on('data', (chunk) => {
    nginxLog += chunk.toString();
  });
  let publisher = null;
  const publisherArgs = [
    '-hide_banner', '-loglevel', 'error', '-re',
    '-f', 'lavfi', '-i', 'testsrc2=size=1280x720:rate=30',
    '-f', 'lavfi', '-i', 'sine=frequency=440:sample_rate=44100',
    '-c:v', 'libx264', '-profile:v', 'baseline', '-level', '3.1', '-pix_fmt', 'yuv420p',
    '-b:v', '2500k', '-g', '60', '-keyint_min', '60', '-sc_threshold', '0', '-r', '30',
    '-c:a', 'aac', '-profile:a', 'aac_low', '-b:a', '128k', '-ar', '44100', '-ac', '2',
    '-t', '24',
    '-f', 'flv', `rtmp://127.0.0.1:${rtmpPort}/${id}?user=situ&pass=secret`,
  ];

  async function got(path) {
    const response = await fetch(`http://127.0.0.1:${nginxPort}${path}`);
    const bytes = Buffer.from(await response.arrayBuffer());
    return {
      status: response.status,
      type: response.headers.get('content-type') ?? '',
      cache: response.headers.get('cache-control') ?? '',
      encoding: response.headers.get('content-encoding'),
      bytes,
    };
  }

  try {
    await waitForPort(nginxPort);
    await waitForPort(rtmpPort);
    publisher = spawn(ffmpeg, publisherArgs, { stdio: ['ignore', 'ignore', 'pipe'] });
    const denied = await got(`/hls/${id}/${expired}/index.m3u8`);
    assert.equal(denied.status, 401);
    const foreign = await got(`/hls/${otherId}/${valid}/index.m3u8`);
    assert.ok(foreign.status === 401 || foreign.status === 403);
    const bare = await got(`/hls/${id}/index.m3u8`);
    assert.equal(bare.status, 401);

    const watchPath = `/hls/${id}/${valid}/index.m3u8`;
    let master = null;
    for (let attempt = 0; attempt < 40 && !master; attempt += 1) {
      const response = await got(watchPath);
      if (response.status === 200 && response.bytes.includes('#EXTM3U')) {
        master = response;
        break;
      }
      await new Promise((resolve) => setTimeout(resolve, 300));
    }
    assert.ok(master, nginxLog ? 'playlist did not appear' : 'playlist did not appear');
    assert.equal(master.type, 'application/vnd.apple.mpegurl');
    assert.equal(master.encoding, null);
    const masterText = master.bytes.toString('utf8');
    assert.match(masterText, /CODECS="avc1\.[0-9a-f]+,mp4a\.40\.2"/);
    assert.match(masterText, /EXT-X-INDEPENDENT-SEGMENTS/);
    assert.doesNotMatch(masterText, /EXT-X-MEDIA/);
    assert.doesNotMatch(masterText, /EXT-X-PART/);
    const mediaRel = uris(masterText)[0];
    assert.ok(mediaRel);
    const mediaUrl = new URL(mediaRel, `http://127.0.0.1:${nginxPort}${watchPath}`);
    const parts = mediaUrl.pathname.split('/').filter(Boolean);
    assert.equal(parts[1], id);
    assert.match(parts[2], /^[0-9]+\.[0-9a-f]{64}$/);

    let mediaText = '';
    let segments = [];
    for (let attempt = 0; attempt < 20; attempt += 1) {
      const media = await got(`${mediaUrl.pathname}${mediaUrl.search}`);
      assert.equal(media.status, 200, 'media playlist');
      assert.equal(media.type, 'application/vnd.apple.mpegurl');
      mediaText = media.bytes.toString('utf8');
      segments = uris(mediaText).filter((uri) => uri.endsWith('.ts'));
      if (segments.length >= 3) {
        break;
      }
      await new Promise((resolve) => setTimeout(resolve, 500));
    }
    assert.equal(segments.length >= 3, true);
    assert.match(mediaText, /#EXT-X-VERSION:3/);
    assert.doesNotMatch(mediaText, /EXT-X-MAP/);
    const firstSegmentAt = mediaText.indexOf(segments[0]);
    const firstDateAt = mediaText.indexOf('#EXT-X-PROGRAM-DATE-TIME');
    assert.equal(firstDateAt >= 0 && firstDateAt < firstSegmentAt, true);
    const saved = [];
    for (const segment of segments.slice(0, 3)) {
      const next = new URL(segment, mediaUrl);
      const file = await got(`${next.pathname}${next.search}`);
      assert.equal(file.status, 200, 'segment');
      assert.equal(file.type, 'video/mp2t');
      assert.equal(file.cache, 'no-store');
      assert.equal(file.encoding, null);
      assert.equal(file.bytes.length > 0, true);
      assert.equal(file.bytes[0], 0x47);
      saved.push(file.bytes);
    }
    const sample = join(work, 'seg.ts');
    await writeFile(sample, saved[0]);
    const probe = await exec(ffprobe, [
      '-hide_banner', '-show_streams',
      '-show_entries', 'stream=codec_name,profile,sample_rate,channels,codec_type',
      '-of', 'compact', sample,
    ]);
    assert.match(probe.stdout, /codec_name=h264/);
    assert.match(probe.stdout, /profile=Constrained Baseline/);
    assert.match(probe.stdout, /codec_name=aac/);
    assert.match(probe.stdout, /sample_rate=44100/);
    assert.match(probe.stdout, /channels=2/);
    const frames = await exec(ffprobe, [
      '-hide_banner', '-select_streams', 'v:0', '-show_frames',
      '-show_entries', 'frame=key_frame,pict_type', '-of', 'csv', sample,
    ]);
    const rows = frames.stdout.trim().split('\n').filter(Boolean);
    assert.equal(rows[0], 'frame,1,I');
    assert.equal(rows.filter((row) => row.startsWith('frame,1')).length >= 1, true);
  } finally {
    publisher?.kill('SIGTERM');
    nginx.kill('SIGTERM');
    mtx.kill('SIGTERM');
    auth.close();
    await rm(work, { recursive: true, force: true });
  }
});
