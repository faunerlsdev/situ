import { useState } from 'react';
import { Pressable, StyleSheet, Text, TextInput, View } from 'react-native';

import { inputStyle } from '@/components/field';
import { PrimaryButton } from '@/components/primary-button';
import { StatusMessage } from '@/components/status-message';
import { theme } from '@/theme/theme';

const examples = [
  {
    id: 'ejemplo-reten',
    title: 'Retén',
    place: 'Ejemplo · Calle 80',
    detail: 'Un pin de muestra. No es un reporte real.',
  },
  {
    id: 'ejemplo-basura',
    title: 'Basura en la vía',
    place: 'Ejemplo · Parque central',
    detail: 'Otro pin de muestra para ver cómo se comenta.',
  },
] as const;

type ExampleId = (typeof examples)[number]['id'];
type Mode = 'browse' | 'report';

export function MapPlaceholder() {
  const [mode, setMode] = useState<Mode>('browse');
  const [selected, setSelected] = useState<ExampleId | null>(null);
  const [comment, setComment] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const current = examples.find((example) => example.id === selected);

  function onComment() {
    if (!selected || comment.trim().length === 0) {
      return;
    }

    setError(null);
    setNotice('Comentar queda para un bloque siguiente.');
  }

  return (
    <View style={styles.stack}>
      <Text style={styles.title}>Mapa</Text>
      <Text style={styles.lede}>
        Adelanto visual. Los pines son ejemplos, no reportes de la calle. La transmisión en vivo se inicia en esta
        app; este adelanto todavía no abre una.
      </Text>
      <View accessibilityRole="radiogroup" style={styles.modes}>
        <ModeButton label="Quiero chismosear" selected={mode === 'browse'} onPress={() => setMode('browse')} />
        <ModeButton label="Quiero reportar algo" selected={mode === 'report'} onPress={() => setMode('report')} />
      </View>
      {mode === 'report' ? (
        <Text style={styles.body}>
          El reporte real guarda la foto, el lugar y la categoría. Este adelanto todavía no publica.
        </Text>
      ) : (
        <View style={styles.list}>
          {examples.map((example) => {
            const isSelected = selected === example.id;
            return (
              <Pressable
                key={example.id}
                accessibilityRole="button"
                accessibilityState={{ selected: isSelected }}
                onPress={() => setSelected(example.id)}
                style={[styles.example, isSelected && styles.exampleSelected]}
              >
                <Text style={styles.exampleTitle}>{example.title}</Text>
                <Text style={styles.place}>{example.place}</Text>
              </Pressable>
            );
          })}
        </View>
      )}
      <View style={styles.map} accessibilityElementsHidden importantForAccessibility="no-hide-descendants">
        <View style={[styles.road, styles.roadHorizontal]} />
        <View style={[styles.road, styles.roadVertical]} />
        <View style={[styles.live, styles.pinSignal]} />
        <View style={[styles.live, styles.pinAmber]} />
      </View>
      <Text style={styles.section}>Comentar</Text>
      {current ? (
        <Text style={styles.body}>
          {current.title}. {current.detail}
        </Text>
      ) : (
        <Text style={styles.body}>Elige un reporte de ejemplo para escribir.</Text>
      )}
      <StatusMessage error={error} notice={notice} />
      <Text style={styles.label}>Comentario</Text>
      <TextInput keyboardAppearance="dark"
        value={comment}
        onChangeText={setComment}
        editable={current !== undefined}
        multiline
        accessibilityLabel="Comentario"
        style={[inputStyle, styles.comment]}
      />
      <PrimaryButton
        label="Comentar"
        pendingLabel="Enviando…"
        pending={false}
        disabled={current === undefined || comment.trim().length === 0}
        onPress={() => {
          void onComment();
        }}
      />
    </View>
  );
}

function ModeButton({ label, selected, onPress }: { label: string; selected: boolean; onPress: () => void }) {
  return (
    <Pressable
      accessibilityRole="radio"
      accessibilityState={{ selected }}
      onPress={onPress}
      style={[styles.mode, selected && styles.modeSelected]}
    >
      <Text style={[styles.modeLabel, selected && styles.modeLabelSelected]}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  stack: {
    gap: 12,
  },
  title: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.display,
    fontSize: 40,
  },
  lede: {
    color: theme.colors.textSecondary,
    fontFamily: theme.fontFamily.sans,
    fontSize: 15,
    lineHeight: 22,
  },
  body: {
    color: theme.colors.textSecondary,
    fontFamily: theme.fontFamily.sans,
    fontSize: 15,
    lineHeight: 22,
  },
  modes: {
    gap: 8,
  },
  mode: {
    minHeight: 48,
    justifyContent: 'center',
    borderRadius: 999,
    borderWidth: 1,
    borderColor: theme.colors.border,
    paddingHorizontal: 16,
    backgroundColor: theme.colors.input,
  },
  modeSelected: {
    borderColor: theme.colors.primary,
    backgroundColor: theme.colors.primarySoft,
  },
  modeLabel: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
  },
  modeLabelSelected: {
    color: theme.colors.primary,
  },
  list: {
    gap: 8,
  },
  example: {
    borderWidth: 1,
    borderColor: theme.colors.border,
    borderRadius: 18,
    paddingHorizontal: 16,
    paddingVertical: 12,
    backgroundColor: theme.colors.surface,
  },
  exampleSelected: {
    borderColor: theme.colors.primary,
    backgroundColor: theme.colors.primarySoft,
  },
  exampleTitle: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
  },
  place: {
    marginTop: 4,
    color: theme.colors.textSecondary,
    fontFamily: theme.fontFamily.sans,
    fontSize: 14,
  },
  map: {
    height: 220,
    borderRadius: 24,
    overflow: 'hidden',
    backgroundColor: theme.colors.mapWater,
  },
  road: {
    position: 'absolute',
    backgroundColor: theme.colors.mapRoad,
    borderRadius: 999,
  },
  roadHorizontal: {
    top: 78,
    left: 28,
    right: 28,
    height: 12,
  },
  roadVertical: {
    top: 28,
    bottom: 28,
    left: '48%',
    width: 12,
  },
  live: {
    position: 'absolute',
    width: 16,
    height: 16,
    borderRadius: 8,
  },
  pinSignal: {
    left: '28%',
    top: '38%',
    backgroundColor: theme.colors.live,
  },
  pinAmber: {
    left: '62%',
    top: '58%',
    backgroundColor: theme.colors.primaryLight,
  },
  section: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.display,
    fontSize: 28,
  },
  label: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 14,
  },
  comment: {
    minHeight: 112,
    textAlignVertical: 'top',
  },
});
