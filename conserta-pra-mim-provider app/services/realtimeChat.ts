import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import {
  ProviderChatConversationSummary,
  ProviderChatMessage,
  ProviderChatMessageReceipt
} from '../types';
import { getApiBaseUrl } from './auth';

type ProviderChatRealtimeHandlers = {
  onChatMessage?: (message: ProviderChatMessage) => void;
  onMessageReceiptUpdated?: (receipt: ProviderChatMessageReceipt) => void;
  onUserPresence?: (payload: { userId: string; isOnline: boolean }) => void;
  onProviderStatus?: (payload: { providerId: string; status: string }) => void;
};

let connection: HubConnection | null = null;
let connectionToken = '';
const subscribers = new Set<ProviderChatRealtimeHandlers>();
const joinedConversations = new Set<string>();

function buildChatHubUrl(): string {
  return `${getApiBaseUrl()}/chatHub`;
}

function buildConversationKey(requestId: string, providerId: string): string {
  return `${requestId.toLowerCase()}::${providerId.toLowerCase()}`;
}

function readString(source: Record<string, unknown>, ...keys: string[]): string {
  for (const key of keys) {
    const value = source[key];
    if (typeof value === 'string' && value.trim()) {
      return value.trim();
    }
  }

  return '';
}

function readNumber(source: Record<string, unknown>, ...keys: string[]): number {
  for (const key of keys) {
    const value = Number(source[key]);
    if (Number.isFinite(value)) {
      return value;
    }
  }

  return 0;
}

function readBoolean(source: Record<string, unknown>, ...keys: string[]): boolean {
  for (const key of keys) {
    if (typeof source[key] === 'boolean') {
      return Boolean(source[key]);
    }
  }

  return false;
}

function normalizeUtcTimestamp(value: string): string {
  const trimmed = String(value || '').trim();
  if (!trimmed) {
    return new Date().toISOString();
  }

  // If backend sends DateTime without timezone (SQL datetime2), assume UTC.
  const hasTimezone = /([zZ]|[+-]\d{2}:\d{2})$/.test(trimmed);
  return hasTimezone ? trimmed : `${trimmed}Z`;
}

function normalizeChatMessage(raw: unknown): ProviderChatMessage | null {
  const payload = raw as Record<string, unknown>;
  const id = readString(payload, 'id', 'Id');
  const requestId = readString(payload, 'requestId', 'RequestId');
  const providerId = readString(payload, 'providerId', 'ProviderId');
  const senderId = readString(payload, 'senderId', 'SenderId');
  if (!id || !requestId || !providerId || !senderId) {
    return null;
  }

  const rawAttachments = payload.attachments || payload.Attachments;
  const attachments = (Array.isArray(rawAttachments) ? rawAttachments : [])
    .map((attachment): ProviderChatMessage['attachments'][number] | null => {
      const source = attachment as Record<string, unknown>;
      const fileUrl = readString(source, 'fileUrl', 'FileUrl');
      if (!fileUrl) {
        return null;
      }

      return {
        id: readString(source, 'id', 'Id') || undefined,
        fileUrl,
        fileName: readString(source, 'fileName', 'FileName') || 'Arquivo',
        contentType: readString(source, 'contentType', 'ContentType') || 'application/octet-stream',
        sizeBytes: readNumber(source, 'sizeBytes', 'SizeBytes'),
        mediaKind: readString(source, 'mediaKind', 'MediaKind') || 'file'
      };
    })
    .filter((item): item is NonNullable<typeof item> => item !== null);

  return {
    id,
    requestId,
    providerId,
    senderId,
    senderName: readString(payload, 'senderName', 'SenderName') || 'Contato',
    senderRole: readString(payload, 'senderRole', 'SenderRole') || 'User',
    text: readString(payload, 'text', 'Text') || undefined,
    createdAt: normalizeUtcTimestamp(readString(payload, 'createdAt', 'CreatedAt') || new Date().toISOString()),
    attachments,
    deliveredAt: (() => {
      const value = readString(payload, 'deliveredAt', 'DeliveredAt');
      return value ? normalizeUtcTimestamp(value) : undefined;
    })(),
    readAt: (() => {
      const value = readString(payload, 'readAt', 'ReadAt');
      return value ? normalizeUtcTimestamp(value) : undefined;
    })()
  };
}

