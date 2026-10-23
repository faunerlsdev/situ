import type { AppErrorCode } from './errors';

export const errorMessages: Record<AppErrorCode, string> = {
  NETWORK: 'Sin conexión. Revisa tu internet e inténtalo de nuevo.',
  TIMEOUT: 'La solicitud tardó demasiado. Inténtalo de nuevo.',
  UNAUTHORIZED: 'Tu sesión ya no es válida. Vuelve a ingresar.',
  FORBIDDEN: 'No puedes hacer esto con tu cuenta.',
  NOT_FOUND: 'No encontramos lo que buscabas.',
  VALIDATION: 'Revisa los datos e inténtalo de nuevo.',
  CONFLICT: 'Esa información ya está en uso.',
  RATE_LIMIT: 'Demasiados intentos. Espera un momento.',
  SERVER: 'Tuvimos un problema. Inténtalo de nuevo en un momento.',
  CANCELLED: '',
  UNKNOWN: 'No pudimos completar la solicitud.',
};

/** Curated product copy. Unknown server text is never shown. */
export const apiMessages: Record<string, string> = {
  'auth.invalid_credentials': 'Correo, usuario o contraseña incorrectos.',
  'auth.locked': 'La cuenta está bloqueada por demasiados intentos.',
  'auth.pending_guardian': 'Esta cuenta espera la autorización de un acudiente.',
  'auth.suspended': 'Esta cuenta está suspendida.',
  'auth.not_allowed': 'Esta cuenta no puede ingresar.',
  'auth.device_limit': 'Alcanzaste el número de dispositivos de tu plan.',
  'auth.session_invalid': 'Tu sesión ya no es válida. Vuelve a ingresar.',
  'auth.rate_limited': 'Demasiados intentos desde esta red. Espera un momento.',
  'auth.confirmation_rate_limited': 'Espera un momento antes de pedir otro correo.',
  'security_token.not_found': 'El enlace no es válido o ya venció.',
  'security_token.already_used': 'Ese enlace ya se usó.',
  'security_token.expired': 'Ese enlace ya venció.',
  'guardian_consent.not_found': 'El enlace de autorización no es válido.',
  'guardian_consent.not_open': 'Esta autorización ya no está disponible.',
  'guardian_consent.email_not_verified': 'Primero verifica el correo del acudiente.',
  'guardian_consent.expired': 'El plazo para autorizar ya venció.',
  'password.current_invalid': 'La contraseña actual no coincide.',
  'password.invalid': 'La contraseña debe tener al menos 8 caracteres, una mayúscula, una minúscula y un número.',
};

export function messageFor(code: AppErrorCode, apiCode?: string): string {
  if (apiCode && apiMessages[apiCode]) {
    return apiMessages[apiCode];
  }

  if (code === 'NOT_FOUND' && !apiCode) {
    return 'Esta solicitud todavía no está disponible.';
  }

  return errorMessages[code];
}
