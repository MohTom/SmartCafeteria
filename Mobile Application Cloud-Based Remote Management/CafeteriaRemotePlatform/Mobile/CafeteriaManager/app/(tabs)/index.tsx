import { useCallback, useEffect, useState } from 'react';
import { StyleSheet, View } from 'react-native';
import { useRouter } from 'expo-router';
import type { DashboardSnapshot } from '@/api/types';
import {
  AppText,
  Badge,
  Button,
  Card,
  EmptyState,
  ErrorBanner,
  Header,
  Metric,
  Screen,
  ScreenLoading
} from '@/components/ui';
import { useI18n } from '@/i18n/I18nProvider';
import { useSelection } from '@/state/SelectionProvider';
import { commandErrorMessage, isOfflineError, runCommand } from '@/remote/commands';
import { formatDateTime, formatMoney, formatNumber } from '@/format';
import { colors, spacing } from '@/theme';

export default function DashboardScreen() {
  const { t, language } = useI18n();
  const { tenant, installation, refreshInstallations, loadingInstallations } = useSelection();
  const router = useRouter();
  const [snapshot, setSnapshot] = useState<DashboardSnapshot | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastUpdatedAt, setLastUpdatedAt] = useState<string | null>(null);

  const online = Boolean(installation?.isOnline);

  const load = useCallback(
    async (force = false) => {
      if (!tenant || !installation) return;
      if (!installation.isOnline) {
        setError(null);
        return;
      }
      setLoading(true);
      setError(null);
      try {
        const result = await runCommand<DashboardSnapshot>({
          tenantId: tenant.id,
          installationId: installation.id,
          type: 'dashboard.snapshot',
          timeoutMs: 25_000
        });
        setSnapshot(result);
        setLastUpdatedAt(new Date().toISOString());
        if (force) await refreshInstallations();
      } catch (caught) {
        if (isOfflineError(caught)) {
          setError(t('offlineCommand'));
          void refreshInstallations();
        } else {
          setError(commandErrorMessage(caught));
        }
      } finally {
        setLoading(false);
      }
    },
    [tenant, installation, refreshInstallations, t]
  );

  useEffect(() => {
    setSnapshot(null);
    void load();
  }, [installation?.id]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (!online) return;
    const timer = setInterval(() => void load(), 60_000);
    return () => clearInterval(timer);
  }, [online, load]);

  if (!tenant || !installation) {
    return (
      <Screen>
        <EmptyState title={t('selectInstallationFirst')} hint={t('noInstallationsHint')} />
      </Screen>
    );
  }

  return (
    <Screen refreshing={loading} onRefresh={() => void load(true)}>
      <Header
        title={t('dashboardTitle')}
        subtitle={installation.name}
        action={
          <Badge
            label={online ? t('online') : t('offline')}
            tone={online ? 'success' : 'neutral'}
          />
        }
      />

      <Card tone="muted">
        <AppText size={12} color={colors.textMuted}>
          {t('installation')}
        </AppText>
        <AppText weight="700">{installation.machineName}</AppText>
        <AppText size={12} color={colors.textMuted}>
          {t('lastSeen')}:{' '}
          {installation.lastSeenAt
            ? formatDateTime(installation.lastSeenAt, language)
            : t('never')}
        </AppText>
      </Card>

      {!online ? (
        <Card tone="muted">
          <AppText weight="600">{t('offline')}</AppText>
          <AppText size={13} color={colors.textMuted}>
            {t('offlineHint')}
          </AppText>
          <Button
            label={t('refresh')}
            variant="secondary"
            onPress={() => void refreshInstallations()}
            loading={loadingInstallations}
          />
        </Card>
      ) : null}

      {error ? <ErrorBanner message={error} onRetry={() => void load()} /> : null}

      {loading && !snapshot ? <ScreenLoading /> : null}

      {snapshot ? (
        <>
          <View style={styles.metrics}>
            <Metric
              label={t('revenue')}
              value={formatMoney(snapshot.totalRevenue, snapshot.currency, language)}
            />
            <Metric label={t('orders')} value={formatNumber(snapshot.completedOrders, language)} />
            <Metric label={t('lowStock')} value={formatNumber(snapshot.lowStockCount, language)} />
            <Metric label={t('products')} value={formatNumber(snapshot.totalProducts, language)} />
          </View>

          <Card>
            <AppText size={12} color={colors.textMuted}>
              {t('generated')}: {formatDateTime(snapshot.generatedAt, language)}
            </AppText>
            {lastUpdatedAt ? (
              <AppText size={12} color={colors.textMuted}>
                {t('lastUpdated')}: {formatDateTime(lastUpdatedAt, language)}
              </AppText>
            ) : null}
            <AppText size={12} color={colors.textMuted}>
              {t('snapshotHint')}
            </AppText>
          </Card>
        </>
      ) : null}

      {!loading && !snapshot && online && !error ? (
        <EmptyState title={t('noData')} hint={t('snapshotHint')} />
      ) : null}

      <View style={styles.actions}>
        <Button
          label={t('reports')}
          variant="secondary"
          onPress={() => router.push('/(tabs)/reports')}
        />
        <Button
          label={t('inventory')}
          variant="secondary"
          onPress={() => router.push('/(tabs)/inventory')}
        />
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  metrics: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: spacing.md
  },
  actions: { flexDirection: 'row', gap: spacing.sm, marginTop: spacing.sm }
});
