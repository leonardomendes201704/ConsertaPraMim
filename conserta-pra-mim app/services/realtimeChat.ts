import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { getApiBaseUrl } from './auth';
import { ChatConversationSummary, ChatMessage, ChatMessageReceipt } from '../types';

type ChatEventHandlers = {
  onChatMessage?: (message: ChatMessage) => void;
  onMessageReceiptUpdated?: (receipt: ChatMessageReceipt) => void;
  onUserPresence?: (payload: { userId: string; isOnline: boolean }) => void;
  onProviderStatus?: (payload: { providerId: string; status: string }) => void;
};

interface ChatAttachmentUploadResult {
  fileUrl: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
}

let connection: HubConnection | null = null;
let connectionToken = '';
const subscribers = new Set<ChatEventHandlers>();
const joinedConversations = new Set<string>();

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
    const value = source[key];
    const parsed = Number(value);
    if (Number.isFinite(parsed)) {
      return parsed;
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

function buildChatHubUrl(): string {
  return `${getApiBaseUrl()}/chatHub`;
}

function buildConversationKey(requestId: string, providerId: string): string {
  return `${requestId.toLowerCase()}::${providerId.toLowerCase()}`;
}

function normalizeChatMessage(raw: unknown): ChatMessage | null {
  const payload = raw as Record<string, unknown>;
  const id = readString(payload, 'id', 'Id');
  const requestId = readString(payload, 'requestId', 'RequestId');
  const providerId = readString(payload, 'providerId', 'ProviderId');
  const senderId = readString(payload, 'senderId', 'SenderId');

  if (!id || !requestId || !providerId || !senderId) {
    return null;
  }

  const rawAttachments = payload?.attachments || payload?.Attachments;
  const attachmentsRaw = Array.isArray(rawAttachments) ? rawAttachments : [];
  const attachments = attachmentsRaw
    .map((attachment): ChatMessage['attachments'][number] | null => {
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
    createdAt: readString(payload, 'createdAt', 'CreatedAt') || new Date().toISOString(),
    attachments,
    deliveredAt: readString(payload, 'deliveredAt', 'DeliveredAt') || undefined,
    readAt: readString(payload, 'readAt', 'ReadAt') || undefined
  };
}

function normalizeReceipt(raw: unknown): ChatMessageReceipt | null {
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
    deliveredAt: readString(payload, 'deliveredAt', 'DeliveredAt') || undefined,
    readAt: readString(payload, 'readAt', 'ReadAt') || undefined
  };
}

function notifyMessage(raw: unknown): void {
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
      // ignore connection shutdown errors
    }
  }

  const hubConnection = new HubConnectionBuilder()
    .withUrl(buildChatHubUrl(), {
      accessTokenFactory: () => accessToken
    })
    .withAutomaticReconnect()
    .build();

  hubConnection.on('ReceiveChatMessage', notifyMessage);
  hubConnection.on('ReceiveMessageReceiptUpdated', notifyReceipt);
  hubConnection.on('ReceiveUserPresence', notifyUserPresence);
  hubConnection.on('ReceiveProviderStatus', notifyProviderStatus);

  hubConnection.onreconnected(async () => {
    try {
      await hubConnection.invoke('JoinPersonalGroup');
      const keys = Array.from(joinedConversations);
      for (const key of keys) {
        const [requestId, providerId] = key.split('::');
        if (!requestId || !providerId) {
          continue;
        }

        await hubConnection.invoke('JoinRequestChat', requestId, providerId);
      }
    } catch {
      // silent retry fallback
    }
  });

  await hubConnection.start();
  await hubConnection.invoke('JoinPersonalGroup');

  connection = hubConnection;
  connectionToken = accessToken;
  return hubConnection;
}

async function ensureChatConnection(accessToken: string): Promise<HubConnection> {
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

function mapConversation(raw: unknown): ChatConversationSummary | null {
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
    counterpartRole: readString(payload, 'counterpartRole', 'CounterpartRole') || 'Provider',
    counterpartName: readString(payload, 'counterpartName', 'CounterpartName') || 'Contato',
    title: readString(payload, 'title', 'Title') || 'Conversa',
    lastMessagePreview: readString(payload, 'lastMessagePreview', 'LastMessagePreview') || 'Sem mensagens.',
    lastMessageAt: readString(payload, 'lastMessageAt', 'LastMessageAt') || new Date().toISOString(),
    unreadMessages: readNumber(payload, 'unreadMessages', 'UnreadMessages'),
    counterpartIsOnline: readBoolean(payload, 'counterpartIsOnline', 'CounterpartIsOnline'),
    providerStatus: readString(payload, 'providerStatus', 'ProviderStatus') || undefined
  };
}

function mapHistoryItem(raw: unknown): ChatMessage | null {
  return normalizeChatMessage(raw);
}

export function subscribeToRealtimeChatEvents(handlers: ChatEventHandlers): () => void {
  subscribers.add(handlers);
  return () => {
    subscribers.delete(handlers);
  };
}

export async function startRealtimeChatConnection(accessToken: string): Promise<void> {
  await ensureChatConnection(accessToken);
}

