import { ServiceRequest, ServiceRequestCategoryOption } from '../types';
import { getApiBaseUrl } from './auth';

export type MobileServiceRequestErrorCode =
  | 'CPM-REQ-001'
  | 'CPM-REQ-002'
  | 'CPM-REQ-400'
  | 'CPM-REQ-401'
  | 'CPM-REQ-403'
  | 'CPM-REQ-404'
  | 'CPM-REQ-4XX'
  | 'CPM-REQ-5XX';

export class MobileServiceRequestError extends Error {
  public readonly code: MobileServiceRequestErrorCode;
  public readonly detail?: string;
  public readonly httpStatus?: number;

  constructor(code: MobileServiceRequestErrorCode, message: string, options?: { detail?: string; httpStatus?: number }) {
    super(message);
    this.name = 'MobileServiceRequestError';
    this.code = code;
    this.detail = options?.detail;
    this.httpStatus = options?.httpStatus;
  }
}

interface MobileCategoryApiItem {
  id: string;
  name: string;
  slug: string;
  legacyCategory: string;
  icon: string;
}

interface MobileResolveZipApiResponse {
  zipCode: string;
  street: string;
  city: string;
  latitude: number;
  longitude: number;
}

interface MobileOrderItemApiResponse {
  id: string;
  title: string;
  status: string;
  category: string;
  date: string;
  icon: string;
  description?: string | null;
  proposalCount?: number | null;
}

interface MobileCreateServiceRequestApiResponse {
  order: MobileOrderItemApiResponse;
  street: string;
  city: string;
  zipCode: string;
  message: string;
}

export interface MobileCreateServiceRequestInput {
  categoryId: string;
  description: string;
  zipCode: string;
  street?: string;
  city?: string;
}

export interface MobileCreateServiceRequestResult {
  order: ServiceRequest;
  street: string;
  city: string;
  zipCode: string;
  message: string;
}

export interface MobileResolvedZipAddress {
  zipCode: string;
  street: string;
  city: string;
  latitude: number;
  longitude: number;
}

const REQUEST_TIMEOUT_MS = 12000;

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

function mapOrder(item: MobileOrderItemApiResponse): ServiceRequest {
  const normalizedProposalCount = Number(item.proposalCount ?? 0);

  return {
    id: item.id,
    title: item.title || `Pedido ${item.id}`,
    status: normalizeStatus(item.status),
    date: item.date,
    category: item.category || 'Servico',
    icon: item.icon || 'build_circle',
    description: item.description || undefined,
    proposalCount: Number.isFinite(normalizedProposalCount) ? Math.max(0, Math.trunc(normalizedProposalCount)) : 0
  };
}

function onlyDigits(value: string): string {
  return (value || '').replace(/\D/g, '');
}

function ensureGuidLike(value: string): string {
  return (value || '').trim();
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

function buildFetchOptions(token: string, method: 'GET' | 'POST', body?: unknown): RequestInit {
  const headers: Record<string, string> = {
    Authorization: `Bearer ${token}`,
    Accept: 'application/json'
  };

  if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  return {
    method,
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined
  };
}

async function callMobileServiceRequestApi(
  token: string,
  endpoint: string,
  method: 'GET' | 'POST',
  body?: unknown): Promise<Response> {
  const controller = new AbortController();
  const timerId = window.setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);

  try {
    return await fetch(`${getApiBaseUrl()}${endpoint}`, {
      ...buildFetchOptions(token, method, body),
      signal: controller.signal
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw new MobileServiceRequestError('CPM-REQ-002', 'Nao foi possivel concluir a operacao agora (timeout).');
    }

    throw new MobileServiceRequestError('CPM-REQ-001', 'Falha de conexao ao comunicar com o servico de pedidos.');
  } finally {
    window.clearTimeout(timerId);
  }
}

async function throwForApiError(response: Response, fallbackMessage: string): Promise<never> {
  const apiMessage = await tryReadApiError(response);

  if (response.status === 401) {
    throw new MobileServiceRequestError('CPM-REQ-401', apiMessage || 'Sua sessao expirou. Faca login novamente.', {
      httpStatus: 401
    });
  }

  if (response.status === 403) {
    throw new MobileServiceRequestError('CPM-REQ-403', apiMessage || 'Perfil nao autorizado para este endpoint do app.', {
      httpStatus: 403
    });
  }

  if (response.status === 404) {
    throw new MobileServiceRequestError('CPM-REQ-404', apiMessage || 'Recurso nao encontrado para este pedido.', {
      httpStatus: 404
    });
  }

  if (response.status === 400) {
    throw new MobileServiceRequestError('CPM-REQ-400', apiMessage || fallbackMessage, {
      httpStatus: 400
    });
  }

  if (response.status >= 500) {
    throw new MobileServiceRequestError('CPM-REQ-5XX', apiMessage || 'Servico de pedidos indisponivel no momento.', {
      httpStatus: response.status
    });
  }

  throw new MobileServiceRequestError('CPM-REQ-4XX', apiMessage || fallbackMessage, {
    httpStatus: response.status
  });
}

export async function fetchMobileServiceRequestCategories(token: string): Promise<ServiceRequestCategoryOption[]> {
  const response = await callMobileServiceRequestApi(token, '/api/mobile/client/service-requests/categories', 'GET');
  if (!response.ok) {
    await throwForApiError(response, 'Nao foi possivel carregar as categorias agora.');
  }

  const payload = await response.json() as MobileCategoryApiItem[];
  return (payload || []).map(item => ({
    id: item.id,
    name: item.name,
    slug: item.slug,
    legacyCategory: item.legacyCategory,
    icon: item.icon || 'build_circle'
  }));
}

export async function resolveMobileServiceRequestZip(token: string, zipCode: string): Promise<MobileResolvedZipAddress> {
  const normalizedZip = onlyDigits(zipCode);
  const response = await callMobileServiceRequestApi(
    token,
    `/api/mobile/client/service-requests/zip-resolution?zipCode=${encodeURIComponent(normalizedZip)}`,
    'GET');

  if (!response.ok) {
    await throwForApiError(response, 'Nao foi possivel localizar esse CEP.');
  }

  const payload = await response.json() as MobileResolveZipApiResponse;
  return {
    zipCode: payload.zipCode,
    street: payload.street,
    city: payload.city,
    latitude: payload.latitude,
    longitude: payload.longitude
  };
}

export async function createMobileServiceRequest(
  token: string,
  input: MobileCreateServiceRequestInput): Promise<MobileCreateServiceRequestResult> {
  const response = await callMobileServiceRequestApi(
    token,
    '/api/mobile/client/service-requests',
    'POST',
    {
      categoryId: ensureGuidLike(input.categoryId),
      description: input.description,
      zipCode: onlyDigits(input.zipCode),
      street: input.street,
      city: input.city
    });

  if (!response.ok) {
    await throwForApiError(response, 'Nao foi possivel criar o pedido agora.');
  }

  const payload = await response.json() as MobileCreateServiceRequestApiResponse;
  return {
    order: mapOrder(payload.order),
    street: payload.street,
    city: payload.city,
    zipCode: payload.zipCode,
    message: payload.message
  };
}
