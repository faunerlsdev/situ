import { StyleSheet, Text, View } from 'react-native';

import { EmptyState } from '@/components/shell';
import { theme } from '@/theme/theme';

export default function NotificationsScreen() {
  return (
    <View style={styles.screen}>
      <EmptyState title="En vivo" />
      <Text style={styles.body}>Alertas de tu ciudad y de lo que pase a un kilómetro. Esta bandeja todavía no recibe avisos.</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: {
    flex: 1,
    backgroundColor: theme.colors.surface,
  },
  body: {
    position: 'absolute',
    left: 24,
    right: 24,
    bottom: 32,
    textAlign: 'center',
    color: theme.colors.gray600,
    fontFamily: theme.fontFamily.sans,
    fontSize: 16,
    lineHeight: 24,
  },
});
