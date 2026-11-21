import { useState } from 'react';
import { StyleSheet, View } from 'react-native';
import { api, ApiError } from '@/api/client';
import type { ClaimCode } from '@/api/types';
import { AppText, Button, Card, ErrorBanner } from '@/components/ui';
import { useI18n } from '@/i18n/I18nProvider';
import { useSelection } from '@/state/SelectionProvider';
import { formatDateTime } from '@/format';
import { colors, radius, spacing } from '@/theme';

export function LinkDesktopCard({ compact = false }: { compact?: boolean }) {
  const { t, language } = useI18n();
  const { tenant } = useSelection();
  const [claim, setClaim] = useState<ClaimCode | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function generate() {
    if (!tenant || busy) return;
    setBusy(true);
    setError(null);
    try {
      const result = await api.request<ClaimCode>(
        `/api/tenants/${tenant.id}/installations/claim-codes`,
        { method: 'POST', body: JSON.stringify({}) }
      );
      setClaim(result);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : t('error'));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Card tone={compact ? 'surface' : 'muted'}>
      <AppText weight="700">{t('linkDesktop')}</AppText>
      <AppText size={13} color={colors.textMuted}>
        {t('generateCodeHint')}
      </AppText>

      {claim ? (
        <View style={styles.codeBox} testID="linkDesktop.claimCode">
          <AppText size={30} weight="700" align="center" selectable style={styles.code}>
            {claim.code}
          </AppText>
          <AppText size={12} color={colors.textMuted} align="center">
            {t('expires')}: {formatDateTime(claim.expiresAt, language)}
          </AppText>
        </View>
      ) : null}

      {error ? <ErrorBanner message={error} /> : null}

      <Button
        label={t('generateClaimCode')}
        onPress={generate}
        loading={busy}
        variant={claim ? 'secondary' : 'primary'}
        testID="linkDesktop.generateCode"
      />
    </Card>
  );
}

const styles = StyleSheet.create({
  codeBox: {
    backgroundColor: colors.primarySoft,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.sm,
    gap: spacing.xs
  },
  code: { letterSpacing: 6, color: colors.primaryDark }
});
