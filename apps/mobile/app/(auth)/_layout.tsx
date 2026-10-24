import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';

import { theme } from '@/theme/theme';

export const unstable_settings = {
  anchor: 'splash',
};

export default function AuthLayout() {
  return (
    <>
      <StatusBar style="light" />
      <Stack
        screenOptions={{
          headerShown: false,
          gestureEnabled: false,
          animation: 'fade',
          contentStyle: { backgroundColor: theme.colors.bg },
        }}
      >
        <Stack.Screen
          name="splash"
          options={{ contentStyle: { backgroundColor: theme.colors.bg } }}
        />
        <Stack.Screen name="login" options={{ title: 'Ingresar' }} />
        <Stack.Screen name="registro" options={{ title: 'Registro' }} />
        <Stack.Screen name="recuperar" options={{ title: 'Recuperar contraseña' }} />
        <Stack.Screen name="esperando-tutor" options={{ title: 'Esperando autorización' }} />
      </Stack>
    </>
  );
}
