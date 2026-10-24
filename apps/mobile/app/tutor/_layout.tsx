import { Stack } from 'expo-router';

import { theme } from '@/theme/theme';

export default function TutorLayout() {
  return (
    <Stack screenOptions={{ headerShown: false, contentStyle: { backgroundColor: theme.colors.bg } }}>
      <Stack.Screen name="autorizar" options={{ title: 'Autorización del acudiente' }} />
    </Stack>
  );
}
