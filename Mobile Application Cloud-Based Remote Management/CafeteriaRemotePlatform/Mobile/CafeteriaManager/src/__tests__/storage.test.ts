import AsyncStorage from '@react-native-async-storage/async-storage';
import type { AuthTokens, UserProfile } from '@/api/types';
import {
  clearProfile,
  clearTokens,
  getApiUrl,
  getSelectedInstallationId,
  getSelectedTenantId,
  getStoredLanguage,
  getStoredProfile,
  getStoredTokens,
  saveApiUrl,
  saveLanguage,
  saveProfile,
  saveSelectedInstallationId,
  saveSelectedTenantId,
  saveTokens
} from '@/storage';

const mockAsyncStore = new Map<string, string>();
const mockSecureStore = new Map<string, string>();

jest.mock('@react-native-async-storage/async-storage', () => ({
  __esModule: true,
  default: {
    getItem: jest.fn(async (key: string) => mockAsyncStore.get(key) ?? null),
    setItem: jest.fn(async (key: string, value: string) => {
      mockAsyncStore.set(key, value);
    }),
    removeItem: jest.fn(async (key: string) => {
      mockAsyncStore.delete(key);
    })
  }
}));

jest.mock('expo-secure-store', () => ({
  getItemAsync: jest.fn(async (key: string) => mockSecureStore.get(key) ?? null),
  setItemAsync: jest.fn(async (key: string, value: string) => {
    mockSecureStore.set(key, value);
  }),
  deleteItemAsync: jest.fn(async (key: string) => {
    mockSecureStore.delete(key);
  })
}));

const tokens: AuthTokens = {
  accessToken: 'access',
  accessTokenExpiresAt: '2026-11-17T11:00:00.000Z',
  refreshToken: 'refresh',
  refreshTokenExpiresAt: '2026-12-17T10:00:00.000Z'
};

const profile: UserProfile = {
  id: 'user-1',
  email: 'owner@example.test',
  displayName: 'Owner',
  tenants: [{ id: 'tenant-1', name: 'Cafeteria', slug: 'cafeteria', role: 'Owner' }]
};

describe('mobile storage', () => {
  beforeEach(() => {
    mockAsyncStore.clear();
    mockSecureStore.clear();
    jest.clearAllMocks();
  });

  it('stores tokens in secure storage', async () => {
    await expect(getStoredTokens()).resolves.toBeNull();
    await saveTokens(tokens);
    await expect(getStoredTokens()).resolves.toEqual(tokens);
    await clearTokens();
    await expect(getStoredTokens()).resolves.toBeNull();
  });

  it('ignores corrupted secure tokens', async () => {
    mockSecureStore.set('cafeteria.manager.tokens', '{bad');
    await expect(getStoredTokens()).resolves.toBeNull();
  });

  it('stores and clears the profile', async () => {
    await expect(getStoredProfile()).resolves.toBeNull();
    await saveProfile(profile);
    await expect(getStoredProfile()).resolves.toEqual(profile);
    await clearProfile();
    await expect(getStoredProfile()).resolves.toBeNull();
  });

  it('stores the API URL and language', async () => {
    await expect(getApiUrl()).resolves.toBeNull();
    await saveApiUrl('https://cloud.example.com');
    await expect(getApiUrl()).resolves.toBe('https://cloud.example.com');

    await expect(getStoredLanguage()).resolves.toBeNull();
    await saveLanguage('ar');
    await expect(getStoredLanguage()).resolves.toBe('ar');
    await saveLanguage('en');
    await expect(getStoredLanguage()).resolves.toBe('en');
  });

  it('ignores invalid languages and JSON', async () => {
    mockAsyncStore.set('cafeteria.manager.language', 'fr');
    await expect(getStoredLanguage()).resolves.toBeNull();

    mockAsyncStore.set('cafeteria.manager.profile', '{bad');
    await expect(getStoredProfile()).resolves.toBeNull();
  });

  it('stores and clears tenant and installation selections', async () => {
    await saveSelectedTenantId('tenant-1');
    await saveSelectedInstallationId('installation-1');
    await expect(getSelectedTenantId()).resolves.toBe('tenant-1');
    await expect(getSelectedInstallationId()).resolves.toBe('installation-1');

    await saveSelectedTenantId(null);
    await saveSelectedInstallationId(null);
    await expect(getSelectedTenantId()).resolves.toBeNull();
    await expect(getSelectedInstallationId()).resolves.toBeNull();
  });

  it('uses async storage for non-secret values', async () => {
    await saveApiUrl('https://cloud.example.com');
    expect(AsyncStorage.setItem).toHaveBeenCalled();
    expect(mockSecureStore.size).toBe(0);
  });
});
