import type { AuthResponse, AuthTokens } from '@/api/types';

const DEFAULT_API_URL =
  process.env.EXPO_PUBLIC_API_URL?.trim() || 'http://10.0.2.2:5080';

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;

  constructor(status: number, code: string, message: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
  }
}

type SessionCallbacks = {
  onTokensChanged?: (tokens: AuthTokens) => void | Promise<void>;
  onUnauthorized?: () => void | Promise<void>;
};

class ApiClient {
  private baseUrl = normalizeBaseUrl(DEFAULT_API_URL);
  private tokens: AuthTokens | null = null;
  private callbacks: SessionCallbacks = {};
  private refreshInFlight: Promise<boolean> | null = null;

  getBaseUrl(): string {
    return this.baseUrl;
  }

  setBaseUrl(url: string): void {
    this.baseUrl = normalizeBaseUrl(url);
  }

  getTokens(): AuthTokens | null {
    return this.tokens;
  }

  setTokens(tokens: AuthTokens | null): void {
    this.tokens = tokens;
  }

  setCallbacks(callbacks: SessionCallbacks): void {
    this.callbacks = callbacks;
  }

  async request<T>(
    path: string,
    init: RequestInit = {},
    authenticated = true
  ): Promise<T> {
    const response = await this.send(path, init, authenticated);
    if (response.status === 401 && authenticated && this.tokens?.refreshToken) {
      const refreshed = await this.tryRefresh();
      if (refreshed) {
        const retried = await this.send(path, init, authenticated);
        return this.parse<T>(retried);
      }
    }
    return this.parse<T>(response);
  }

  private async send(
    path: string,
    init: RequestInit,
    authenticated: boolean
  ): Promise<Response> {
    const headers = new Headers(init.headers);
    if (!headers.has('Content-Type') && init.body) {
      headers.set('Content-Type', 'application/json');
    }
    if (authenticated && this.tokens?.accessToken) {
      headers.set('Authorization', `Bearer ${this.tokens.accessToken}`);
    }

    return fetch(`${this.baseUrl}${path}`, {
      ...init,
      headers
    });
  }

  private async parse<T>(response: Response): Promise<T> {
    if (response.status === 204) return undefined as T;
    const text = await response.text();
    const body = text ? safeJson(text) : null;

    if (!response.ok) {
      const error = (body as { error?: { code?: string; message?: string } } | null)?.error;
      throw new ApiError(
        response.status,
        error?.code ?? 'request_failed',
        error?.message ?? `Request failed (${response.status}).`
      );
    }

    return body as T;
  }

  private async tryRefresh(): Promise<boolean> {
    if (this.refreshInFlight) return this.refreshInFlight;

    this.refreshInFlight = (async () => {
      const refreshToken = this.tokens?.refreshToken;
      if (!refreshToken) return false;

      try {
        const response = await fetch(`${this.baseUrl}/api/auth/refresh`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ refreshToken })
        });

        if (!response.ok) {
          await this.callbacks.onUnauthorized?.();
          return false;
        }

        const auth = (await response.json()) as AuthResponse;
        this.tokens = auth.tokens;
        await this.callbacks.onTokensChanged?.(auth.tokens);
        return true;
      } catch {
        return false;
      } finally {
        this.refreshInFlight = null;
      }
    })();

    return this.refreshInFlight;
  }
}

function safeJson(text: string): unknown {
  try {
    return JSON.parse(text) as unknown;
  } catch {
    return { error: { code: 'invalid_response', message: text } };
  }
}

function normalizeBaseUrl(url: string): string {
  const trimmed = url.trim();
  const withScheme = /^https?:\/\//i.test(trimmed) ? trimmed : `https://${trimmed}`;
  return withScheme.replace(/\/+$/, '');
}

export const api = new ApiClient();
