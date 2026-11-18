import { useState } from 'react';
import { KeyboardAvoidingView, Platform, Pressable, StyleSheet, View } from 'react-native';
import { Link, Redirect, useRouter } from 'expo-router';
import { ApiError } from '@/api/client';
import { useAuth } from '@/auth/AuthProvider';
import { AppText, Button, Card, Screen, TextField } from '@/components/ui';
import { LanguageToggle } from '@/components/LanguageToggle';
import { useI18n } from '@/i18n/I18nProvider';
import { colors, spacing } from '@/theme';

export default function RegisterScreen() {
  const { status, register, apiBaseUrl, setApiBaseUrl } = useAuth();
  const { t } = useI18n();
  const router = useRouter();
  const [companyName, setCompanyName] = useState('');
  const [ownerDisplayName, setOwnerDisplayName] = useState('');
  const [ownerEmail, setOwnerEmail] = useState('');
  const [password, setPassword] = useState('');
  const [apiUrl, setApiUrl] = useState(apiBaseUrl);
  const [advanced, setAdvanced] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (status === 'signedIn') return <Redirect href="/" />;

  async function submit() {
    if (busy) return;
    if (!companyName.trim() || !ownerDisplayName.trim() || !ownerEmail.trim() || password.length < 8) {
      setError(password.length < 8 ? 'Password must be at least 8 characters.' : t('error'));
      return;
    }

    setBusy(true);
    setError(null);
    try {
      if (apiUrl.trim() && apiUrl.trim() !== apiBaseUrl) {
        await setApiBaseUrl(apiUrl);
      }
      await register({
        companyName: companyName.trim(),
        ownerDisplayName: ownerDisplayName.trim(),
        ownerEmail: ownerEmail.trim(),
        password
      });
      router.replace('/');
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : t('networkError')
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <KeyboardAvoidingView
      style={styles.flex}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <Screen>
        <View style={styles.top}>
          <LanguageToggle />
        </View>

        <View style={styles.hero}>
          <AppText size={13} weight="700" color={colors.accent}>
            {t('appName')}
          </AppText>
          <AppText size={30} weight="700">
            {t('registerTitle')}
          </AppText>
          <AppText color={colors.textMuted}>{t('registerSubtitle')}</AppText>
        </View>

        <Card>
          <TextField
            label={t('companyName')}
            value={companyName}
            onChangeText={setCompanyName}
            autoCapitalize="words"
          />
          <TextField
            label={t('displayName')}
            value={ownerDisplayName}
            onChangeText={setOwnerDisplayName}
            autoCapitalize="words"
          />
          <TextField
            label={t('email')}
            value={ownerEmail}
            onChangeText={setOwnerEmail}
            keyboardType="email-address"
          />
          <TextField
            label={t('password')}
            value={password}
            onChangeText={setPassword}
            secureTextEntry
          />

          {error ? (
            <AppText size={13} color={colors.danger}>
              {error}
            </AppText>
          ) : null}

          <Button label={t('createAccount')} onPress={submit} loading={busy} />

          <Pressable onPress={() => setAdvanced((value) => !value)}>
            <AppText size={13} color={colors.textMuted} weight="600">
              {t('advanced')}
            </AppText>
          </Pressable>

          {advanced ? (
            <View style={styles.advanced}>
              <TextField
                label={t('apiUrl')}
                value={apiUrl}
                onChangeText={setApiUrl}
                placeholder="https://cloud.example.com"
                keyboardType="url"
              />
              <AppText size={12} color={colors.textMuted}>
                {t('apiUrlHint')}
              </AppText>
            </View>
          ) : null}
        </Card>

        <View style={styles.footer}>
          <AppText size={13} color={colors.textMuted}>
            {t('haveAccount')}
          </AppText>
          <Link href="/login" asChild>
            <Pressable>
              <AppText size={13} weight="700" color={colors.primary}>
                {t('signIn')}
              </AppText>
            </Pressable>
          </Link>
        </View>
      </Screen>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  top: { alignItems: 'flex-start' },
  hero: { gap: spacing.sm, paddingVertical: spacing.lg },
  advanced: { gap: spacing.xs, paddingTop: spacing.sm },
  footer: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: spacing.xs }
});
