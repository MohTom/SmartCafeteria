import { useCallback, useEffect, useState } from 'react';
import { Pressable, StyleSheet, View } from 'react-native';
import { Redirect, useRouter } from 'expo-router';
import { api, ApiError } from '@/api/client';
import type { InvitationCreated, TenantMember } from '@/api/types';
import { useAuth } from '@/auth/AuthProvider';
import {
  AppText,
  Badge,
  Button,
  Card,
  Divider,
  EmptyState,
  ErrorBanner,
  Header,
  Screen,
  ScreenLoading,
  SectionTitle,
  SelectRow,
  TextField
} from '@/components/ui';
import { LinkDesktopCard } from '@/components/LinkDesktopCard';
import { useI18n } from '@/i18n/I18nProvider';
import { useSelection } from '@/state/SelectionProvider';
import { formatDateTime, roleKey } from '@/format';
import { colors, spacing } from '@/theme';

export default function CompaniesScreen() {
  const { status, profile, refreshProfile, acceptInvitation } = useAuth();
  const { tenant, selectTenant, ready } = useSelection();
  const canInvite = tenant ? ['owner', 'admin'].includes(tenant.role.toLowerCase()) : false;
  const { t, language } = useI18n();
  const router = useRouter();

  const [inviteCode, setInviteCode] = useState('');
  const [joining, setJoining] = useState(false);
  const [joinError, setJoinError] = useState<string | null>(null);
  const [members, setMembers] = useState<TenantMember[]>([]);
  const [loadingMembers, setLoadingMembers] = useState(false);
  const [membersError, setMembersError] = useState<string | null>(null);
  const [inviteEmail, setInviteEmail] = useState('');
  const [inviteRole, setInviteRole] = useState<'Manager' | 'Viewer'>('Manager');
  const [inviting, setInviting] = useState(false);
  const [invitation, setInvitation] = useState<InvitationCreated | null>(null);
  const [inviteError, setInviteError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const loadMembers = useCallback(async () => {
    if (!tenant) {
      setMembers([]);
      return;
    }
    setLoadingMembers(true);
    setMembersError(null);
    try {
      const rows = await api.request<TenantMember[]>(`/api/tenants/${tenant.id}/members`);
      setMembers(rows);
    } catch (caught) {
      setMembersError(caught instanceof ApiError ? caught.message : t('error'));
    } finally {
      setLoadingMembers(false);
    }
  }, [tenant, t]);

  useEffect(() => {
    void loadMembers();
  }, [loadMembers]);

  if (status === 'signedOut') return <Redirect href="/login" />;
  if (status === 'loading' || !ready) return <ScreenLoading />;
  if (!profile) return <ScreenLoading />;

  async function chooseCompany(id: string) {
    await selectTenant(id);
    router.replace('/installations');
  }

  async function join() {
    if (!inviteCode.trim() || joining) return;
    setJoining(true);
    setJoinError(null);
    try {
      const joined = await acceptInvitation(inviteCode.trim());
      await refreshProfile();
      await selectTenant(joined.id);
      setInviteCode('');
      router.replace('/installations');
    } catch (caught) {
      setJoinError(caught instanceof ApiError ? caught.message : t('networkError'));
    } finally {
      setJoining(false);
    }
  }

  async function invite() {
    if (!tenant || !inviteEmail.trim() || inviting) return;
    setInviting(true);
    setInviteError(null);
    setInvitation(null);
    try {
      const created = await api.request<InvitationCreated>(
        `/api/tenants/${tenant.id}/invitations`,
        {
          method: 'POST',
          body: JSON.stringify({ email: inviteEmail.trim(), role: inviteRole })
        }
      );
      setInvitation(created);
      setInviteEmail('');
    } catch (caught) {
      setInviteError(caught instanceof ApiError ? caught.message : t('error'));
    } finally {
      setInviting(false);
    }
  }

  async function refresh() {
    setRefreshing(true);
    try {
      await Promise.all([refreshProfile(), loadMembers()]);
    } finally {
      setRefreshing(false);
    }
  }

  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      <Header
        title={t('companiesTitle')}
        subtitle={t('companiesSubtitle')}
        action={
          <Button
            compact
            variant="secondary"
            label={t('installations')}
            onPress={() => router.replace('/installations')}
          />
        }
      />

      <SectionTitle>{t('companies')}</SectionTitle>
      {profile.tenants.length === 0 ? (
        <EmptyState title={t('noCompanies')} hint={t('noCompaniesHint')} />
      ) : (
        profile.tenants.map((item) => (
          <SelectRow
            key={item.id}
            title={item.name}
            subtitle={`${t('role')}: ${t(roleKey(item.role))}`}
            selected={tenant?.id === item.id}
            onPress={() => void chooseCompany(item.id)}
            trailing={<Badge label={t(roleKey(item.role))} tone="primary" />}
          />
        ))
      )}

      <SectionTitle>{t('joinCompany')}</SectionTitle>
      <Card>
        <TextField
          label={t('inviteCode')}
          value={inviteCode}
          onChangeText={setInviteCode}
          placeholder="INV-XXXXXX"
          autoCapitalize="characters"
        />
        {joinError ? <ErrorBanner message={joinError} /> : null}
        <Button
          label={t('acceptInvitation')}
          onPress={join}
          loading={joining}
          disabled={!inviteCode.trim()}
        />
      </Card>

      {tenant ? (
        <>
          <SectionTitle>{tenant.name}</SectionTitle>
          <Card>
            <View style={styles.row}>
              <View style={styles.grow}>
                <AppText weight="700">{tenant.name}</AppText>
                <AppText size={12} color={colors.textMuted}>
                  {tenant.slug}
                </AppText>
              </View>
              <Badge label={t(roleKey(tenant.role))} tone="primary" />
            </View>
            <Divider />
            <AppText size={12} color={colors.textMuted}>
              {members.length} {t('membersCount')}
            </AppText>
          </Card>

          <SectionTitle>{t('members')}</SectionTitle>
          {membersError ? <ErrorBanner message={membersError} onRetry={() => void loadMembers()} /> : null}
          {loadingMembers ? <ScreenLoading /> : null}
          {!loadingMembers && members.length === 0 ? <EmptyState title={t('noMembers')} /> : null}
          {members.map((member) => (
            <Card key={member.userId}>
              <View style={styles.row}>
                <View style={styles.grow}>
                  <AppText weight="600">{member.displayName}</AppText>
                  <AppText size={12} color={colors.textMuted}>
                    {member.email}
                  </AppText>
                </View>
                <Badge label={t(roleKey(member.role))} tone="neutral" />
              </View>
              <View style={styles.row}>
                <Badge
                  label={member.isActive ? t('active') : t('inactive')}
                  tone={member.isActive ? 'success' : 'danger'}
                />
                <AppText size={12} color={colors.textMuted}>
                  {formatDateTime(member.createdAt, language)}
                </AppText>
              </View>
            </Card>
          ))}

          {canInvite ? (
            <>
          <SectionTitle>{t('inviteMember')}</SectionTitle>
          <Card>
            <TextField
              label={t('inviteEmail')}
              value={inviteEmail}
              onChangeText={setInviteEmail}
              keyboardType="email-address"
            />
            <View style={styles.roleRow}>
              {(['Manager', 'Viewer'] as const).map((role) => (
                <Pressable
                  key={role}
                  onPress={() => setInviteRole(role)}
                  style={[styles.roleChip, inviteRole === role && styles.roleChipActive]}
                >
                  <AppText
                    size={12}
                    weight="700"
                    color={inviteRole === role ? colors.surface : colors.textMuted}
                  >
                    {t(role === 'Manager' ? 'manager' : 'viewer')}
                  </AppText>
                </Pressable>
              ))}
            </View>
            <AppText size={12} color={colors.textMuted}>
              {t('inviteRoleHint')}
            </AppText>
            {inviteError ? <ErrorBanner message={inviteError} /> : null}
            {invitation ? (
              <View style={styles.inviteBox}>
                <AppText weight="600">{t('invitationCreated')}</AppText>
                <AppText size={24} weight="700" align="center" selectable style={styles.code}>
                  {invitation.inviteCode}
                </AppText>
                <AppText size={12} color={colors.textMuted} align="center">
                  {t('inviteCreatedHint')}
                </AppText>
                <AppText size={12} color={colors.textMuted} align="center">
                  {t('expires')}: {formatDateTime(invitation.expiresAt, language)}
                </AppText>
              </View>
            ) : null}
            <Button
              label={t('sendInvite')}
              onPress={invite}
              loading={inviting}
              disabled={!inviteEmail.trim()}
            />
          </Card>
            </>
          ) : null}

          <LinkDesktopCard />
        </>
      ) : null}
    </Screen>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: spacing.sm },
  grow: { flex: 1, gap: 2 },
  roleRow: { flexDirection: 'row', gap: spacing.sm },
  roleChip: {
    paddingVertical: spacing.xs,
    paddingHorizontal: spacing.md,
    borderRadius: 999,
    backgroundColor: colors.surfaceMuted
  },
  roleChipActive: { backgroundColor: colors.primary },
  inviteBox: {
    backgroundColor: colors.primarySoft,
    borderRadius: 12,
    padding: spacing.md,
    gap: spacing.xs
  },
  code: { letterSpacing: 3, color: colors.primaryDark }
});
