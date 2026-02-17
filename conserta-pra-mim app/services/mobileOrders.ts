import {
  OrderFlowStep,
  OrderProposalDetailsData,
  OrderTimelineEvent,
  ProposalAppointmentSummary,
  ProposalScheduleSlot,
  ServiceRequest,
  ServiceRequestDetailsData
} from '../types';
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
  proposalCount?: number | null;
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
  relatedEntityType?: string | null;
  relatedEntityId?: string | null;
}

interface MobileClientOrderDetailsApiResponse {
  order: MobileClientOrderApiItem;
  flowSteps: MobileClientOrderFlowStepApi[];
  timeline: MobileClientOrderTimelineEventApi[];
}

interface MobileClientOrderProposalDetailsApiItem {
  id: string;
  orderId: string;
  providerId: string;
  providerName: string;
  estimatedValue?: number | null;
  message?: string | null;
  accepted: boolean;
  invalidated: boolean;
  statusLabel: string;
  sentAtUtc: string;
}

interface MobileClientOrderProposalDetailsApiResponse {
  order: MobileClientOrderApiItem;
  proposal: MobileClientOrderProposalDetailsApiItem;
  currentAppointment?: MobileClientOrderProposalAppointmentApiItem | null;
}

interface MobileClientAcceptProposalApiResponse {
  order: MobileClientOrderApiItem;
  proposal: MobileClientOrderProposalDetailsApiItem;
  message: string;
}

interface MobileClientOrderProposalAppointmentApiItem {
  id: string;
  orderId: string;
  proposalId: string;
  providerId: string;
  providerName: string;
  status: string;
  statusLabel: string;
  windowStartUtc: string;
  windowEndUtc: string;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
}

interface MobileClientOrderProposalSlotsApiItem {
  windowStartUtc: string;
  windowEndUtc: string;
}

interface MobileClientOrderProposalSlotsApiResponse {
  orderId: string;
  proposalId: string;
  providerId: string;
  date: string;
  slots: MobileClientOrderProposalSlotsApiItem[];
}

interface MobileClientScheduleProposalRequestApi {
  windowStartUtc: string;
  windowEndUtc: string;
  reason?: string;
}

interface MobileClientScheduleProposalResponseApi {
  order: MobileClientOrderApiItem;
  proposal: MobileClientOrderProposalDetailsApiItem;
  appointment: MobileClientOrderProposalAppointmentApiItem;
  message: string;
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

function formatWindowLabel(windowStartUtc?: string, windowEndUtc?: string): string {
  if (!windowStartUtc || !windowEndUtc) {
    return '';
  }

  const start = new Date(windowStartUtc);
  const end = new Date(windowEndUtc);
  if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) {
    return `${windowStartUtc} - ${windowEndUtc}`;
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
    occurredAt: formatDateTime(item.occurredAtUtc),
    relatedEntityType: item.relatedEntityType || undefined,
    relatedEntityId: item.relatedEntityId || undefined
  };
}

