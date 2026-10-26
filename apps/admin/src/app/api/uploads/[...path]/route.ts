import { apiBase, clientForwarding } from '@/lib/upstream';
import { NextResponse } from 'next/server';

export async function GET(request: Request, context: { params: Promise<{ path: string[] }> }) {
  const { path } = await context.params;
  const forwarded = clientForwarding(request);
  const headers = new Headers();
  if (forwarded.for) {
    headers.set('x-forwarded-for', forwarded.for);
  }

  if (forwarded.proto) {
    headers.set('x-forwarded-proto', forwarded.proto);
  }

  const response = await fetch(`${apiBase()}/uploads/${path.map(encodeURIComponent).join('/')}`, { headers, cache: 'no-store' });
  if (!response.ok) {
    return new NextResponse(null, { status: response.status });
  }

  return new NextResponse(response.body, {
    status: 200,
    headers: { 'content-type': response.headers.get('content-type') ?? 'application/octet-stream', 'cache-control': 'private, max-age=60' },
  });
}
