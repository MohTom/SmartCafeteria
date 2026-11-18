import type { ReactNode } from 'react';
import { act, renderHook, waitFor } from '@testing-library/react-native';
import { api } from '@/api/client';
import { useAuth } from '@/auth/AuthProvider';
import {
  NotificationsProvider,
  usePushNotifications
} from '@/notifications/NotificationsProvider';
import * as Notifications from 'expo-notifications';

const mockRouterPush = jest.fn();
const mockRemove = jest.fn();
const mockDevice = { isDevice: true };
const mockConstants: { expoConfig?: { extra?: { eas?: { projectId?: string } } } } = {
  expoConfig: { extra: { eas: { projectId: 'project-1' } } }
};

jest.mock('expo-device', () => ({
  __esModule: true,
  get isDevice() {
    return mockDevice.isDevice;
  }
}));

jest.mock('expo-constants', () => ({
  __esModule: true,
  get default() {
    return mockConstants;
  }
}));

jest.mock('expo-router', () => ({
  useRouter: () => ({ push: mockRouterPush })
}));

jest.mock('expo-notifications', () => ({
  AndroidImportance: { HIGH: 4 },
  setNotificationHandler: jest.fn(),
  setNotificationChannelAsync: jest.fn().mockResolvedValue(undefined),
  getPermissionsAsync: jest.fn(),
  requestPermissionsAsync: jest.fn(),
  getExpoPushTokenAsync: jest.fn(),
  addNotificationResponseReceivedListener: jest.fn(() => ({ remove: mockRemove }))
}));

jest.mock('@/auth/AuthProvider', () => ({
  useAuth: jest.fn()
}));

function wrapper({ children }: { children: ReactNode }) {
  return <NotificationsProvider>{children}</NotificationsProvider>;
}

async function renderNotifications() {
  return renderHook(() => usePushNotifications(), { wrapper });
}

describe('NotificationsProvider', () => {
  let request: jest.Mock;

  beforeEach(() => {
    jest.clearAllMocks();
    mockDevice.isDevice = true;
    mockConstants.expoConfig = { extra: { eas: { projectId: 'project-1' } } };
    request = jest.spyOn(api, 'request') as jest.Mock;
    request.mockResolvedValue(undefined);
    (useAuth as jest.Mock).mockReturnValue({ status: 'signedIn' });
    (Notifications.getPermissionsAsync as jest.Mock).mockResolvedValue({ status: 'granted' });
    (Notifications.requestPermissionsAsync as jest.Mock).mockResolvedValue({ status: 'granted' });
    (Notifications.getExpoPushTokenAsync as jest.Mock).mockResolvedValue({
      data: 'ExponentPushToken[test]'
    });
    (Notifications.addNotificationResponseReceivedListener as jest.Mock).mockReturnValue({
      remove: mockRemove
    });
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('does not register while signed out', async () => {
    (useAuth as jest.Mock).mockReturnValue({ status: 'signedOut' });
    const { result } = await renderNotifications();
    await waitFor(() => expect(result.current.pushEnabled).toBe(false));
    expect(request).not.toHaveBeenCalled();
  });

  it('requires a physical device', async () => {
    mockDevice.isDevice = false;
    const { result } = await renderNotifications();
    await waitFor(() =>
      expect(result.current.pushError).toBe('Push notifications require a physical device.')
    );
    expect(request).not.toHaveBeenCalled();
  });

  it('requests denied permission', async () => {
    (Notifications.getPermissionsAsync as jest.Mock).mockResolvedValue({ status: 'denied' });
    (Notifications.requestPermissionsAsync as jest.Mock).mockResolvedValue({ status: 'denied' });

    const { result } = await renderNotifications();
    await waitFor(() =>
      expect(result.current.pushError).toBe('Notification permission was not granted.')
    );
  });

  it('requires an EAS project id', async () => {
    mockConstants.expoConfig = undefined;
    const { result } = await renderNotifications();
    await waitFor(() => expect(result.current.pushError).toBe('Push notifications need an EAS project id.'));
  });

  it('registers a push token with the API', async () => {
    const { result } = await renderNotifications();
    await waitFor(() => expect(result.current.pushEnabled).toBe(true));

    expect(Notifications.getExpoPushTokenAsync).toHaveBeenCalledWith({ projectId: 'project-1' });
    const registration = request.mock.calls.find(
      ([path]) => path === '/api/notifications/devices'
    );
    expect(registration).toBeDefined();
    expect(registration?.[1].method).toBe('POST');
    expect(JSON.parse(registration?.[1].body as string)).toEqual({
      expoPushToken: 'ExponentPushToken[test]',
      platform: expect.any(String)
    });
    expect(result.current.pushError).toBeNull();
  });

  it('reports API registration failures', async () => {
    request.mockRejectedValue(new Error('network down'));
    const { result } = await renderNotifications();
    await waitFor(() => expect(result.current.pushError).toBe('network down'));
    expect(result.current.pushEnabled).toBe(false);
  });

  it('routes notification taps and removes listeners on unmount', async () => {
    const { unmount } = await renderNotifications();
    const listener = (Notifications.addNotificationResponseReceivedListener as jest.Mock).mock
      .calls[0][0];

    await act(() => {
      listener();
    });
    expect(mockRouterPush).toHaveBeenCalledWith('/notifications');

    await unmount();
    expect(mockRemove).toHaveBeenCalled();
  });

  it('requires a provider', async () => {
    const spy = jest.spyOn(console, 'error').mockImplementation(() => undefined);
    await expect(renderHook(() => usePushNotifications())).rejects.toThrow(
      'usePushNotifications must be used inside NotificationsProvider.'
    );
    spy.mockRestore();
  });
});
