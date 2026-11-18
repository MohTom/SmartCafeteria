import { useCallback, useEffect, useState } from 'react';
import { Pressable, StyleSheet, View } from 'react-native';
import { api, ApiError } from '@/api/client';
import type { NotificationItem, UnreadCount } from '@/api/types';
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
import { useI18n } from '@/i18n/I18nProvider';
import { useSelection } from '@/state/SelectionProvider';
import { useRealtime } from '@/realtime/RealtimeProvider';
import { formatDateTime } from '@/format';
import { colors, radius, spacing } from '@/theme';

export default function NotificationsScreen() {
  const { t, language } = useI18n();
  const { tenant } = useSelection();
  const { notificationVersion } = useRealtime();
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [unread, setUnread] = useState(0);
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [markingAll, setMarkingAll] = useState(false);

  const load = useCallback(async () => {
    if (!tenant) return;
    setLoading(true);
    setError(null);
    try {
      const [rows, count] = await Promise.all([
        api.request<NotificationItem[]>(
          `/api/tenants/${tenant.id}/notifications?unreadOnly=${unreadOnly}`
        ),
        api.request<UnreadCount>(`/api/tenants/${tenant.id}/notifications/unread-count`)
      ]);
      setItems(rows);
      setUnread(count.count);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : t('networkError'));
    } finally {
      setLoading(false);
    }
  }, [tenant, unreadOnly, t]);

  useEffect(() => {
    void load();
  }, [load, notificationVersion]);

  async function markRead(item: NotificationItem) {
    if (!tenant || item.isRead || busyId) return;
    setBusyId(item.id);
    try {
      const updated = await api.request<NotificationItem>(
        `/api/tenants/${tenant.id}/notifications/${item.id}/read`,
        { method: 'POST', body: JSON.stringify({}) }
      );
      setItems((current) =>
        unreadOnly ? current.filter((row) => row.id !== item.id) : current.map((row) => (row.id === item.id ? updated : row))
      );
      setUnread((value) => Math.max(0, value - 1));
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : t('error'));
    } finally {
      setBusyId(null);
    }
  }

  async function markAll() {
    if (!tenant || markingAll || unread === 0) return;
    setMarkingAll(true);
    setError(null);
    try {
      const unreadItems = items.filter((item) => !item.isRead);
      const source = unreadItems.length > 0 ? unreadItems : await api.request<NotificationItem[]>(
        `/api/tenants/${tenant.id}/notifications?unreadOnly=true`
      );
      await Promise.all(
        source.map((item) =>
          api.request<NotificationItem>(
            `/api/tenants/${tenant.id}/notifications/${item.id}/read`,
            { method: 'POST', body: JSON.stringify({}) }
          )
        )
      );
      setItems((current) =>
        unreadOnly ? [] : current.map((item) => ({ ...item, isRead: true }))
      );
      setUnread(0);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : t('error'));
    } finally {
      setMarkingAll(false);
    }
  }

  if (!tenant) {
    return (
      <Screen>
        <EmptyState title={t('selectCompanyFirst')} hint={t('noCompaniesHint')} />
      </Screen>
    );
  }

  return (
    <Screen refreshing={loading} onRefresh={() => void load()}>
      <Header
        title={t('notificationsTitle')}
        subtitle={`${unread} ${t('unread')}`}
        action={
          <Button
            compact
            variant="secondary"
            label={t('markAll')}
            onPress={markAll}
            loading={markingAll}
            disabled={unread === 0}
          />
        }
      />

      <View style={styles.filters}>
        {[false, true].map((value) => {
          const active = unreadOnly === value;
          return (
            <Pressable
              key={String(value)}
              onPress={() => setUnreadOnly(value)}
              style={[styles.chip, active && styles.chipActive]}
            >
              <AppText
                size={12}
                weight="700"
                color={active ? colors.surface : colors.textMuted}
              >
                {value ? t('unread') : t('all')}
              </AppText>
            </Pressable>
          );
        })}
      </View>

      {error ? <ErrorBanner message={error} onRetry={() => void load()} /> : null}
      {loading && items.length === 0 ? <ScreenLoading /> : null}
      {!loading && items.length === 0 ? <EmptyState title={t('emptyNotifications')} /> : null}

      {items.map((item) => (
        <Card key={item.id} tone={item.isRead ? 'surface' : 'muted'}>
          <View style={styles.row}>
            <View style={styles.grow}>
              <AppText weight={item.isRead ? '600' : '700'}>{item.title}</AppText>
              <AppText size={13} color={colors.textMuted}>
                {item.body}
              </AppText>
            </View>
            {!item.isRead ? <Badge label={t('unread')} tone="warning" /> : null}
          </View>
          <View style={styles.row}>
            <AppText size={11} color={colors.textMuted}>
              {formatDateTime(item.createdAt, language)}
            </AppText>
            {!item.isRead ? (
              <Button
                label={t('markRead')}
                variant="ghost"
                compact
                loading={busyId === item.id}
                onPress={() => void markRead(item)}
              />
            ) : null}
          </View>
        </Card>
      ))}
    </Screen>
  );
}

const styles = StyleSheet.create({
  filters: { flexDirection: 'row', gap: spacing.xs },
  chip: {
    paddingVertical: 6,
    paddingHorizontal: spacing.md,
    borderRadius: radius.pill,
    backgroundColor: colors.surfaceMuted
  },
  chipActive: { backgroundColor: colors.primary },
  row: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: spacing.sm },
  grow: { flex: 1, gap: 2 }
});
