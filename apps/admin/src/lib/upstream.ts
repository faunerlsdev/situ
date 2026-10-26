const localApi = 'http://127.0.0.1:5080';

export type ClientForwarding = { for: string | null; proto: string | null };

export function apiBase() {
  const value = process.env.API_BASE_URL?.trim() || localApi;
  return value.replace(/\/$/, '');
}

export function clientForwarding(request: Request): ClientForwarding {
  const forwarded = first(request.headers.get('x-forwarded-for'));
  const realIp = first(request.headers.get('x-real-ip'));
  const proto = first(request.headers.get('x-forwarded-proto')) || new URL(request.url).protocol.replace(':', '') || null;
  return { for: forwarded || realIp || null, proto };
}

function first(value: string | null) {
  const part = value?.split(',')[0]?.trim() ?? '';
  return part.length > 0 ? part : null;
}

// The BFF calls the API with the cookie token and forwards the browser address. The API trusts it only from Network:KnownProxies.
export async function upstream(
  path: string,
  init: { method: string; token?: string; body?: BodyInit | null; contentType?: string | null; forwarded?: ClientForwarding },
) {
  const headers = new Headers({ accept: 'application/json' });
  if (init.token) {
    headers.set('authorization', `Bearer ${init.token}`);
  }

  if (init.contentType) {
    headers.set('content-type', init.contentType);
  }

  if (init.forwarded?.for) {
    headers.set('x-forwarded-for', init.forwarded.for);
  }

  if (init.forwarded?.proto) {
    headers.set('x-forwarded-proto', init.forwarded.proto);
  }

  const response = await fetch(`${apiBase()}/api/v1/${path.replace(/^\//, '')}`, {
    method: init.method,
    headers,
    body: init.body ?? undefined,
    cache: 'no-store',
  });
  const text = await response.text();
  return { status: response.status, text, contentType: response.headers.get('content-type') };
}
