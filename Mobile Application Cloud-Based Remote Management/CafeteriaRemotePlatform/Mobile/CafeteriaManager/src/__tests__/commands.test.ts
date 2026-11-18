import { api, ApiError } from '@/api/client';
import type { RemoteCommand } from '@/api/types';
import {
  cancelCommand,
  commandErrorMessage,
  createCommand,
  isOfflineError,
  listCommands,
  parseResult,
  RemoteCommandError,
  runCommand,
  waitForCommand
} from '@/remote/commands';

function command(overrides: Partial<RemoteCommand> = {}): RemoteCommand {
  return {
    id: 'command-1',
    tenantId: 'tenant-1',
    installationId: 'installation-1',
    requestedByUserId: 'user-1',
    type: 'dashboard.snapshot',
    payloadJson: '{}',
    status: 'Completed',
    resultJson: '{"ok":true}',
    error: null,
    createdAt: '2026-11-17T10:00:00.000Z',
    expiresAt: '2026-11-17T10:05:00.000Z',
    dispatchedAt: null,
    startedAt: null,
    completedAt: null,
    ...overrides
  };
}

describe('remote commands', () => {
  let request: jest.Mock;

  beforeEach(() => {
    request = jest.spyOn(api, 'request') as jest.Mock;
  });

  afterEach(() => {
    jest.useRealTimers();
    jest.restoreAllMocks();
  });

  it('creates a command with a unique idempotency key', async () => {
    request.mockResolvedValue(command());
    await createCommand({
      tenantId: 'tenant-1',
      installationId: 'installation-1',
      type: 'inventory.adjust',
      payload: { productId: 'product-1', delta: 3 }
    });

    const [path, init] = request.mock.calls[0];
    const body = JSON.parse(init.body as string);
    expect(path).toBe('/api/tenants/tenant-1/commands');
    expect(body.installationId).toBe('installation-1');
    expect(body.type).toBe('inventory.adjust');
    expect(JSON.parse(body.payloadJson)).toEqual({ productId: 'product-1', delta: 3 });
    expect(body.idempotencyKey).toMatch(/^inventory\.adjust:/);
  });

  it('returns a completed command and reports updates', async () => {
    const pending = command({ status: 'Pending', resultJson: null });
    const completed = command();
    request.mockResolvedValueOnce(pending).mockResolvedValueOnce(completed);
    const onUpdate = jest.fn();

    const result = await waitForCommand('tenant-1', 'command-1', {
      timeoutMs: 5000,
      onUpdate
    });

    expect(result).toEqual(completed);
    expect(onUpdate).toHaveBeenNthCalledWith(1, pending);
    expect(onUpdate).toHaveBeenNthCalledWith(2, completed);
  });

  it('polls until a command completes', async () => {
    jest.useFakeTimers();
    const pending = command({ status: 'Executing', resultJson: null });
    const completed = command();
    request.mockResolvedValueOnce(pending).mockResolvedValueOnce(completed);

    const promise = waitForCommand('tenant-1', 'command-1', { timeoutMs: 5000 });
    await jest.advanceTimersByTimeAsync(1000);

    await expect(promise).resolves.toEqual(completed);
    expect(request).toHaveBeenCalledTimes(2);
  });

  it('throws for failed, cancelled, and expired commands', async () => {
    request.mockResolvedValueOnce(command({ status: 'Failed', error: 'Desktop rejected it' }));
    await expect(waitForCommand('tenant-1', 'command-1')).rejects.toMatchObject({
      code: 'command_failed',
      message: 'Desktop rejected it'
    });

    request.mockResolvedValueOnce(command({ status: 'Cancelled' }));
    await expect(waitForCommand('tenant-1', 'command-1')).rejects.toMatchObject({
      code: 'command_cancelled'
    });

    request.mockResolvedValueOnce(command({ status: 'Expired' }));
    await expect(waitForCommand('tenant-1', 'command-1')).rejects.toMatchObject({
      code: 'command_expired'
    });
  });

  it('times out and supports aborts', async () => {
    await expect(waitForCommand('tenant-1', 'command-1', { timeoutMs: 0 })).rejects.toMatchObject({
      code: 'command_timeout'
    });

    const controller = new AbortController();
    controller.abort();
    await expect(
      waitForCommand('tenant-1', 'command-1', { signal: controller.signal })
    ).rejects.toMatchObject({ code: 'aborted' });
  });

  it('runs commands with and without waiting', async () => {
    const created = command({ status: 'Completed' });
    request.mockResolvedValueOnce(created).mockResolvedValueOnce(created);

    await expect(
      runCommand({
        tenantId: 'tenant-1',
        installationId: 'installation-1',
        type: 'dashboard.snapshot',
        wait: true
      })
    ).resolves.toEqual({ ok: true });

    const queued = command({ status: 'Pending', resultJson: null });
    request.mockResolvedValueOnce(queued);
    await expect(
      runCommand({
        tenantId: 'tenant-1',
        installationId: 'installation-1',
        type: 'inventory.adjust',
        wait: false
      })
    ).resolves.toEqual(queued);
  });

  it('lists and cancels commands', async () => {
    request.mockResolvedValueOnce([command()]).mockResolvedValueOnce(command({ status: 'Cancelled' }));

    await expect(listCommands('tenant-1', 'installation 1')).resolves.toHaveLength(1);
    expect(request.mock.calls[0][0]).toBe(
      '/api/tenants/tenant-1/commands?installationId=installation%201'
    );

    await expect(cancelCommand('tenant-1', 'command-1')).resolves.toMatchObject({
      status: 'Cancelled'
    });
  });

  it('parses and validates command results', () => {
    expect(parseResult<{ ok: boolean }>('{"ok":true}')).toEqual({ ok: true });
    expect(parseResult(null)).toBeUndefined();
    expect(() => parseResult('{bad')).toThrow(RemoteCommandError);
  });

  it('maps command errors and offline states', () => {
    expect(commandErrorMessage(new RemoteCommandError('timeout', 'command_timeout'))).toBe('timeout');
    expect(commandErrorMessage(new ApiError(409, 'installation_offline', 'offline'))).toBe('offline');
    expect(commandErrorMessage(new Error('boom'))).toBe('boom');
    expect(commandErrorMessage('nope')).toBe('Unknown error');

    expect(isOfflineError(new ApiError(409, 'installation_offline', 'offline'))).toBe(true);
    expect(isOfflineError(new RemoteCommandError('timeout', 'command_timeout'))).toBe(true);
    expect(isOfflineError(new Error('boom'))).toBe(false);
  });
});
