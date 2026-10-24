import { Redirect } from 'expo-router';

import { useSession } from '@/providers/session-provider';

export default function Index() {
  const { isSignedIn } = useSession();

  if (isSignedIn) {
    return <Redirect href="/(tabs)/mapa" />;
  }

  return <Redirect href="/(auth)/splash" />;
}
