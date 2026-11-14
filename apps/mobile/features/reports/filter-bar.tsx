import { categoriesUnder } from '@situ/shared';
import type { ReportCategory } from '@situ/shared';
import { Pressable, ScrollView, StyleSheet, Text } from 'react-native';

import { theme } from '@/theme/theme';

import { setMapFilters, useMapFilters } from './filters';

export function FilterBar({ categories }: { categories: ReportCategory[] }) {
  const filters = useMapFilters();
  const roots = categoriesUnder(categories, null);
  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.content}>
      {roots.map((category) => {
        const active = filters.categoryCodes.includes(category.code);
        return (
          <Pressable
            key={category.id}
            accessibilityRole="button"
            accessibilityState={{ selected: active }}
            accessibilityLabel={category.name}
            onPress={() => setMapFilters({
              ...filters,
              categoryCodes: active ? filters.categoryCodes.filter((code) => code !== category.code) : [...filters.categoryCodes, category.code],
            })}
            style={[styles.chip, active && styles.chipOn]}
          >
            <Text style={styles.label}>{category.name}</Text>
          </Pressable>
        );
      })}
      {(['all', 'hour', 'today'] as const).map((since) => (
        <Pressable key={since} accessibilityRole="button" accessibilityState={{ selected: filters.since === since }} accessibilityLabel={sinceLabel(since)} onPress={() => setMapFilters({ ...filters, since })} style={[styles.chip, filters.since === since && styles.chipOn]}>
          <Text style={styles.label}>{sinceLabel(since)}</Text>
        </Pressable>
      ))}
      <Pressable accessibilityRole="button" accessibilityState={{ selected: filters.friendsOnly }} accessibilityLabel="Solo amigos" onPress={() => setMapFilters({ ...filters, friendsOnly: !filters.friendsOnly })} style={[styles.chip, filters.friendsOnly && styles.chipOn]}>
        <Text style={styles.label}>Solo amigos</Text>
      </Pressable>
    </ScrollView>
  );
}

function sinceLabel(since: 'all' | 'hour' | 'today'): string {
  if (since === 'hour') return 'Última hora';
  if (since === 'today') return 'Hoy';
  return 'Todo';
}

const styles = StyleSheet.create({
  content: { gap: 8, alignItems: 'center', paddingVertical: 4 },
  chip: { minHeight: 44, paddingHorizontal: 12, borderRadius: 22, justifyContent: 'center', backgroundColor: theme.colors.surface },
  chipOn: { backgroundColor: theme.colors.primary },
  label: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 13 },
});
