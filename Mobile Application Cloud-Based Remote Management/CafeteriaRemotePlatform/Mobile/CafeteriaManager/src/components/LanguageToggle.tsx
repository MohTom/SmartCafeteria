import { Pressable, StyleSheet, View } from 'react-native';
import { AppText } from '@/components/ui';
import { useI18n } from '@/i18n/I18nProvider';
import { colors, radius, spacing } from '@/theme';

export function LanguageToggle() {
  const { language, setLanguage, t } = useI18n();
  return (
    <View style={styles.container}>
      {(['ar', 'en'] as const).map((code) => {
        const active = language === code;
        return (
          <Pressable
            key={code}
            onPress={() => void setLanguage(code)}
            style={[styles.option, active && styles.active]}
            accessibilityRole="button"
          >
            <AppText size={12} weight="700" color={active ? colors.surface : colors.textMuted} align="center">
              {code === 'ar' ? t('arabic') : t('english')}
            </AppText>
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    alignSelf: 'flex-start',
    backgroundColor: colors.surfaceMuted,
    borderRadius: radius.pill,
    padding: 3,
    gap: 2
  },
  option: { paddingVertical: 5, paddingHorizontal: spacing.md, borderRadius: radius.pill },
  active: { backgroundColor: colors.primary }
});
