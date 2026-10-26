export type ProblemBody = {
  code?: string;
  detail?: string;
  title?: string;
  retryAfter?: string;
};

export function problemMessage(body: ProblemBody | null, fallback = 'No pudimos completar la solicitud.') {
  const detail = body?.detail?.trim();
  return detail || fallback;
}

export function denied() {
  return new PanelError(403, 'admin.forbidden', 'No tienes permiso para esta acción.');
}

export class PanelError extends Error {
  constructor(
    readonly status: number,
    readonly code: string | undefined,
    message: string,
  ) {
    super(message);
  }
}
