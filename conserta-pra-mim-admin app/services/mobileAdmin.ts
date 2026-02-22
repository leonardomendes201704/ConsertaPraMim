import {
  AdminDashboardData,
  AdminMonitoringOverviewData,
  AdminMonitoringTopEndpoint,
  AdminSupportTicketDetails,
  AdminSupportTicketsListResponse,
  MonitoringRangePreset
} from '../types';
import { buildAuthHeaders, getApiBaseUrl } from './http';

const REQUEST_TIMEOUT_MS = 12000;

export class MobileAdminError extends Error {
  public readonly code: string;
  public readonly httpStatus?: number;

  constructor(code: string, message: string, httpStatus?: number) {
    super(message);
    this.name = 'MobileAdminError';
    this.code = code;
    this.httpStatus = httpStatus;
  }
}

interface CallAdminApiOptions {
  method?: 'GET' | 'POST' | 'PATCH';
  body?: unknown;
}

interface SupportTicketsQuery {
  status?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

interface SupportMessagePayload {
  message: string;
  isInternal?: boolean;
}

function buildQuery(params: Record<string, string | number | boolean | undefined>): string {
  const query = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value === undefined || value === null || value === '') {
      return;
    }

    query.set(key, String(value));
  });

  const serialized = query.toString();
  return serialized ? `?${serialized}` : '';
}

function createTimeoutController(timeoutMs: number): { controller: AbortController; timerId: number } {
  const controller = new AbortController();
  const timerId = window.setTimeout(() => controller.abort(), timeoutMs);
  return { controller, timerId };
}

function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError';
}

async function readApiErrorMessage(response: Response): Promise<string> {
  const contentType = response.headers.get('content-type') || '';
  if (contentType.includes('application/json')) {
    try {
      const payload = await response.json();
      if (typeof payload?.errorMessage === 'string' && payload.errorMessage.trim()) {
        return payload.errorMessage;
      }

      if (typeof payload?.message === 'string' && payload.message.trim()) {
        return payload.message;
      }

      if (typeof payload === 'string' && payload.trim()) {
        return payload;
      }
    } catch {
      return 'Falha ao processar resposta da API.';
    }
  }

  const text = await response.text();
  return text.trim() || 'Falha ao processar resposta da API.';
}

async function callAdminApi<T>(token: string, endpoint: string, options?: CallAdminApiOptions): Promise<T> {
  const { controller, timerId } = createTimeoutController(REQUEST_TIMEOUT_MS);
  const method = options?.method || 'GET';

  const headers: HeadersInit = buildAuthHeaders(token);
  if (options?.body !== undefined) {
    (headers as Record<string, string>)['Content-Type'] = 'application/json';
  }

  let response: Response;
  try {
    response = await fetch(`${getApiBaseUrl()}${endpoint}`, {
      method,
      headers,
      body: options?.body === undefined ? undefined : JSON.stringify(options.body),
      signal: controller.signal
    });
  } catch (error) {
    if (isAbortError(error)) {
      throw new MobileAdminError('CPM-ADMIN-REQ-002', 'Tempo limite excedido na chamada da API.');
    }

    throw new MobileAdminError('CPM-ADMIN-REQ-001', 'Falha de conexao com a API admin.');
  } finally {
    window.clearTimeout(timerId);
  }

  if (!response.ok) {
    const apiMessage = await readApiErrorMessage(response);

    if (response.status === 401) {
      throw new MobileAdminError('CPM-ADMIN-REQ-401', 'Sessao expirada. Faca login novamente.', 401);
    }

    if (response.status === 403) {
      throw new MobileAdminError('CPM-ADMIN-REQ-403', 'Usuario sem permissao administrativa.', 403);
    }

    if (response.status >= 500) {
      throw new MobileAdminError('CPM-ADMIN-REQ-5XX', apiMessage || 'Falha interna ao consultar API admin.', response.status);
    }

    throw new MobileAdminError('CPM-ADMIN-REQ-4XX', apiMessage || 'Falha ao consultar API admin.', response.status);
  }

  return response.json() as Promise<T>;
}

export async function fetchMobileAdminDashboard(token: string): Promise<AdminDashboardData> {
  return callAdminApi<AdminDashboardData>(
    token,
    `/api/admin/dashboard${buildQuery({ page: 1, pageSize: 8 })}`
  );
}

export async function fetchMobileAdminMonitoringOverview(
  token: string,
  range: MonitoringRangePreset
): Promise<AdminMonitoringOverviewData> {
  return callAdminApi<AdminMonitoringOverviewData>(
    token,
    `/api/admin/monitoring/overview${buildQuery({ range })}`
  );
}

export async function fetchMobileAdminMonitoringTopEndpoints(
  token: string,
  range: MonitoringRangePreset,
  take: number = 8
): Promise<AdminMonitoringTopEndpoint[]> {
  const payload = await callAdminApi<{ items: AdminMonitoringTopEndpoint[] }>(
    token,
    `/api/admin/monitoring/top-endpoints${buildQuery({ range, take })}`
  );

  return payload.items || [];
}

export async function fetchMobileAdminSupportTickets(
  token: string,
  query: SupportTicketsQuery = {}
): Promise<AdminSupportTicketsListResponse> {
  const status = query.status && query.status.toLowerCase() !== 'all' ? query.status : undefined;
  return callAdminApi<AdminSupportTicketsListResponse>(
    token,
    `/api/admin/support/tickets${buildQuery({
      status,
      search: query.search,
      page: query.page || 1,
      pageSize: query.pageSize || 20,
      sortBy: 'lastInteraction',
      sortDescending: true
    })}`
  );
}

export async function fetchMobileAdminSupportTicketDetails(
  token: string,
  ticketId: string
): Promise<AdminSupportTicketDetails> {
  return callAdminApi<AdminSupportTicketDetails>(token, `/api/admin/support/tickets/${encodeURIComponent(ticketId)}`);
}

export async function addMobileAdminSupportTicketMessage(
  token: string,
  ticketId: string,
  payload: SupportMessagePayload
): Promise<AdminSupportTicketDetails> {
  return callAdminApi<AdminSupportTicketDetails>(
    token,
    `/api/admin/support/tickets/${encodeURIComponent(ticketId)}/messages`,
    {
      method: 'POST',
      body: {
        message: payload.message,
        isInternal: payload.isInternal || false
      }
    }
  );
}

export async function assignMobileAdminSupportTicket(
  token: string,
  ticketId: string,
  assignedAdminUserId: string | null,
  note?: string
): Promise<AdminSupportTicketDetails> {
  return callAdminApi<AdminSupportTicketDetails>(
    token,
    `/api/admin/support/tickets/${encodeURIComponent(ticketId)}/assign`,
    {
      method: 'PATCH',
      body: {
        assignedAdminUserId,
        note
      }
    }
  );
}

export async function updateMobileAdminSupportTicketStatus(
  token: string,
  ticketId: string,
  status: string,
  note?: string
): Promise<AdminSupportTicketDetails> {
  return callAdminApi<AdminSupportTicketDetails>(
    token,
    `/api/admin/support/tickets/${encodeURIComponent(ticketId)}/status`,
    {
      method: 'PATCH',
      body: {
        status,
        note
      }
    }
  );
}