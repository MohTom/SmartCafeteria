import { Redirect } from 'expo-router';
import { ScreenLoading } from '@/components/ui';
import { useAuth } from '@/auth/AuthProvider';
import { useSelection } from '@/state/SelectionProvider';

export default function Index() {
  const { status } = useAuth();
  const { ready, tenant, installation } = useSelection();

  if (status === 'loading' || (status === 'signedIn' && !ready)) {
    return <ScreenLoading />;
  }

  if (status === 'signedOut') {
    return <Redirect href="/login" />;
  }

  if (!tenant) {
    return <Redirect href="/companies" />;
  }

  if (!installation) {
    return <Redirect href="/installations" />;
  }

  return <Redirect href="/(tabs)" />;
}
