import { passwordSchema, profileMessages } from '@situ/shared';

const userNamePattern = /^[a-z0-9][a-z0-9_.]{2,29}$/;
const months = [
  'enero',
  'febrero',
  'marzo',
  'abril',
  'mayo',
  'junio',
  'julio',
  'agosto',
  'septiembre',
  'octubre',
  'noviembre',
  'diciembre',
];

export type ProfileField = 'userName' | 'displayName' | 'bio';

export function normalizeUserName(value: string): string {
  return value.trim().toLowerCase();
}

export function isValidUserName(value: string): boolean {
  return userNamePattern.test(normalizeUserName(value));
}

export function validateProfileForm(input: { userName: string; displayName: string; bio: string }): Partial<Record<ProfileField, string>> {
  const errors: Partial<Record<ProfileField, string>> = {};
  if (!isValidUserName(input.userName)) {
    errors.userName = profileMessages.invalidUserName;
  }
  if (input.displayName.trim().length === 0) {
    errors.displayName = profileMessages.displayNameRequired;
  } else if (input.displayName.trim().length > 60) {
    errors.displayName = 'El nombre visible puede tener hasta 60 caracteres.';
  }
  if (input.bio.trim().length > 160) {
    errors.bio = profileMessages.bioTooLong;
  }
  return errors;
}

/** Reserved and taken names share this message so the screen cannot tell them apart. */
export function usernameAvailabilityMessage(available: boolean | null): string | null {
  if (available === null) {
    return null;
  }
  return available ? profileMessages.available : profileMessages.taken;
}

export function usernameCooldownLabel(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return profileMessages.taken;
  }
  const formatted = new Intl.DateTimeFormat('es-CO', {
    day: 'numeric',
    month: 'long',
    timeZone: 'America/Bogota',
  }).format(date);
  return `Podrás cambiarlo de nuevo el ${formatted}`;
}

export function memberSinceLabel(year: number | string, month: number | string): string {
  const name = months[Number(month) - 1] ?? '';
  return `Miembro desde ${name} de ${Number(year)}`;
}

export function canUploadAvatarPhoto(isMinor: boolean): boolean {
  return !isMinor;
}

export function displayEmail(value: string | null | undefined): string {
  const email = (value ?? '').trim();
  const at = email.indexOf('@');
  if (at <= 0 || at >= email.length - 1) {
    return '***';
  }
  const local = email.slice(0, at);
  if (local.includes('***')) {
    return email;
  }
  return `${local[0]}***@${email.slice(at + 1)}`;
}

export function passwordFieldErrors(input: { current: string; next: string; confirm: string }): {
  current?: string;
  next?: string;
  confirm?: string;
} {
  const errors: { current?: string; next?: string; confirm?: string } = {};
  if (input.current.trim().length === 0) {
    errors.current = 'Escribe tu contraseña actual.';
  }
  const parsed = passwordSchema.safeParse(input.next);
  if (!parsed.success) {
    errors.next = parsed.error.issues[0]?.message ?? profileMessages.strengthWeak;
  }
  if (input.next !== input.confirm) {
    errors.confirm = 'Las contraseñas no coinciden.';
  }
  return errors;
}

export function passwordStrength(password: string): 'empty' | 'weak' | 'good' {
  if (password.length === 0) {
    return 'empty';
  }
  return passwordSchema.safeParse(password).success ? 'good' : 'weak';
}

export function canRevokeDevice(isCurrent: boolean): boolean {
  return !isCurrent;
}
