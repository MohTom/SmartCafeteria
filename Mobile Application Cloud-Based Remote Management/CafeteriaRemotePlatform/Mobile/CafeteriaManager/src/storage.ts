import AsyncStorage from '@react-native-async-storage/async-storage';
import * as SecureStore from 'expo-secure-store';
import type { AuthTokens, UserProfile } from '@/api/types';

const TOKENS_KEY = 'cafeteria.manager.tokens';
const PROFILE_KEY = 'cafeteria.manager.profile';
const API_URL_KEY = 'cafeteria.manager.apiUrl';
const LANGUAGE_KEY = 'cafeteria.manager.language';
const TENANT_KEY = 'cafeteria.manager.tenantId';
const INSTALLATION_KEY = 'cafeteria.manager.installationId';

export async function getStoredTokens(): Promise<AuthTokens | null> {
  try {
    const value = await SecureStore.getItemAsync(TOKENS_KEY);
    if (!value) return null;
    return JSON.parse(value) as AuthTokens;
  } catch {
    return null;
  }
}

export async function saveTokens(tokens: AuthTokens): Promise<void> {
  await SecureStore.setItemAsync(TOKENS_KEY, JSON.stringify(tokens));
}

export async function clearTokens(): Promise<void> {
  await SecureStore.deleteItemAsync(TOKENS_KEY);
}

export async function getStoredProfile(): Promise<UserProfile | null> {
  return getJson<UserProfile>(PROFILE_KEY);
}

export async function saveProfile(profile: UserProfile): Promise<void> {
  await setJson(PROFILE_KEY, profile);
}

export async function clearProfile(): Promise<void> {
  await AsyncStorage.removeItem(PROFILE_KEY);
}

export async function getApiUrl(): Promise<string | null> {
  return AsyncStorage.getItem(API_URL_KEY);
}

export async function saveApiUrl(url: string): Promise<void> {
  await AsyncStorage.setItem(API_URL_KEY, url);
}

export async function getStoredLanguage(): Promise<'ar' | 'en' | null> {
  const value = await AsyncStorage.getItem(LANGUAGE_KEY);
  return value === 'ar' || value === 'en' ? value : null;
}

export async function saveLanguage(language: 'ar' | 'en'): Promise<void> {
  await AsyncStorage.setItem(LANGUAGE_KEY, language);
}

export async function getSelectedTenantId(): Promise<string | null> {
  return AsyncStorage.getItem(TENANT_KEY);
}

export async function saveSelectedTenantId(tenantId: string | null): Promise<void> {
  if (tenantId) await AsyncStorage.setItem(TENANT_KEY, tenantId);
  else await AsyncStorage.removeItem(TENANT_KEY);
}

export async function getSelectedInstallationId(): Promise<string | null> {
  return AsyncStorage.getItem(INSTALLATION_KEY);
}

export async function saveSelectedInstallationId(installationId: string | null): Promise<void> {
  if (installationId) await AsyncStorage.setItem(INSTALLATION_KEY, installationId);
  else await AsyncStorage.removeItem(INSTALLATION_KEY);
}

async function getJson<T>(key: string): Promise<T | null> {
  try {
    const value = await AsyncStorage.getItem(key);
    return value ? (JSON.parse(value) as T) : null;
  } catch {
    return null;
  }
}

async function setJson(key: string, value: unknown): Promise<void> {
  await AsyncStorage.setItem(key, JSON.stringify(value));
}
