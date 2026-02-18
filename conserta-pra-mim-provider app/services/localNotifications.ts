import { Capacitor } from '@capacitor/core';
import { LocalNotifications } from '@capacitor/local-notifications';

const PROVIDER_REALTIME_CHANNEL_ID = 'cpm_provider_realtime';

let initialized = false;

function isNativeRuntime(): boolean {
  return Capacitor.getPlatform() !== 'web';
}

function buildNotificationId(): number {
  return Math.floor(Date.now() % 2147483000);
}

export async function initializeProviderRealtimeLocalNotifications(): Promise<void> {
  if (!isNativeRuntime() || initialized) {
    return;
  }

  const permission = await LocalNotifications.requestPermissions();
  if (permission.display !== 'granted') {
    return;
  }

  if (Capacitor.getPlatform() === 'android') {
    try {
      await LocalNotifications.createChannel({
        id: PROVIDER_REALTIME_CHANNEL_ID,
        name: 'Mensagens em tempo real',
        description: 'Alertas sonoros para mensagens recebidas via SignalR.',
        importance: 5,
        visibility: 1,
        vibration: true,
        lights: true
      });
    } catch {
      // best effort: canal pode ja existir
    }
  }

  initialized = true;
}

export async function notifyProviderRealtimeMessage(
  title: string,
  body: string,
  extra?: Record<string, string>): Promise<void> {
  if (!isNativeRuntime()) {
    return;
  }

  if (!initialized) {
    await initializeProviderRealtimeLocalNotifications();
  }

  if (!initialized) {
    return;
  }

  await LocalNotifications.schedule({
    notifications: [
      {
        id: buildNotificationId(),
        title: String(title || 'Nova mensagem'),
        body: String(body || 'Voce recebeu uma mensagem em tempo real.'),
        channelId: PROVIDER_REALTIME_CHANNEL_ID,
        extra
      }
    ]
  });
}
