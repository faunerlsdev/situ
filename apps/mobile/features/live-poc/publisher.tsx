import { Text } from 'react-native';

import { theme } from '@/theme/theme';

export function Publisher(_props: { url: string; secret: string }) {
  return <Text style={{ color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 15 }}>La publicación de esta prueba es solo en la app nativa.</Text>;
}
