import { profileMessages } from '@situ/shared';
import { describe, expect, it } from 'vitest';

import {
  canRevokeDevice,
  canUploadAvatarPhoto,
  displayEmail,
  passwordFieldErrors,
  usernameAvailabilityMessage,
  usernameCooldownLabel,
  validateProfileForm,
} from './logic';

describe('profile form', () => {
  it('rejects an empty display name, a long bio and a short user name', () => {
    const errors = validateProfileForm({ userName: 'ab', displayName: '  ', bio: 'a'.repeat(161) });
    expect(errors.userName).toBe(profileMessages.invalidUserName);
    expect(errors.displayName).toBe(profileMessages.displayNameRequired);
    expect(errors.bio).toBe(profileMessages.bioTooLong);
    expect(validateProfileForm({ userName: 'ana.perez', displayName: 'Ana', bio: 'Hola' })).toEqual({});
  });

  it('uses the same message for a taken name and a reserved name', () => {
    expect(usernameAvailabilityMessage(false)).toBe(profileMessages.taken);
    expect(usernameAvailabilityMessage(false)).toBe('Ese nombre de usuario ya está en uso.');
    expect(usernameAvailabilityMessage(true)).toBe(profileMessages.available);
  });

  it('says when the user name can change again', () => {
    expect(usernameCooldownLabel('2026-11-12T15:00:00.000Z')).toBe('Podrás cambiarlo de nuevo el 12 de noviembre');
  });
});

describe('avatar and account', () => {
  it('hides the photo upload for a minor', () => {
    expect(canUploadAvatarPhoto(true)).toBe(false);
    expect(canUploadAvatarPhoto(false)).toBe(true);
  });

  it('explains a wrong current password and a password that breaks the policy', () => {
    const errors = passwordFieldErrors({ current: '', next: 'abcdefgh', confirm: 'abcdefgh' });
    expect(errors.current).toBe('Escribe tu contraseña actual.');
    expect(errors.next).toMatch(/mayúscula/);
    expect(passwordFieldErrors({ current: 'Clave123', next: 'Nueva1234', confirm: 'Otra1234' }).confirm).toBe(
      'Las contraseñas no coinciden.',
    );
  });

  it('does not revoke the current device', () => {
    expect(canRevokeDevice(true)).toBe(false);
    expect(canRevokeDevice(false)).toBe(true);
  });

  it('always shows a masked email', () => {
    expect(displayEmail('ana@correo.com')).toBe('a***@correo.com');
    expect(displayEmail('a***@correo.com')).toBe('a***@correo.com');
    expect(displayEmail('ana@correo.com')).not.toContain('ana@');
  });
});
