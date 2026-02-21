import { Capacitor, type PluginListenerHandle } from '@capacitor/core';
import { PushNotifications, type PushNotificationSchema } from '@capacitor/push-notifications';
import { getApiBaseUrl } from './auth';

const PUSH_TOKEN_STORAGE_KEY = 'conserta.client.push.token';
const ANDROID_DEFAULT_CHANNEL_ID = 'default';
const ANDROID_DEFAULT_CHANNEL_NAME = 'ConsertaPraMim';

export interface ClientPushPayload {
  title: string;
  body: string;
  actionUrl?: string;
  requestId?: string;
  providerId?: string;
  providerName?: string;
  notificationType?: string;
  rawData: Record<string, string>;
}

interface ClientPushCallbacks {
  onForegroundNotification?: (payload: ClientPushPayload) => void;
  onNotificationAction?: (payload: ClientPushPayload) => void;
  onError?: (message: string) => void;
}

let listeners: PluginListenerHandle[] = [];
let initialized = false;

function isNativeRuntime(): boolean {
  return Capacitor.getPlatform() !== 'web';
}

function mapNotificationPayload(notification: PushNotificationSchema): ClientPushPayload {
  const data = normalizeData(notification.data);

  return {
    title: String(notification.title || data.title || 'Nova notificacao'),
    body: String(notification.body || data.body || 'Voce recebeu uma atualizacao.'),
    actionUrl: data.actionUrl || data.actionURL || data.url,
    requestId: data.requestId,
    providerId: data.providerId,
    providerName: data.providerName || data.senderName,
    notificationType: data.type,
    rawData: data
  };
}

function normalizeData(raw: unknown): Record<string, string> {
  if (!raw || typeof raw !== 'object') {
    return {};
  }

  const normalized: Record<string, string> = {};
  for (const [key, value] of Object.entries(raw as Record<string, unknown>)) {
    if (!key) {
      continue;
    }

    if (typeof value === 'string') {
      normalized[key] = value;
      continue;
    }

    if (value === null || value === undefined) {
      continue;
    }

    normalized[key] = String(value);
  }

  return normalized;
}

async function registerTokenOnBackend(accessToken: string, token: string): Promise<void> {
  const response = await fetch(`${getApiBaseUrl()}/api/mobile/client/push-devices/register`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`
    },
    body: JSON.stringify({
      token,
      platform: Capacitor.getPlatform()
    })
  });

  if (!response.ok) {
    const details = await response.text();
    throw new Error(`Push register failed with status ${response.status}. ${details || 'No response body.'}`);
  }
}

async function unregisterTokenOnBackend(accessToken: string, token: string): Promise<void> {
  const response = await fetch(`${getApiBaseUrl()}/api/mobile/client/push-devices/unregister`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`
    },
    body: JSON.stringify({
      token
    })
  });

  if (!response.ok) {
    const details = await response.text();
    throw new Error(`Push unregister failed with status ${response.status}. ${details || 'No response body.'}`);
  }
}

async function ensureAndroidDefaultChannel(): Promise<void> {
  if (Capacitor.getPlatform() !== 'android') {
    return;
  }

  try {
    await PushNotifications.createChannel({
      id: ANDROID_DEFAULT_CHANNEL_ID,
      name: ANDROID_DEFAULT_CHANNEL_NAME,
      description: 'Canal padrao para notificacoes do ConsertaPraMim.',
      importance: 5,
      visibility: 1,
      sound: 'default'
    });
  } catch {
    // best effort
  }
}

export async function initializeClientPushNotifications(
  accessToken: string,
  callbacks: ClientPushCallbacks = {}): Promise<void> {
  if (!isNativeRuntime() || !accessToken) {
    return;
  }

  if (initialized) {
    await teardownClientPushNotifications();
  }

  await ensureAndroidDefaultChannel();

  const permission = await PushNotifications.requestPermissions();
  if (permission.receive !== 'granted') {
    callbacks.onError?.('Permissao de notificacao push negada no dispositivo.');
    return;
  }

  listeners.push(await PushNotifications.addListener('registration', async (token) => {
    try {
      localStorage.setItem(PUSH_TOKEN_STORAGE_KEY, token.value);
      await registerTokenOnBackend(accessToken, token.value);
    } catch (error) {
      const details = error instanceof Error ? error.message : 'Erro desconhecido.';
      callbacks.onError?.(`Falha ao registrar token push no backend. ${details}`);
    }
  }));

  listeners.push(await PushNotifications.addListener('registrationError', (error) => {
    callbacks.onError?.(`Erro ao registrar push no dispositivo: ${error.error}`);
  }));

  listeners.push(await PushNotifications.addListener('pushNotificationReceived', (notification) => {
    callbacks.onForegroundNotification?.(mapNotificationPayload(notification));
  }));

  listeners.push(await PushNotifications.addListener('pushNotificationActionPerformed', ({ notification }) => {
    callbacks.onNotificationAction?.(mapNotificationPayload(notification));
  }));

  await PushNotifications.register();
  initialized = true;
}

export async function teardownClientPushNotifications(): Promise<void> {
  for (const listener of listeners) {
    await listener.remove();
  }

  listeners = [];
  initialized = false;
}

export async function unregisterClientPushNotifications(accessToken: string): Promise<void> {
  if (!isNativeRuntime()) {
    return;
  }

  const token = localStorage.getItem(PUSH_TOKEN_STORAGE_KEY);
  if (token && accessToken) {
    try {
      await unregisterTokenOnBackend(accessToken, token);
    } catch {
      // best effort
    }
  }

  localStorage.removeItem(PUSH_TOKEN_STORAGE_KEY);

  try {
    await PushNotifications.unregister();
  } catch {
    // best effort
  }

  await teardownClientPushNotifications();
}
