import { GestureHandlerRootView } from 'react-native-gesture-handler';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { AuthProvider } from '@/auth/AuthProvider';
import { I18nProvider } from '@/i18n/I18nProvider';
import { NotificationsProvider } from '@/notifications/NotificationsProvider';
import { RealtimeProvider } from '@/realtime/RealtimeProvider';
import { SelectionProvider } from '@/state/SelectionProvider';
import { colors } from '@/theme';

export default function RootLayout() {
  return (
    <GestureHandlerRootView style={{ flex: 1 }}>
      <SafeAreaProvider>
        <I18nProvider>
          <AuthProvider>
            <SelectionProvider>
              <RealtimeProvider>
                <NotificationsProvider>
                  <StatusBar style="dark" />
                  <Stack
                    screenOptions={{
                      headerShown: false,
                      contentStyle: { backgroundColor: colors.background }
                    }}
                  >
                    <Stack.Screen name="index" />
                    <Stack.Screen name="login" />
                    <Stack.Screen name="register" />
                    <Stack.Screen name="companies" />
                    <Stack.Screen name="installations" />
                    <Stack.Screen name="(tabs)" />
                    <Stack.Screen name="history" />
                  </Stack>
                </NotificationsProvider>
              </RealtimeProvider>
            </SelectionProvider>
          </AuthProvider>
        </I18nProvider>
      </SafeAreaProvider>
    </GestureHandlerRootView>
  );
}
