import { ServiceRequest } from '../types';
import { getApiBaseUrl } from './auth';

export type MobileOrdersErrorCode =
  | 'CPM-ORDERS-001'
  | 'CPM-ORDERS-002'
  | 'CPM-ORDERS-401'
  | 'CPM-ORDERS-403'
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

export interface MobileClientOrdersResult {
  openOrders: ServiceRequest[];
  finalizedOrders: ServiceRequest[];
}

const ORDERS_TIMEOUT_MS = 12000;

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

export async function fetchMobileClientOrders(token: string, takePerBucket = 100): Promise<MobileClientOrdersResult> {
  const controller = new AbortController();
  const timerId = window.setTimeout(() => controller.abort(), ORDERS_TIMEOUT_MS);

  let response: Response;
  try {
    response = await fetch(`${getApiBaseUrl()}/api/mobile/client/orders?takePerBucket=${takePerBucket}`, {
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

  if (!response.ok) {
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
    if (response.status >= 500) {
      throw new MobileOrdersError('CPM-ORDERS-5XX', apiMessage || 'Servico de pedidos indisponivel no momento.', {
        httpStatus: response.status
      });
    }

    throw new MobileOrdersError('CPM-ORDERS-4XX', apiMessage || 'Nao foi possivel carregar seus pedidos.', {
      httpStatus: response.status
    });
  }

  const payload = await response.json() as MobileClientOrdersApiResponse;
  return {
    openOrders: (payload.openOrders || []).map(mapOrderItem),
    finalizedOrders: (payload.finalizedOrders || []).map(mapOrderItem)
  };
}
