export const sections = [
  { href: '/resumen', label: 'Resumen', permission: 'metrics.view' },
  { href: '/denuncias', label: 'Denuncias', permission: 'complaints.resolve' },
  { href: '/usuarios', label: 'Usuarios', permission: 'users.view' },
  { href: '/categorias', label: 'Categorías', permission: 'categories.manage' },
  { href: '/avatares', label: 'Avatares', permission: 'avatars.manage' },
  { href: '/configuracion', label: 'Configuración', permission: 'settings.manage' },
  { href: '/retencion', label: 'Retención', permission: 'legalholds.manage' },
  { href: '/retiros', label: 'Retiros', permission: 'takedowns.handle' },
  { href: '/bitacora', label: 'Bitácora', permission: 'audit.view' },
  { href: '/personal', label: 'Personal', permission: 'staff.manage' },
] as const;

export function visibleSections(permissions: readonly string[]) {
  const owned = new Set(permissions);
  return sections.filter((section) => owned.has(section.permission));
}

export function can(permissions: readonly string[], permission: string) {
  return permissions.includes(permission);
}
