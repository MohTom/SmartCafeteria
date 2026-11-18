import { api, ApiError } from '@/api/client';
import type { AuthTokens } from '@/api/types';

const tokens: AuthTokens = {
  accessToken: 'access-token',
  accessTokenExpiresAt: '2026-11-17T11:00:00.000Z',
  refreshToken: 'refresh-token',
  refreshTokenExpiresAt: '2026-12-17T10:00:00.000Z'
};

function response(status: number, body: unknown, ok = status >= 200 && status < 300): Response {
  return {
    status,
    ok,
    text: async () => (typeof body === 'string' ? body : JSON.stringify(body)),
    json: async () => body
  } as Response;
}

describe('api client', () => {
  beforeEach(() => {
    api.setTokens(null);
    api.setCallbacks({});
    api.setBaseUrl('http://localhost:5080');
    globalThis.fetch = jest.fn();
  });

  afterEach(() => {
    jest.resetAllMocks();
  });

  it('normalizes the configured base URL', () => {
    api.setBaseUrl('cloud.example.com/');
    expect(api.getBaseUrl()).toBe('https://cloud.example.com');

    api.setBaseUrl('http://localhost:5080///');
    expect(api.getBaseUrl()).toBe('http://localhost:5080');
  });

  it('parses JSON responses and sends bearer tokens', async () => {
    api.setTokens(tokens);
    (fetch as jest.Mock).mockResolvedValue(response(200, { ok: true }));

    await expect(api.request('/api/test')).resolves.toEqual({ ok: true });

    const [, init] = (fetch as jest.Mock).mock.calls[0];
    expect(init.headers.get('Authorization')).toBe('Bearer access-token');
  });

  it('omits authorization for public requests', async () => {
    api.setTokens(tokens);
    (fetch as jest.Mock).mockResolvedValue(response(200, { ok: true }));

    await api.request('/api/auth/login', { method: 'POST', body: '{}' }, false);

    const [, init] = (fetch as jest.Mock).mock.calls[0];
    expect(init.headers.has('Authorization')).toBe(false);
    expect(init.headers.get('Content-Type')).toBe('application/json');
  });

  it('returns undefined for empty responses', async () => {
    (fetch as jest.Mock).mockResolvedValue(response(204, ''));

    await expect(api.request('/api/empty')).resolves.toBeUndefined();
  });

  it('maps API errors', async () => {
    (fetch as jest.Mock).mockResolvedValue(
      response(403, { error: { code: 'forbidden', message: 'No access' } })
    );

    await expect(api.request('/api/test')).rejects.toMatchObject({
      name: 'ApiError',
      status: 403,
      code: 'forbidden',
      message: 'No access'
    });
  });

  it('handles non-JSON error bodies', async () => {
    (fetch as jest.Mock).mockResolvedValue(response(500, 'upstream failed'));

    await expect(api.request('/api/test')).rejects.toMatchObject({
      status: 500,
      code: 'invalid_response',
      message: 'upstream failed'
    });
  });

  it('refreshes an expired token and retries the request', async () => {
    const onTokensChanged = jest.fn();
    api.setTokens(tokens);
    api.setCallbacks({ onTokensChanged });
    const refreshed = { ...tokens, accessToken: 'new-access-token' };

    (fetch as jest.Mock)
      .mockResolvedValueOnce(response(401, { error: { code: 'unauthorized' } }))
      .mockResolvedValueOnce(response(200, { tokens: refreshed, user: { id: 'user-1' } }))
      .mockResolvedValueOnce(response(200, { value: 42 }));

    await expect(api.request('/api/protected')).resolves.toEqual({ value: 42 });

    expect(onTokensChanged).toHaveBeenCalledWith(refreshed);
    const retryCall = (fetch as jest.Mock).mock.calls[2];
    expect(retryCall[1].headers.get('Authorization')).toBe('Bearer new-access-token');
  });

  it('clears the session when refresh fails', async () => {
    const onUnauthorized = jest.fn();
    api.setTokens(tokens);
    api.setCallbacks({ onUnauthorized });

    (fetch as jest.Mock)
      .mockResolvedValueOnce(response(401, { error: { code: 'unauthorized' } }))
      .mockResolvedValueOnce(response(401, { error: { code: 'refresh_failed' } }));

    await expect(api.request('/api/protected')).rejects.toBeInstanceOf(ApiError);
    expect(onUnauthorized).toHaveBeenCalledTimes(1);
  });

  it('does not refresh public requests', async () => {
    api.setTokens(tokens);
    (fetch as jest.Mock).mockResolvedValue(response(401, { error: { code: 'unauthorized' } }));

    await expect(api.request('/api/auth/refresh', { method: 'POST' }, false)).rejects.toBeInstanceOf(
      ApiError
    );
    expect(fetch).toHaveBeenCalledTimes(1);
  });
});
