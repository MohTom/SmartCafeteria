import { useCallback, useEffect, useMemo, useState } from 'react';
import { Alert, StyleSheet, View } from 'react-native';
import type { DesktopUser } from '@/api/types';
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
  TextField
} from '@/components/ui';
import { useI18n } from '@/i18n/I18nProvider';
import { useSelection } from '@/state/SelectionProvider';
import { commandErrorMessage, isOfflineError, runCommand } from '@/remote/commands';
import { formatDateTime, roleKey } from '@/format';
import { colors, spacing } from '@/theme';

export default function UsersScreen() {
  const { t, language } = useI18n();
  const { tenant, installation, refreshInstallations } = useSelection();
  const [users, setUsers] = useState<DesktopUser[]>([]);
  const [query, setQuery] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const online = Boolean(installation?.isOnline);
  const allowed = Boolean(installation?.access.canManageUsers);

  const load = useCallback(async () => {
    if (!tenant || !installation) return;
    if (!installation.isOnline || !installation.access.canManageUsers) {
      setError(null);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const result = await runCommand<DesktopUser[]>({
        tenantId: tenant.id,
        installationId: installation.id,
        type: 'users.list',
        timeoutMs: 25_000
      });
      setUsers(result);
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
  }, [tenant, installation, refreshInstallations, t]);

  useEffect(() => {
    setUsers([]);
    void load();
  }, [installation?.id]); // eslint-disable-line react-hooks/exhaustive-deps

  const filtered = useMemo(() => {
    const needle = query.trim().toLowerCase();
    if (!needle) return users;
    return users.filter(
      (user) =>
        user.username.toLowerCase().includes(needle) ||
        user.displayName.toLowerCase().includes(needle) ||
        user.role.toLowerCase().includes(needle)
    );
  }, [users, query]);

  async function toggle(user: DesktopUser) {
    if (!tenant || !installation || busyId) return;
    if (!installation.isOnline) {
      setError(t('offlineCommand'));
      return;
    }

    const lock = user.isActive;
    Alert.alert(
      lock ? t('lock') : t('unlock'),
      lock ? t('lockConfirm') : t('unlockConfirm'),
      [
        { text: t('cancel'), style: 'cancel' },
        {
          text: lock ? t('lock') : t('unlock'),
          style: lock ? 'destructive' : 'default',
          onPress: () => void performToggle(user, lock)
        }
      ]
    );
  }

  async function performToggle(user: DesktopUser, lock: boolean) {
    if (!tenant || !installation) return;
    setBusyId(user.id);
    setError(null);
    try {
      const result = await runCommand<{ userId: string; isActive: boolean }>({
        tenantId: tenant.id,
        installationId: installation.id,
        type: lock ? 'users.lock' : 'users.unlock',
        payload: { userId: user.id },
        timeoutMs: 25_000
      });
      setUsers((current) =>
        current.map((item) => (item.id === user.id ? { ...item, isActive: result.isActive } : item))
      );
    } catch (caught) {
      if (isOfflineError(caught)) {
        setError(t('offlineCommand'));
        void refreshInstallations();
      } else {
        setError(commandErrorMessage(caught));
      }
    } finally {
      setBusyId(null);
    }
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
        <Header title={t('usersTitle')} />
        <EmptyState title={t('accessDenied')} hint={t('noAccess')} />
      </Screen>
    );
  }

  return (
    <Screen refreshing={loading} onRefresh={() => void load()}>
      <Header
        title={t('usersTitle')}
        subtitle={installation.name}
        action={
          <Badge
            label={online ? t('online') : t('offline')}
            tone={online ? 'success' : 'neutral'}
          />
        }
      />

      <TextField
        label={t('search')}
        value={query}
        onChangeText={setQuery}
        placeholder={t('username')}
      />

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

      {error ? <ErrorBanner message={error} onRetry={() => void load()} /> : null}
      {loading && users.length === 0 ? <ScreenLoading /> : null}
      {!loading && filtered.length === 0 && online ? <EmptyState title={t('noUsers')} /> : null}

      {filtered.map((user) => (
        <Card key={user.id}>
          <View style={styles.row}>
            <View style={styles.grow}>
              <AppText weight="700">{user.displayName}</AppText>
              <AppText size={12} color={colors.textMuted}>
                {user.username}
              </AppText>
            </View>
            <Badge
              label={user.isActive ? t('active') : t('inactive')}
              tone={user.isActive ? 'success' : 'danger'}
            />
          </View>

          <View style={styles.row}>
            <Badge label={t(roleKey(user.role))} tone="neutral" />
            <AppText size={12} color={colors.textMuted}>
              {t('createdAt')}: {formatDateTime(user.createdAt, language)}
            </AppText>
          </View>

          <Button
            label={user.isActive ? t('lock') : t('unlock')}
            variant={user.isActive ? 'danger' : 'secondary'}
            compact
            loading={busyId === user.id}
            disabled={!online || Boolean(busyId)}
            onPress={() => void toggle(user)}
          />
        </Card>
      ))}
    </Screen>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: spacing.sm },
  grow: { flex: 1, gap: 2 }
});