function mapProposalAppointment(item: MobileClientOrderProposalAppointmentApiItem): ProposalAppointmentSummary {
  return {
    id: item.id,
    orderId: item.orderId,
    proposalId: item.proposalId,
    providerId: item.providerId,
    providerName: item.providerName,
    status: item.status,
    statusLabel: item.statusLabel,
    windowStartUtc: item.windowStartUtc,
    windowEndUtc: item.windowEndUtc,
    windowLabel: formatWindowLabel(item.windowStartUtc, item.windowEndUtc),
    createdAtUtc: item.createdAtUtc,
    updatedAtUtc: item.updatedAtUtc || undefined
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

async function callMobileOrdersApi(
  token: string,
  endpoint: string,
  method: 'GET' | 'POST' = 'GET',
  body?: unknown): Promise<Response> {
  const controller = new AbortController();
  const timerId = window.setTimeout(() => controller.abort(), ORDERS_TIMEOUT_MS);

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

export async function fetchMobileClientOrderProposalDetails(
  token: string,
  orderId: string,
  proposalId: string): Promise<OrderProposalDetailsData> {
  const response = await callMobileOrdersApi(token, `/api/mobile/client/orders/${orderId}/proposals/${proposalId}`);
  if (!response.ok) {
    await throwForOrdersApiError(response);
  }

  const payload = await response.json() as MobileClientOrderProposalDetailsApiResponse;
  return {
    order: mapOrderItem(payload.order),
    proposal: {
      id: payload.proposal.id,
      orderId: payload.proposal.orderId,
      providerId: payload.proposal.providerId,
      providerName: payload.proposal.providerName,
      estimatedValue: payload.proposal.estimatedValue ?? undefined,
      message: payload.proposal.message ?? undefined,
      accepted: payload.proposal.accepted,
      invalidated: payload.proposal.invalidated,
      statusLabel: payload.proposal.statusLabel,
      sentAt: formatDateTime(payload.proposal.sentAtUtc)
    },
    currentAppointment: payload.currentAppointment
      ? mapProposalAppointment(payload.currentAppointment)
      : undefined
  };
}

export async function acceptMobileClientOrderProposal(
  token: string,
  orderId: string,
  proposalId: string): Promise<{ details: OrderProposalDetailsData; message: string }> {
  const response = await callMobileOrdersApi(token, `/api/mobile/client/orders/${orderId}/proposals/${proposalId}/accept`, 'POST');
  if (!response.ok) {
    await throwForOrdersApiError(response);
  }

  const payload = await response.json() as MobileClientAcceptProposalApiResponse;
  return {
    details: {
      order: mapOrderItem(payload.order),
      proposal: {
        id: payload.proposal.id,
        orderId: payload.proposal.orderId,
        providerId: payload.proposal.providerId,
        providerName: payload.proposal.providerName,
        estimatedValue: payload.proposal.estimatedValue ?? undefined,
        message: payload.proposal.message ?? undefined,
        accepted: payload.proposal.accepted,
        invalidated: payload.proposal.invalidated,
        statusLabel: payload.proposal.statusLabel,
        sentAt: formatDateTime(payload.proposal.sentAtUtc)
      }
    },
    message: payload.message || 'Proposta aceita com sucesso.'
  };
}

export async function fetchMobileClientOrderProposalSlots(
  token: string,
  orderId: string,
  proposalId: string,
  date: string): Promise<ProposalScheduleSlot[]> {
  const dateQuery = encodeURIComponent(date);
  const response = await callMobileOrdersApi(
    token,
    `/api/mobile/client/orders/${orderId}/proposals/${proposalId}/schedule/slots?date=${dateQuery}`);

  if (!response.ok) {
    await throwForOrdersApiError(response);
  }

  const payload = await response.json() as MobileClientOrderProposalSlotsApiResponse;
  return (payload.slots || []).map((slot) => ({
    windowStartUtc: slot.windowStartUtc,
    windowEndUtc: slot.windowEndUtc,
    label: formatWindowLabel(slot.windowStartUtc, slot.windowEndUtc)
  }));
}

export async function scheduleMobileClientOrderProposal(
  token: string,
  orderId: string,
  proposalId: string,
  payload: { windowStartUtc: string; windowEndUtc: string; reason?: string }): Promise<{
    details: OrderProposalDetailsData;
    appointment: ProposalAppointmentSummary;
    message: string;
  }> {
  const body: MobileClientScheduleProposalRequestApi = {
    windowStartUtc: payload.windowStartUtc,
    windowEndUtc: payload.windowEndUtc,
    ...(payload.reason ? { reason: payload.reason } : {})
  };

  const response = await callMobileOrdersApi(
    token,
    `/api/mobile/client/orders/${orderId}/proposals/${proposalId}/schedule`,
    'POST',
    body);

  if (!response.ok) {
    await throwForOrdersApiError(response);
  }

  const result = await response.json() as MobileClientScheduleProposalResponseApi;
  return {
    details: {
      order: mapOrderItem(result.order),
      proposal: {
        id: result.proposal.id,
        orderId: result.proposal.orderId,
        providerId: result.proposal.providerId,
        providerName: result.proposal.providerName,
        estimatedValue: result.proposal.estimatedValue ?? undefined,
        message: result.proposal.message ?? undefined,
        accepted: result.proposal.accepted,
        invalidated: result.proposal.invalidated,
        statusLabel: result.proposal.statusLabel,
        sentAt: formatDateTime(result.proposal.sentAtUtc)
      },
      currentAppointment: result.appointment ? mapProposalAppointment(result.appointment) : undefined
    },
    appointment: mapProposalAppointment(result.appointment),
    message: result.message || 'Agendamento solicitado com sucesso.'
  };
}
