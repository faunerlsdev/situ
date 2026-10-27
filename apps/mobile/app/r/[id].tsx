import { useLocalSearchParams, useRouter } from 'expo-router';
import { useEffect } from 'react';
import { View } from 'react-native';

import { rememberReportLink } from '@/lib/report-link';
import { useSession } from '@/providers/session-provider';
import { theme } from '@/theme/theme';

export default function ReportLinkScreen() {
  const params = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const { isReady, isSignedIn } = useSession();
  const id = typeof params.id === 'string' ? params.id : '';

  useEffect(() => {
    if (!isReady || id.length === 0) {
      return;
    }

    if (isSignedIn) {
      router.replace({ pathname: '/reporte/[id]', params: { id } });
      return;
    }

    rememberReportLink(id);
    router.replace('/login');
  }, [id, isReady, isSignedIn, router]);

  return <View style={{ flex: 1, backgroundColor: theme.colors.bg }} />;
}
