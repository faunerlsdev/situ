import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));

function read(path) {
  return readFileSync(join(root, path), 'utf8');
}

test('the live site reads the HLS token from the original URI', () => {
  const site = read('nginx/situ-live.conf');
  assert.match(site, /map \$request_uri \$live_token/);
  assert.match(site, /proxy_set_header X-Live-Token \$live_token;/);
  assert.doesNotMatch(site, /X-Live-Token \$arg_token/);
  assert.match(site, /rewrite "\^\/hls\/\(\[a-f0-9\]\{32\}\)\/\[0-9\]\+\\\.\[0-9a-f\]\{64\}\/\(\.\*\)\$"/);
  assert.match(site, /log_format situ_live/);
  assert.doesNotMatch(site, /Set-Cookie/);
  assert.doesNotMatch(site, /Cookie \$http_cookie/);
});

test('the live HLS variant is muxed MPEG-TS', () => {
  const yml = read('mediamtx/mediamtx.yml');
  assert.match(yml, /hlsVariant: mpegts/);
  assert.match(yml, /hlsSegmentDuration: 2s/);
  assert.doesNotMatch(yml, /hlsVariant: lowLatency/);
  assert.doesNotMatch(yml, /hlsVariant: fmp4/);
});

test('ports.env keeps the shared server defaults', () => {
  const ports = read('ports.env');
  for (const line of [
    'SITU_DOMAIN_ROOT=somossitu.com',
    'SITU_API_PORT=15080',
    'SITU_ADMIN_PORT=15081',
    'SITU_WEB_PORT=15082',
    'SITU_LANDING_PORT=15083',
    'SITU_SUPPORT_PORT=15084',
    'SITU_RTMPS_PORT=1936',
    'SITU_HLS_PORT=18890',
    'SITU_MTX_API_PORT=19998',
    'SITU_LIVE_AUTH_PORT=5099',
    'SITU_POSTGRES_HOST=/var/run/postgresql',
    'SITU_POSTGRES_PORT=',
  ]) {
    assert.match(ports, new RegExp(`^${line}$`, 'm'));
  }
});

test('install scripts do not change the shared host', () => {
  const forbidden = [
    [/^\s*(sudo\s+)?ufw\b/m, 'ufw'],
    [/systemctl restart nginx/, 'restart nginx'],
    [/systemctl restart postgresql/, 'restart postgresql'],
    [/systemctl restart systemd-journald/, 'restart journald'],
    [/systemctl restart fail2ban/, 'restart fail2ban'],
    [/apt-get/, 'apt-get'],
    [/sites-enabled\/default/, 'default site'],
    [/\/swapfile/, 'swap'],
  ];
  for (const script of ['provision.sh', 'nginx/install.sh', 'mediamtx/install.sh', 'remote-release.sh', 'check-ports.sh']) {
    const text = read(script);
    for (const [pattern, label] of forbidden) {
      assert.doesNotMatch(text, pattern, `${script} contains ${label}`);
    }
  }
  assert.match(read('provision.sh'), /prereqs-check\.sh/);
  assert.match(read('nginx/install.sh'), /situ_nginx_reload/);
  assert.match(read('nginx/install.sh'), /SITU_DOMAIN_ROOT|situ_apply_domain/);
  assert.match(read('mediamtx/install.sh'), /situ_nginx_reload/);
  assert.match(read('mediamtx/install.sh'), /situ_port_listening/);
  assert.doesNotMatch(read('mediamtx/install.sh'), /conf\.d/);
  assert.doesNotMatch(read('check-ports.sh'), /SITU_POSTGRES/);
  assert.match(read('remote-release.sh'), /SITU_API_PORT/);
  assert.doesNotMatch(read('gen-secrets.sh'), /Port=5432/);
  assert.match(read('gen-secrets.sh'), /SITU_POSTGRES_HOST/);
  assert.match(read('lib/start-api.sh'), /SITU_API_PORT/);
  assert.match(read('lib/start-next.sh'), /SITU_WEB_PORT/);
  assert.match(read('lib/start-next.sh'), /SITU_ADMIN_PORT/);
  assert.match(read('lib/start-next.sh'), /SITU_LANDING_PORT/);
  assert.match(read('lib/start-next.sh'), /SITU_SUPPORT_PORT/);
});

