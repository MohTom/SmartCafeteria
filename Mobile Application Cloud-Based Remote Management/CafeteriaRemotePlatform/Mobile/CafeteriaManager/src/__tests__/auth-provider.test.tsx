import type { ReactNode } from 'react';
import { act, renderHook, waitFor } from '@testing-library/react-native';
import { api } from '@/api/client';
import type { AuthResponse, AuthTokens, UserProfile } from '@/api/types';
import { AuthProvider, useAuth } from '@/auth/AuthProvider';
import {
  clearProfile,
  clearTokens,
  getApiUrl,
  getStoredProfile,
  getStoredTokens,
  saveApiUrl,
  saveProfile,
  saveTokens
} from '@/storage';

jest.mock('@/storage', () => ({
  clearProfile: jest.fn(),
  clearTokens: jest.fn(),
  getApiUrl: jest.fn(),
  getStoredProfile: jest.fn(),
  getStoredTokens: jest.fn(),
  saveApiUrl: jest.fn(),
  saveProfile: jest.fn(),
  saveTokens: jest.fn()
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

const auth: AuthResponse = { tokens, user: profile };

function wrapper({ children }: { children: ReactNode }) {
  return <AuthProvider>{children}</AuthProvider>;
}

async function renderAuth() {
  return renderHook(() => useAuth(), { wrapper });
}

describe('AuthProvider', () => {
  let request: jest.Mock;

  beforeEach(() => {
    jest.clearAllMocks();
    api.setBaseUrl('http://localhost:5080');
    api.setTokens(null);
    request = jest.spyOn(api, 'request') as jest.Mock;

    (getApiUrl as jest.Mock).mockResolvedValue(null);
    (getStoredTokens as jest.Mock).mockResolvedValue(null);
    (getStoredProfile as jest.Mock).mockResolvedValue(null);
    (saveTokens as jest.Mock).mockResolvedValue(undefined);
    (saveProfile as jest.Mock).mockResolvedValue(undefined);
    (saveApiUrl as jest.Mock).mockResolvedValue(undefined);
    (clearTokens as jest.Mock).mockResolvedValue(undefined);
    (clearProfile as jest.Mock).mockResolvedValue(undefined);
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('starts signed out when there are no stored tokens', async () => {
    const { result } = await renderAuth();
    await waitFor(() => expect(result.current.status).toBe('signedOut'));
    expect(result.current.profile).toBeNull();
  });

  it('restores a session from stored tokens', async () => {
    (getStoredTokens as jest.Mock).mockResolvedValue(tokens);
    request.mockResolvedValue(profile);

    const { result } = await renderAuth();
    await waitFor(() => expect(result.current.status).toBe('signedIn'));
    expect(result.current.profile).toEqual(profile);
    expect(request).toHaveBeenCalledWith('/api/auth/me');
    expect(saveProfile).toHaveBeenCalledWith(profile);
  });

  it('falls back to the cached profile when /me fails', async () => {
    (getStoredTokens as jest.Mock).mockResolvedValue(tokens);
    (getStoredProfile as jest.Mock).mockResolvedValue(profile);
    request.mockRejectedValue(new Error('offline'));

    const { result } = await renderAuth();
    await waitFor(() => expect(result.current.status).toBe('signedIn'));
    expect(result.current.profile).toEqual(profile);
  });

  it('clears the session when no cached profile is available', async () => {
    (getStoredTokens as jest.Mock).mockResolvedValue(tokens);
    request.mockRejectedValue(new Error('offline'));

    const { result } = await renderAuth();
    await waitFor(() => expect(result.current.status).toBe('signedOut'));
    expect(clearTokens).toHaveBeenCalled();
    expect(clearProfile).toHaveBeenCalled();
  });

  it('logs in and persists the session', async () => {
    request.mockResolvedValue(auth);
    const { result } = await renderAuth();
    await waitFor(() => expect(result.current.status).toBe('signedOut'));

    await act(async () => {
      await result.current.login('owner@example.test', 'password');
    });

    expect(request).toHaveBeenCalledWith(
      '/api/auth/login',
      { method: 'POST', body: JSON.stringify({ email: 'owner@example.test', password: 'password' }) },
      false
    );
    expect(result.current.status).toBe('signedIn');
    expect(result.current.profile).toEqual(profile);
    expect(saveTokens).toHaveBeenCalledWith(tokens);
    expect(saveProfile).toHaveBeenCalledWith(profile);
  });

  it('registers a new account', async () => {
    request.mockResolvedValue(auth);
    const { result } = await renderAuth();
    await waitFor(() => expect(result.current.status).toBe('signedOut'));

    await act(async () => {
      await result.current.register({
        companyName: 'Cafeteria',
        ownerEmail: 'owner@example.test',
        ownerDisplayName: 'Owner',
        password: 'password'
      });
    });

    expect(request).toHaveBeenCalledWith(
      '/api/auth/register',
      expect.objectContaining({ method: 'POST' }),
      false
    );
    expect(result.current.status).toBe('signedIn');
  });

  it('logs out locally even when the cloud call fails', async () => {
    request.mockRejectedValue(new Error('offline'));
    const { result } = await renderAuth();
    await waitFor(() => expect(result.current.status).toBe('signedOut'));

    await act(async () => {
      await result.current.logout();
    });

    expect(result.current.status).toBe('signedOut');
    expect(clearTokens).toHaveBeenCalled();
  });

  it('calls the logout endpoint when a refresh token exists', async () => {
    request.mockResolvedValueOnce(undefined).mockResolvedValue(auth);
    const { result } = await renderAuth();
    await waitFor(() => expect(result.current.status).toBe('signedOut'));
    api.setTokens(tokens);

    await act(async () => {
      await result.current.logout();
    });

    expect(request).toHaveBeenCalledWith(
      '/api/auth/logout',
      { method: 'POST', body: JSON.stringify({ refreshToken: 'refresh' }) },
      false
    );
  });

  it('accepts invitations and refreshes the profile', async () => {
    request.mockResolvedValueOnce(profile.tenants[0]).mockResolvedValueOnce(profile);
    const { result } = await renderAuth();
    await waitFor(() => expect(result.current.status).toBe('signedOut'));

    let tenant: unknown;
    await act(async () => {
      tenant = await result.current.acceptInvitation('INVITE-1');
    });

    expect(tenant).toEqual(profile.tenants[0]);
    expect(result.current.profile).toEqual(profile);
  });

  it('refreshes the profile and saves the API base URL', async () => {
    request.mockResolvedValue(profile);
    const { result } = await renderAuth();
    await waitFor(() => expect(result.current.status).toBe('signedOut'));

    await act(async () => {
      await result.current.refreshProfile();
      await result.current.setApiBaseUrl('cloud.example.com/');
    });

    expect(result.current.profile).toEqual(profile);
    expect(result.current.apiBaseUrl).toBe('https://cloud.example.com');
    expect(saveApiUrl).toHaveBeenCalledWith('https://cloud.example.com');
  });

  it('requires a provider', async () => {
    const spy = jest.spyOn(console, 'error').mockImplementation(() => undefined);
    await expect(renderHook(() => useAuth())).rejects.toThrow(
      'useAuth must be used inside AuthProvider.'
    );
    spy.mockRestore();
  });
});
