import { fireEvent, render, screen } from '@testing-library/react-native';
import {
  AppText,
  Badge,
  Button,
  Card,
  Divider,
  EmptyState,
  ErrorBanner,
  Header,
  Metric,
  Screen,
  ScreenLoading,
  SectionTitle,
  SelectRow,
  TextField
} from '@/components/ui';
import { LanguageToggle } from '@/components/LanguageToggle';

const mockSetLanguage = jest.fn();

jest.mock('@/i18n/I18nProvider', () => ({
  useI18n: () => ({
    t: (key: string) => key,
    textAlign: 'left',
    rowDirection: 'row',
    language: 'en',
    isRTL: false,
    setLanguage: mockSetLanguage
  })
}));

describe('ui components', () => {
  it('renders text, headers, cards, and screen containers', async () => {
    const { rerender } = await render(
      <Screen scroll={false}>
        <Header title="Dashboard" subtitle="Live overview" action={<AppText>Action</AppText>} />
        <Card tone="muted">
          <AppText>Card body</AppText>
        </Card>
        <Card tone="primary">
          <AppText>Primary card</AppText>
        </Card>
      </Screen>
    );

    expect(screen.getByText('Dashboard')).toBeTruthy();
    expect(screen.getByText('Live overview')).toBeTruthy();
    expect(screen.getByText('Action')).toBeTruthy();
    expect(screen.getByText('Card body')).toBeTruthy();

    const onRefresh = jest.fn();
    await rerender(
      <Screen refreshing onRefresh={onRefresh}>
        <AppText>Scrollable</AppText>
      </Screen>
    );
    expect(screen.getByText('Scrollable')).toBeTruthy();
  });

  it('handles button variants and disabled/loading states', async () => {
    const onPress = jest.fn();
    const { rerender } = await render(
      <>
        <Button label="Primary" onPress={onPress} />
        <Button label="Secondary" onPress={onPress} variant="secondary" compact />
        <Button label="Danger" onPress={onPress} variant="danger" />
        <Button label="Ghost" onPress={onPress} variant="ghost" />
      </>
    );

    await fireEvent.press(screen.getByText('Primary'));
    expect(onPress).toHaveBeenCalledTimes(1);

    await rerender(<Button label="Disabled" onPress={onPress} disabled />);
    await fireEvent.press(screen.getByText('Disabled'));
    expect(onPress).toHaveBeenCalledTimes(1);

    await rerender(<Button label="Loading" onPress={onPress} loading />);
    expect(screen.queryByText('Loading')).toBeNull();
  });

  it('renders text fields and forwards changes', async () => {
    const onChangeText = jest.fn();
    await render(
      <>
        <TextField label="Email" value="owner@example.test" onChangeText={onChangeText} />
        <TextField
          value="Notes"
          onChangeText={onChangeText}
          multiline
          secureTextEntry
          keyboardType="email-address"
        />
        <TextField value="Locked" onChangeText={onChangeText} editable={false} />
      </>
    );

    await fireEvent.changeText(screen.getByDisplayValue('owner@example.test'), 'new@example.test');
    expect(onChangeText).toHaveBeenCalledWith('new@example.test');
  });

  it('exposes test identifiers for Maestro', async () => {
    await render(
      <>
        <Button label="Sign in" onPress={jest.fn()} testID="login.signIn" />
        <TextField label="Email" value="" onChangeText={jest.fn()} testID="login.email" />
      </>
    );

    expect(screen.getByTestId('login.signIn')).toBeTruthy();
    expect(screen.getByTestId('login.email')).toBeTruthy();
  });

  it('renders badges, metrics, empty states, and error banners', async () => {
    const onRetry = jest.fn();
    await render(
      <>
        <Badge label="Success" tone="success" />
        <Badge label="Warning" tone="warning" />
        <Badge label="Danger" tone="danger" />
        <Badge label="Primary" tone="primary" />
        <Metric label="Revenue" value="1,234 SAR" />
        <EmptyState title="Nothing here" hint="Try again later" />
        <ErrorBanner message="Failed" onRetry={onRetry} />
        <ScreenLoading label="Working" />
        <Divider />
        <SectionTitle>Section</SectionTitle>
      </>
    );

    expect(screen.getByText('1,234 SAR')).toBeTruthy();
    expect(screen.getByText('Nothing here')).toBeTruthy();
    await fireEvent.press(screen.getByText('retry'));
    expect(onRetry).toHaveBeenCalledTimes(1);
    expect(screen.getByText('Working')).toBeTruthy();
  });

  it('renders selectable rows and handles presses', async () => {
    const onPress = jest.fn();
    await render(
      <>
        <SelectRow title="Company" subtitle="Cafeteria" selected onPress={onPress} />
        <SelectRow title="Installation" onPress={onPress} trailing={<AppText>Online</AppText>} />
      </>
    );

    await fireEvent.press(screen.getByText('Company'));
    await fireEvent.press(screen.getByText('Installation'));
    expect(onPress).toHaveBeenCalledTimes(2);
    expect(screen.getByText('Online')).toBeTruthy();
  });

  it('switches language from the toggle', async () => {
    await render(<LanguageToggle />);
    await fireEvent.press(screen.getByText('arabic'));
    expect(mockSetLanguage).toHaveBeenCalledWith('ar');
    await fireEvent.press(screen.getByText('english'));
    expect(mockSetLanguage).toHaveBeenCalledWith('en');
  });
});
