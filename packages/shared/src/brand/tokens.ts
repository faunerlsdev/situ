/** Brand source of truth. Keep the hex values aligned with theme.css. */
export const brandColors = {
  primary: '#FF6400',
  primaryPressed: '#E05800',
  primaryLight: '#FF9F24',
  live: '#DE0506',
  black: '#000000',
  ink: '#424242',
  gray600: '#5A5A5A',
  gray400: '#BABABA',
  gray200: '#CDCDD5',
  white: '#FFFFFF',
  bgDarkEdge: '#1C1C1C',
  bgDarkCenter: '#34352B',
  surface: '#FFFFFF',
} as const;

/** Dark theme for the mobile app and the web app. Landing and support keep brandColors. */
export const appColors = {
  bg: '#1C1C1C',
  bgGradientCenter: '#34352B',
  surface: '#242424',
  surfaceElevated: '#2E2E2C',
  input: '#2A2A2A',
  border: '#3A3A38',
  overlay: 'rgba(0,0,0,0.6)',
  textPrimary: '#F2F2F2',
  textSecondary: '#A8A8A8',
  textPlaceholder: '#7A7A7A',
  textOnPrimary: '#141414',
  textOnDanger: '#FFFFFF',
  primary: '#FF6400',
  primaryPressed: '#E05800',
  primarySoft: 'rgba(255,100,0,0.14)',
  primaryLight: '#FF9F24',
  success: '#3DDC84',
  danger: '#FF5A5A',
  dangerSolid: '#DE0506',
  warning: '#FF9F24',
  info: '#A8A8A8',
  live: '#DE0506',
  skeleton: '#2A2A2A',
  skeletonShimmer: '#333331',
  mapWater: '#17263C',
  mapRoad: '#2E2E2C',
} as const;

export const brandFonts = {
  sans: 'Source Sans 3',
  display: 'Montserrat',
} as const;

export const brandRadius = {
  input: 2,
  button: 4,
} as const;

function configuredRoot() {
  const value = process.env.NEXT_PUBLIC_DOMAIN_ROOT || process.env.DOMAIN_ROOT || '';
  return value.trim().replace(/^https?:\/\//, '').replace(/\/$/, '');
}

function siteOrigin(subdomain: string | null, port: string) {
  const root = configuredRoot();
  if (!root) {
    return `http://localhost:${port}`;
  }

  return `https://${subdomain ? `${subdomain}.${root}` : root}`;
}

export const sites = {
  web: siteOrigin('app', '3002'),
  landing: siteOrigin(null, '3003'),
  support: siteOrigin('ayuda', '3004'),
  admin: siteOrigin('admin', '3001'),
} as const;

export const defaultApiUrl = 'http://localhost:5080';
