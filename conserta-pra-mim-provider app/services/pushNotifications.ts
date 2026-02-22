import { Capacitor, type PluginListenerHandle } from '@capacitor/core';
import { PushNotifications, type PushNotificationSchema } from '@capacitor/push-notifications';
import { getApiBaseUrl } from './auth';

const PUSH_TOKEN_STORAGE_KEY = 'conserta.provider.push.token';
const PUSH_INSTALLATION_ID_STORAGE_KEY = 'conserta.provider.push.installationId';
const ANDROID_DEFAULT_CHANNEL_ID = 'default';
const ANDROID_DEFAULT_CHANNEL_NAME = 'ConsertaPraMim';

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

function buildPseudoUuid(): string {
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 12)}`;
}

function getOrCreateInstallationId(): string {
  const existing = localStorage.getItem(PUSH_INSTALLATION_ID_STORAGE_KEY);
  if (existing) {
    return existing;
  }

  const generated = typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
    ? crypto.randomUUID()
    : buildPseudoUuid();

  localStorage.setItem(PUSH_INSTALLATION_ID_STORAGE_KEY, generated);
  return generated;
}

function readDeviceModel(): string | undefined {
  if (typeof navigator === 'undefined' || typeof navigator.userAgent !== 'string') {
    return undefined;
  }

  const userAgent = navigator.userAgent.trim();
  if (!userAgent) {
    return undefined;
  }

  return userAgent.length > 200 ? userAgent.slice(0, 200) : userAgent;
}

function readTimeZone(): string | undefined {
  try {
    const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
    if (!timeZone || typeof timeZone !== 'string') {
      return undefined;
    }

    return timeZone.length > 128 ? timeZone.slice(0, 128) : timeZone;
  } catch {
    return undefined;
  }
}

function readAppVersion(): string | undefined {
  const raw = (import.meta.env?.VITE_APP_VERSION ?? import.meta.env?.VITE_APP_BUILD_VERSION ?? '').toString().trim();
  if (!raw) {
    return undefined;
  }

  return raw.length > 64 ? raw.slice(0, 64) : raw;
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

function buildPushDevicePayload(token: string): Record<string, unknown> {
  const installationId = getOrCreateInstallationId();

  return {
    token,
    platform: Capacitor.getPlatform(),
    installationId,
    deviceId: installationId,
    deviceModel: readDeviceModel(),
    appVersion: readAppVersion(),
    timeZone: readTimeZone()
  };
}

async function registerTokenOnBackend(accessToken: string, token: string): Promise<void> {
  const response = await fetch(`${getApiBaseUrl()}/api/mobile/provider/push-devices/register`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`
    },
    body: JSON.stringify(buildPushDevicePayload(token))
  });

  if (!response.ok) {
    const details = await response.text();
    throw new Error(`Push register failed with status ${response.status}. ${details || 'No response body.'}`);
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
      token,
      installationId: getOrCreateInstallationId()
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

export async function initializeProviderPushNotifications(
  accessToken: string,
  callbacks: ProviderPushCallbacks = {}): Promise<void> {
  if (!isNativeRuntime() || !accessToken) {
    return;
  }

  getOrCreateInstallationId();

  if (initialized) {
    await teardownProviderPushNotifications();
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
