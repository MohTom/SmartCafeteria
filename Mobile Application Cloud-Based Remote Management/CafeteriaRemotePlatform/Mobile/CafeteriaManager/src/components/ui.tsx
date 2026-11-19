import type { ReactNode } from 'react';
import {
  ActivityIndicator,
  Pressable,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
  type KeyboardTypeOptions,
  type StyleProp,
  type TextStyle,
  type ViewStyle
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { colors, radius, spacing, typography } from '@/theme';
import { useI18n } from '@/i18n/I18nProvider';

type ScreenProps = {
  children: ReactNode;
  scroll?: boolean;
  refreshing?: boolean;
  onRefresh?: () => void;
  contentStyle?: StyleProp<ViewStyle>;
};

export function Screen({ children, scroll = true, refreshing, onRefresh, contentStyle }: ScreenProps) {
  const body = scroll ? (
    <ScrollView
      contentContainerStyle={[styles.scrollContent, contentStyle]}
      keyboardShouldPersistTaps="handled"
      refreshControl={
        onRefresh ? (
          <RefreshControl
            refreshing={Boolean(refreshing)}
            onRefresh={onRefresh}
            tintColor={colors.primary}
            colors={[colors.primary]}
          />
        ) : undefined
      }
    >
      {children}
    </ScrollView>
  ) : (
    <View style={[styles.fill, contentStyle]}>{children}</View>
  );

  return (
    <SafeAreaView edges={['top', 'left', 'right', 'bottom']} style={styles.safe}>
      {body}
    </SafeAreaView>
  );
}

export function AppText({
  children,
  size = typography.body,
  weight = '400',
  color = colors.text,
  align,
  style,
  numberOfLines,
  selectable
}: {
  children: ReactNode;
  size?: number;
  weight?: TextStyle['fontWeight'];
  color?: string;
  align?: 'left' | 'right' | 'center';
  style?: StyleProp<TextStyle>;
  numberOfLines?: number;
  selectable?: boolean;
}) {
  const { textAlign } = useI18n();
  return (
    <Text
      numberOfLines={numberOfLines}
      selectable={selectable}
      style={[{ fontSize: size, fontWeight: weight, color, textAlign: align ?? textAlign }, style]}
    >
      {children}
    </Text>
  );
}

export function Header({
  title,
  subtitle,
  action
}: {
  title: string;
  subtitle?: string;
  action?: ReactNode;
}) {
  const { rowDirection } = useI18n();
  return (
    <View style={[styles.header, { flexDirection: rowDirection }]}>
      <View style={styles.headerText}>
        <AppText size={typography.title} weight="700">
          {title}
        </AppText>
        {subtitle ? (
          <AppText size={typography.small} color={colors.textMuted} style={styles.headerSubtitle}>
            {subtitle}
          </AppText>
        ) : null}
      </View>
      {action}
    </View>
  );
}

export function Card({
  children,
  style,
  tone = 'surface'
}: {
  children: ReactNode;
  style?: StyleProp<ViewStyle>;
  tone?: 'surface' | 'muted' | 'primary';
}) {
  return (
    <View
      style={[
        styles.card,
        tone === 'muted' && styles.cardMuted,
        tone === 'primary' && styles.cardPrimary,
        style
      ]}
    >
      {children}
    </View>
  );
}

export function Button({
  label,
  onPress,
  variant = 'primary',
  loading = false,
  disabled = false,
  compact = false,
  testID
}: {
  label: string;
  onPress: () => void;
  variant?: 'primary' | 'secondary' | 'danger' | 'ghost';
  loading?: boolean;
  disabled?: boolean;
  compact?: boolean;
  testID?: string;
}) {
  const isDisabled = disabled || loading;
  return (
    <Pressable
      testID={testID}
      accessibilityRole="button"
      onPress={onPress}
      disabled={isDisabled}
      style={({ pressed }) => [
        styles.button,
        compact && styles.buttonCompact,
        variant === 'primary' && styles.buttonPrimary,
        variant === 'secondary' && styles.buttonSecondary,
        variant === 'danger' && styles.buttonDanger,
        variant === 'ghost' && styles.buttonGhost,
        isDisabled && styles.buttonDisabled,
        pressed && !isDisabled && styles.buttonPressed
      ]}
    >
      {loading ? (
        <ActivityIndicator color={variant === 'primary' ? '#FFFFFF' : colors.primary} />
      ) : (
        <Text
          style={[
            styles.buttonLabel,
            (variant === 'primary' || variant === 'danger') && styles.buttonLabelLight,
            variant === 'secondary' && styles.buttonLabelPrimary,
            variant === 'ghost' && styles.buttonLabelMuted
          ]}
        >
          {label}
        </Text>
      )}
    </Pressable>
  );
}

export function TextField({
  label,
  value,
  onChangeText,
  placeholder,
  secureTextEntry,
  keyboardType,
  autoCapitalize = 'none',
  multiline = false,
  editable = true,
  testID
}: {
  label?: string;
  value: string;
  onChangeText: (value: string) => void;
  placeholder?: string;
  secureTextEntry?: boolean;
  keyboardType?: KeyboardTypeOptions;
  autoCapitalize?: 'none' | 'sentences' | 'words' | 'characters';
  multiline?: boolean;
  editable?: boolean;
  testID?: string;
}) {
  const { textAlign, language } = useI18n();
  return (
    <View style={styles.field}>
      {label ? (
        <AppText size={typography.small} weight="600" color={colors.textMuted}>
          {label}
        </AppText>
      ) : null}
      <TextInput
        testID={testID}
        value={value}
        onChangeText={onChangeText}
        placeholder={placeholder}
        placeholderTextColor={colors.textMuted}
        secureTextEntry={secureTextEntry}
        keyboardType={keyboardType}
        autoCapitalize={autoCapitalize}
        autoCorrect={false}
        multiline={multiline}
        editable={editable}
        textAlign={textAlign}
        style={[
          styles.input,
          multiline && styles.inputMultiline,
          !editable && styles.inputDisabled,
          { writingDirection: language === 'ar' ? 'rtl' : 'ltr' }
        ]}
      />
    </View>
  );
}

export function Badge({
  label,
  tone = 'neutral'
}: {
  label: string;
  tone?: 'neutral' | 'success' | 'warning' | 'danger' | 'primary';
}) {
  return (
    <View
      style={[
        styles.badge,
        tone === 'success' && styles.badgeSuccess,
        tone === 'warning' && styles.badgeWarning,
        tone === 'danger' && styles.badgeDanger,
        tone === 'primary' && styles.badgePrimary
      ]}
    >
      <Text
        style={[
          styles.badgeText,
          tone === 'success' && styles.badgeTextSuccess,
          tone === 'warning' && styles.badgeTextWarning,
          tone === 'danger' && styles.badgeTextDanger,
          tone === 'primary' && styles.badgeTextPrimary
        ]}
      >
        {label}
      </Text>
    </View>
  );
}

export function Metric({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.metric}>
      <AppText size={typography.small} color={colors.textMuted}>
        {label}
      </AppText>
      <AppText size={22} weight="700">
        {value}
      </AppText>
    </View>
  );
}

