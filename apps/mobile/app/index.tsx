import { Redirect } from 'expo-router';

import { takeReportLink } from '@/lib/report-link';
import { useSession } from '@/providers/session-provider';

export default function Index() {
  const { isSignedIn } = useSession();

  if (isSignedIn) {
    const reportId = takeReportLink();
    if (reportId) {
      return <Redirect href={{ pathname: '/reporte/[id]', params: { id: reportId } }} />;
    }

    return <Redirect href="/(tabs)/mapa" />;
  }

  return <Redirect href="/(auth)/splash" />;
}
