import { useCallback, useEffect, useState } from 'react';
import { Redirect, useRouter } from 'expo-router';
import { StyleSheet, View } from 'react-native';
import { ApiError } from '@/api/client';
import type { RemoteCommand } from '@/api/types';
import {
  AppText,
  Badge,
  Button,
  Card,
  EmptyState,
  ErrorBanner,
  Header,
  Screen,
  ScreenLoading
} from '@/components/ui';
import { useAuth } from '@/auth/AuthProvider';
import { useI18n } from '@/i18n/I18nProvider';
import { useSelection } from '@/state/SelectionProvider';
import { useRealtime } from '@/realtime/RealtimeProvider';
import { cancelCommand, listCommands } from '@/remote/commands';
import { commandStatusKey, commandStatusTone, formatDateTime } from '@/format';
import { colors, spacing } from '@/theme';

export default function HistoryScreen() {
  const { status } = useAuth();
  const { tenant, installation } = useSelection();
  const { commandVersion } = useRealtime();
  const { t, language } = useI18n();
  const router = useRouter();
  const [commands, setCommands] = useState<RemoteCommand[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!tenant) return;
    setLoading(true);
    setError(null);
    try {
      const rows = await listCommands(tenant.id, installation?.id);
      setCommands(rows);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : t('networkError'));
    } finally {
      setLoading(false);
    }
  }, [tenant, installation, t]);

  useEffect(() => {
    void load();
  }, [load, commandVersion]);

  async function cancel(command: RemoteCommand) {
    if (!tenant || busyId) return;
    setBusyId(command.id);
    try {
      const updated = await cancelCommand(tenant.id, command.id);
      setCommands((current) =>
        current.map((item) => (item.id === command.id ? updated : item))
      );
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : t('error'));
    } finally {
      setBusyId(null);
    }
  }

  if (status === 'signedOut') return <Redirect href="/login" />;
  if (!tenant) return <Redirect href="/companies" />;

  return (
    <Screen refreshing={loading} onRefresh={() => void load()}>
      <Header
        title={t('commandHistoryTitle')}
        subtitle={installation?.name ?? tenant.name}
        action={<Button compact variant="secondary" label={t('close')} onPress={() => router.back()} />}
      />

      {error ? <ErrorBanner message={error} onRetry={() => void load()} /> : null}
      {loading && commands.length === 0 ? <ScreenLoading /> : null}
      {!loading && commands.length === 0 ? <EmptyState title={t('noHistory')} /> : null}

      {commands.map((command) => (
        <Card key={command.id}>
          <View style={styles.row}>
            <View style={styles.grow}>
              <AppText weight="700">{command.type}</AppText>
              <AppText size={11} color={colors.textMuted}>
                {formatDateTime(command.createdAt, language)}
              </AppText>
            </View>
            <Badge
              label={t(commandStatusKey(command.status))}
              tone={commandStatusTone(command.status)}
            />
          </View>

          {command.error ? (
            <AppText size={12} color={colors.danger}>
              {command.error}
            </AppText>
          ) : null}

          {command.resultJson ? (
            <AppText size={11} color={colors.textMuted} numberOfLines={4} selectable>
              {command.resultJson}
            </AppText>
          ) : null}

          {command.status === 'Pending' || command.status === 'Dispatched' ? (
            <Button
              label={t('cancelCommand')}
              variant="ghost"
              compact
              loading={busyId === command.id}
              onPress={() => void cancel(command)}
            />
          ) : null}
        </Card>
      ))}
    </Screen>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: spacing.sm },
  grow: { flex: 1, gap: 2 }
});