export function EmptyState({ title, hint }: { title: string; hint?: string }) {
  return (
    <Card tone="muted" style={styles.empty}>
      <AppText weight="600" align="center">
        {title}
      </AppText>
      {hint ? (
        <AppText size={typography.small} color={colors.textMuted} align="center">
          {hint}
        </AppText>
      ) : null}
    </Card>
  );
}

export function ErrorBanner({ message, onRetry }: { message: string; onRetry?: () => void }) {
  const { t } = useI18n();
  return (
    <View style={styles.errorBanner}>
      <AppText size={typography.small} color={colors.danger}>
        {message}
      </AppText>
      {onRetry ? (
        <Pressable onPress={onRetry}>
          <Text style={styles.retry}>{t('retry')}</Text>
        </Pressable>
      ) : null}
    </View>
  );
}

export function ScreenLoading({ label }: { label?: string }) {
  const { t } = useI18n();
  return (
    <View style={styles.loading}>
      <ActivityIndicator color={colors.primary} size="large" />
      <AppText color={colors.textMuted}>{label ?? t('loading')}</AppText>
    </View>
  );
}

export function Divider() {
  return <View style={styles.divider} />;
}

export function SectionTitle({ children }: { children: string }) {
  return (
    <AppText size={typography.heading} weight="700" style={styles.sectionTitle}>
      {children}
    </AppText>
  );
}

