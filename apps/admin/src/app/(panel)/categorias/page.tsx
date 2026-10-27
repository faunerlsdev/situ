'use client';

import { CategoryList, type Category } from '@/app/(panel)/categorias/category-list';
import { PageHeader } from '@/components/catalog';
import { useMe } from '@/components/frame';
import { PanelState } from '@/components/states';
import { apiGet } from '@/lib/client';
import { can } from '@/lib/navigation';
import { denied } from '@/lib/problem';
import { useQuery, useQueryClient } from '@tanstack/react-query';

type Catalog = { categories: Category[]; catalogVersion: number };

export default function CategoriasPage() {
  const me = useMe();
  const client = useQueryClient();
  const query = useQuery({ queryKey: ['categories'], queryFn: () => apiGet<Catalog>('admin/categories'), enabled: can(me.permissions, 'categories.manage') });
  if (!can(me.permissions, 'categories.manage')) {
    return <PanelState loading={false} error={denied()}>{null}</PanelState>;
  }

  return (
    <section>
      <PageHeader title="Categorías" hint={query.data ? `Versión del catálogo: ${query.data.catalogVersion}` : 'Vigencia, orden y reportes que siguen activos.'} />
      <PanelState loading={query.isLoading} error={query.error}>
        <CategoryList categories={query.data?.categories ?? []} onChange={() => client.invalidateQueries({ queryKey: ['categories'] })} />
      </PanelState>
    </section>
  );
}
