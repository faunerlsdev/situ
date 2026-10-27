export const deletionGraceDays = 14;
export const reportRetentionDays = 365;

export const deletionSteps = [
  'Abre la app de SITU.',
  'Entra a Perfil y elige Mi cuenta.',
  'En Eliminar cuenta escribe tu contraseña y confirma.',
];

export const deletionLoginLabel = 'Ingresar y volver a esta página';
export const deletionRequestLabel = 'Solicitar eliminación';
export const deletionCancelLabel = 'Cancelar eliminación';

export const deletionActions = {
  login: deletionLoginLabel,
  request: deletionRequestLabel,
  cancel: deletionCancelLabel,
};

export type DeletionProfile = {
  userName?: string;
  maskedEmail?: string;
  phone?: string | null;
  deletionScheduledFor?: string | null;
};

export type DeletionView = {
  signedIn: boolean;
  scheduledFor: string | null;
  loginHref: '/login?next=/cuenta/eliminar';
};

export function deletionView(signedIn: boolean, profile: DeletionProfile | null): DeletionView {
  return {
    signedIn,
    scheduledFor: signedIn ? profile?.deletionScheduledFor ?? null : null,
    loginHref: '/login?next=/cuenta/eliminar',
  };
}

export function deletionRouteCopy(state: DeletionView) {
  const lines = ['Eliminar cuenta', 'Qué se borra', 'Qué se conserva', 'Plazos', 'Desde la app', ...deletionSteps];
  if (state.signedIn && state.scheduledFor) {
    lines.push(deletionActions.cancel, state.scheduledFor);
  }
  if (state.signedIn && !state.scheduledFor) {
    lines.push('Contraseña', deletionActions.request);
  }
  if (!state.signedIn) {
    lines.push(deletionActions.login, state.loginHref);
  }
  return lines;
}

export function safeReturnPath(value: string | null | undefined) {
  if (!value || !value.startsWith('/') || value.startsWith('//') || value.includes('\\') || value.includes('://')) {
    return '/mapa';
  }
  return value;
}
