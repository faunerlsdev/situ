import { useRouter } from 'expo-router';
import { useEffect, useRef } from 'react';
import { ActivityIndicator, View } from 'react-native';

import { beginPublicLive } from '@/features/live/start-public';
import { useMe } from '@/features/reports/use-me';
import { theme } from '@/theme/theme';

export default function StartLiveScreen() {
  const router = useRouter();
  const me = useMe();
  const started = useRef(false);

  useEffect(() => {
    if (started.current || me.isPending) {
      return;
    }
    started.current = true;
    void beginPublicLive(router, me.data).then((opened) => {
      if (!opened && router.canGoBack()) {
        router.back();
      }
    });
  }, [me.data, me.isPending, router]);

  return (
    <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: theme.colors.bg }}>
      <ActivityIndicator color={theme.colors.primary} />
    </View>
  );
}
