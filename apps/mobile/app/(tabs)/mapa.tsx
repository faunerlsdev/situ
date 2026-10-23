import { messageFromUnknown } from '@situ/shared';
import { useState } from 'react';
import { Image, Modal, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';

import { StatusMessage } from '@/components/status-message';
import { api } from '@/lib/api';
import { theme } from '@/theme/theme';

const live = require('../../assets/images/icons/situ-pin-mapa.png');
const isotipo = require('../../assets/images/brand/situ-isotipo-negro.png');
const news = require('../../assets/images/icons/situ-noticias.png');

const examples = [
  {
    id: 'ejemplo-reten',
    title: 'Retén',
    place: 'Ejemplo · Calle 80',
    detail: 'Un pin de muestra. No es un reporte real.',
    top: '34%',
    left: '28%',
  },
  {
    id: 'ejemplo-basura',
    title: 'Basura en la vía',
    place: 'Ejemplo · Parque central',
    detail: 'Otro pin de muestra para ver cómo se comenta.',
    top: '52%',
    left: '58%',
  },
] as const;

type ExampleId = (typeof examples)[number]['id'];

export default function MapScreen() {
  const [selected, setSelected] = useState<ExampleId | null>(null);
  const [comment, setComment] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [sheet, setSheet] = useState(false);
  const current = examples.find((example) => example.id === selected);

  async function onComment() {
    if (!selected || comment.trim().length === 0) {
      return;
    }

    setError(null);
    setNotice(null);
    setPending(true);

    try {
      await api.commentOnReport(selected, { body: comment.trim() });
      setNotice('Comentario enviado.');
      setComment('');
    } catch (cause) {
      setError(messageFromUnknown(cause));
    } finally {
      setPending(false);
    }
  }

  return (
    <View style={styles.map}>
      <View style={styles.glow} />
      {examples.map((example) => (
        <Pressable
          key={example.id}
          accessibilityRole="button"
          accessibilityLabel={example.title}
          accessibilityState={{ selected: selected === example.id }}
          onPress={() => setSelected(example.id)}
          style={[styles.pinHit, { top: example.top, left: example.left }]}
        >
          <Image source={live} resizeMode="contain" style={styles.live} />
        </Pressable>
      ))}
      <Pressable accessibilityRole="button" accessibilityLabel="Centrar en mi ubicación" style={[styles.locate, theme.shadowSheet]}>
        <View style={styles.locateDot} />
      </Pressable>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Reportar"
        onPress={() => setSheet(true)}
        style={[styles.fab, theme.shadowSheet]}
      >
        <Image source={isotipo} resizeMode="contain" style={styles.fabMark} />
      </Pressable>
      {current ? (
        <View style={styles.comment}>
          <Text style={styles.commentTitle}>{current.title}</Text>
          <Text style={styles.commentBody}>{current.detail}</Text>
          <StatusMessage error={error} notice={notice} />
          <TextInput
            value={comment}
            onChangeText={setComment}
            placeholder="Comentario"
            placeholderTextColor={theme.colors.gray400}
            accessibilityLabel="Comentario"
            style={styles.input}
          />
          <Pressable
            accessibilityRole="button"
            disabled={pending || comment.trim().length === 0}
            onPress={() => {
              void onComment();
            }}
            style={styles.send}
          >
            <Text style={styles.sendLabel}>{pending ? 'Enviando…' : 'Comentar'}</Text>
          </Pressable>
        </View>
      ) : null}
      <ReportSheet visible={sheet} onClose={() => setSheet(false)} />
    </View>
  );
}

function ReportSheet({ visible, onClose }: { visible: boolean; onClose: () => void }) {
  return (
    <Modal visible={visible} transparent animationType="slide" onRequestClose={onClose}>
      <View style={styles.sheetWrap}>
        <Pressable accessibilityLabel="Cerrar" style={styles.sheetBackdrop} onPress={onClose} />
        <View style={[styles.sheet, theme.shadowSheet]}>
          <View style={styles.handle} />
          <Text style={styles.sheetTitle}>Reportar</Text>
          <View style={styles.sheetDivider} />
          <Text style={styles.sheetBody}>
            El reporte real guarda la foto, el lugar y la categoría. Este adelanto todavía no publica.
          </Text>
          <View style={styles.grid}>
            {['Retén', 'Basura', 'Hurto'].map((label) => (
              <View key={label} style={styles.cell}>
                <Image source={news} resizeMode="contain" style={styles.cellIcon} />
                <Text style={styles.cellLabel}>{label}</Text>
              </View>
            ))}
          </View>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  map: {
    flex: 1,
    backgroundColor: theme.colors.bgDarkEdge,
  },
  glow: {
    position: 'absolute',
    top: '18%',
    alignSelf: 'center',
    width: 280,
    height: 280,
    borderRadius: 140,
    backgroundColor: theme.colors.bgDarkCenter,
  },
  pinHit: {
    position: 'absolute',
  },
  live: {
    width: 28,
    height: 36,
  },
  locate: {
    position: 'absolute',
    left: 16,
    bottom: 16,
    width: 48,
    height: 48,
    borderRadius: 24,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: theme.colors.white,
  },
  locateDot: {
    width: 14,
    height: 14,
    borderRadius: 7,
    borderWidth: 2,
    borderColor: theme.colors.primary,
  },
  fab: {
    position: 'absolute',
    right: 16,
    bottom: 16,
    width: 64,
    height: 64,
    borderRadius: 32,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: theme.colors.white,
  },
  fabMark: {
    width: 40,
    height: 52,
  },
  comment: {
    position: 'absolute',
    left: 16,
    right: 88,
    bottom: 76,
    gap: 8,
    borderRadius: 20,
    backgroundColor: theme.colors.surface,
    padding: 12,
  },
  commentTitle: {
    color: theme.colors.ink,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
  },
  commentBody: {
    color: theme.colors.gray600,
    fontFamily: theme.fontFamily.sans,
    fontSize: 13,
  },
  input: {
    minHeight: 44,
    borderRadius: theme.radius.input,
    borderWidth: 1,
    borderColor: theme.colors.gray200,
    paddingHorizontal: 10,
    color: theme.colors.ink,
    fontFamily: theme.fontFamily.sans,
    fontSize: 16,
  },
  send: {
    minHeight: 40,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: theme.radius.button,
    backgroundColor: theme.colors.primary,
  },
  sendLabel: {
    color: theme.colors.white,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
  },
  sheetWrap: {
    flex: 1,
    justifyContent: 'flex-end',
  },
  sheetBackdrop: {
    ...StyleSheet.absoluteFill,
    backgroundColor: theme.colors.black,
    opacity: 0.4,
  },
  sheet: {
    borderTopLeftRadius: 20,
    borderTopRightRadius: 20,
    backgroundColor: theme.colors.surface,
    padding: 16,
    gap: 12,
  },
  handle: {
    alignSelf: 'center',
    width: 40,
    height: 4,
    borderRadius: 2,
    backgroundColor: theme.colors.gray200,
  },
  sheetTitle: {
    color: theme.colors.ink,
    fontFamily: theme.fontFamily.display,
    fontSize: 22,
  },
  sheetDivider: {
    height: 1,
    backgroundImage: `linear-gradient(90deg, transparent, ${theme.colors.primary}, transparent)`,
  },
  sheetBody: {
    color: theme.colors.gray600,
    fontFamily: theme.fontFamily.sans,
    fontSize: 16,
    lineHeight: 22,
  },
  grid: {
    flexDirection: 'row',
    gap: 8,
  },
  cell: {
    flex: 1,
    minHeight: 96,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    borderRadius: theme.radius.button,
    borderWidth: 1,
    borderColor: theme.colors.gray200,
  },
  cellIcon: {
    width: 36,
    height: 28,
  },
  cellLabel: {
    color: theme.colors.ink,
    fontFamily: theme.fontFamily.sans,
    fontSize: 13,
  },
});
