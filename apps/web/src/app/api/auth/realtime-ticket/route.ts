import { rejectCrossSite } from '@/lib/csrf';
import { http } from '@/lib/http';
import { bearerToken } from '@/lib/session';
import { NextResponse } from 'next/server';

export async function POST(request: Request) {
  const denied = await rejectCrossSite(request);
  if (denied) {
    return denied;
  }

  const token = await bearerToken();
  if (!token) {
    return NextResponse.json({ code: 'auth.session_invalid', detail: 'Inicia sesión para ver el mapa en tiempo real.' }, { status: 401 });
  }

  const response = await http.requestResponse<unknown>({
    method: 'POST',
    url: '/api/v1/auth/realtime-ticket',
    headers: { authorization: `Bearer ${token}` },
    skipAuthRefresh: true,
    validateStatus: () => true,
  });
  if (response.data === undefined) {
    return new NextResponse(null, { status: response.status });
  }

  return NextResponse.json(response.data, { status: response.status });
}
