import { ReasonDialog } from '@/components/reason-dialog';
import { render, screen } from '@/test-render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

describe('reason dialog', () => {
  it('requires a reason before confirming', async () => {
    const user = userEvent.setup();
    const onConfirm = vi.fn();
    render(<ReasonDialog title="Suspender" confirmLabel="Confirmar" onClose={() => undefined} onConfirm={onConfirm} />);
    await user.click(screen.getByRole('button', { name: 'Confirmar' }));
    expect(screen.getByRole('alert')).toHaveTextContent('Escribe el motivo.');
    expect(onConfirm).not.toHaveBeenCalled();
    await user.type(screen.getByLabelText('Motivo'), 'Reincidencia comprobada');
    await user.click(screen.getByRole('button', { name: 'Confirmar' }));
    expect(onConfirm).toHaveBeenCalledWith('Reincidencia comprobada');
  });
});
