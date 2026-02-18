import { Capacitor } from '@capacitor/core';
import { LocalNotifications } from '@capacitor/local-notifications';

const CLIENT_REALTIME_CHANNEL_ID = 'cpm_client_realtime';

let initialized = false;

function isNativeRuntime(): boolean {
  return Capacitor.getPlatform() !== 'web';
}

function buildNotificationId(): number {
  return Math.floor(Date.now() % 2147483000);
}

export async function initializeClientRealtimeLocalNotifications(): Promise<void> {
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
        id: CLIENT_REALTIME_CHANNEL_ID,
        name: 'Notificacoes em tempo real',
        description: 'Alertas sonoros para eventos recebidos via SignalR.',
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

export async function notifyClientRealtimeMessage(
  title: string,
  body: string,
  extra?: Record<string, string>): Promise<void> {
  if (!isNativeRuntime()) {
    return;
  }

  if (!initialized) {
    await initializeClientRealtimeLocalNotifications();
  }

  if (!initialized) {
    return;
  }

  await LocalNotifications.schedule({
    notifications: [
      {
        id: buildNotificationId(),
        title: String(title || 'Nova notificacao'),
        body: String(body || 'Voce recebeu uma atualizacao em tempo real.'),
        channelId: CLIENT_REALTIME_CHANNEL_ID,
        extra
      }
    ]
  });
}
