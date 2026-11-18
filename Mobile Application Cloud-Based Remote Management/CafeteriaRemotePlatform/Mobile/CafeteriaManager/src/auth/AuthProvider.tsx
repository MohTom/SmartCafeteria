import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode
} from 'react';
import { api } from '@/api/client';
import type { AuthResponse, AuthTokens, TenantSummary, UserProfile } from '@/api/types';
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

type AuthStatus = 'loading' | 'signedOut' | 'signedIn';

type AuthValue = {
  status: AuthStatus;
  profile: UserProfile | null;
  tokens: AuthTokens | null;
  apiBaseUrl: string;
  login: (email: string, password: string) => Promise<void>;
  register: (input: {
    companyName: string;
    ownerEmail: string;
    ownerDisplayName: string;
    password: string;
  }) => Promise<void>;
  logout: () => Promise<void>;
  refreshProfile: () => Promise<UserProfile>;
  acceptInvitation: (inviteCode: string) => Promise<TenantSummary>;
  setApiBaseUrl: (url: string) => Promise<void>;
};

const AuthContext = createContext<AuthValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('loading');
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [tokens, setTokens] = useState<AuthTokens | null>(null);
  const [apiBaseUrl, setApiBaseUrlState] = useState(api.getBaseUrl());

  const clearSession = useCallback(async () => {
    api.setTokens(null);
    setTokens(null);
    setProfile(null);
    setStatus('signedOut');
    await Promise.allSettled([clearTokens(), clearProfile()]);
  }, []);

  useEffect(() => {
    api.setCallbacks({
      onTokensChanged: async (next) => {
        api.setTokens(next);
        setTokens(next);
        await saveTokens(next);
      },
      onUnauthorized: clearSession
    });

    let active = true;

    (async () => {
      try {
        const storedUrl = await getApiUrl();
        if (storedUrl) {
          api.setBaseUrl(storedUrl);
          if (active) setApiBaseUrlState(api.getBaseUrl());
        }

        const storedTokens = await getStoredTokens();
        if (!storedTokens) {
          if (active) setStatus('signedOut');
          return;
        }

        api.setTokens(storedTokens);
        if (active) setTokens(storedTokens);

        try {
          const current = await api.request<UserProfile>('/api/auth/me');
          if (!active) return;
          setProfile(current);
          setStatus('signedIn');
          await saveProfile(current);
        } catch {
          const cached = await getStoredProfile();
          if (cached) {
            setProfile(cached);
            setStatus('signedIn');
          } else {
            await clearSession();
          }
        }
      } catch {
        if (active) setStatus('signedOut');
      }
    })();

    return () => {
      active = false;
    };
  }, [clearSession]);

  const applyAuth = useCallback(async (auth: AuthResponse) => {
    api.setTokens(auth.tokens);
    setTokens(auth.tokens);
    setProfile(auth.user);
    setStatus('signedIn');
    await Promise.all([saveTokens(auth.tokens), saveProfile(auth.user)]);
  }, []);

  const login = useCallback(
    async (email: string, password: string) => {
      const auth = await api.request<AuthResponse>(
        '/api/auth/login',
        {
          method: 'POST',
          body: JSON.stringify({ email, password })
        },
        false
      );
      await applyAuth(auth);
    },
    [applyAuth]
  );

  const register = useCallback(
    async (input: {
      companyName: string;
      ownerEmail: string;
      ownerDisplayName: string;
      password: string;
    }) => {
      const auth = await api.request<AuthResponse>(
        '/api/auth/register',
        {
          method: 'POST',
          body: JSON.stringify(input)
        },
        false
      );
      await applyAuth(auth);
    },
    [applyAuth]
  );

  const refreshProfile = useCallback(async () => {
    const current = await api.request<UserProfile>('/api/auth/me');
    setProfile(current);
    await saveProfile(current);
    return current;
  }, []);

  const logout = useCallback(async () => {
    const refreshToken = api.getTokens()?.refreshToken;
    if (refreshToken) {
      try {
        await api.request<undefined>(
          '/api/auth/logout',
          {
            method: 'POST',
            body: JSON.stringify({ refreshToken })
          },
          false
        );
      } catch {
        // Local sign-out must still succeed when the cloud is unreachable.
      }
    }
    await clearSession();
  }, [clearSession]);

  const acceptInvitation = useCallback(
    async (inviteCode: string) => {
      const tenant = await api.request<TenantSummary>('/api/auth/accept-invitation', {
        method: 'POST',
        body: JSON.stringify({ inviteCode })
      });
      await refreshProfile();
      return tenant;
    },
    [refreshProfile]
  );

  const setApiBaseUrl = useCallback(async (url: string) => {
    api.setBaseUrl(url);
    await saveApiUrl(api.getBaseUrl());
    setApiBaseUrlState(api.getBaseUrl());
  }, []);

  const value = useMemo<AuthValue>(
    () => ({
      status,
      profile,
      tokens,
      apiBaseUrl,
      login,
      register,
      logout,
      refreshProfile,
      acceptInvitation,
      setApiBaseUrl
    }),
    [
      status,
      profile,
      tokens,
      apiBaseUrl,
      login,
      register,
      logout,
      refreshProfile,
      acceptInvitation,
      setApiBaseUrl
    ]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthValue {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth must be used inside AuthProvider.');
  return value;
}
