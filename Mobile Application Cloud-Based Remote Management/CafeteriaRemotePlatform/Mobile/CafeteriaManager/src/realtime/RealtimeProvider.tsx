import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode
} from 'react';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { api } from '@/api/client';
import type { Installation, NotificationItem, RemoteCommand } from '@/api/types';
import { useAuth } from '@/auth/AuthProvider';
import { useSelection } from '@/state/SelectionProvider';

type RealtimeValue = {
  connected: boolean;
  notificationVersion: number;
  commandVersion: number;
  lastNotification: NotificationItem | null;
  lastCommand: RemoteCommand | null;
};

const RealtimeContext = createContext<RealtimeValue | null>(null);

export function RealtimeProvider({ children }: { children: ReactNode }) {
  const { status, tokens } = useAuth();
  const { updateInstallation } = useSelection();
  const [connected, setConnected] = useState(false);
  const [notificationVersion, setNotificationVersion] = useState(0);
  const [commandVersion, setCommandVersion] = useState(0);
  const [lastNotification, setLastNotification] = useState<NotificationItem | null>(null);
  const [lastCommand, setLastCommand] = useState<RemoteCommand | null>(null);
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    if (status !== 'signedIn' || !tokens) {
      setConnected(false);
      return;
    }

    const connection = new HubConnectionBuilder()
      .withUrl(`${api.getBaseUrl()}/hubs/user`, {
        accessTokenFactory: () => api.getTokens()?.accessToken ?? ''
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.None)
      .build();

    connectionRef.current = connection;

    connection.on('NotificationReceived', (notification: NotificationItem) => {
      setLastNotification(notification);
      setNotificationVersion((value) => value + 1);
    });

    connection.on('CommandCreated', (command: RemoteCommand) => {
      setLastCommand(command);
      setCommandVersion((value) => value + 1);
    });

    connection.on('CommandCompleted', (command: RemoteCommand) => {
      setLastCommand(command);
      setCommandVersion((value) => value + 1);
    });

    connection.on('InstallationStatusChanged', (installation: Installation) => {
      updateInstallation(installation);
    });

    connection.onreconnecting(() => setConnected(false));
    connection.onreconnected(() => setConnected(true));
    connection.onclose(() => setConnected(false));

    let active = true;
    connection
      .start()
      .then(() => {
        if (active) setConnected(true);
      })
      .catch(() => {
        if (active) setConnected(false);
      });

    return () => {
      active = false;
      connectionRef.current = null;
      void connection.stop();
    };
  }, [status, tokens, updateInstallation]);

  const value = useMemo<RealtimeValue>(
    () => ({
      connected,
      notificationVersion,
      commandVersion,
      lastNotification,
      lastCommand
    }),
    [connected, notificationVersion, commandVersion, lastNotification, lastCommand]
  );

  return <RealtimeContext.Provider value={value}>{children}</RealtimeContext.Provider>;
}

export function useRealtime(): RealtimeValue {
  const value = useContext(RealtimeContext);
  if (!value) throw new Error('useRealtime must be used inside RealtimeProvider.');
  return value;
}