function normalizeReceipt(raw: unknown): ProviderChatMessageReceipt | null {
  const payload = raw as Record<string, unknown>;
  const messageId = readString(payload, 'messageId', 'MessageId');
  const requestId = readString(payload, 'requestId', 'RequestId');
  const providerId = readString(payload, 'providerId', 'ProviderId');
  if (!messageId || !requestId || !providerId) {
    return null;
  }

  return {
    messageId,
    requestId,
    providerId,
    deliveredAt: (() => {
      const value = readString(payload, 'deliveredAt', 'DeliveredAt');
      return value ? normalizeUtcTimestamp(value) : undefined;
    })(),
    readAt: (() => {
      const value = readString(payload, 'readAt', 'ReadAt');
      return value ? normalizeUtcTimestamp(value) : undefined;
    })()
  };
}

function mapConversation(raw: unknown): ProviderChatConversationSummary | null {
  const payload = raw as Record<string, unknown>;
  const requestId = readString(payload, 'requestId', 'RequestId');
  const providerId = readString(payload, 'providerId', 'ProviderId');
  const counterpartUserId = readString(payload, 'counterpartUserId', 'CounterpartUserId');
  if (!requestId || !providerId || !counterpartUserId) {
    return null;
  }

  return {
    requestId,
    providerId,
    counterpartUserId,
    counterpartRole: readString(payload, 'counterpartRole', 'CounterpartRole') || 'Client',
    counterpartName: readString(payload, 'counterpartName', 'CounterpartName') || 'Cliente',
    title: readString(payload, 'title', 'Title') || 'Conversa',
    lastMessagePreview: readString(payload, 'lastMessagePreview', 'LastMessagePreview') || 'Sem mensagens.',
    lastMessageAt: normalizeUtcTimestamp(readString(payload, 'lastMessageAt', 'LastMessageAt') || new Date().toISOString()),
    unreadMessages: readNumber(payload, 'unreadMessages', 'UnreadMessages'),
    counterpartIsOnline: readBoolean(payload, 'counterpartIsOnline', 'CounterpartIsOnline'),
    providerStatus: readString(payload, 'providerStatus', 'ProviderStatus') || undefined
  };
}

function notifyChatMessage(raw: unknown): void {
  const message = normalizeChatMessage(raw);
  if (!message) {
    return;
  }

  subscribers.forEach((subscriber) => {
    subscriber.onChatMessage?.(message);
  });
}

function notifyReceipt(raw: unknown): void {
  const receipt = normalizeReceipt(raw);
  if (!receipt) {
    return;
  }

  subscribers.forEach((subscriber) => {
    subscriber.onMessageReceiptUpdated?.(receipt);
  });
}

function notifyUserPresence(raw: unknown): void {
  const payload = raw as Record<string, unknown>;
  const userId = readString(payload, 'userId', 'UserId');
  if (!userId) {
    return;
  }

  const isOnline = readBoolean(payload, 'isOnline', 'IsOnline');
  subscribers.forEach((subscriber) => {
    subscriber.onUserPresence?.({ userId, isOnline });
  });
}

function notifyProviderStatus(raw: unknown): void {
  const payload = raw as Record<string, unknown>;
  const providerId = readString(payload, 'providerId', 'ProviderId');
  const status = readString(payload, 'status', 'Status');
  if (!providerId) {
    return;
  }

  subscribers.forEach((subscriber) => {
    subscriber.onProviderStatus?.({ providerId, status });
  });
}

