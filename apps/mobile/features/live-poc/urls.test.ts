import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

import { playbackUrl, publishUrl } from './urls';

const mediamtx = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../deploy/mediamtx');

describe('live poc urls', () => {
  it('builds a local RTMP publish url and an RTMPS url without printing a raw secret twice', () => {
    expect(publishUrl({ host: '192.168.1.20', path: 'poc', secure: false })).toBe('rtmp://192.168.1.20:1935/poc');
    expect(publishUrl({ host: 'rtmp://192.168.1.20/ignored', path: 'poc', secure: false })).toBe('rtmp://192.168.1.20:1935/poc');
    expect(publishUrl({ host: 'live.example.test', path: 'poc', secure: true })).toBe('rtmps://live.example.test:1936/poc');
    expect(() => publishUrl({ host: '192.168.1.20', path: '../x', secure: false })).toThrow(/nombre/);
  });

  it('builds the dev HLS url and refuses a production url without a token', () => {
    expect(playbackUrl({ host: '192.168.1.20', path: 'poc', secure: false })).toBe('http://192.168.1.20:8888/poc/index.m3u8');
    expect(playbackUrl({ host: 'live.example.test', path: 'poc', secure: true, token: '10.aa' })).toBe(
      'https://live.example.test/hls/poc/index.m3u8?token=10.aa',
    );
    expect(() => playbackUrl({ host: 'live.example.test', path: 'poc', secure: true })).toThrow(/token/);
  });
});

describe('mediamtx config', () => {
  const production = readFileSync(path.join(mediamtx, 'mediamtx.yml'), 'utf8');
  const development = readFileSync(path.join(mediamtx, 'mediamtx.dev.yml'), 'utf8');

  it('keeps production on RTMPS, localhost HLS and recordings without automatic deletion', () => {
    expect(production).toContain('rtmpEncryption: "strict"');
    expect(production).toContain('rtmpsAddress: :__SITU_RTMPS_PORT__');
    expect(production).toContain('rtmpAddress: 127.0.0.1:11935');
    expect(production).toContain('rtsp: false');
    expect(production).toContain('srt: false');
    expect(production).toContain('webrtc: false');
    expect(production).toContain('moq: false');
    expect(production).toContain('hlsAddress: 127.0.0.1:__SITU_HLS_PORT__');
    expect(production).toContain('apiAddress: 127.0.0.1:__SITU_MTX_API_PORT__');
    expect(production).not.toContain(':1935');
    expect(production).not.toContain('8888');
    expect(production).not.toContain('9997');
    expect(production).not.toContain('18888');
    expect(production).not.toContain('19997');
    expect(production).toContain('authMethod: http');
    expect(production).toContain('recordFormat: fmp4');
    expect(production).toContain('recordDeleteAfter: 0s');
    expect(production).toContain('/var/lib/situ/live/');
    expect(production).not.toContain('rtmpEncryption: "no"');
  });

  it('keeps the Mac config on plain RTMP without authentication', () => {
    expect(development).toContain('rtmpEncryption: "no"');
    expect(development).toContain('rtmpAddress: :1935');
    expect(development).toContain('hlsAddress: :8888');
    expect(development).toContain('user: any');
    expect(development).toContain('webrtc: false');
    expect(development).toContain('moq: false');
    expect(development).toContain('record: false');
  });
});