export async function stopRealtimeChatConnection(): Promise<void> {
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

export async function getMyActiveConversations(accessToken: string): Promise<ChatConversationSummary[]> {
  const hubConnection = await ensureChatConnection(accessToken);
  const raw = await hubConnection.invoke('GetMyActiveConversations');
  const list = Array.isArray(raw) ? raw : [];
  return list
    .map(mapConversation)
    .filter((item): item is NonNullable<typeof item> => item !== null)
    .sort((a, b) => new Date(b.lastMessageAt).getTime() - new Date(a.lastMessageAt).getTime());
}

export async function joinRequestConversation(
  accessToken: string,
  requestId: string,
  providerId: string): Promise<boolean> {
  const hubConnection = await ensureChatConnection(accessToken);
  const joined = await hubConnection.invoke<boolean>('JoinRequestChat', requestId, providerId);
  if (joined) {
    joinedConversations.add(buildConversationKey(requestId, providerId));
  }
  return joined;
}

export async function getConversationHistory(
  accessToken: string,
  requestId: string,
  providerId: string): Promise<ChatMessage[]> {
  await joinRequestConversation(accessToken, requestId, providerId);
  const hubConnection = await ensureChatConnection(accessToken);
  const raw = await hubConnection.invoke('GetHistory', requestId, providerId);
  const list = Array.isArray(raw) ? raw : [];
  return list
    .map(mapHistoryItem)
    .filter((item): item is NonNullable<typeof item> => item !== null)
    .sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());
}

export async function markConversationDelivered(
  accessToken: string,
  requestId: string,
  providerId: string): Promise<void> {
  await joinRequestConversation(accessToken, requestId, providerId);
  const hubConnection = await ensureChatConnection(accessToken);
  await hubConnection.invoke('MarkConversationDelivered', requestId, providerId);
}

export async function markConversationRead(
  accessToken: string,
  requestId: string,
  providerId: string): Promise<void> {
  await joinRequestConversation(accessToken, requestId, providerId);
  const hubConnection = await ensureChatConnection(accessToken);
  await hubConnection.invoke('MarkConversationRead', requestId, providerId);
}

export async function sendConversationMessage(
  accessToken: string,
  requestId: string,
  providerId: string,
  text: string,
  attachments?: ChatAttachmentUploadResult[]): Promise<void> {
  await joinRequestConversation(accessToken, requestId, providerId);
  const hubConnection = await ensureChatConnection(accessToken);
  await hubConnection.invoke(
    'SendMessage',
    requestId,
    providerId,
    text,
    attachments || []);
}

export async function getConversationParticipantPresence(
  accessToken: string,
  requestId: string,
  providerId: string): Promise<{ userId: string; role: string; isOnline: boolean; operationalStatus?: string } | null> {
  await joinRequestConversation(accessToken, requestId, providerId);
  const hubConnection = await ensureChatConnection(accessToken);
  const raw = await hubConnection.invoke('GetConversationParticipantPresence', requestId, providerId) as Record<string, unknown> | null;
  if (!raw) {
    return null;
  }

  const userId = readString(raw, 'userId', 'UserId');
  if (!userId) {
    return null;
  }

  return {
    userId,
    role: readString(raw, 'role', 'Role') || 'Provider',
    isOnline: readBoolean(raw, 'isOnline', 'IsOnline'),
    operationalStatus: readString(raw, 'operationalStatus', 'OperationalStatus') || undefined
  };
}

export function resolveChatAttachmentUrl(fileUrl: string): string {
  const trimmed = String(fileUrl || '').trim();
  if (!trimmed) {
    return '';
  }

  if (trimmed.startsWith('http://') || trimmed.startsWith('https://')) {
    return trimmed;
  }

  return `${getApiBaseUrl()}${trimmed.startsWith('/') ? '' : '/'}${trimmed}`;
}

export async function uploadConversationAttachments(
  accessToken: string,
  requestId: string,
  providerId: string,
  files: File[]): Promise<ChatAttachmentUploadResult[]> {
  const normalizedRequestId = String(requestId || '').trim();
  const normalizedProviderId = String(providerId || '').trim();
  if (!normalizedRequestId || !normalizedProviderId || files.length === 0) {
    return [];
  }

  const uploaded: ChatAttachmentUploadResult[] = [];

  for (const file of files) {
    const formData = new FormData();
    formData.append('requestId', normalizedRequestId);
    formData.append('providerId', normalizedProviderId);
    formData.append('file', file);

    const response = await fetch(`${getApiBaseUrl()}/api/chat-attachments/upload`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${accessToken}`
      },
      body: formData
    });

    if (!response.ok) {
      throw new Error('Nao foi possivel enviar anexo para o chat.');
    }

    const payload = await response.json() as Record<string, unknown>;
    uploaded.push({
      fileUrl: String(payload.fileUrl || '').trim(),
      fileName: String(payload.fileName || '').trim() || file.name,
      contentType: String(payload.contentType || '').trim() || file.type || 'application/octet-stream',
      sizeBytes: Number(payload.sizeBytes || file.size || 0)
    });
  }

  return uploaded;
}