test('prereqs prints the postgres command and does not run it', () => {
  const text = read('prereqs-check.sh');
  assert.match(text, /SHOW port/);
  assert.match(text, /postgresql-18-postgis-3/);
  assert.match(text, /sudo apt-get update && sudo apt-get install -y postgresql/);
  assert.doesNotMatch(text, /command -v pnpm/);
  assert.doesNotMatch(text, /^\s*(sudo\s+)?apt-get\b/m);
  assert.doesNotMatch(text, /^\s*(sudo\s+)?ufw\b/m);
});

test('nginx zones stay prefixed and out of the other vhosts', () => {
  const limits = read('nginx/situ-limits.conf.template');
  assert.match(limits, /zone=situ_auth:10m/);
  assert.match(limits, /zone=situ_live:10m/);
  assert.match(limits, /\$situ_connection_upgrade/);
  assert.doesNotMatch(limits, /^\s*gzip\b/m);
  assert.doesNotMatch(limits, /^\s*limit_req_status\b/m);
  assert.doesNotMatch(limits, /default_server/);

  const templates = readdirSync(join(root, 'nginx')).filter((name) => name.endsWith('.conf.template'));
  assert.ok(templates.includes('situ-api.conf.template'));
  assert.ok(!templates.includes('situ.conf.template'));
  for (const name of templates) {
    const text = read(join('nginx', name));
    assert.doesNotMatch(text, /zone=(?!situ_)/, name);
    assert.doesNotMatch(text, /listen[^;]*default_server/, name);
    assert.doesNotMatch(text, /127\.0\.0\.1:5080/, name);
    assert.doesNotMatch(text, /127\.0\.0\.1:300[1234]/, name);
  }
  assert.match(read('nginx/situ-api.conf.template'), /location \/hubs\//);
  assert.match(read('nginx/situ-proxy.conf'), /\$situ_connection_upgrade/);
  assert.match(read('systemd/situ-api.service'), /MemoryMax=512M/);
  assert.match(read('systemd/situ-api.service'), /CPUQuota=80%/);
  assert.match(read('systemd/situ-web.service'), /MemoryMax=384M/);
  assert.match(read('systemd/situ-admin.service'), /MemoryMax=256M/);
  assert.match(read('systemd/situ-landing.service'), /MemoryMax=192M/);
  assert.match(read('systemd/situ-support.service'), /MemoryMax=192M/);
  assert.match(read('mediamtx/situ-mediamtx.service'), /MemoryMax=768M/);
  assert.match(read('mediamtx/situ-mediamtx.service'), /CPUQuota=100%/);
  assert.doesNotMatch(read('mediamtx/situ-mediamtx.service'), /CPUQuota=300%/);
  assert.match(read('mediamtx/situ-live-auth.service'), /MemoryMax=64M/);
});

test('mediamtx accepts only the opaque path and still authorizes it', () => {
  const yml = read('mediamtx/mediamtx.yml');
  const dev = read('mediamtx/mediamtx.dev.yml');
  const install = read('mediamtx/install.sh');
  assert.match(yml, /authMethod: http/);
  assert.match(yml, /authHTTPAddress: __LIVE_AUTH_URL__/);
  assert.match(yml, /action: api/);
  assert.doesNotMatch(yml, /action: publish/);
  assert.doesNotMatch(yml, /action: read/);
  assert.doesNotMatch(yml, /action: playback/);
  assert.match(yml, /"~\^\[a-f0-9\]\{32\}\$":\n    source: publisher/);
  assert.doesNotMatch(yml, /^\s*all_others:\s*$/m);
  assert.doesNotMatch(yml, /^\s*all:\s*$/m);
  assert.doesNotMatch(yml, /~\^\.\*/);
  assert.doesNotMatch(yml, /~\^\.\+/);
  assert.doesNotMatch(yml, /^\s*poc:\s*$/m);
  assert.match(dev, /^\s*poc:\n\s*source: publisher/m);
  assert.doesNotMatch(dev, /a-f0-9/);
  assert.match(install, /\[\[ ! -f "\$ENV_FILE" \]\]/);
  assert.match(install, /Se conserva \$\{ENV_FILE\}\./);
  assert.match(install, /MainPID/);
  assert.match(install, /situ_port_listening/);
  const control = readFileSync(join(root, '../apps/backend/src/Situ.Api/Live/MediaMtxControl.cs'), 'utf8');
  assert.match(control, /\/v3\/rtmps\/conns\/list/);
  assert.match(control, /\/v3\/rtmps\/conns\/kick\//);
  assert.match(control, /connection\.Id/);
  assert.match(control, /connection\.Path/);
});
