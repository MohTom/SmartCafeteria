import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { Pressable, Text, View } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { getStoredLanguage, saveLanguage } from '@/storage';
import { I18nProvider, useI18n } from '@/i18n/I18nProvider';

const mockAsyncStore = new Map<string, string>();

jest.mock('expo-localization', () => ({
  getLocales: () => [{ languageCode: 'en' }]
}));

jest.mock('@/storage', () => ({
  getStoredLanguage: jest.fn(),
  saveLanguage: jest.fn()
}));

jest.mock('@react-native-async-storage/async-storage', () => ({
  __esModule: true,
  default: {
    setItem: jest.fn(async (key: string, value: string) => {
      mockAsyncStore.set(key, value);
    })
  }
}));

function Probe() {
  const { language, isRTL, textAlign, rowDirection, setLanguage, t } = useI18n();
  return (
    <View>
      <Text>{language}</Text>
      <Text>{t('appName')}</Text>
      <Text>{isRTL ? 'rtl' : 'ltr'}</Text>
      <Text>{textAlign}</Text>
      <Text>{rowDirection}</Text>
      <Pressable onPress={() => void setLanguage('ar')}>
        <Text>switch</Text>
      </Pressable>
    </View>
  );
}

describe('I18nProvider', () => {
  beforeEach(() => {
    mockAsyncStore.clear();
    jest.clearAllMocks();
    (getStoredLanguage as jest.Mock).mockResolvedValue(null);
  });

  it('defaults to English and switches to Arabic', async () => {
    (saveLanguage as jest.Mock).mockResolvedValue(undefined);

    await render(
      <I18nProvider>
        <Probe />
      </I18nProvider>
    );

    await waitFor(() => expect(screen.getByText('Cafeteria Manager')).toBeTruthy());
    expect(screen.getByText('ltr')).toBeTruthy();
    expect(screen.getByText('left')).toBeTruthy();
    expect(screen.getByText('row')).toBeTruthy();

    await fireEvent.press(screen.getByText('switch'));

    await waitFor(() => expect(screen.getByText('مدير الكافتيريا')).toBeTruthy());
    expect(screen.getByText('rtl')).toBeTruthy();
    expect(screen.getByText('right')).toBeTruthy();
    expect(screen.getByText('row-reverse')).toBeTruthy();
    expect(saveLanguage).toHaveBeenCalledWith('ar');
    expect(mockAsyncStore.get('cafeteria.manager.direction')).toBe('rtl');
    expect(AsyncStorage.setItem).toHaveBeenCalled();
  });

  it('loads a stored language', async () => {
    (getStoredLanguage as jest.Mock).mockResolvedValue('ar');

    await render(
      <I18nProvider>
        <Probe />
      </I18nProvider>
    );

    await waitFor(() => expect(screen.getByText('ar')).toBeTruthy());
    expect(screen.getByText('rtl')).toBeTruthy();
  });

  it('requires a provider', async () => {
    const spy = jest.spyOn(console, 'error').mockImplementation(() => undefined);
    await expect(render(<Probe />)).rejects.toThrow('useI18n must be used inside I18nProvider.');
    spy.mockRestore();
  });
});
