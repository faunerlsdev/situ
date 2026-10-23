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
import * as SplashScreen from 'expo-splash-screen';
import { useEffect } from 'react';

import { FeedbackProvider } from '@/providers/feedback-provider';
import { SessionProvider, useSession } from '@/providers/session-provider';
import { theme } from '@/theme/theme';

SplashScreen.preventAutoHideAsync().catch(() => {
  // Expo rejects this call when the splash is already hidden after a reload.
});

export const unstable_settings = {
  anchor: '(tabs)',
};

export default function RootLayout() {
  return (
    <SessionProvider>
      <FeedbackProvider>
        <RootNavigator />
      </FeedbackProvider>
    </SessionProvider>
  );
}

function RootNavigator() {
  const { isReady, isSignedIn } = useSession();
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
    if (isReady && fontsReady) {
      SplashScreen.hideAsync().catch(() => {
        // A reload can hide the splash before this effect runs.
      });
    }
  }, [fontsReady, isReady]);

  if (!isReady || !fontsReady) {
    return null;
  }

  return (
    <Stack
      screenOptions={{
        headerShown: false,
        contentStyle: { backgroundColor: theme.colors.surface },
      }}
    >
      <Stack.Protected guard={!isSignedIn}>
        <Stack.Screen name="(auth)" />
      </Stack.Protected>
      <Stack.Protected guard={isSignedIn}>
        <Stack.Screen name="(tabs)" />
      </Stack.Protected>
      <Stack.Screen name="confirmar-correo" options={{ contentStyle: { backgroundColor: theme.colors.primary } }} />
      <Stack.Screen name="restablecer" options={{ contentStyle: { backgroundColor: theme.colors.primary } }} />
      <Stack.Screen name="tutor" options={{ contentStyle: { backgroundColor: theme.colors.primary } }} />
    </Stack>
  );
}
