import { Capacitor, type PluginListenerHandle } from '@capacitor/core';
import { PushNotifications, type PushNotificationSchema } from '@capacitor/push-notifications';
import type { AdminPushEventOrigin, AdminPushStoredEvent } from '../types';
import { getApiBaseUrl } from './http';

const PUSH_TOKEN_STORAGE_KEY = 'conserta.admin.push.token';
const PUSH_INSTALLATION_ID_STORAGE_KEY = 'conserta.admin.push.installationId';
const PUSH_EVENTS_STORAGE_KEY = 'conserta.admin.push.events';
const PUSH_EVENTS_MAX_ITEMS = 120;
const ANDROID_DEFAULT_CHANNEL_ID = 'default';
const ANDROID_DEFAULT_CHANNEL_NAME = 'ConsertaPraMim Admin';

export interface AdminPushPayload {
  title: string;
  body: string;
  actionUrl?: string;
  notificationType?: string;
  rawData: Record<string, string>;
}

interface AdminPushCallbacks {
  onForegroundNotification?: (event: AdminPushStoredEvent) => void;
  onNotificationAction?: (event: AdminPushStoredEvent) => void;
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
    if (!key || value === null || value === undefined) {
      continue;
    }

    normalized[key] = typeof value === 'string' ? value : String(value);
  }

  return normalized;
}

function mapNotificationPayload(notification: PushNotificationSchema): AdminPushPayload {
  const data = normalizeData(notification.data);
  return {
    title: String(notification.title || data.title || 'Nova notificacao'),
    body: String(notification.body || data.body || 'Voce recebeu uma atualizacao.'),
    actionUrl: data.actionUrl || data.actionURL || data.url,
    notificationType: data.type,
    rawData: data
  };
}

function mapStoredPushEvent(raw: unknown): AdminPushStoredEvent | null {
  if (!raw || typeof raw !== 'object') {
    return null;
  }

  const source = raw as Record<string, unknown>;
  const id = typeof source.id === 'string' ? source.id.trim() : '';
  const title = typeof source.title === 'string' ? source.title.trim() : '';
  const body = typeof source.body === 'string' ? source.body.trim() : '';
  const createdAtIso = typeof source.createdAtIso === 'string' ? source.createdAtIso.trim() : '';
  const originRaw = typeof source.origin === 'string' ? source.origin.trim().toLowerCase() : '';
  const origin: AdminPushEventOrigin = originRaw === 'action' ? 'action' : 'foreground';

  if (!id || !title || !body || !createdAtIso) {
    return null;
  }

  return {
    id,
    title,
    body,
    createdAtIso,
    origin,
    actionUrl: typeof source.actionUrl === 'string' ? source.actionUrl : undefined,
    notificationType: typeof source.notificationType === 'string' ? source.notificationType : undefined,
    rawData: normalizeData(source.rawData)
  };
}

function readStoredPushEvents(): AdminPushStoredEvent[] {
  const raw = localStorage.getItem(PUSH_EVENTS_STORAGE_KEY);
  if (!raw) {
    return [];
  }

  try {
    const parsed = JSON.parse(raw);
    if (!Array.isArray(parsed)) {
      return [];
    }

    const items = parsed
      .map((entry) => mapStoredPushEvent(entry))
      .filter((entry): entry is AdminPushStoredEvent => Boolean(entry))
      .sort((left, right) => {
        return new Date(right.createdAtIso).getTime() - new Date(left.createdAtIso).getTime();
      });

    return items.slice(0, PUSH_EVENTS_MAX_ITEMS);
  } catch {
    return [];
  }
}

function persistStoredPushEvents(items: AdminPushStoredEvent[]): void {
  localStorage.setItem(PUSH_EVENTS_STORAGE_KEY, JSON.stringify(items.slice(0, PUSH_EVENTS_MAX_ITEMS)));
}

function buildPushEventId(): string {
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 9)}`;
}

function appendPushEvent(payload: AdminPushPayload, origin: AdminPushEventOrigin): AdminPushStoredEvent {
  const nowIso = new Date().toISOString();
  const event: AdminPushStoredEvent = {
    id: buildPushEventId(),
    title: payload.title,
    body: payload.body,
    actionUrl: payload.actionUrl,
    notificationType: payload.notificationType,
    createdAtIso: nowIso,
    origin,
    rawData: payload.rawData
  };

  const existing = readStoredPushEvents();
  existing.unshift(event);
  persistStoredPushEvents(existing);
  return event;
}

export function listAdminPushStoredEvents(limit = 60): AdminPushStoredEvent[] {
  const safeLimit = Number.isFinite(limit) ? Math.max(1, Math.min(200, Math.floor(limit))) : 60;
  return readStoredPushEvents().slice(0, safeLimit);
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
  const response = await fetch(`${getApiBaseUrl()}/api/mobile/admin/push-devices/register`, {
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
  const response = await fetch(`${getApiBaseUrl()}/api/mobile/admin/push-devices/unregister`, {
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
      description: 'Canal padrao de notificacoes do admin.',
      importance: 5,
      visibility: 1,
      sound: 'default'
    });
  } catch {
    // best effort
  }
}

export async function initializeAdminPushNotifications(
  accessToken: string,
  callbacks: AdminPushCallbacks = {}): Promise<void> {
  if (!isNativeRuntime() || !accessToken) {
    return;
  }

  getOrCreateInstallationId();

  if (initialized) {
    await teardownAdminPushNotifications();
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
    const event = appendPushEvent(mapNotificationPayload(notification), 'foreground');
    callbacks.onForegroundNotification?.(event);
  }));

  listeners.push(await PushNotifications.addListener('pushNotificationActionPerformed', ({ notification }) => {
    const event = appendPushEvent(mapNotificationPayload(notification), 'action');
    callbacks.onNotificationAction?.(event);
  }));

  await PushNotifications.register();
  initialized = true;
}

export async function teardownAdminPushNotifications(): Promise<void> {
  for (const listener of listeners) {
    await listener.remove();
  }

  listeners = [];
  initialized = false;
}

export async function unregisterAdminPushNotifications(accessToken: string): Promise<void> {
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

  await teardownAdminPushNotifications();
}
