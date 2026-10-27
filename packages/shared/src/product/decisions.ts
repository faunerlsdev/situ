export const callingCodes = [
  { code: '57', label: 'Colombia +57' },
  { code: '52', label: 'México +52' },
  { code: '1', label: 'Estados Unidos +1' },
  { code: '34', label: 'España +34' },
  { code: '51', label: 'Perú +51' },
  { code: '593', label: 'Ecuador +593' },
  { code: '58', label: 'Venezuela +58' },
] as const;

export const phonePurpose =
  'Lo usamos para seguridad de tu cuenta y, más adelante, verificación en dos pasos. No lo mostramos a otros usuarios.';

const e164 = /^\+[1-9]\d{7,14}$/;

export function composePhone(callingCode: string, national: string) {
  const code = callingCode.replace(/\D/g, '');
  const local = national.replace(/\D/g, '').replace(/^0+/, '');
  return `+${code}${local}`;
}

export function isE164(value: string) {
  return e164.test(value);
}

export function anonymityStatus(anonymous: boolean) {
  return anonymous ? 'Se publicará como Situero anónimo' : 'Se publicará con tu nombre de usuario';
}

export const anonymityNotice = {
  version: '2026-10-27',
  title: 'Publicas como anónimo',
  body: 'Otros usuarios no verán quién eres. El equipo de moderación puede ver tu identidad si hay abuso o un requerimiento legal.',
};

export function shellTabHrefs(socialTabs: boolean) {
  const tabs = ['/mapa', '/notificaciones'];
  if (socialTabs) {
    tabs.splice(1, 0, '/chisme');
    tabs.push('/mensajes');
  }
  return tabs;
}

export function socialTabsFrom(config: unknown) {
  if (!config || typeof config !== 'object' || !('features' in config)) {
    return false;
  }
  const features = (config as { features?: { socialTabs?: boolean } }).features;
  return features?.socialTabs === true;
}

export function anonymityNoticeAccepted(profile: unknown) {
  if (!profile || typeof profile !== 'object' || !('anonymityNoticeAccepted' in profile)) {
    return false;
  }
  return (profile as { anonymityNoticeAccepted?: boolean }).anonymityNoticeAccepted === true;
}

export function refreshCookieMaxAge(expiresAt: string | undefined, now: number) {
  const expires = expiresAt ? new Date(expiresAt).getTime() : Number.NaN;
  if (!Number.isFinite(expires)) {
    return 60;
  }
  return Math.max(60, Math.floor((expires - now) / 1000));
}
