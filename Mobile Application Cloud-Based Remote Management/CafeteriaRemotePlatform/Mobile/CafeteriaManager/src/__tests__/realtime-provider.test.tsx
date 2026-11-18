import type { ReactNode } from 'react';
import { act, renderHook, waitFor } from '@testing-library/react-native';
import type { Installation, NotificationItem, RemoteCommand } from '@/api/types';
import { useAuth } from '@/auth/AuthProvider';
import { useSelection } from '@/state/SelectionProvider';
import { RealtimeProvider, useRealtime } from '@/realtime/RealtimeProvider';

const mockConnection = {
  on: jest.fn(),
  onreconnecting: jest.fn(),
  onreconnected: jest.fn(),
  onclose: jest.fn(),
  start: jest.fn().mockResolvedValue(undefined),
  stop: jest.fn().mockResolvedValue(undefined)
};

const mockBuilder = {
  withUrl: jest.fn(),
  withAutomaticReconnect: jest.fn(),
  configureLogging: jest.fn(),
  build: jest.fn()
};

jest.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: jest.fn(() => mockBuilder),
  LogLevel: { None: 0 }
}));

jest.mock('@/auth/AuthProvider', () => ({
  useAuth: jest.fn()
}));

jest.mock('@/state/SelectionProvider', () => ({
  useSelection: jest.fn()
}));

const notification: NotificationItem = {
  id: 'notification-1',
  tenantId: 'tenant-1',
  installationId: 'installation-1',
  title: 'Alert',
  body: 'Body',
  type: 'System',
  isRead: false,
  createdAt: '2026-11-17T10:00:00.000Z'
};

const command: RemoteCommand = {
  id: 'command-1',
  tenantId: 'tenant-1',
  installationId: 'installation-1',
  requestedByUserId: 'user-1',
  type: 'dashboard.snapshot',
  payloadJson: '{}',
  status: 'Completed',
  resultJson: '{}',
  error: null,
  createdAt: '2026-11-17T10:00:00.000Z',
  expiresAt: '2026-11-17T10:05:00.000Z',
  dispatchedAt: null,
  startedAt: null,
  completedAt: null
};

const installation: Installation = {
  id: 'installation-1',
  tenantId: 'tenant-1',
  name: 'Front desk',
  machineName: 'CAFE-PC',
  appVersion: '1.0.0',
  isOnline: true,
  lastSeenAt: null,
  createdAt: '2026-11-01T10:00:00.000Z',
  access: { canViewReports: true, canManageUsers: true, canAdjustInventory: true }
};

function wrapper({ children }: { children: ReactNode }) {
  return <RealtimeProvider>{children}</RealtimeProvider>;
}

async function renderRealtime() {
  return renderHook(() => useRealtime(), { wrapper });
}

function handler(name: string) {
  return mockConnection.on.mock.calls.find(([event]) => event === name)?.[1];
}

describe('RealtimeProvider', () => {
  const updateInstallation = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
    mockBuilder.withUrl.mockReturnValue(mockBuilder);
    mockBuilder.withAutomaticReconnect.mockReturnValue(mockBuilder);
    mockBuilder.configureLogging.mockReturnValue(mockBuilder);
    mockBuilder.build.mockReturnValue(mockConnection);
    mockConnection.start.mockResolvedValue(undefined);
    mockConnection.stop.mockResolvedValue(undefined);

    (useAuth as jest.Mock).mockReturnValue({
      status: 'signedIn',
      tokens: { accessToken: 'access', refreshToken: 'refresh' }
    });
    (useSelection as jest.Mock).mockReturnValue({ updateInstallation });
  });

  it('does not connect while signed out', async () => {
    (useAuth as jest.Mock).mockReturnValue({ status: 'signedOut', tokens: null });
    const { result } = await renderRealtime();
    expect(result.current.connected).toBe(false);
    expect(mockBuilder.build).not.toHaveBeenCalled();
  });

  it('connects and registers realtime handlers', async () => {
    const { result } = await renderRealtime();
    await waitFor(() => expect(result.current.connected).toBe(true));

    expect(mockBuilder.withUrl).toHaveBeenCalledWith(
      'http://10.0.2.2:5080/hubs/user',
      expect.objectContaining({ accessTokenFactory: expect.any(Function) })
    );
    expect(mockConnection.on).toHaveBeenCalledWith('NotificationReceived', expect.any(Function));
    expect(mockConnection.on).toHaveBeenCalledWith('CommandCreated', expect.any(Function));
    expect(mockConnection.on).toHaveBeenCalledWith('CommandCompleted', expect.any(Function));
    expect(mockConnection.on).toHaveBeenCalledWith(
      'InstallationStatusChanged',
      expect.any(Function)
    );
  });

  it('updates notification and command state', async () => {
    const { result } = await renderRealtime();
    await waitFor(() => expect(result.current.connected).toBe(true));

    await act(() => {
      handler('NotificationReceived')?.(notification);
    });
    expect(result.current.lastNotification).toEqual(notification);
    expect(result.current.notificationVersion).toBe(1);

    await act(() => {
      handler('CommandCreated')?.(command);
    });
    expect(result.current.lastCommand).toEqual(command);
    expect(result.current.commandVersion).toBe(1);

    await act(() => {
      handler('CommandCompleted')?.(command);
    });
    expect(result.current.commandVersion).toBe(2);
  });

  it('updates installation status and connection state', async () => {
    const { result } = await renderRealtime();
    await waitFor(() => expect(result.current.connected).toBe(true));

    await act(() => {
      handler('InstallationStatusChanged')?.(installation);
    });
    expect(updateInstallation).toHaveBeenCalledWith(installation);

    await act(() => {
      mockConnection.onreconnecting.mock.calls[0][0]();
    });
    expect(result.current.connected).toBe(false);

    await act(() => {
      mockConnection.onreconnected.mock.calls[0][0]();
    });
    expect(result.current.connected).toBe(true);

    await act(() => {
      mockConnection.onclose.mock.calls[0][0]();
    });
    expect(result.current.connected).toBe(false);
  });

  it('handles connection failures and cleanup', async () => {
    mockConnection.start.mockRejectedValueOnce(new Error('offline'));
    const { result, unmount } = await renderRealtime();
    await waitFor(() => expect(result.current.connected).toBe(false));

    await unmount();
    expect(mockConnection.stop).toHaveBeenCalled();
  });

  it('requires a provider', async () => {
    const spy = jest.spyOn(console, 'error').mockImplementation(() => undefined);
    await expect(renderHook(() => useRealtime())).rejects.toThrow(
      'useRealtime must be used inside RealtimeProvider.'
    );
    spy.mockRestore();
  });
});
