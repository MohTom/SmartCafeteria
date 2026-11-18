import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Platform } from 'react-native';
import Constants from 'expo-constants';
import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import { AndroidImportance } from 'expo-notifications';
import { useRouter } from 'expo-router';
import { api } from '@/api/client';
import { useAuth } from '@/auth/AuthProvider';

type NotificationValue = {
  pushEnabled: boolean;
  pushError: string | null;
  requestPushRegistration: () => Promise<void>;
};

const NotificationContext = createContext<NotificationValue | null>(null);

Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowBanner: true,
    shouldShowList: true,
    shouldPlaySound: true,
    shouldSetBadge: true
  })
});

export function NotificationsProvider({ children }: { children: ReactNode }) {
  const { status } = useAuth();
  const router = useRouter();
  const [pushEnabled, setPushEnabled] = useState(false);
  const [pushError, setPushError] = useState<string | null>(null);
  const registering = useRef(false);

  const requestPushRegistration = useCallback(async () => {
    if (registering.current || status !== 'signedIn') return;
    registering.current = true;
    setPushError(null);

    try {
      if (!Device.isDevice) {
        setPushError('Push notifications require a physical device.');
        return;
      }

      if (Platform.OS === 'android') {
        await Notifications.setNotificationChannelAsync('default', {
          name: 'Cafeteria alerts',
          importance: AndroidImportance.HIGH,
          vibrationPattern: [0, 250, 250, 250],
          lightColor: '#0F5132'
        });
      }

      let permission = await Notifications.getPermissionsAsync();
      if (permission.status !== 'granted') {
        permission = await Notifications.requestPermissionsAsync();
      }
      if (permission.status !== 'granted') {
        setPushError('Notification permission was not granted.');
        return;
      }

      const projectId =
        Constants.expoConfig?.extra?.eas?.projectId ??
        (Constants as { easConfig?: { projectId?: string } }).easConfig?.projectId;

      if (!projectId) {
        setPushError('Push notifications need an EAS project id.');
        return;
      }

      const token = await Notifications.getExpoPushTokenAsync({ projectId });
      await api.request<undefined>('/api/notifications/devices', {
        method: 'POST',
        body: JSON.stringify({
          expoPushToken: token.data,
          platform: Platform.OS
        })
      });
      setPushEnabled(true);
    } catch (error) {
      setPushEnabled(false);
      setPushError(error instanceof Error ? error.message : 'Could not register notifications.');
    } finally {
      registering.current = false;
    }
  }, [status]);

  useEffect(() => {
    if (status === 'signedIn') {
      void requestPushRegistration();
    }
  }, [status, requestPushRegistration]);

  useEffect(() => {
    const subscription = Notifications.addNotificationResponseReceivedListener(() => {
      router.push('/notifications');
    });
    return () => subscription.remove();
  }, [router]);

  const value = useMemo<NotificationValue>(
    () => ({ pushEnabled, pushError, requestPushRegistration }),
    [pushEnabled, pushError, requestPushRegistration]
  );

  return (
    <NotificationContext.Provider value={value}>{children}</NotificationContext.Provider>
  );
}

export function usePushNotifications(): NotificationValue {
  const value = useContext(NotificationContext);
  if (!value) throw new Error('usePushNotifications must be used inside NotificationsProvider.');
  return value;
}
