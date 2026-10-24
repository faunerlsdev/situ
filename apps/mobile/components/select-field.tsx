import { useState } from 'react';
import { FlatList, Modal, Pressable, StyleSheet, Text, View } from 'react-native';

import { theme } from '@/theme/theme';

export function SelectField({
  label,
  value,
  placeholder,
  options,
  disabled,
  onChange,
}: {
  label: string;
  value: string;
  placeholder: string;
  options: { id: string; name: string }[];
  disabled?: boolean;
  onChange: (id: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const selected = options.find((option) => option.id === value);
  const isDisabled = disabled === true || options.length === 0;

  return (
    <View style={styles.wrap}>
      <Text style={styles.label}>{label}</Text>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={label}
        accessibilityState={{ disabled: isDisabled, expanded: open }}
        disabled={isDisabled}
        onPress={() => setOpen(true)}
        style={[styles.field, isDisabled && styles.disabled]}
      >
        <Text style={[styles.value, selected ? styles.valueOn : null]} numberOfLines={1}>
          {selected?.name ?? placeholder}
        </Text>
        <Text style={styles.chevron}>▾</Text>
      </Pressable>
      <Modal visible={open} transparent animationType="fade" onRequestClose={() => setOpen(false)}>
        <View style={styles.backdrop}>
          <Pressable accessibilityLabel="Cerrar" style={styles.dismiss} onPress={() => setOpen(false)} />
          <View style={styles.sheet}>
            <Text style={styles.sheetTitle}>{label}</Text>
            <FlatList
              data={options}
              keyExtractor={(option) => option.id}
              keyboardShouldPersistTaps="handled"
              renderItem={({ item }) => {
                const isSelected = item.id === value;
                return (
                  <Pressable
                    accessibilityRole="button"
                    accessibilityState={{ selected: isSelected }}
                    onPress={() => {
                      onChange(item.id);
                      setOpen(false);
                    }}
                    style={[styles.option, isSelected && styles.optionOn]}
                  >
                    <Text style={[styles.optionLabel, isSelected && styles.optionLabelOn]}>{item.name}</Text>
                  </Pressable>
                );
              }}
            />
          </View>
        </View>
      </Modal>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    gap: 6,
  },
  label: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
  },
  field: {
    height: 52,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 8,
    borderRadius: theme.radius.input,
    borderWidth: 1,
    borderColor: theme.colors.border,
    backgroundColor: theme.colors.input,
    paddingHorizontal: 14,
  },
  disabled: {
    opacity: 0.5,
  },
  value: {
    flex: 1,
    color: theme.colors.textPlaceholder,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 16,
  },
  valueOn: {
    color: theme.colors.textPrimary,
  },
  chevron: {
    color: theme.colors.textSecondary,
    fontSize: 16,
  },
  backdrop: {
    flex: 1,
    justifyContent: 'flex-end',
    backgroundColor: theme.colors.overlay,
  },
  dismiss: {
    flex: 1,
  },
  sheet: {
    maxHeight: '70%',
    borderTopLeftRadius: 20,
    borderTopRightRadius: 20,
    backgroundColor: theme.colors.surfaceElevated,
    paddingTop: 16,
    paddingBottom: 28,
  },
  sheetTitle: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sansBold,
    fontSize: 18,
    paddingHorizontal: 20,
    paddingBottom: 12,
  },
  option: {
    minHeight: 48,
    justifyContent: 'center',
    paddingHorizontal: 20,
  },
  optionOn: {
    backgroundColor: theme.colors.primarySoft,
  },
  optionLabel: {
    color: theme.colors.textPrimary,
    fontFamily: theme.fontFamily.sans,
    fontSize: 16,
  },
  optionLabelOn: {
    color: theme.colors.primary,
    fontFamily: theme.fontFamily.sansBold,
  },
});
