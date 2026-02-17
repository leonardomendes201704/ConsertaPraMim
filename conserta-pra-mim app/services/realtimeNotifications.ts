import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { getApiBaseUrl } from './auth';

const HUB_EVENT_NAME = 'ReceiveNotification';
const USER_GROUP_METHOD = 'JoinUserGroup';

export interface RealtimeNotificationPayload {
  subject?: string;
  message?: string;
  actionUrl?: string | null;
  timestamp?: string;
}

let connection: HubConnection | null = null;

function normalizePayload(raw: unknown): RealtimeNotificationPayload {
  const payload = (raw || {}) as RealtimeNotificationPayload;
  return {
    subject: typeof payload.subject === 'string' ? payload.subject : '',
    message: typeof payload.message === 'string' ? payload.message : '',
    actionUrl: typeof payload.actionUrl === 'string' ? payload.actionUrl : null,
    timestamp: typeof payload.timestamp === 'string' ? payload.timestamp : undefined
  };
}

function buildHubUrl(): string {
  return `${getApiBaseUrl()}/notificationHub`;
}

export function extractRequestIdFromActionUrl(actionUrl?: string | null): string | null {
  if (!actionUrl) {
    return null;
  }

  const normalized = String(actionUrl).trim();
  if (!normalized) {
    return null;
  }

  try {
    const parsed = new URL(normalized, window.location.origin);
    const match = parsed.pathname.match(/\/ServiceRequests\/Details\/([0-9a-fA-F-]{36})$/i);
    if (!match?.[1]) {
      return null;
    }

    return match[1].toLowerCase();
  } catch {
    return null;
  }
}

export async function startRealtimeNotificationConnection(
  accessToken: string,
  onNotification: (payload: RealtimeNotificationPayload) => void): Promise<void> {
  await stopRealtimeNotificationConnection();

  const hubConnection = new HubConnectionBuilder()
    .withUrl(buildHubUrl(), {
      accessTokenFactory: () => accessToken
    })
    .withAutomaticReconnect()
    .build();

  hubConnection.on(HUB_EVENT_NAME, (payload: unknown) => {
    onNotification(normalizePayload(payload));
  });

  hubConnection.onreconnected(async () => {
    try {
      await hubConnection.invoke(USER_GROUP_METHOD);
    } catch {
      // silent reconnect retry fallback
    }
  });

  await hubConnection.start();
  await hubConnection.invoke(USER_GROUP_METHOD);
  connection = hubConnection;
}

export async function stopRealtimeNotificationConnection(): Promise<void> {
  if (!connection) {
    return;
  }

  connection.off(HUB_EVENT_NAME);
  try {
    await connection.stop();
  } finally {
    connection = null;
  }
}
