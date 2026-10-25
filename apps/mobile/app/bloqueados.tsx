import { anonymousBlockLeaks, blockRowLabel, confirm, feedbackMessages, notify, profileMessages } from '@situ/shared';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

export default function BloqueadosScreen() {
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const queryClient = useQueryClient();
  const query = useQuery({ queryKey: ['blocks'], queryFn: () => api.blocks() });
  const blocks = (query.data?.blocks ?? []).filter((item) => !anonymousBlockLeaks(item));

  async function unblock(blockId: string) {
    const accepted = await confirm({
      title: feedbackMessages.unblockTitle,
      message: feedbackMessages.unblockBody,
      type: 'danger',
      confirmText: 'Desbloquear',
      cancelText: 'Cancelar',
    });
    if (!accepted) {
      return;
    }
    try {
      await api.unblock(blockId);
      await queryClient.invalidateQueries({ queryKey: ['blocks'] });
      await queryClient.invalidateQueries({ queryKey: ['reports-map'] });
      notify.success(feedbackMessages.unblockDone);
    } catch (error) {
      notify.error(error, { action: { label: feedbackMessages.retry, onPress: () => void unblock(blockId) } });
    }
  }

  return (
    <View style={[styles.screen, { paddingTop: insets.top + 12 }]}>
      <Pressable accessibilityRole="button" accessibilityLabel="Volver" onPress={() => router.back()} style={styles.back}>
        <Text style={styles.backLabel}>Volver</Text>
      </Pressable>
      <Text style={styles.title}>{feedbackMessages.blockedTitle}</Text>
      <View style={styles.row}>
        <Text style={styles.name}>{profileMessages.privateTitle}</Text>
        <Text style={styles.body}>{profileMessages.privateBody}</Text>
      </View>
      {query.isLoading ? <View style={styles.skeleton} /> : null}
      {query.isError ? (
        <View style={styles.empty}>
          <Text style={styles.body}>No pudimos cargar los bloqueos.</Text>
          <Pressable accessibilityRole="button" accessibilityLabel="Reintentar" onPress={() => void query.refetch()} style={styles.button}>
            <Text style={styles.buttonLabel}>Reintentar</Text>
          </Pressable>
        </View>
      ) : null}
      {!query.isLoading && !query.isError && blocks.length === 0 ? <Text style={styles.body}>{feedbackMessages.blockedEmpty}</Text> : null}
      <ScrollView contentContainerStyle={styles.list}>
        {blocks.map((block) => (
          <View key={block.blockId} style={styles.row}>
            <Text style={styles.name}>{blockRowLabel(block)}</Text>
            <Pressable accessibilityRole="button" accessibilityLabel={`Desbloquear ${blockRowLabel(block)}`} onPress={() => void unblock(block.blockId)} style={styles.unblock}>
              <Text style={styles.unblockLabel}>Desbloquear</Text>
            </Pressable>
          </View>
        ))}
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, paddingHorizontal: 20, backgroundColor: theme.colors.bg },
  back: { minHeight: 44, justifyContent: 'center' },
  backLabel: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 28, marginBottom: 12 },
  body: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 16 },
  list: { gap: 12, paddingBottom: 32 },
  row: { gap: 8, padding: 12, borderRadius: 12, backgroundColor: theme.colors.surface },
  name: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 16 },
  unblock: { minHeight: 44, justifyContent: 'center' },
  unblockLabel: { color: theme.colors.danger, fontFamily: theme.fontFamily.sansBold },
  skeleton: { height: 72, borderRadius: 12, backgroundColor: theme.colors.skeleton },
  empty: { gap: 12 },
  button: { minHeight: 44, borderRadius: theme.radius.button, backgroundColor: theme.colors.primary, alignItems: 'center', justifyContent: 'center' },
  buttonLabel: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold },
});
