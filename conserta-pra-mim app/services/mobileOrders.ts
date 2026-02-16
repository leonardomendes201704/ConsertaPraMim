import { OrderFlowStep, OrderTimelineEvent, ServiceRequest, ServiceRequestDetailsData } from '../types';
import { getApiBaseUrl } from './auth';

export type MobileOrdersErrorCode =
  | 'CPM-ORDERS-001'
  | 'CPM-ORDERS-002'
  | 'CPM-ORDERS-401'
  | 'CPM-ORDERS-403'
  | 'CPM-ORDERS-404'
  | 'CPM-ORDERS-4XX'
  | 'CPM-ORDERS-5XX';

export class MobileOrdersError extends Error {
  public readonly code: MobileOrdersErrorCode;
  public readonly detail?: string;
  public readonly httpStatus?: number;

  constructor(code: MobileOrdersErrorCode, message: string, options?: { detail?: string; httpStatus?: number }) {
    super(message);
    this.name = 'MobileOrdersError';
    this.code = code;
    this.detail = options?.detail;
    this.httpStatus = options?.httpStatus;
  }
}

interface MobileClientOrderApiItem {
  id: string;
  title: string;
  status: string;
  category: string;
  date: string;
  icon: string;
  description?: string | null;
}

interface MobileClientOrdersApiResponse {
  openOrders: MobileClientOrderApiItem[];
  finalizedOrders: MobileClientOrderApiItem[];
  openOrdersCount: number;
  finalizedOrdersCount: number;
  totalOrdersCount: number;
}

interface MobileClientOrderFlowStepApi {
  step: number;
  title: string;
  completed: boolean;
  current: boolean;
}

interface MobileClientOrderTimelineEventApi {
  eventCode: string;
  title: string;
  description: string;
  occurredAtUtc: string;
}

interface MobileClientOrderDetailsApiResponse {
  order: MobileClientOrderApiItem;
  flowSteps: MobileClientOrderFlowStepApi[];
  timeline: MobileClientOrderTimelineEventApi[];
}

export interface MobileClientOrdersResult {
  openOrders: ServiceRequest[];
  finalizedOrders: ServiceRequest[];
}

const ORDERS_TIMEOUT_MS = 12000;

function formatDateTime(value?: string): string {
  if (!value) {
    return '';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  const dd = String(date.getDate()).padStart(2, '0');
  const mm = String(date.getMonth() + 1).padStart(2, '0');
  const yyyy = date.getFullYear();
  const hh = String(date.getHours()).padStart(2, '0');
  const min = String(date.getMinutes()).padStart(2, '0');
  return `${dd}/${mm}/${yyyy} ${hh}:${min}`;
}

function normalizeStatus(status: string): ServiceRequest['status'] {
  const normalized = (status || '').trim().toUpperCase();
  if (normalized === 'EM_ANDAMENTO') {
    return 'EM_ANDAMENTO';
  }
  if (normalized === 'CONCLUIDO') {
    return 'CONCLUIDO';
  }
  if (normalized === 'CANCELADO') {
    return 'CANCELADO';
  }
  return 'AGUARDANDO';
}

function mapOrderItem(item: MobileClientOrderApiItem): ServiceRequest {
  return {
    id: item.id,
    title: item.title || `Pedido ${item.id}`,
    status: normalizeStatus(item.status),
    date: item.date,
    category: item.category || 'Servico',
    icon: item.icon || 'build_circle',
    description: item.description || undefined
  };
}

function mapFlowStep(item: MobileClientOrderFlowStepApi): OrderFlowStep {
  return {
    step: item.step,
    title: item.title,
    completed: item.completed,
    current: item.current
  };
}

function mapTimelineEvent(item: MobileClientOrderTimelineEventApi): OrderTimelineEvent {
  return {
    eventCode: item.eventCode,
    title: item.title,
    description: item.description,
    occurredAt: formatDateTime(item.occurredAtUtc)
  };
}

async function tryReadApiError(response: Response): Promise<string | undefined> {
  const contentType = response.headers.get('content-type') || '';
  if (contentType.includes('application/json')) {
    try {
      const payload = await response.json();
      if (typeof payload?.message === 'string' && payload.message.trim()) {
        return payload.message;
      }
    } catch {
      return undefined;
    }
  }

  try {
    const text = await response.text();
    return text?.trim() || undefined;
  } catch {
    return undefined;
  }
}

async function callMobileOrdersApi(token: string, endpoint: string): Promise<Response> {
  const controller = new AbortController();
  const timerId = window.setTimeout(() => controller.abort(), ORDERS_TIMEOUT_MS);

  try {
    return await fetch(`${getApiBaseUrl()}${endpoint}`, {
      method: 'GET',
      headers: {
        Authorization: `Bearer ${token}`,
        Accept: 'application/json'
      },
      signal: controller.signal
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw new MobileOrdersError('CPM-ORDERS-002', 'Nao foi possivel carregar seus pedidos agora (timeout).');
    }

    throw new MobileOrdersError('CPM-ORDERS-001', 'Falha de conexao ao buscar seus pedidos.');
  } finally {
    window.clearTimeout(timerId);
  }
}

async function throwForOrdersApiError(response: Response): Promise<never> {
  const apiMessage = await tryReadApiError(response);

  if (response.status === 401) {
    throw new MobileOrdersError('CPM-ORDERS-401', apiMessage || 'Sua sessao expirou. Faca login novamente.', {
      httpStatus: 401
    });
  }

  if (response.status === 403) {
    throw new MobileOrdersError('CPM-ORDERS-403', apiMessage || 'Perfil nao autorizado para listar pedidos do app.', {
      httpStatus: 403
    });
  }

  if (response.status === 404) {
    throw new MobileOrdersError('CPM-ORDERS-404', apiMessage || 'Pedido nao encontrado.', {
      httpStatus: 404
    });
  }

  if (response.status >= 500) {
    throw new MobileOrdersError('CPM-ORDERS-5XX', apiMessage || 'Servico de pedidos indisponivel no momento.', {
      httpStatus: response.status
    });
  }

  throw new MobileOrdersError('CPM-ORDERS-4XX', apiMessage || 'Nao foi possivel carregar seus pedidos.', {
    httpStatus: response.status
  });
}

export async function fetchMobileClientOrders(token: string, takePerBucket = 100): Promise<MobileClientOrdersResult> {
  const response = await callMobileOrdersApi(token, `/api/mobile/client/orders?takePerBucket=${takePerBucket}`);
  if (!response.ok) {
    await throwForOrdersApiError(response);
  }

  const payload = await response.json() as MobileClientOrdersApiResponse;
  return {
    openOrders: (payload.openOrders || []).map(mapOrderItem),
    finalizedOrders: (payload.finalizedOrders || []).map(mapOrderItem)
  };
}

export async function fetchMobileClientOrderDetails(token: string, orderId: string): Promise<ServiceRequestDetailsData> {
  const response = await callMobileOrdersApi(token, `/api/mobile/client/orders/${orderId}`);
  if (!response.ok) {
    await throwForOrdersApiError(response);
  }

  const payload = await response.json() as MobileClientOrderDetailsApiResponse;
  return {
    order: mapOrderItem(payload.order),
    flowSteps: (payload.flowSteps || []).map(mapFlowStep),
    timeline: (payload.timeline || []).map(mapTimelineEvent)
  };
}
