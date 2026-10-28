export function friendActionLabel(relation: string | undefined): { label: string; enabled: boolean } {
  if (relation === 'friends') {
    return { label: 'Ya son amigos', enabled: false };
  }
  if (relation === 'outgoing') {
    return { label: 'Solicitud enviada', enabled: false };
  }
  if (relation === 'incoming') {
    return { label: 'Solicitud recibida', enabled: false };
  }
  return { label: 'Agregar', enabled: true };
}

export function pendingRequestCount(incoming: readonly unknown[] | undefined): number {
  return incoming?.length ?? 0;
}