async function recreateConnection(accessToken: string): Promise<HubConnection> {
  if (connection) {
    try {
      connection.off('ReceiveChatMessage');
      connection.off('ReceiveMessageReceiptUpdated');
      connection.off('ReceiveUserPresence');
      connection.off('ReceiveProviderStatus');
      await connection.stop();
    } catch {
      // ignore close errors
    }
  }

  const hubConnection = new HubConnectionBuilder()
    .withUrl(buildChatHubUrl(), {
      accessTokenFactory: () => accessToken
    })
    .withAutomaticReconnect()
    .build();

  hubConnection.on('ReceiveChatMessage', notifyChatMessage);
  hubConnection.on('ReceiveMessageReceiptUpdated', notifyReceipt);
  hubConnection.on('ReceiveUserPresence', notifyUserPresence);
  hubConnection.on('ReceiveProviderStatus', notifyProviderStatus);

  hubConnection.onreconnected(async () => {
    try {
      await hubConnection.invoke('JoinPersonalGroup');
      for (const key of Array.from(joinedConversations)) {
        const [requestId, providerId] = key.split('::');
        if (!requestId || !providerId) {
          continue;
        }

        await hubConnection.invoke('JoinRequestChat', requestId, providerId);
      }
    } catch {
      // ignore and keep auto-reconnect
    }
  });

  await hubConnection.start();
  await hubConnection.invoke('JoinPersonalGroup');

  connection = hubConnection;
  connectionToken = accessToken;
  return hubConnection;
}

async function ensureConnection(accessToken: string): Promise<HubConnection> {
  if (!connection || !connectionToken || connectionToken !== accessToken) {
    return recreateConnection(accessToken);
  }

  if (connection.state === HubConnectionState.Connected) {
    return connection;
  }

  if (connection.state === HubConnectionState.Disconnected) {
    await connection.start();
    await connection.invoke('JoinPersonalGroup');
  }

  return connection;
}

export function subscribeToProviderRealtimeChatEvents(handlers: ProviderChatRealtimeHandlers): () => void {
  subscribers.add(handlers);
  return () => {
    subscribers.delete(handlers);
  };
}

export async function startProviderRealtimeChatConnection(accessToken: string): Promise<void> {
  await ensureConnection(accessToken);
}

export async function stopProviderRealtimeChatConnection(): Promise<void> {
  joinedConversations.clear();
  if (!connection) {
    return;
  }

  connection.off('ReceiveChatMessage');
  connection.off('ReceiveMessageReceiptUpdated');
  connection.off('ReceiveUserPresence');
  connection.off('ReceiveProviderStatus');

  try {
    await connection.stop();
  } finally {
    connection = null;
    connectionToken = '';
  }
}

export async function getProviderRealtimeConversations(accessToken: string): Promise<ProviderChatConversationSummary[]> {
  const hubConnection = await ensureConnection(accessToken);
  const raw = await hubConnection.invoke('GetMyActiveConversations');
  const list = Array.isArray(raw) ? raw : [];
  return list
    .map(mapConversation)
    .filter((item): item is NonNullable<typeof item> => item !== null)
    .sort((a, b) => new Date(b.lastMessageAt).getTime() - new Date(a.lastMessageAt).getTime());
}

export async function joinProviderRealtimeConversation(
  accessToken: string,
  requestId: string,
  providerId: string): Promise<boolean> {
  const hubConnection = await ensureConnection(accessToken);
  const joined = await hubConnection.invoke<boolean>('JoinRequestChat', requestId, providerId);
  if (joined) {
    joinedConversations.add(buildConversationKey(requestId, providerId));
  }

  return joined;
}

export async function getProviderRealtimeConversationHistory(
  accessToken: string,
  requestId: string,
  providerId: string): Promise<ProviderChatMessage[]> {
  await joinProviderRealtimeConversation(accessToken, requestId, providerId);
  const hubConnection = await ensureConnection(accessToken);
  const raw = await hubConnection.invoke('GetHistory', requestId, providerId);
  const list = Array.isArray(raw) ? raw : [];
  return list
    .map(normalizeChatMessage)
    .filter((item): item is NonNullable<typeof item> => item !== null)
    .sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());
}

export async function markProviderRealtimeConversationDelivered(
  accessToken: string,
  requestId: string,
  providerId: string): Promise<void> {
  await joinProviderRealtimeConversation(accessToken, requestId, providerId);
  const hubConnection = await ensureConnection(accessToken);
  await hubConnection.invoke('MarkConversationDelivered', requestId, providerId);
}

export async function markProviderRealtimeConversationRead(
  accessToken: string,
  requestId: string,
  providerId: string): Promise<void> {
  await joinProviderRealtimeConversation(accessToken, requestId, providerId);
  const hubConnection = await ensureConnection(accessToken);
  await hubConnection.invoke('MarkConversationRead', requestId, providerId);
}
