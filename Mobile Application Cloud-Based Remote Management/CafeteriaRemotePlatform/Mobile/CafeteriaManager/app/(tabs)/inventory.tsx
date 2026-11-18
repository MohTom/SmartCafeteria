import { useCallback, useEffect, useMemo, useState } from 'react';
import { Modal, Pressable, StyleSheet, View } from 'react-native';
import type { DesktopProduct } from '@/api/types';
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
import { commandErrorMessage, createCommand, isOfflineError, runCommand } from '@/remote/commands';
import { formatNumber } from '@/format';
import { colors, radius, spacing } from '@/theme';

type Mode = 'increase' | 'decrease';

export default function InventoryScreen() {
  const { t, language } = useI18n();
  const { tenant, installation, refreshInstallations } = useSelection();
  const [products, setProducts] = useState<DesktopProduct[]>([]);
  const [query, setQuery] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [selected, setSelected] = useState<DesktopProduct | null>(null);
  const [amount, setAmount] = useState('');
  const [mode, setMode] = useState<Mode>('increase');
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const online = Boolean(installation?.isOnline);
  const allowed = Boolean(installation?.access.canAdjustInventory);

  const load = useCallback(async () => {
    if (!tenant || !installation) return;
    if (!installation.isOnline || !installation.access.canAdjustInventory) {
      setError(null);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const result = await runCommand<DesktopProduct[]>({
        tenantId: tenant.id,
        installationId: installation.id,
        type: 'inventory.list',
        timeoutMs: 25_000
      });
      setProducts(result);
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
    setProducts([]);
    void load();
  }, [installation?.id]); // eslint-disable-line react-hooks/exhaustive-deps

  const filtered = useMemo(() => {
    const needle = query.trim().toLowerCase();
    if (!needle) return products;
    return products.filter(
      (product) =>
        product.nameEn.toLowerCase().includes(needle) ||
        product.nameAr.toLowerCase().includes(needle) ||
        product.category.toLowerCase().includes(needle)
    );
  }, [products, query]);

  function open(product: DesktopProduct) {
    setSelected(product);
    setAmount('');
    setMode('increase');
    setFormError(null);
    setNotice(null);
  }

  async function submit() {
    if (!tenant || !installation || !selected || saving) return;
    const parsed = Number(amount);
    if (!Number.isInteger(parsed) || parsed <= 0) {
      setFormError(t('invalidAmount'));
      return;
    }

    setSaving(true);
    setFormError(null);
    const delta = mode === 'increase' ? parsed : -parsed;

    try {
      if (installation.isOnline) {
        const result = await runCommand<{ inventory: number; previousInventory: number }>({
          tenantId: tenant.id,
          installationId: installation.id,
          type: 'inventory.adjust',
          payload: { productId: selected.id, delta },
          timeoutMs: 25_000
        });
        setProducts((current) =>
          current.map((item) =>
            item.id === selected.id ? { ...item, inventory: result.inventory } : item
          )
        );
        setNotice(t('adjustSuccess'));
      } else {
        await createCommand({
          tenantId: tenant.id,
          installationId: installation.id,
          type: 'inventory.adjust',
          payload: { productId: selected.id, delta }
        });
        setNotice(t('adjustQueued'));
      }
      setSelected(null);
      setAmount('');
    } catch (caught) {
      if (isOfflineError(caught)) {
        setFormError(t('offlineCommand'));
        void refreshInstallations();
      } else {
        setFormError(commandErrorMessage(caught));
      }
    } finally {
      setSaving(false);
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
        <Header title={t('inventoryTitle')} />
        <EmptyState title={t('accessDenied')} hint={t('noAccess')} />
      </Screen>
    );
  }

  return (
    <>
      <Screen refreshing={loading} onRefresh={() => void load()}>
        <Header
          title={t('inventoryTitle')}
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
          placeholder={t('product')}
        />

        {!online ? (
          <Card tone="muted">
            <AppText weight="600">{t('queuedOffline')}</AppText>
            <AppText size={13} color={colors.textMuted}>
              {t('adjustQueued')}
            </AppText>
          </Card>
        ) : null}

        {notice ? (
          <Card tone="muted">
            <AppText size={13} color={colors.success}>
              {notice}
            </AppText>
          </Card>
        ) : null}

        {error ? <ErrorBanner message={error} onRetry={() => void load()} /> : null}
        {loading && products.length === 0 ? <ScreenLoading /> : null}

        {!loading && filtered.length === 0 ? (
          <EmptyState title={t('noProducts')} hint={online ? undefined : t('queuedOffline')} />
        ) : null}

        {filtered.map((product) => (
          <Card key={product.id}>
            <View style={styles.row}>
              <View style={styles.grow}>
                <AppText weight="700">{language === 'ar' ? product.nameAr : product.nameEn}</AppText>
                <AppText size={12} color={colors.textMuted}>
                  {product.category || '—'}
                </AppText>
              </View>
              <Badge
                label={`${t('inStock')}: ${formatNumber(product.inventory, language)}`}
                tone={product.lowStock ? 'danger' : 'success'}
              />
            </View>
            <Button label={t('adjust')} variant="secondary" compact onPress={() => open(product)} />
          </Card>
        ))}
      </Screen>

      <Modal
        visible={Boolean(selected)}
        transparent
        animationType="fade"
        onRequestClose={() => setSelected(null)}
      >
        <Pressable style={styles.backdrop} onPress={() => setSelected(null)}>
          <Pressable style={styles.modal} onPress={() => undefined}>
            <AppText size={20} weight="700">
              {t('adjustInventoryTitle')}
            </AppText>
            {selected ? (
              <AppText size={13} color={colors.textMuted}>
                {language === 'ar' ? selected.nameAr : selected.nameEn} · {t('inStock')}:{' '}
                {formatNumber(selected.inventory, language)}
              </AppText>
            ) : null}

            <View style={styles.modeRow}>
              {(['increase', 'decrease'] as const).map((item) => (
                <Pressable
                  key={item}
                  onPress={() => setMode(item)}
                  style={[styles.mode, mode === item && styles.modeActive]}
                >
                  <AppText
                    size={13}
                    weight="700"
                    color={mode === item ? colors.surface : colors.textMuted}
                  >
                    {t(item)}
                  </AppText>
                </Pressable>
              ))}
            </View>

            <TextField
              label={t('amount')}
              value={amount}
              onChangeText={setAmount}
              keyboardType="number-pad"
              placeholder="0"
            />
            <AppText size={12} color={colors.textMuted}>
              {selected && !online ? t('adjustQueued') : t('amountHint')}
            </AppText>
            {formError ? <ErrorBanner message={formError} /> : null}

            <View style={styles.modalActions}>
              <Button
                label={t('cancel')}
                variant="ghost"
                onPress={() => setSelected(null)}
              />
              <Button label={t('save')} onPress={() => void submit()} loading={saving} />
            </View>
          </Pressable>
        </Pressable>
      </Modal>
    </>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: spacing.sm },
  grow: { flex: 1, gap: 2 },
  backdrop: {
    flex: 1,
    backgroundColor: 'rgba(10, 30, 20, 0.45)',
    justifyContent: 'center',
    padding: spacing.xl
  },
  modal: {
    backgroundColor: colors.surface,
    borderRadius: radius.lg,
    padding: spacing.lg,
    gap: spacing.sm
  },
  modeRow: { flexDirection: 'row', gap: spacing.sm },
  mode: {
    flex: 1,
    alignItems: 'center',
    paddingVertical: spacing.sm,
    borderRadius: radius.md,
    backgroundColor: colors.surfaceMuted
  },
  modeActive: { backgroundColor: colors.primary },
  modalActions: { flexDirection: 'row', justifyContent: 'flex-end', gap: spacing.sm }
});
