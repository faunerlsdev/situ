/** Dev-only live proof. Production builds hide the screen. */
export const livePocEnabled = typeof __DEV__ !== 'undefined' && __DEV__;

export const livePocVideo = {
  width: 1280,
  height: 720,
  fps: 30,
  bitrate: 2_500_000,
  iFrameInterval: 2,
} as const;

export const livePocAudio = {
  bitrate: 128_000,
  sampleRate: 44_100,
  stereo: true,
} as const;

const PATH_PATTERN = /^[A-Za-z0-9_-]{1,64}$/;

export function assertStreamPath(path: string): string {
  const trimmed = path.trim();
  if (!PATH_PATTERN.test(trimmed)) {
    throw new Error('El nombre solo admite letras, números, guion y guion bajo.');
  }
  return trimmed;
}

export function publishUrl(input: { host: string; path: string; secure: boolean }): string {
  const host = input.host.trim().replace(/^rtmps?:\/\//, '').replace(/\/.*$/, '');
  const path = assertStreamPath(input.path);
  const scheme = input.secure ? 'rtmps' : 'rtmp';
  const port = input.secure ? 1936 : 1935;
  return `${scheme}://${host}:${port}/${path}`;
}

export function playbackUrl(input: { host: string; path: string; secure: boolean; token?: string }): string {
  const host = input.host.trim().replace(/^https?:\/\//, '').replace(/\/.*$/, '');
  const path = assertStreamPath(input.path);
  if (!input.secure) {
    return `http://${host}:8888/${path}/index.m3u8`;
  }
  const token = input.token?.trim();
  if (!token) {
    throw new Error('El HLS del servidor necesita el token de corta vida.');
  }
  return `https://${host}/hls/${path}/index.m3u8?token=${encodeURIComponent(token)}`;
}
