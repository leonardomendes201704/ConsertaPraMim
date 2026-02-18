import { Capacitor, type PluginListenerHandle } from '@capacitor/core';
import { PushNotifications, type PushNotificationSchema } from '@capacitor/push-notifications';
import { getApiBaseUrl } from './auth';

const PUSH_TOKEN_STORAGE_KEY = 'conserta.provider.push.token';

export interface ProviderPushPayload {
  title: string;
  body: string;
  actionUrl?: string;
  requestId?: string;
  providerId?: string;
  counterpartName?: string;
  notificationType?: string;
  rawData: Record<string, string>;
}

interface ProviderPushCallbacks {
  onForegroundNotification?: (payload: ProviderPushPayload) => void;
  onNotificationAction?: (payload: ProviderPushPayload) => void;
  onError?: (message: string) => void;
}

let listeners: PluginListenerHandle[] = [];
let initialized = false;

function isNativeRuntime(): boolean {
  return Capacitor.getPlatform() !== 'web';
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

function mapNotificationPayload(notification: PushNotificationSchema): ProviderPushPayload {
  const data = normalizeData(notification.data);
  return {
    title: String(notification.title || data.title || 'Nova notificacao'),
    body: String(notification.body || data.body || 'Voce recebeu uma atualizacao.'),
    actionUrl: data.actionUrl || data.actionURL || data.url,
    requestId: data.requestId,
    providerId: data.providerId,
    counterpartName: data.senderName || data.counterpartName || data.clientName,
    notificationType: data.type,
    rawData: data
  };
}

async function registerTokenOnBackend(accessToken: string, token: string): Promise<void> {
  const response = await fetch(`${getApiBaseUrl()}/api/mobile/provider/push-devices/register`, {
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
    throw new Error(`Push register failed with status ${response.status}`);
  }
}

async function unregisterTokenOnBackend(accessToken: string, token: string): Promise<void> {
  const response = await fetch(`${getApiBaseUrl()}/api/mobile/provider/push-devices/unregister`, {
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
    throw new Error(`Push unregister failed with status ${response.status}`);
  }
}

export async function initializeProviderPushNotifications(
  accessToken: string,
  callbacks: ProviderPushCallbacks = {}): Promise<void> {
  if (!isNativeRuntime() || !accessToken) {
    return;
  }

  if (initialized) {
    await teardownProviderPushNotifications();
  }

  const permission = await PushNotifications.requestPermissions();
  if (permission.receive !== 'granted') {
    callbacks.onError?.('Permissao de notificacao push negada no dispositivo.');
    return;
  }

  listeners.push(await PushNotifications.addListener('registration', async (token) => {
    try {
      localStorage.setItem(PUSH_TOKEN_STORAGE_KEY, token.value);
      await registerTokenOnBackend(accessToken, token.value);
    } catch {
      callbacks.onError?.('Falha ao registrar token push no backend.');
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

export async function teardownProviderPushNotifications(): Promise<void> {
  for (const listener of listeners) {
    await listener.remove();
  }

  listeners = [];
  initialized = false;
}

export async function unregisterProviderPushNotifications(accessToken: string): Promise<void> {
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

  await teardownProviderPushNotifications();
}
