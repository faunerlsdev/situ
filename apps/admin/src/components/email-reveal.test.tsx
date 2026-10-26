import { EmailReveal } from '@/components/email-reveal';
import { render, screen } from '@/test-render';
import { describe, expect, it, vi } from 'vitest';

describe('email reveal', () => {
  it('hides the full-email action without the permission', () => {
    render(<EmailReveal email="a***@correo.com" canReveal={false} onReveal={vi.fn()} />);
    expect(screen.queryByRole('button', { name: 'Ver correo completo' })).not.toBeInTheDocument();
    expect(screen.getByText('a***@correo.com')).toBeInTheDocument();
  });
});
