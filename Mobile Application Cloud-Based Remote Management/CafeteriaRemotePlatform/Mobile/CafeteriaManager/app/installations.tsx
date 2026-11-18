import { Redirect, useRouter } from 'expo-router';
import {
  AppText,
  Badge,
  Button,
  Card,
  EmptyState,
  ErrorBanner,
  Header,
  Screen,
  ScreenLoading,
  SectionTitle,
  SelectRow
} from '@/components/ui';
import { LinkDesktopCard } from '@/components/LinkDesktopCard';
import { useAuth } from '@/auth/AuthProvider';
import { useI18n } from '@/i18n/I18nProvider';
import { useSelection } from '@/state/SelectionProvider';
import { formatDateTime } from '@/format';
import { colors, spacing } from '@/theme';
import { StyleSheet, View } from 'react-native';

export default function InstallationsScreen() {
  const { status } = useAuth();
  const {
    ready,
    tenant,
    installation,
    installations,
    loadingInstallations,
    installationsError,
    refreshInstallations,
    selectInstallation
  } = useSelection();
  const { t, language } = useI18n();
  const router = useRouter();

  if (status === 'signedOut') return <Redirect href="/login" />;
  if (status === 'loading' || !ready) return <ScreenLoading />;
  if (!tenant) return <Redirect href="/companies" />;

  async function choose(id: string) {
    await selectInstallation(id);
    router.replace('/(tabs)');
  }

  return (
    <Screen
      refreshing={loadingInstallations}
      onRefresh={() => void refreshInstallations()}
    >
      <Header
        title={t('installationsTitle')}
        subtitle={t('installationsSubtitle')}
        action={
          <Button
            compact
            variant="secondary"
            label={t('changeCompany')}
            onPress={() => router.replace('/companies')}
          />
        }
      />

      <Card tone="muted">
        <AppText size={12} color={colors.textMuted}>
          {t('tenant')}
        </AppText>
        <AppText weight="700">{tenant.name}</AppText>
      </Card>

      {installationsError ? (
        <ErrorBanner message={installationsError} onRetry={() => void refreshInstallations()} />
      ) : null}

      {loadingInstallations && installations.length === 0 ? <ScreenLoading /> : null}

      {!loadingInstallations && installations.length === 0 ? (
        <EmptyState title={t('noInstallations')} hint={t('noInstallationsHint')} />
      ) : null}

      {installations.map((item) => (
        <SelectRow
          key={item.id}
          title={item.name}
          subtitle={`${item.machineName} · ${t('lastSeen')}: ${
            item.lastSeenAt ? formatDateTime(item.lastSeenAt, language) : t('never')
          }`}
          selected={installation?.id === item.id}
          onPress={() => void choose(item.id)}
          trailing={
            <Badge
              label={item.isOnline ? t('online') : t('offline')}
              tone={item.isOnline ? 'success' : 'neutral'}
            />
          }
        />
      ))}

      {installation ? (
        <Card>
          <AppText weight="700">{installation.name}</AppText>
          <AppText size={12} color={colors.textMuted}>
            {t('version')}: {installation.appVersion}
          </AppText>
          <View style={styles.badges}>
            {installation.access.canViewReports ? (
              <Badge label={t('reportsAccess')} tone="success" />
            ) : null}
            {installation.access.canManageUsers ? (
              <Badge label={t('usersAccess')} tone="primary" />
            ) : null}
            {installation.access.canAdjustInventory ? (
              <Badge label={t('inventoryAccess')} tone="warning" />
            ) : null}
          </View>
          <Button
            label={t('dashboard')}
            onPress={() => router.replace('/(tabs)')}
            variant="primary"
          />
        </Card>
      ) : null}

      <SectionTitle>{t('linkDesktop')}</SectionTitle>
      <LinkDesktopCard />
    </Screen>
  );
}

const styles = StyleSheet.create({
  badges: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.xs, paddingVertical: spacing.xs }
});
