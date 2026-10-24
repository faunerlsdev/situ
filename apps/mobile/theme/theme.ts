import { appColors, brandFonts } from '@situ/shared';

export const fontFamily = {
  sans: 'SourceSans3_400Regular',
  sansSemibold: 'SourceSans3_600SemiBold',
  sansBold: 'SourceSans3_700Bold',
  sansBoldItalic: 'SourceSans3_700Bold_Italic',
  display: 'Montserrat_700Bold',
  displayHeavy: 'Montserrat_800ExtraBold',
  displayHeavyItalic: 'Montserrat_800ExtraBold_Italic',
} as const;

export const theme = {
  colors: appColors,
  brandFonts,
  fontFamily,
  radius: {
    input: 8,
    button: 8,
    card: 12,
  },
  shadow: {
    boxShadow: '0 4px 16px rgba(0,0,0,0.4)',
  },
  type: {
    display: { fontFamily: fontFamily.displayHeavy, fontSize: 40, lineHeight: 44 },
    h1: { fontFamily: fontFamily.display, fontSize: 28, lineHeight: 34 },
    h2: { fontFamily: fontFamily.sansBold, fontSize: 22, lineHeight: 28 },
    body: { fontFamily: fontFamily.sans, fontSize: 16, lineHeight: 24 },
    small: { fontFamily: fontFamily.sans, fontSize: 13, lineHeight: 18 },
    caption: { fontFamily: fontFamily.sans, fontSize: 11, lineHeight: 14 },
    emphasis: { fontFamily: fontFamily.sansBoldItalic, fontSize: 16, lineHeight: 22 },
  },
} as const;
