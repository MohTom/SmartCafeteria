import { useCallback, useState } from 'react';
import { api, ApiError } from '@/api/client';
import type { RemoteCommand } from '@/api/types';

export class RemoteCommandError extends Error {
  readonly code: string;
  readonly command: RemoteCommand | null;

  constructor(message: string, code = 'command_failed', command: RemoteCommand | null = null) {
    super(message);
    this.name = 'RemoteCommandError';
    this.code = code;
    this.command = command;
  }
}

export type RunCommandOptions = {
  tenantId: string;
  installationId: string;
  type: string;
  payload?: Record<string, unknown>;
  wait?: boolean;
  timeoutMs?: number;
  signal?: AbortSignal;
  onCommandCreated?: (command: RemoteCommand) => void;
};

export async function createCommand(options: RunCommandOptions): Promise<RemoteCommand> {
  const command = await api.request<RemoteCommand>(
    `/api/tenants/${options.tenantId}/commands`,
    {
      method: 'POST',
      body: JSON.stringify({
        installationId: options.installationId,
        type: options.type,
        payloadJson: JSON.stringify(options.payload ?? {}),
        idempotencyKey: createIdempotencyKey(options.type)
      })
    }
  );
  return command;
}

export async function waitForCommand(
  tenantId: string,
  commandId: string,
  options: {
    timeoutMs?: number;
    signal?: AbortSignal;
    onUpdate?: (command: RemoteCommand) => void;
  } = {}
): Promise<RemoteCommand> {
  const timeoutMs = options.timeoutMs ?? 30_000;
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    if (options.signal?.aborted) throw new RemoteCommandError('The request was cancelled.', 'aborted');
    const command = await api.request<RemoteCommand>(
      `/api/tenants/${tenantId}/commands/${commandId}`
    );
    options.onUpdate?.(command);

    if (command.status === 'Completed') return command;
    if (command.status === 'Failed') {
      throw new RemoteCommandError(
        command.error || 'The desktop could not complete the command.',
        'command_failed',
        command
      );
    }
    if (command.status === 'Cancelled') {
      throw new RemoteCommandError('The command was cancelled.', 'command_cancelled', command);
    }
    if (command.status === 'Expired') {
      throw new RemoteCommandError('The command expired before it could run.', 'command_expired', command);
    }

    await delay(1000, options.signal);
  }

  throw new RemoteCommandError(
    'The desktop did not respond in time. It may be offline.',
    'command_timeout'
  );
}

export async function runCommand<T = unknown>(options: RunCommandOptions): Promise<T> {
  const command = await createCommand(options);
  options.onCommandCreated?.(command);

  if (options.wait === false) {
    return command as unknown as T;
  }

  const finished = await waitForCommand(options.tenantId, command.id, {
    timeoutMs: options.timeoutMs,
    signal: options.signal
  });

  return parseResult<T>(finished.resultJson);
}

export async function listCommands(
  tenantId: string,
  installationId?: string
): Promise<RemoteCommand[]> {
  const query = installationId ? `?installationId=${encodeURIComponent(installationId)}` : '';
  return api.request<RemoteCommand[]>(`/api/tenants/${tenantId}/commands${query}`);
}

export async function cancelCommand(tenantId: string, commandId: string): Promise<RemoteCommand> {
  return api.request<RemoteCommand>(`/api/tenants/${tenantId}/commands/${commandId}/cancel`, {
    method: 'POST'
  });
}

export function parseResult<T>(resultJson: string | null): T {
  if (!resultJson) return undefined as T;
  try {
    return JSON.parse(resultJson) as T;
  } catch {
    throw new RemoteCommandError('The desktop returned an invalid result.', 'invalid_result');
  }
}

export function commandErrorMessage(error: unknown): string {
  if (error instanceof RemoteCommandError) return error.message;
  if (error instanceof ApiError) return error.message;
  if (error instanceof Error) return error.message;
  return 'Unknown error';
}

export function isOfflineError(error: unknown): boolean {
  return (
    (error instanceof ApiError && error.code === 'installation_offline') ||
    (error instanceof RemoteCommandError && error.code === 'command_timeout')
  );
}

export function useRemoteCommand() {
  const [running, setRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [command, setCommand] = useState<RemoteCommand | null>(null);

  const run = useCallback(async <T,>(options: RunCommandOptions): Promise<T> => {
    setRunning(true);
    setError(null);
    setCommand(null);
    try {
      const command = await createCommand(options);
      setCommand(command);
      options.onCommandCreated?.(command);

      if (options.wait === false) return command as unknown as T;

      const finished = await waitForCommand(options.tenantId, command.id, {
        timeoutMs: options.timeoutMs,
        signal: options.signal,
        onUpdate: setCommand
      });
      return parseResult<T>(finished.resultJson);
    } catch (caught) {
      setError(commandErrorMessage(caught));
      throw caught;
    } finally {
      setRunning(false);
    }
  }, []);

  return { run, running, error, command, setError };
}

function createIdempotencyKey(type: string): string {
  const random = Math.random().toString(36).slice(2, 10);
  return `${type}:${Date.now().toString(36)}:${random}`;
}

function delay(ms: number, signal?: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    if (signal?.aborted) {
      reject(new RemoteCommandError('The request was cancelled.', 'aborted'));
      return;
    }
    const timer = setTimeout(resolve, ms);
    signal?.addEventListener(
      'abort',
      () => {
        clearTimeout(timer);
        reject(new RemoteCommandError('The request was cancelled.', 'aborted'));
      },
      { once: true }
    );
  });
}
