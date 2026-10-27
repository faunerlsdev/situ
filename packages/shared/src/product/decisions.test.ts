import { describe, expect, it } from 'vitest';

import { anonymityStatus, composePhone, emailSpamHint, isE164, refreshCookieMaxAge, shellTabHrefs } from './decisions';

describe('decisiones de producto', () => {
  it('arma un teléfono E.164 con Colombia por defecto', () => {
    expect(composePhone('57', '300 123 4567')).toBe('+573001234567');
    expect(isE164('+573001234567')).toBe(true);
    expect(isE164('3001234567')).toBe(false);
  });

  it('avisa que el correo puede caer en spam', () => {
    expect(emailSpamHint).toBe('Si no lo ves en unos minutos, revisa tu carpeta de spam o correo no deseado.');
  });

  it('explica si la publicación sale anónima', () => {
    expect(anonymityStatus(true)).toBe('Se publicará como Situero anónimo');
    expect(anonymityStatus(false)).toBe('Se publicará con tu nombre de usuario');
  });

  it('oculta Mensajes y Chisme cuando la bandera está apagada', () => {
    expect(shellTabHrefs(false)).toEqual(['/mapa', '/notificaciones']);
    expect(shellTabHrefs(true)).toEqual(['/mapa', '/chisme', '/notificaciones', '/mensajes']);
  });

  it('toma la duración de la cookie del vencimiento que entrega la API', () => {
    const now = Date.parse('2026-10-27T00:00:00Z');
    expect(refreshCookieMaxAge('2026-11-26T00:00:00Z', now)).toBe(60 * 60 * 24 * 30);
    expect(refreshCookieMaxAge('2027-01-25T00:00:00Z', now)).toBe(60 * 60 * 24 * 90);
    expect(refreshCookieMaxAge(undefined, now)).toBe(60);
  });
});
