import { socialTabsFrom } from '@situ/shared';
import { Tabs } from 'expo-router';
import { StatusBar } from 'expo-status-bar';

import { ShellProvider, WazeHeader, WazeTabBar } from '@/components/shell';
import { useAppConfig } from '@/features/reports/use-app-config';
import { theme } from '@/theme/theme';

export const unstable_settings = {
  anchor: 'mapa',
};

export default function TabsLayout() {
  const config = useAppConfig();
  const social = socialTabsFrom(config.data);
  return (
    <ShellProvider>
      <StatusBar style="light" />
      <Tabs
        tabBar={() => <WazeTabBar />}
        screenOptions={{
          header: () => <WazeHeader />,
          sceneStyle: { backgroundColor: theme.colors.bg },
        }}
      >
        <Tabs.Screen name="mapa" options={{ title: 'Mapa' }} />
        <Tabs.Screen name="chisme" options={{ title: 'Chisme', href: social ? undefined : null }} />
        <Tabs.Screen name="notificaciones" options={{ title: 'Alertas' }} />
        <Tabs.Screen name="mensajes" options={{ title: 'Mensajes', href: social ? undefined : null }} />
        <Tabs.Screen name="amigos" options={{ href: null, title: 'Amigos' }} />
        <Tabs.Screen name="perfil" options={{ href: null, title: 'Perfil' }} />
      </Tabs>
    </ShellProvider>
  );
}
