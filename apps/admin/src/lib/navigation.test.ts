import { visibleSections } from '@/lib/navigation';
import { describe, expect, it } from 'vitest';

describe('navigation', () => {
  it('shows only the sections allowed by the effective permissions', () => {
    expect(visibleSections(['users.view']).map((item) => item.href)).toEqual(['/usuarios']);
    expect(visibleSections(['metrics.view', 'audit.view']).map((item) => item.label)).toEqual(['Resumen', 'Bitácora']);
  });
});
