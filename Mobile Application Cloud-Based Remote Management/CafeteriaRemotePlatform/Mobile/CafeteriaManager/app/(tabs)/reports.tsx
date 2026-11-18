import { useCallback, useEffect, useState } from 'react';
import { Pressable, StyleSheet, View } from 'react-native';
import type { AnalyticsBundle, AnalyticsReport } from '@/api/types';
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
  ScreenLoading,
  SectionTitle
} from '@/components/ui';
import { useI18n } from '@/i18n/I18nProvider';
import { useSelection } from '@/state/SelectionProvider';
import { commandErrorMessage, isOfflineError, runCommand } from '@/remote/commands';
import { daysAgoIso, formatMoney, formatNumber, nowIso, startOfDayIso } from '@/format';
import { colors, radius, spacing } from '@/theme';

type Period = 'today' | 'last7' | 'last30' | 'all';

const periods: { key: Period; label: 'today' | 'last7' | 'last30' | 'all' }[] = [
  { key: 'today', label: 'today' },
  { key: 'last7', label: 'last7' },
  { key: 'last30', label: 'last30' },
  { key: 'all', label: 'all' }
];

export default function ReportsScreen() {
  const { t, language } = useI18n();
  const { tenant, installation, refreshInstallations } = useSelection();
  const [period, setPeriod] = useState<Period>('today');
  const [bundle, setBundle] = useState<AnalyticsBundle | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const online = Boolean(installation?.isOnline);
  const allowed = Boolean(installation?.access.canViewReports);

  const load = useCallback(
    async (nextPeriod: Period) => {
      if (!tenant || !installation) return;
      if (!installation.isOnline || !installation.access.canViewReports) {
        setError(null);
        return;
      }

      const payload =
        nextPeriod === 'today'
          ? { start: startOfDayIso(), end: nowIso() }
          : nextPeriod === 'last7'
            ? { start: daysAgoIso(6), end: nowIso() }
            : nextPeriod === 'last30'
              ? { start: daysAgoIso(29), end: nowIso() }
              : {};

      setLoading(true);
      setError(null);
      try {
        const result = await runCommand<AnalyticsBundle>({
          tenantId: tenant.id,
          installationId: installation.id,
          type: 'reports.sales',
          payload,
          timeoutMs: 40_000
        });
        setBundle(result);
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
    setBundle(null);
    void load(period);
  }, [installation?.id]); // eslint-disable-line react-hooks/exhaustive-deps

  function changePeriod(next: Period) {
    setPeriod(next);
    void load(next);
  }

  if (!tenant || !installation) {
    return (
      <Screen>
        <EmptyState title={t('selectInstallationFirst')} hint={t('noInstallationsHint')} />
      </Screen>
    );
  }

  if (!allowed) {
    return (
      <Screen>
        <Header title={t('reportsTitle')} />
        <EmptyState title={t('accessDenied')} hint={t('noAccess')} />
      </Screen>
    );
  }

  return (
    <Screen refreshing={loading} onRefresh={() => void load(period)}>
      <Header
        title={t('reportsTitle')}
        subtitle={installation.name}
        action={
          <Badge
            label={online ? t('online') : t('offline')}
            tone={online ? 'success' : 'neutral'}
          />
        }
      />

      <View style={styles.periods}>
        {periods.map((item) => {
          const active = period === item.key;
          return (
            <Pressable
              key={item.key}
              onPress={() => changePeriod(item.key)}
              style={[styles.chip, active && styles.chipActive]}
            >
              <AppText
                size={12}
                weight="700"
                color={active ? colors.surface : colors.textMuted}
              >
                {t(item.label)}
              </AppText>
            </Pressable>
          );
        })}
      </View>

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
          />
        </Card>
      ) : null}

      {error ? <ErrorBanner message={error} onRetry={() => void load(period)} /> : null}
      {loading && !bundle ? <ScreenLoading /> : null}

      {bundle ? (
        <>
          <Card tone="muted">
            <AppText weight="700">{bundle.periodLabel || t('summary')}</AppText>
            <AppText size={12} color={colors.textMuted}>
              {t('period')}: {bundle.periodLabel || t('all')}
            </AppText>
          </Card>

          <View style={styles.metrics}>
            <Metric
              label={t('revenue')}
              value={formatMoney(bundle.summary.revenue, undefined, language)}
            />
            <Metric label={t('orders')} value={formatNumber(bundle.summary.orders, language)} />
            <Metric
              label={t('averageBasket')}
              value={formatMoney(bundle.summary.averageBasket, undefined, language)}
            />
            <Metric
              label={t('netProfit')}
              value={formatMoney(bundle.summary.netProfit, undefined, language)}
            />
            <Metric
              label={t('expenses')}
              value={formatMoney(bundle.summary.expenses, undefined, language)}
            />
            <Metric
              label={t('unitsSold')}
              value={formatNumber(bundle.summary.unitsSold, language)}
            />
          </View>

          {bundle.kpis.length > 0 ? (
            <>
              <SectionTitle>{t('metrics')}</SectionTitle>
              <Card>
                {bundle.kpis.map((kpi, index) => (
                  <View key={`${kpi.label}-${index}`} style={styles.kpi}>
                    <AppText size={13} color={colors.textMuted}>
                      {kpi.label}
                    </AppText>
                    <AppText weight="700">{kpi.value}</AppText>
                  </View>
                ))}
              </Card>
            </>
          ) : null}

          {bundle.reports.map((report) => (
            <ReportCard key={`${report.kind}-${report.title}`} report={report} />
          ))}

          {bundle.reports.length === 0 ? (
            <EmptyState title={t('noReports')} />
          ) : null}
        </>
      ) : null}
    </Screen>
  );
}

