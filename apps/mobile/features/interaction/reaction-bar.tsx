import { reactionKinds, reactionLabels } from '@situ/shared';
import type { ReactionCount } from '@situ/shared';
import { useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';

import { theme } from '@/theme/theme';

const glyphs: Record<(typeof reactionKinds)[number], string> = {
  Like: '👍',
  Love: '❤️',
  Haha: '😂',
  Wow: '😮',
  Sad: '😢',
  Angry: '😠',
};

export function ReactionBar({
  counts,
  mine,
  onToggle,
}: {
  counts: ReactionCount[];
  mine: string | null;
  onToggle: (type: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const mineLabel = mine && mine in reactionLabels ? reactionLabels[mine as keyof typeof reactionLabels] : 'Me gusta';

  return (
    <View style={styles.row}>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={mine ? `Quitar ${mineLabel}` : 'Me gusta'}
        onPress={() => onToggle(mine ?? 'Like')}
        onLongPress={() => setOpen(true)}
        delayLongPress={350}
        style={styles.main}
      >
        <Text style={[styles.glyph, mine && styles.on]}>{mine && mine in glyphs ? glyphs[mine as keyof typeof glyphs] : glyphs.Like}</Text>
        <Text style={[styles.label, mine && styles.on]}>{mineLabel}</Text>
      </Pressable>
      {counts.map((item) => (
        <Text key={item.type} style={styles.count} accessibilityLabel={`${labelOf(item.type)}: ${item.count}`}>
          {labelOf(item.type)} {item.count}
        </Text>
      ))}
      {open ? (
        <View style={styles.picker}>
          {reactionKinds.map((type) => (
            <Pressable
              key={type}
              accessibilityRole="button"
              accessibilityLabel={reactionLabels[type]}
              onPress={() => {
                setOpen(false);
                onToggle(type);
              }}
              style={styles.choice}
            >
              <Text style={styles.glyph}>{glyphs[type]}</Text>
            </Pressable>
          ))}
        </View>
      ) : null}
    </View>
  );
}

function labelOf(type: string): string {
  if (type in reactionLabels) {
    return reactionLabels[type as keyof typeof reactionLabels];
  }
  return type;
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: 8 },
  main: { minHeight: 44, minWidth: 44, flexDirection: 'row', alignItems: 'center', gap: 6 },
  glyph: { color: theme.colors.textSecondary, fontSize: 18 },
  label: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sansBold, fontSize: 14 },
  on: { color: theme.colors.primary },
  count: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 12 },
  picker: { flexDirection: 'row', gap: 4, padding: 4, borderRadius: 12, backgroundColor: theme.colors.surface },
  choice: { minWidth: 44, minHeight: 44, alignItems: 'center', justifyContent: 'center' },
});
