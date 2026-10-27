import { fireEvent } from '@testing-library/dom';
import { act } from 'react';
import { render, screen } from '@/test-render';
import { apiSend } from '@/lib/client';
import { afterEach, beforeAll, expect, it, vi } from 'vitest';
import { CategoryList, categoryCodeFromName, type Category } from './category-list';

vi.mock('@/lib/client', () => ({
  apiSend: vi.fn(async () => ({})),
  apiGet: vi.fn(),
}));

beforeAll(() => {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: (query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener() {},
      removeListener() {},
      addEventListener() {},
      removeEventListener() {},
      dispatchEvent() {
        return false;
      },
    }),
  });
});

const mounted: Array<{ unmount: () => void }> = [];

afterEach(() => {
  vi.clearAllMocks();
  while (mounted.length > 0) {
    mounted.pop()?.unmount();
  }
});

function mount(node: Parameters<typeof render>[0]) {
  const view = render(node);
  mounted.push(view);
  return view;
}

function category(partial: Partial<Category> & Pick<Category, 'id' | 'name' | 'ttlMinutes'>): Category {
  return {
    code: 'hurto',
    description: 'Robo en la vía.',
    buttonIconUrl: '',
    mapIconUrl: '',
    colorHex: '#112233',
    sortOrder: 4,
    isActive: true,
    importanceWeight: 2.5,
    mediaRequirement: 'None',
    isAdultOnly: false,
    activeReportCount: 3,
    translations: [],
    overrides: [],
    ...partial,
  };
}

it('arma el código desde el nombre y no lo pide al crear', async () => {
  expect(categoryCodeFromName('Accidente de tránsito')).toBe('accidente_de_transito');
  expect(categoryCodeFromName('Retén')).toBe('reten');
  expect(categoryCodeFromName('A')).toBeNull();

  mount(<CategoryList categories={[]} onChange={async () => undefined} />);
  await act(async () => {
    fireEvent.click(screen.getByRole('button', { name: 'Nuevo' }));
  });

  expect(screen.queryByLabelText('Código')).not.toBeInTheDocument();
  fireEvent.change(screen.getByLabelText('Nombre'), { target: { value: 'Accidente de tránsito' } });
  fireEvent.change(screen.getByLabelText('Descripción'), { target: { value: 'Choque en la vía.' } });
  const form = screen.getByLabelText('Nombre').closest('form');
  await act(async () => {
    fireEvent.submit(form as HTMLFormElement);
  });

  expect(apiSend).toHaveBeenCalledWith('admin/categories', 'POST', expect.objectContaining({
    code: 'accidente_de_transito',
    name: 'Accidente de tránsito',
  }));
});

it('muestra la vigencia de cada categoría de la lista', () => {
  mount(
    <CategoryList
      categories={[
        category({ id: '1', name: 'Hurto', ttlMinutes: 90, sortOrder: 4, activeReportCount: 3 }),
        category({ id: '2', name: 'Accidente', code: 'accidente', ttlMinutes: 60 * 24 * 2, sortOrder: 1, isActive: false, activeReportCount: 0 }),
      ]}
      onChange={async () => undefined}
    />,
  );

  const items = screen.getAllByRole('row').slice(1);
  expect(items).toHaveLength(2);
  expect(screen.getByRole('columnheader', { name: 'Vigencia' })).toBeInTheDocument();
  for (const item of items) {
    expect(item.textContent ?? '').toMatch(/\d+ (minuto|minutos|hora|horas|día|días)/);
  }

  expect(items[0]).toHaveTextContent('Hurto');
  expect(items[0]).toHaveTextContent('Activa');
  expect(items[0]).toHaveTextContent('1 hora y 30 minutos');
  expect(items[0]).toHaveTextContent('4');
  expect(items[0]).toHaveTextContent('3');
  expect(items[1]).toHaveTextContent('Inactiva');
  expect(items[1]).toHaveTextContent('2 días');
});

it('carga la vigencia guardada y no guarda un valor fuera de rango', async () => {
  mount(<CategoryList categories={[category({ id: '1', name: 'Hurto', ttlMinutes: 90 })]} onChange={async () => undefined} />);
  await act(async () => {
    fireEvent.click(screen.getByRole('button', { name: 'Editar Hurto' }));
  });

  const input = screen.getByLabelText('Vigencia de Hurto');
  expect(input).toHaveValue(90);
  expect(screen.getByLabelText('Orden de Hurto')).toHaveValue(4);
  expect(screen.getByLabelText('Peso de Hurto')).toHaveValue(2.5);
  expect(screen.getByLabelText('Color de Hurto')).toHaveValue('#112233');

  fireEvent.change(input, { target: { value: '2' } });
  const form = screen.getByRole('form', { name: 'Editar Hurto' });
  expect(input).toHaveValue(2);
  expect(new FormData(form as HTMLFormElement).get('ttl')).toBe('2');
  await act(async () => {
    fireEvent.submit(form);
  });

  expect(apiSend).not.toHaveBeenCalled();
  expect(screen.getByRole('alert')).toHaveTextContent('La vigencia debe estar entre 5 minutos y 30 días.');
});
