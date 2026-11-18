import { useState } from 'react';
import { Alert, StyleSheet, View } from 'react-native';
import { useRouter } from 'expo-router';
import Constants from 'expo-constants';
import { api } from '@/api/client';
import { useAuth } from '@/auth/AuthProvider';
import {
  AppText,
  Badge,
  Button,
  Card,
  Divider,
  Header,
  Screen,
  SectionTitle,
  TextField
} from '@/components/ui';
import { LanguageToggle } from '@/components/LanguageToggle';
import { useI18n } from '@/i18n/I18nProvider';
import { usePushNotifications } from '@/notifications/NotificationsProvider';
import { useSelection } from '@/state/SelectionProvider';
import { roleKey } from '@/format';
import { colors, spacing } from '@/theme';

export default function SettingsScreen() {
  const { profile, apiBaseUrl, setApiBaseUrl, logout } = useAuth();
  const { tenant, installation, clearSelection } = useSelection();
  const { pushEnabled, pushError, requestPushRegistration } = usePushNotifications();
  const { t } = useI18n();
  const router = useRouter();
  const [apiUrl, setApiUrl] = useState(apiBaseUrl);
  const [savingUrl, setSavingUrl] = useState(false);
  const [urlNotice, setUrlNotice] = useState<string | null>(null);
  const [loggingOut, setLoggingOut] = useState(false);

  async function saveUrl() {
    if (!apiUrl.trim() || savingUrl) return;
    setSavingUrl(true);
    setUrlNotice(null);
    try {
      await setApiBaseUrl(apiUrl);
      setApiUrl(api.getBaseUrl());
      setUrlNotice(t('apiSaved'));
    } finally {
      setSavingUrl(false);
    }
  }

  function confirmLogout() {
    Alert.alert(t('signOut'), t('signOut') + '?', [
      { text: t('cancel'), style: 'cancel' },
      {
        text: t('signOut'),
        style: 'destructive',
        onPress: () => void performLogout()
      }
    ]);
  }

  async function performLogout() {
    setLoggingOut(true);
    try {
      await clearSelection();
      await logout();
      router.replace('/login');
    } finally {
      setLoggingOut(false);
    }
  }

  return (
    <Screen>
      <Header title={t('settingsTitle')} subtitle={profile?.email} />

      <SectionTitle>{t('account')}</SectionTitle>
      <Card>
        <View style={styles.row}>
          <View style={styles.grow}>
            <AppText size={12} color={colors.textMuted}>
              {t('signedInAs')}
            </AppText>
            <AppText weight="700">{profile?.displayName || profile?.email}</AppText>
            <AppText size={12} color={colors.textMuted}>
              {profile?.email}
            </AppText>
          </View>
        </View>
        <Divider />
        <AppText size={12} color={colors.textMuted}>
          {t('language')}
        </AppText>
        <LanguageToggle />
      </Card>

      <SectionTitle>{t('tenant')}</SectionTitle>
      <Card>
        <AppText weight="700">{tenant?.name ?? t('notSet')}</AppText>
        <AppText size={12} color={colors.textMuted}>
          {t('role')}: {tenant ? t(roleKey(tenant.role)) : t('notSet')}
        </AppText>
        <Button
          label={t('changeCompany')}
          variant="secondary"
          compact
          onPress={() => router.replace('/companies')}
        />
      </Card>

      <SectionTitle>{t('installation')}</SectionTitle>
      <Card>
        <View style={styles.row}>
          <View style={styles.grow}>
            <AppText weight="700">{installation?.name ?? t('notSet')}</AppText>
            <AppText size={12} color={colors.textMuted}>
              {installation?.machineName ?? t('notSet')}
            </AppText>
          </View>
          {installation ? (
            <Badge
              label={installation.isOnline ? t('online') : t('offline')}
              tone={installation.isOnline ? 'success' : 'neutral'}
            />
          ) : null}
        </View>
        {installation ? (
          <View style={styles.badges}>
            <Badge
              label={t('reportsAccess')}
              tone={installation.access.canViewReports ? 'success' : 'neutral'}
            />
            <Badge
              label={t('usersAccess')}
              tone={installation.access.canManageUsers ? 'primary' : 'neutral'}
            />
            <Badge
              label={t('inventoryAccess')}
              tone={installation.access.canAdjustInventory ? 'warning' : 'neutral'}
            />
          </View>
        ) : null}
        <Button
          label={t('changeInstallation')}
          variant="secondary"
          compact
          onPress={() => router.replace('/installations')}
        />
        <Button
          label={t('openHistory')}
          variant="ghost"
          compact
          onPress={() => router.push('/history')}
        />
      </Card>

      <SectionTitle>{t('notifications')}</SectionTitle>
      <Card>
        <View style={styles.row}>
          <AppText size={13} color={colors.textMuted}>
            {pushEnabled ? t('success') : t('notificationsOff')}
          </AppText>
          <Badge label={pushEnabled ? t('active') : t('inactive')} tone={pushEnabled ? 'success' : 'neutral'} />
        </View>
        {pushError ? (
          <AppText size={12} color={colors.textMuted}>
            {pushError.includes('EAS') ? t('pushNotConfigured') : pushError}
          </AppText>
        ) : null}
        <Button
          label={t('refresh')}
          variant="secondary"
          compact
          onPress={() => void requestPushRegistration()}
        />
      </Card>

      <SectionTitle>{t('advanced')}</SectionTitle>
      <Card>
        <TextField
          label={t('apiUrl')}
          value={apiUrl}
          onChangeText={setApiUrl}
          keyboardType="url"
        />
        <AppText size={12} color={colors.textMuted}>
          {t('apiUrlHint')}
        </AppText>
        {urlNotice ? (
          <AppText size={12} color={colors.success}>
            {urlNotice}
          </AppText>
        ) : null}
        <Button label={t('save')} onPress={() => void saveUrl()} loading={savingUrl} />
      </Card>

      <SectionTitle>{t('about')}</SectionTitle>
      <Card tone="muted">
        <AppText size={12} color={colors.textMuted}>
          {t('appName')}
        </AppText>
        <AppText>
          {t('version')}: {Constants.expoConfig?.version ?? '1.0.0'}
        </AppText>
      </Card>

      <Button label={t('signOut')} variant="danger" onPress={confirmLogout} loading={loggingOut} />
    </Screen>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: spacing.sm },
  grow: { flex: 1, gap: 2 },
  badges: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.xs, paddingVertical: spacing.xs }
});
