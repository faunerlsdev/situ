import 'react-native-gesture-handler';

import {
  Montserrat_700Bold,
  Montserrat_800ExtraBold,
  Montserrat_800ExtraBold_Italic,
} from '@expo-google-fonts/montserrat';
import {
  SourceSans3_400Regular,
  SourceSans3_600SemiBold,
  SourceSans3_700Bold,
  SourceSans3_700Bold_Italic,
} from '@expo-google-fonts/source-sans-3';
import { useFonts } from 'expo-font';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import * as SplashScreen from 'expo-splash-screen';
import { useEffect } from 'react';
import { View } from 'react-native';
import { GestureHandlerRootView } from 'react-native-gesture-handler';

import { NotificationRuntime } from '@/features/notifications/push-runtime';
import { LocationEmitter } from '@/features/location/emitter';
import { useMe } from '@/features/reports/use-me';
import { clearLegacyProfile } from '@/lib/local-profile';
import { FeedbackProvider } from '@/providers/feedback-provider';
import { QueryProvider } from '@/providers/query-provider';
import { SessionProvider, useSession } from '@/providers/session-provider';
import { theme } from '@/theme/theme';

SplashScreen.preventAutoHideAsync().catch(() => {
  // Expo rejects this call when the splash is already hidden after a reload.
});

export const unstable_settings = {
  anchor: 'index',
};

export default function RootLayout() {
  return (
    <GestureHandlerRootView style={{ flex: 1 }}>
      <SessionProvider>
        <FeedbackProvider>
          <QueryProvider>
            <RootNavigator />
          </QueryProvider>
        </FeedbackProvider>
      </SessionProvider>
    </GestureHandlerRootView>
  );
}

function RootNavigator() {
  const { isReady, isSignedIn } = useSession();
  const me = useMe();
  const [fontsLoaded, fontError] = useFonts({
    SourceSans3_400Regular,
    SourceSans3_600SemiBold,
    SourceSans3_700Bold,
    SourceSans3_700Bold_Italic,
    Montserrat_700Bold,
    Montserrat_800ExtraBold,
    Montserrat_800ExtraBold_Italic,
  });
  const fontsReady = fontsLoaded || fontError !== null;

  useEffect(() => {
    if (isReady) {
      void clearLegacyProfile();
    }
  }, [isReady]);

  useEffect(() => {
    if (isReady && fontsReady) {
      SplashScreen.hideAsync().catch(() => {
        // A reload can hide the splash before this effect runs.
      });
    }
  }, [fontsReady, isReady]);

  if (!isReady || !fontsReady) {
    return <View style={{ flex: 1, backgroundColor: theme.colors.bg }} />;
  }

  return (
    <>
    <StatusBar style="light" />
    {isSignedIn ? <NotificationRuntime /> : null}
    {isSignedIn ? <LocationEmitter /> : null}
    <Stack
      screenOptions={{
        headerShown: false,
        contentStyle: { backgroundColor: theme.colors.bg },
      }}
    >
      <Stack.Protected guard={!isSignedIn}>
        <Stack.Screen name="(auth)" />
      </Stack.Protected>
      <Stack.Protected guard={isSignedIn && me.data?.status !== 'DeletionRequested'}>
        <Stack.Screen name="(tabs)" />
        <Stack.Screen name="ajustes" />
        <Stack.Screen name="reportar" />
        <Stack.Screen name="reporte/[id]" />
        <Stack.Screen name="reportes" />
        <Stack.Screen name="bloqueados" />
        <Stack.Screen name="cuenta" />
        <Stack.Screen name="visibilidad" />
        <Stack.Screen name="ubicacion" />
        <Stack.Screen name="usuario/[userName]" />
        <Stack.Screen name="editar-perfil" />
        <Stack.Screen name="cambiar-contrasena" />
        <Stack.Screen name="dispositivos" />
        <Stack.Screen name="esperando-autorizacion" />
      </Stack.Protected>
      <Stack.Protected guard={isSignedIn && me.data?.status === 'DeletionRequested'}>
        <Stack.Screen name="eliminacion" />
      </Stack.Protected>
      <Stack.Screen name="r/[id]" options={{ animation: 'fade', contentStyle: { backgroundColor: theme.colors.bg } }} />
      <Stack.Screen name="adios" options={{ gestureEnabled: false, animation: 'fade', contentStyle: { backgroundColor: theme.colors.bg } }} />
      <Stack.Screen name="confirmar-correo" options={{ gestureEnabled: false, animation: 'fade', contentStyle: { backgroundColor: theme.colors.bg } }} />
      <Stack.Screen name="invitacion" options={{ animation: 'fade', contentStyle: { backgroundColor: theme.colors.bg } }} />
      <Stack.Screen name="restablecer" options={{ gestureEnabled: false, animation: 'fade', contentStyle: { backgroundColor: theme.colors.bg } }} />
      <Stack.Screen name="tutor" options={{ gestureEnabled: false, animation: 'fade', contentStyle: { backgroundColor: theme.colors.bg } }} />
    </Stack>
    </>
  );
}
