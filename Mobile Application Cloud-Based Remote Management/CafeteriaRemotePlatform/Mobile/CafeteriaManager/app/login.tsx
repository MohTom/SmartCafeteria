import { useState } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  Pressable,
  StyleSheet,
  View
} from 'react-native';
import { Link, Redirect, useRouter } from 'expo-router';
import { ApiError } from '@/api/client';
import { useAuth } from '@/auth/AuthProvider';
import { AppText, Button, Card, Screen, TextField } from '@/components/ui';
import { LanguageToggle } from '@/components/LanguageToggle';
import { useI18n } from '@/i18n/I18nProvider';
import { colors, spacing } from '@/theme';

export default function LoginScreen() {
  const { status, login, apiBaseUrl, setApiBaseUrl } = useAuth();
  const { t } = useI18n();
  const router = useRouter();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [apiUrl, setApiUrl] = useState(apiBaseUrl);
  const [advanced, setAdvanced] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (status === 'signedIn') return <Redirect href="/" />;

  async function submit() {
    if (busy) return;
    if (!email.trim() || !password) {
      setError(t('invalidCredentials'));
      return;
    }

    setBusy(true);
    setError(null);
    try {
      if (apiUrl.trim() && apiUrl.trim() !== apiBaseUrl) {
        await setApiBaseUrl(apiUrl);
      }
      await login(email.trim(), password);
      router.replace('/');
    } catch (caught) {
      if (caught instanceof ApiError) {
        setError(
          caught.status === 401 || caught.code === 'invalid_credentials'
            ? t('invalidCredentials')
            : caught.message
        );
      } else {
        setError(t('networkError'));
      }
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
            {t('loginTitle')}
          </AppText>
          <AppText color={colors.textMuted}>{t('loginSubtitle')}</AppText>
        </View>

        <Card>
          <TextField
            label={t('email')}
            value={email}
            onChangeText={setEmail}
            placeholder="manager@company.com"
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

          <Button label={t('signIn')} onPress={submit} loading={busy} />

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
            {t('noAccount')}
          </AppText>
          <Link href="/register" asChild>
            <Pressable>
              <AppText size={13} weight="700" color={colors.primary}>
                {t('createAccount')}
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
