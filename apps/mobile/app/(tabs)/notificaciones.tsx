import { StyleSheet, View } from 'react-native';

import { NotificationCenter } from '@/features/notifications/notification-center';
import { theme } from '@/theme/theme';

// Phase 2 adds "En vivo" beside this inbox. The tab stays /notificaciones.
export default function NotificationsScreen() {
  return (
    <View style={styles.screen}>
      <NotificationCenter />
    </View>
  );
}

const styles = StyleSheet.create({
  screen: {
    flex: 1,
    backgroundColor: theme.colors.bg,
  },
});
