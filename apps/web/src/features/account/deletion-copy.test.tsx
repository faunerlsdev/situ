import { describe, expect, it } from 'vitest';
import { deletionRouteCopy, deletionView, safeReturnPath } from './deletion-copy';

describe('eliminar cuenta', () => {
  it('sin sesión invita a ingresar y no muestra datos de un perfil', () => {
    const view = deletionView(false, {
      userName: 'ana',
      maskedEmail: 'a•••@correo.com',
      phone: '+573001234567',
      deletionScheduledFor: '2026-11-10T00:00:00Z',
    });
    const copy = deletionRouteCopy(view).join('\n');
    expect(copy).toContain('Ingresar y volver a esta página');
    expect(copy).toContain('/login?next=/cuenta/eliminar');
    expect(copy).not.toContain('Solicitar eliminación');
    expect(copy).not.toContain('ana');
    expect(copy).not.toContain('+573001234567');
    expect(copy).not.toContain('correo.com');
  });

  it('con sesión pide la contraseña y no publica el correo ni el teléfono', () => {
    const view = deletionView(true, { maskedEmail: 'a•••@correo.com', phone: '+573001234567', deletionScheduledFor: null });
    const copy = deletionRouteCopy(view).join('\n');
    expect(copy).toContain('Contraseña');
    expect(copy).toContain('Solicitar eliminación');
    expect(copy).not.toContain('Cancelar eliminación');
    expect(copy).not.toContain('a•••@correo.com');
    expect(copy).not.toContain('+573001234567');
  });

  it('con una eliminación en curso permite cancelarla y no muestra el teléfono', () => {
    const view = deletionView(true, { phone: '+573001234567', deletionScheduledFor: '2026-11-10T00:00:00Z' });
    const copy = deletionRouteCopy(view).join('\n');
    expect(copy).toContain('Cancelar eliminación');
    expect(copy).toContain('2026-11-10T00:00:00Z');
    expect(copy).not.toContain('Solicitar eliminación');
    expect(copy).not.toContain('+573001234567');
  });

  it('solo acepta un regreso interno', () => {
    expect(safeReturnPath('/cuenta/eliminar')).toBe('/cuenta/eliminar');
    expect(safeReturnPath('https://otro.example')).toBe('/mapa');
    expect(safeReturnPath('//otro.example')).toBe('/mapa');
  });
});
