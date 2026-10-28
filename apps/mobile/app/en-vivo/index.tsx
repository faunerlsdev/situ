import type { LiveCard } from '@situ/shared';
import { messageFromUnknown } from '@situ/shared';
import { useRouter } from 'expo-router';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { LiveMark } from '@/features/live/live-mark';
import { visibilityLabel } from '@/features/live/logic';
import { useLiveRoster } from '@/features/live/roster';
import { TransmitLiveControl } from '@/features/live/transmit-control';
import { theme } from '@/theme/theme';

export default function LiveListScreen() {
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const query = useLiveRoster();
  const streams = query.streams;

  return (
    <ScrollView style={styles.screen} contentContainerStyle={[styles.content, { paddingTop: insets.top + 16, paddingBottom: insets.bottom + 24 }]}>
      <Pressable accessibilityRole="button" accessibilityLabel="Volver" onPress={() => router.back()} style={styles.back}>
        <Text style={styles.backLabel}>Volver</Text>
      </Pressable>
      <Text style={styles.title}>En vivo ahora</Text>
      <TransmitLiveControl label="Transmitir" />
      <Text style={styles.body}>Las transmisiones son anónimas. El video puede mostrar rostros y lugares.</Text>
      {query.isPending ? <Text style={styles.body}>Cargando transmisiones…</Text> : null}
      {query.isError ? (
        <View style={styles.block}>
          <Text accessibilityRole="alert" style={styles.error}>{messageFromUnknown(query.error) || 'No se pudo cargar la lista.'}</Text>
          <Pressable accessibilityRole="button" accessibilityLabel="Reintentar" onPress={() => void query.refetch()} style={styles.button}>
            <Text style={styles.buttonLabel}>Reintentar</Text>
          </Pressable>
        </View>
      ) : null}
      {!query.isPending && !query.isError && streams.length === 0 ? <Text style={styles.body}>Nadie está en vivo ahora.</Text> : null}
      {streams.map((item) => (
        <LiveRow key={item.id} item={item} onPress={() => router.push({ pathname: '/en-vivo/[id]', params: { id: item.id } })} />
      ))}
    </ScrollView>
  );
}

function LiveRow({ item, onPress }: { item: LiveCard; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={`Transmisión en vivo: ${item.title}`} onPress={onPress} style={styles.row}>
      <LiveMark size={44} pulse />
      <View style={styles.rowCopy}>
        <Text style={styles.rowTitle}>{item.title}</Text>
        <Text style={styles.body}>{`${item.displayName} · ${visibilityLabel(item.visibility)} · ${item.viewers} espectadores`}</Text>
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: theme.colors.bg },
  content: { gap: 12, paddingHorizontal: 20 },
  title: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.display, fontSize: 28, lineHeight: 34 },
  body: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 15, lineHeight: 22 },
  error: { color: theme.colors.danger, fontFamily: theme.fontFamily.sansBold, fontSize: 16, lineHeight: 22 },
  block: { gap: 8 },
  back: { minHeight: 44, alignSelf: 'flex-start', justifyContent: 'center' },
  backLabel: { color: theme.colors.primary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 12, padding: 14, borderRadius: theme.radius.card, backgroundColor: theme.colors.surfaceElevated },
  rowCopy: { flex: 1, gap: 4 },
  rowTitle: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 18 },
  button: { minHeight: 44, borderRadius: theme.radius.button, backgroundColor: theme.colors.primary, alignItems: 'center', justifyContent: 'center' },
  buttonLabel: { color: theme.colors.textOnPrimary, fontFamily: theme.fontFamily.sansBold, fontSize: 16 },
});