export function SelectRow({
  title,
  subtitle,
  selected,
  onPress,
  trailing
}: {
  title: string;
  subtitle?: string;
  selected?: boolean;
  onPress: () => void;
  trailing?: ReactNode;
}) {
  const { rowDirection } = useI18n();
  return (
    <Pressable
      onPress={onPress}
      style={({ pressed }) => [
        styles.selectRow,
        { flexDirection: rowDirection },
        selected && styles.selectRowSelected,
        pressed && styles.buttonPressed
      ]}
    >
      <View style={styles.selectRowText}>
        <AppText weight="600">{title}</AppText>
        {subtitle ? (
          <AppText size={typography.small} color={colors.textMuted}>
            {subtitle}
          </AppText>
        ) : null}
      </View>
      {trailing}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  fill: { flex: 1 },
  scrollContent: { padding: spacing.lg, paddingBottom: spacing.xxl, gap: spacing.md },
  header: { alignItems: 'flex-start', justifyContent: 'space-between', gap: spacing.md },
  headerText: { flex: 1, gap: spacing.xs },
  headerSubtitle: { lineHeight: 20 },
  card: {
    backgroundColor: colors.surface,
    borderRadius: radius.lg,
    padding: spacing.lg,
    borderWidth: 1,
    borderColor: colors.border,
    gap: spacing.sm
  },
  cardMuted: { backgroundColor: colors.surfaceMuted },
  cardPrimary: { backgroundColor: colors.primary, borderColor: colors.primary },
  button: {
    minHeight: 48,
    borderRadius: radius.md,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: spacing.lg,
    borderWidth: 1,
    borderColor: 'transparent'
  },
  buttonCompact: { minHeight: 38, paddingHorizontal: spacing.md },
  buttonPrimary: { backgroundColor: colors.primary },
  buttonSecondary: { backgroundColor: colors.primarySoft },
  buttonDanger: { backgroundColor: colors.danger },
  buttonGhost: { backgroundColor: 'transparent' },
  buttonDisabled: { opacity: 0.5 },
  buttonPressed: { opacity: 0.82 },
  buttonLabel: { fontSize: typography.body, fontWeight: '700' },
  buttonLabelLight: { color: '#FFFFFF' },
  buttonLabelPrimary: { color: colors.primaryDark },
  buttonLabelMuted: { color: colors.textMuted },
  field: { gap: spacing.xs },
  input: {
    minHeight: 48,
    backgroundColor: colors.surface,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    fontSize: typography.body,
    color: colors.text
  },
  inputMultiline: { minHeight: 96, textAlignVertical: 'top' },
  inputDisabled: { backgroundColor: colors.surfaceMuted, color: colors.textMuted },
  badge: {
    backgroundColor: colors.surfaceMuted,
    borderRadius: radius.pill,
    paddingHorizontal: spacing.sm,
    paddingVertical: 3
  },
  badgeSuccess: { backgroundColor: colors.successSoft },
  badgeWarning: { backgroundColor: colors.warningSoft },
  badgeDanger: { backgroundColor: colors.dangerSoft },
  badgePrimary: { backgroundColor: colors.primarySoft },
  badgeText: { fontSize: typography.tiny, color: colors.textMuted, fontWeight: '700' },
  badgeTextSuccess: { color: colors.success },
  badgeTextWarning: { color: colors.warning },
  badgeTextDanger: { color: colors.danger },
  badgeTextPrimary: { color: colors.primaryDark },
  metric: { flex: 1, minWidth: '45%', gap: 2 },
  empty: { alignItems: 'center', paddingVertical: spacing.xl },
  errorBanner: {
    backgroundColor: colors.dangerSoft,
    borderRadius: radius.md,
    padding: spacing.md,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: spacing.sm
  },
  retry: { color: colors.danger, fontWeight: '700' },
  loading: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing.md },
  divider: { height: 1, backgroundColor: colors.border },
  sectionTitle: { marginTop: spacing.sm },
  selectRow: {
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: spacing.md,
    backgroundColor: colors.surface,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    padding: spacing.md
  },
  selectRowSelected: { borderColor: colors.primary, backgroundColor: colors.primarySoft },
  selectRowText: { flex: 1, gap: 2 }
});
