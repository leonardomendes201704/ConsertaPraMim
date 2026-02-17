import {
  ProviderAgendaData,
  ProviderAgendaItem,
  ProviderAgendaHighlight,
  ProviderCreateProposalPayload,
  ProviderDashboardData,
  ProviderProposalSummary,
  ProviderProposalsData,
  ProviderRequestCard,
  ProviderRequestDetailsData
} from '../types';
import { getApiBaseUrl } from './auth';

export type MobileProviderErrorCode =
  | 'CPM-PROV-REQ-001'
  | 'CPM-PROV-REQ-401'
  | 'CPM-PROV-REQ-403'
  | 'CPM-PROV-REQ-404'
  | 'CPM-PROV-REQ-409'
  | 'CPM-PROV-REQ-4XX'
  | 'CPM-PROV-REQ-5XX';

export class MobileProviderError extends Error {
  public readonly code: MobileProviderErrorCode;
  public readonly detail?: string;
  public readonly httpStatus?: number;

  constructor(code: MobileProviderErrorCode, message: string, options?: { detail?: string; httpStatus?: number }) {
    super(message);
    this.name = 'MobileProviderError';
    this.code = code;
    this.detail = options?.detail;
    this.httpStatus = options?.httpStatus;
  }
}

interface ProviderDashboardApiResponse {
  providerName: string;
  kpis: {
    nearbyRequestsCount: number;
    activeProposalsCount: number;
    acceptedProposalsCount: number;
    pendingAppointmentsCount: number;
    upcomingConfirmedVisitsCount: number;
  };
  nearbyRequests: ProviderRequestCardApiItem[];
  agendaHighlights: ProviderAgendaHighlightApiItem[];
}

interface ProviderRequestCardApiItem {
  id: string;
  category: string;
  categoryIcon: string;
  description: string;
  status: string;
  createdAtUtc: string;
  street: string;
  city: string;
  zip: string;
  distanceKm?: number | null;
  estimatedValue?: number | null;
  alreadyProposed: boolean;
}

interface ProviderAgendaHighlightApiItem {
  appointmentId: string;
  serviceRequestId: string;
  status: string;
  statusLabel: string;
  windowStartUtc: string;
  windowEndUtc: string;
  category?: string | null;
  clientName?: string | null;
}

interface ProviderRequestsApiResponse {
  items: ProviderRequestCardApiItem[];
  totalCount: number;
}

interface ProviderProposalSummaryApiItem {
  id: string;
  requestId: string;
  estimatedValue?: number | null;
  message?: string | null;
  accepted: boolean;
  invalidated: boolean;
  statusLabel: string;
  createdAtUtc: string;
}

interface ProviderProposalsApiResponse {
  items: ProviderProposalSummaryApiItem[];
  totalCount: number;
  acceptedCount: number;
  openCount: number;
}

interface ProviderRequestDetailsApiResponse {
  request: ProviderRequestCardApiItem;
  existingProposal?: ProviderProposalSummaryApiItem | null;
  canSubmitProposal: boolean;
}

interface ProviderCreateProposalApiResponse {
  proposal: ProviderProposalSummaryApiItem;
  message: string;
}

interface ProviderAgendaApiItem {
  appointmentId: string;
  serviceRequestId: string;
  appointmentStatus: string;
  appointmentStatusLabel: string;
  windowStartUtc: string;
  windowEndUtc: string;
  category?: string | null;
  description?: string | null;
  clientName?: string | null;
  street?: string | null;
  city?: string | null;
  zip?: string | null;
  canConfirm: boolean;
  canReject: boolean;
  canRespondReschedule: boolean;
}

interface ProviderAgendaApiResponse {
  pendingItems: ProviderAgendaApiItem[];
  upcomingItems: ProviderAgendaApiItem[];
  pendingCount: number;
  upcomingCount: number;
}

interface ProviderAgendaOperationApiResponse {
  success: boolean;
  item?: ProviderAgendaApiItem | null;
  message?: string | null;
  errorCode?: string | null;
  errorMessage?: string | null;
}

const MOBILE_PROVIDER_TIMEOUT_MS = 12000;

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

