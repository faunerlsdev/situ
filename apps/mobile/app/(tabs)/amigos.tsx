import { StyleSheet, Text, View } from 'react-native';

import { theme } from '@/theme/theme';

export default function FriendsScreen() {
  return (
    <View style={styles.screen}>
      <Text style={styles.title}>Amigos</Text>
      <Text style={styles.body}>
        Cuando el servidor confirme la amistad, las personas aparecen aquí. Un menor no se muestra en el mapa público.
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: {
    flex: 1,
    gap: 12,
    padding: 20,
    backgroundColor: theme.colors.bg,
  },
  title: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.display,
    fontSize: 28,
  },
  body: {
    color: theme.colors.textSecondary,
    fontFamily: theme.fontFamily.sans,
    fontSize: 16,
    lineHeight: 24,
  },
});
