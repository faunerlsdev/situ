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

export const brandFonts = {
  sans: 'Source Sans 3',
  display: 'Montserrat',
} as const;

export const brandRadius = {
  input: 2,
  button: 4,
} as const;

export const sites = {
  web: 'http://localhost:3000',
  landing: 'http://localhost:3001',
  support: 'http://localhost:3002',
} as const;

export const defaultApiUrl = 'http://localhost:5080';