function formatWindowLabel(startUtc?: string, endUtc?: string): string {
  if (!startUtc || !endUtc) {
    return '';
  }

  const start = new Date(startUtc);
  const end = new Date(endUtc);
  if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) {
    return `${startUtc} - ${endUtc}`;
  }

  const dd = String(start.getDate()).padStart(2, '0');
  const mm = String(start.getMonth() + 1).padStart(2, '0');
  const yyyy = start.getFullYear();
  const sh = String(start.getHours()).padStart(2, '0');
  const sm = String(start.getMinutes()).padStart(2, '0');
  const eh = String(end.getHours()).padStart(2, '0');
  const em = String(end.getMinutes()).padStart(2, '0');
  return `${dd}/${mm}/${yyyy}, ${sh}:${sm} - ${eh}:${em}`;
}

function mapRequestCard(item: ProviderRequestCardApiItem): ProviderRequestCard {
  return {
    id: item.id,
    category: item.category,
    categoryIcon: item.categoryIcon || 'build_circle',
    description: item.description,
    status: item.status,
    createdAtUtc: item.createdAtUtc,
    createdAtLabel: formatDateTime(item.createdAtUtc),
    street: item.street,
    city: item.city,
    zip: item.zip,
    distanceKm: Number.isFinite(Number(item.distanceKm)) ? Number(item.distanceKm) : undefined,
    estimatedValue: Number.isFinite(Number(item.estimatedValue)) ? Number(item.estimatedValue) : undefined,
    alreadyProposed: Boolean(item.alreadyProposed)
  };
}

function mapAgendaHighlight(item: ProviderAgendaHighlightApiItem): ProviderAgendaHighlight {
  return {
    appointmentId: item.appointmentId,
    serviceRequestId: item.serviceRequestId,
    status: item.status,
    statusLabel: item.statusLabel,
    windowStartUtc: item.windowStartUtc,
    windowEndUtc: item.windowEndUtc,
    category: item.category || undefined,
    clientName: item.clientName || undefined,
    windowLabel: formatWindowLabel(item.windowStartUtc, item.windowEndUtc)
  };
}

function mapAgendaItem(item: ProviderAgendaApiItem): ProviderAgendaItem {
  return {
    appointmentId: item.appointmentId,
    serviceRequestId: item.serviceRequestId,
    appointmentStatus: item.appointmentStatus,
    appointmentStatusLabel: item.appointmentStatusLabel,
    windowStartUtc: item.windowStartUtc,
    windowEndUtc: item.windowEndUtc,
    windowLabel: formatWindowLabel(item.windowStartUtc, item.windowEndUtc),
    category: item.category || undefined,
    description: item.description || undefined,
    clientName: item.clientName || undefined,
    street: item.street || undefined,
    city: item.city || undefined,
    zip: item.zip || undefined,
    canConfirm: Boolean(item.canConfirm),
    canReject: Boolean(item.canReject),
    canRespondReschedule: Boolean(item.canRespondReschedule)
  };
}

