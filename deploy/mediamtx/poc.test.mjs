import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

import { estimate } from './capacity.mjs';
import { signHlsToken, verifyHlsToken } from './token.mjs';

const repoRoot = fileURLToPath(new URL('../..', import.meta.url));

test('a short HLS token expires and does not accept another path', () => {
  const now = 1_700_000_000;
  const signed = signHlsToken({ secret: 'secret', path: 'poc', nowSeconds: now, ttlSeconds: 120 });
  assert.equal(verifyHlsToken({ secret: 'secret', path: 'poc', token: signed.token, nowSeconds: now + 10 }), true);
  assert.equal(verifyHlsToken({ secret: 'secret', path: 'other', token: signed.token, nowSeconds: now + 10 }), false);
  assert.equal(verifyHlsToken({ secret: 'secret', path: 'poc', token: signed.token, nowSeconds: now + 121 }), false);
  assert.equal(verifyHlsToken({ secret: 'other', path: 'poc', token: signed.token, nowSeconds: now + 10 }), false);
  assert.throws(() => signHlsToken({ secret: 'secret', path: 'poc', nowSeconds: now, ttlSeconds: 181 }));
});

test('capacity stays inside the reserved headroom', () => {
  assert.deepEqual(estimate({ cores: 2, memoryMb: 2048 }), { streams: 2, viewers: 50 });
  assert.deepEqual(estimate({ cores: 1, memoryMb: 1024 }), { streams: 0, viewers: 0 });
});

test('the python gate accepts the same token', () => {
  const now = 1_700_000_000;
  const signed = signHlsToken({ secret: 'secret', path: 'poc', nowSeconds: now, ttlSeconds: 120 });
  const probe = spawnSync(
    'python3',
    [
      '-c',
      `import importlib.util
spec = importlib.util.spec_from_file_location("auth", "deploy/mediamtx/poc-auth.py")
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
token = ${JSON.stringify(signed.token)}
assert mod.verify_token("secret", "poc", token, ${now + 10})
assert not mod.verify_token("secret", "poc", token, ${now + 121})
assert not mod.verify_token("secret", "other", token, ${now + 10})
print("ok")`,
    ],
    { encoding: 'utf8', cwd: repoRoot },
  );
  assert.equal(probe.status, 0, probe.stderr);
  assert.match(probe.stdout, /ok/);
});
