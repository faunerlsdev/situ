import { describe, expect, it } from 'vitest';

import { createDialogStore } from './dialogStore';
import { createToastQueue } from './toastQueue';

describe('toast queue', () => {
  it('shows one toast at a time and skips a duplicate inside two seconds', () => {
    let clock = 1_000;
    const queue = createToastQueue({
      now: () => clock,
      schedule: () => 0 as unknown as ReturnType<typeof setTimeout>,
      cancel: () => undefined,
      createId: (() => {
        let id = 0;
        return () => `id-${++id}`;
      })(),
    });

    expect(queue.enqueue({ kind: 'success', message: 'Listo' })).toBe('id-1');
    expect(queue.enqueue({ kind: 'success', message: 'Listo' })).toBeNull();
    clock += 2_000;
    expect(queue.enqueue({ kind: 'error', message: 'Falló' })).toBe('id-2');
    expect(queue.current()?.message).toBe('Listo');
    expect(queue.pending()).toBe(1);
    queue.dismiss();
    expect(queue.current()?.message).toBe('Falló');
  });
});

describe('confirm', () => {
  it('resolves true or false from the answer', async () => {
    const dialogs = createDialogStore();
    const accepted = dialogs.confirm({ title: 'Eliminar', message: '¿Seguro?', type: 'danger' });
    dialogs.answer(true);
    await expect(accepted).resolves.toBe(true);

    const rejected = dialogs.confirm({ title: 'Eliminar', message: '¿Seguro?', type: 'danger' });
    dialogs.answer(false);
    await expect(rejected).resolves.toBe(false);
  });
});
