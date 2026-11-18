import { Redirect, Tabs } from 'expo-router';
import { StyleSheet } from 'react-native';
import { ScreenLoading } from '@/components/ui';
import { useAuth } from '@/auth/AuthProvider';
import { useI18n } from '@/i18n/I18nProvider';
import { useSelection } from '@/state/SelectionProvider';
import { colors, typography } from '@/theme';

export default function TabsLayout() {
  const { status } = useAuth();
  const { ready, tenant, installation } = useSelection();
  const { t } = useI18n();

  if (status === 'signedOut') return <Redirect href="/login" />;
  if (status === 'loading' || !ready) return <ScreenLoading />;
  if (!tenant) return <Redirect href="/companies" />;
  if (!installation) return <Redirect href="/installations" />;

  return (
    <Tabs
      screenOptions={{
        headerShown: false,
        tabBarActiveTintColor: colors.primary,
        tabBarInactiveTintColor: colors.textMuted,
        tabBarLabelStyle: styles.label,
        tabBarStyle: styles.bar,
        tabBarItemStyle: styles.item
      }}
    >
      <Tabs.Screen name="index" options={{ title: t('dashboard') }} />
      <Tabs.Screen name="reports" options={{ title: t('reports') }} />
      <Tabs.Screen name="inventory" options={{ title: t('inventory') }} />
      <Tabs.Screen name="users" options={{ title: t('users') }} />
      <Tabs.Screen name="notifications" options={{ title: t('notifications') }} />
      <Tabs.Screen name="settings" options={{ title: t('settings') }} />
    </Tabs>
  );
}

const styles = StyleSheet.create({
  bar: {
    backgroundColor: colors.surface,
    borderTopColor: colors.border,
    borderTopWidth: 1,
    height: 64,
    paddingBottom: 6,
    paddingTop: 6
  },
  item: { paddingHorizontal: 0 },
  label: { fontSize: typography.tiny, fontWeight: '700' }
});