function ReportCard({ report }: { report: AnalyticsReport }) {
  const { t } = useI18n();
  return (
    <Card>
      <AppText weight="700">{report.title}</AppText>
      {report.rows.length === 0 ? (
        <AppText size={13} color={colors.textMuted}>
          {report.emptyMessage || t('noReports')}
        </AppText>
      ) : (
        report.rows.map((row, index) => (
          <View key={`${report.kind}-${index}`} style={styles.reportRow}>
            <View style={styles.reportText}>
              <AppText weight="600">{row.primary}</AppText>
              {row.secondary || row.tertiary ? (
                <AppText size={12} color={colors.textMuted}>
                  {[row.secondary, row.tertiary].filter(Boolean).join(' · ')}
                </AppText>
              ) : null}
              {row.barRatio > 0 ? (
                <View style={styles.barTrack}>
                  <View
                    style={[
                      styles.barFill,
                      { width: `${Math.max(4, Math.min(100, row.barRatio * 100))}%` }
                    ]}
                  />
                </View>
              ) : null}
            </View>
            <View style={styles.reportValue}>
              <AppText weight="700">{row.value}</AppText>
              {row.rank ? (
                <AppText size={11} color={colors.textMuted}>
                  #{row.rank}
                </AppText>
              ) : null}
            </View>
          </View>
        ))
      )}
    </Card>
  );
}

const styles = StyleSheet.create({
  periods: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.xs },
  chip: {
    paddingVertical: 6,
    paddingHorizontal: spacing.md,
    borderRadius: radius.pill,
    backgroundColor: colors.surfaceMuted
  },
  chipActive: { backgroundColor: colors.primary },
  metrics: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md },
  kpi: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    gap: spacing.md,
    paddingVertical: 4
  },
  reportRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    gap: spacing.md,
    paddingVertical: spacing.sm,
    borderTopWidth: 1,
    borderTopColor: colors.border
  },
  reportText: { flex: 1, gap: 3 },
  reportValue: { alignItems: 'flex-end' },
  barTrack: {
    height: 6,
    borderRadius: 999,
    backgroundColor: colors.surfaceMuted,
    overflow: 'hidden',
    marginTop: spacing.xs
  },
  barFill: { height: 6, borderRadius: 999, backgroundColor: colors.primary }
});
