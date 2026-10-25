import { useEffect, useState } from 'react';
import { StyleSheet, Switch, Text, View } from 'react-native';

import { theme } from '@/theme/theme';

export function SettingsToggle({
  label,
  value,
  disabled,
  hint,
  save,
}: {
  label: string;
  value: boolean;
  disabled?: boolean;
  hint?: string;
  save: (next: boolean) => Promise<boolean>;
}) {
  const [shown, setShown] = useState(value);

  useEffect(() => {
    setShown(value);
  }, [value]);

  return (
    <View style={styles.row}>
      <View style={styles.copy}>
        <Text style={styles.label}>{label}</Text>
        {hint ? <Text style={styles.hint}>{hint}</Text> : null}
      </View>
      <Switch
        accessibilityLabel={label}
        value={shown}
        disabled={disabled}
        onValueChange={(next) => {
          const previous = shown;
          setShown(next);
          void save(next).then((ok) => {
            if (!ok) {
              setShown(previous);
            }
          });
        }}
        trackColor={{ true: theme.colors.primary, false: theme.colors.border }}
        thumbColor={theme.colors.textOnPrimary}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  row: { minHeight: 44, flexDirection: 'row', alignItems: 'center', gap: 12 },
  copy: { flex: 1, gap: 2 },
  label: { color: theme.colors.textPrimary, fontFamily: theme.fontFamily.sans, fontSize: 16 },
  hint: { color: theme.colors.textSecondary, fontFamily: theme.fontFamily.sans, fontSize: 13 },
});
