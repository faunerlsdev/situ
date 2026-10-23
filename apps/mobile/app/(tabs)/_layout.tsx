import { Tabs } from 'expo-router';
import { StatusBar } from 'expo-status-bar';

import { ShellProvider, WazeHeader, WazeTabBar } from '@/components/shell';
import { theme } from '@/theme/theme';

export const unstable_settings = {
  initialRouteName: 'mapa',
};

export default function TabsLayout() {
  return (
    <ShellProvider>
      <StatusBar style="dark" />
      <Tabs
        tabBar={() => <WazeTabBar />}
        screenOptions={{
          header: () => <WazeHeader />,
          sceneStyle: { backgroundColor: theme.colors.surface },
        }}
      >
        <Tabs.Screen name="mapa" options={{ title: 'Mapa' }} />
        <Tabs.Screen name="chisme" options={{ title: 'Chisme' }} />
        <Tabs.Screen name="notificaciones" options={{ title: 'En vivo' }} />
        <Tabs.Screen name="mensajes" options={{ title: 'Mensajes' }} />
        <Tabs.Screen name="amigos" options={{ href: null, title: 'Amigos' }} />
        <Tabs.Screen name="perfil" options={{ href: null, title: 'Perfil' }} />
      </Tabs>
    </ShellProvider>
  );
}