function mapProposal(item: ProviderProposalSummaryApiItem): ProviderProposalSummary {
  return {
    id: item.id,
    requestId: item.requestId,
    estimatedValue: Number.isFinite(Number(item.estimatedValue)) ? Number(item.estimatedValue) : undefined,
    message: item.message || undefined,
    accepted: Boolean(item.accepted),
    invalidated: Boolean(item.invalidated),
    statusLabel: item.statusLabel,
    createdAtUtc: item.createdAtUtc,
    createdAtLabel: formatDateTime(item.createdAtUtc)
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

async function callProviderApi(token: string, endpoint: string, method: 'GET' | 'POST' = 'GET', body?: unknown): Promise<Response> {
  const controller = new AbortController();
  const timerId = window.setTimeout(() => controller.abort(), MOBILE_PROVIDER_TIMEOUT_MS);

  try {
    return await fetch(`${getApiBaseUrl()}${endpoint}`, {
      method,
      headers: {
        Authorization: `Bearer ${token}`,
        Accept: 'application/json',
        ...(body ? { 'Content-Type': 'application/json' } : {})
      },
      body: body ? JSON.stringify(body) : undefined,
      signal: controller.signal
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw new MobileProviderError('CPM-PROV-REQ-001', 'A requisicao demorou mais que o esperado.', {
        detail: `Timeout ao chamar ${endpoint}.`
      });
    }

    throw new MobileProviderError('CPM-PROV-REQ-001', 'Nao foi possivel conectar com o servidor.', {
      detail: `Falha de rede/CORS/SSL ao chamar ${endpoint}.`
    });
  } finally {
    window.clearTimeout(timerId);
  }
}

async function ensureOk(response: Response, endpoint: string): Promise<Response> {
  if (response.ok) {
    return response;
  }

  const apiMessage = await tryReadApiError(response);

  if (response.status === 401) {
    throw new MobileProviderError('CPM-PROV-REQ-401', apiMessage || 'Sessao expirada. Faca login novamente.', {
      httpStatus: response.status,
      detail: `401 ao chamar ${endpoint}.`
    });
  }

  if (response.status === 403) {
    throw new MobileProviderError('CPM-PROV-REQ-403', apiMessage || 'Acesso negado para este recurso.', {
      httpStatus: response.status,
      detail: `403 ao chamar ${endpoint}.`
    });
  }

  if (response.status === 404) {
    throw new MobileProviderError('CPM-PROV-REQ-404', apiMessage || 'Recurso nao encontrado.', {
      httpStatus: response.status,
      detail: `404 ao chamar ${endpoint}.`
    });
  }

  if (response.status === 409) {
    throw new MobileProviderError('CPM-PROV-REQ-409', apiMessage || 'Conflito de regra de negocio.', {
      httpStatus: response.status,
      detail: `409 ao chamar ${endpoint}.`
    });
  }

  if (response.status >= 500) {
    throw new MobileProviderError('CPM-PROV-REQ-5XX', apiMessage || 'Erro interno no servidor.', {
      httpStatus: response.status,
      detail: `Erro ${response.status} ao chamar ${endpoint}.`
    });
  }

  throw new MobileProviderError('CPM-PROV-REQ-4XX', apiMessage || 'Falha na requisicao.', {
    httpStatus: response.status,
    detail: `Erro ${response.status} ao chamar ${endpoint}.`
  });
}

export async function fetchMobileProviderDashboard(token: string): Promise<ProviderDashboardData> {
  const endpoint = '/api/mobile/provider/dashboard';
  const response = await callProviderApi(token, endpoint, 'GET');
  const ok = await ensureOk(response, endpoint);
  const payload = await ok.json() as ProviderDashboardApiResponse;

  return {
    providerName: payload.providerName,
    kpis: payload.kpis,
    nearbyRequests: (payload.nearbyRequests || []).map(mapRequestCard),
    agendaHighlights: (payload.agendaHighlights || []).map(mapAgendaHighlight)
  };
}

export async function fetchMobileProviderRequests(token: string, searchTerm = '', take = 60): Promise<ProviderRequestCard[]> {
  const query = new URLSearchParams();
  if (searchTerm.trim()) {
    query.set('searchTerm', searchTerm.trim());
  }
  query.set('take', String(take));
  const endpoint = `/api/mobile/provider/requests?${query.toString()}`;

  const response = await callProviderApi(token, endpoint, 'GET');
  const ok = await ensureOk(response, endpoint);
  const payload = await ok.json() as ProviderRequestsApiResponse;
  return (payload.items || []).map(mapRequestCard);
}

export async function fetchMobileProviderRequestDetails(token: string, requestId: string): Promise<ProviderRequestDetailsData> {
  const endpoint = `/api/mobile/provider/requests/${requestId}`;
  const response = await callProviderApi(token, endpoint, 'GET');
  const ok = await ensureOk(response, endpoint);
  const payload = await ok.json() as ProviderRequestDetailsApiResponse;

  return {
    request: mapRequestCard(payload.request),
    existingProposal: payload.existingProposal ? mapProposal(payload.existingProposal) : undefined,
    canSubmitProposal: Boolean(payload.canSubmitProposal)
  };
}

export async function fetchMobileProviderProposals(token: string, take = 100): Promise<ProviderProposalsData> {
  const endpoint = `/api/mobile/provider/proposals?take=${encodeURIComponent(String(take))}`;
  const response = await callProviderApi(token, endpoint, 'GET');
  const ok = await ensureOk(response, endpoint);
  const payload = await ok.json() as ProviderProposalsApiResponse;

  return {
    items: (payload.items || []).map(mapProposal),
    totalCount: payload.totalCount,
    acceptedCount: payload.acceptedCount,
    openCount: payload.openCount
  };
}

export async function createMobileProviderProposal(
  token: string,
  requestId: string,
  proposal: ProviderCreateProposalPayload): Promise<ProviderProposalSummary> {
  const endpoint = `/api/mobile/provider/requests/${requestId}/proposals`;
  const response = await callProviderApi(token, endpoint, 'POST', {
    estimatedValue: proposal.estimatedValue,
    message: proposal.message
  });
  const ok = await ensureOk(response, endpoint);
  const payload = await ok.json() as ProviderCreateProposalApiResponse;
  return mapProposal(payload.proposal);
}

export async function fetchMobileProviderAgenda(
  token: string,
  options?: {
    fromUtc?: string;
    toUtc?: string;
    statusFilter?: string;
    take?: number;
  }): Promise<ProviderAgendaData> {
  const query = new URLSearchParams();
  if (options?.fromUtc) {
    query.set('fromUtc', options.fromUtc);
  }
  if (options?.toUtc) {
    query.set('toUtc', options.toUtc);
  }
  if (options?.statusFilter) {
    query.set('statusFilter', options.statusFilter);
  }
  query.set('take', String(options?.take ?? 60));

  const endpoint = `/api/mobile/provider/agenda?${query.toString()}`;
  const response = await callProviderApi(token, endpoint, 'GET');
  const ok = await ensureOk(response, endpoint);
  const payload = await ok.json() as ProviderAgendaApiResponse;

  return {
    pendingItems: (payload.pendingItems || []).map(mapAgendaItem),
    upcomingItems: (payload.upcomingItems || []).map(mapAgendaItem),
    pendingCount: payload.pendingCount ?? 0,
    upcomingCount: payload.upcomingCount ?? 0
  };
}

async function parseAgendaOperation(
  response: Response,
  endpoint: string): Promise<ProviderAgendaItem | undefined> {
  const ok = await ensureOk(response, endpoint);
  const payload = await ok.json() as ProviderAgendaOperationApiResponse;
  if (!payload?.success) {
    throw new MobileProviderError('CPM-PROV-REQ-4XX', payload?.errorMessage || 'Operacao de agenda nao concluida.', {
      detail: payload?.errorCode || `Erro logico ao chamar ${endpoint}.`
    });
  }

  return payload.item ? mapAgendaItem(payload.item) : undefined;
}

export async function confirmMobileProviderAgendaAppointment(token: string, appointmentId: string): Promise<ProviderAgendaItem | undefined> {
  const endpoint = `/api/mobile/provider/agenda/${appointmentId}/confirm`;
  const response = await callProviderApi(token, endpoint, 'POST');
  return parseAgendaOperation(response, endpoint);
}

export async function rejectMobileProviderAgendaAppointment(
  token: string,
  appointmentId: string,
  reason: string): Promise<ProviderAgendaItem | undefined> {
  const endpoint = `/api/mobile/provider/agenda/${appointmentId}/reject`;
  const response = await callProviderApi(token, endpoint, 'POST', { reason });
  return parseAgendaOperation(response, endpoint);
}

export async function respondMobileProviderAgendaReschedule(
  token: string,
  appointmentId: string,
  accept: boolean,
  reason?: string): Promise<ProviderAgendaItem | undefined> {
  const endpoint = `/api/mobile/provider/agenda/${appointmentId}/reschedule/respond`;
  const response = await callProviderApi(token, endpoint, 'POST', { accept, reason });
  return parseAgendaOperation(response, endpoint);
}
