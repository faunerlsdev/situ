import { Image, Modal, Pressable, StyleSheet, Text, View } from 'react-native';

import { theme } from '@/theme/theme';

export function PhotoViewer({ uri, onClose }: { uri: string | null; onClose: () => void }) {
  return (
    <Modal visible={uri !== null} transparent animationType="fade" onRequestClose={onClose}>
      <View style={styles.fill}>
        <Pressable accessibilityRole="button" accessibilityLabel="Cerrar foto" onPress={onClose} style={styles.close}>
          <Text style={styles.closeLabel}>Cerrar</Text>
        </Pressable>
        {uri ? <Image source={{ uri }} resizeMode="contain" style={styles.image} /> : null}
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1, backgroundColor: 'rgba(0,0,0,0.92)', justifyContent: 'center' },
  image: { width: '100%', height: '80%' },
  close: { position: 'absolute', top: 56, right: 16, zIndex: 2, minHeight: 44, justifyContent: 'center', paddingHorizontal: 8 },
  closeLabel: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
});
