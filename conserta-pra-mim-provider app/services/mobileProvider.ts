import {
  ProviderAppointmentChecklist,
  ProviderAgendaData,
  ProviderAgendaItem,
  ProviderAgendaHighlight,
  ProviderChatConversationSummary,
  ProviderChatMessage,
  ProviderChatMessageReceipt,
  ProviderChecklistEvidenceUploadResult,
  ProviderChecklistHistoryItem,
  ProviderChecklistItem,
  ProviderChecklistItemUpsertPayload,
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

interface ProviderChecklistItemApiItem {
  templateItemId: string;
  title: string;
  helpText?: string | null;
  isRequired: boolean;
  requiresEvidence: boolean;
  allowNote: boolean;
  sortOrder: number;
  isChecked: boolean;
  note?: string | null;
  evidenceUrl?: string | null;
  evidenceFileName?: string | null;
  evidenceContentType?: string | null;
  evidenceSizeBytes?: number | null;
  checkedByUserId?: string | null;
  checkedAtUtc?: string | null;
}

interface ProviderChecklistHistoryApiItem {
  id: string;
  templateItemId: string;
  itemTitle: string;
  previousIsChecked?: boolean | null;
  newIsChecked: boolean;
  previousNote?: string | null;
  newNote?: string | null;
  previousEvidenceUrl?: string | null;
  newEvidenceUrl?: string | null;
  actorUserId: string;
  actorRole: string;
  occurredAtUtc: string;
}

interface ProviderAppointmentChecklistApiResponse {
  appointmentId: string;
  templateId?: string | null;
  templateName?: string | null;
  categoryName: string;
  isRequiredChecklist: boolean;
  requiredItemsCount: number;
  requiredCompletedCount: number;
  items: ProviderChecklistItemApiItem[];
  history: ProviderChecklistHistoryApiItem[];
}

interface ProviderChatAttachmentApiItem {
  id?: string;
  fileUrl: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  mediaKind: string;
}

interface ProviderChatMessageApiItem {
  id: string;
  requestId: string;
  providerId: string;
  senderId: string;
  senderName: string;
  senderRole: string;
  text?: string | null;
  createdAt: string;
  attachments: ProviderChatAttachmentApiItem[];
  deliveredAt?: string | null;
  readAt?: string | null;
}

interface ProviderChatMessagesApiResponse {
  requestId: string;
  providerId: string;
  messages: ProviderChatMessageApiItem[];
  totalCount: number;
}

interface ProviderChatReceiptApiItem {
  messageId: string;
  requestId: string;
  providerId: string;
  deliveredAt?: string | null;
  readAt?: string | null;
}

interface ProviderChatConversationsApiItem {
  requestId: string;
  providerId: string;
  counterpartUserId: string;
  counterpartRole: string;
  counterpartName: string;
  title: string;
  lastMessagePreview: string;
  lastMessageAt: string;
  unreadMessages: number;
  counterpartIsOnline: boolean;
  providerStatus?: string | null;
}

interface ProviderChatConversationsApiResponse {
  conversations: ProviderChatConversationsApiItem[];
  totalCount: number;
  totalUnreadMessages: number;
}

interface ProviderSendChatMessageApiResponse {
  success: boolean;
  message?: ProviderChatMessageApiItem | null;
  errorCode?: string | null;
  errorMessage?: string | null;
}

interface ProviderChatReceiptOperationApiResponse {
  success: boolean;
  receipts: ProviderChatReceiptApiItem[];
  errorCode?: string | null;
  errorMessage?: string | null;
}

const MOBILE_PROVIDER_TIMEOUT_MS = 12000;

interface ParsedApiError {
  message?: string;
  errorCode?: string;
}

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

function mapChecklistItem(item: ProviderChecklistItemApiItem): ProviderChecklistItem {
  return {
    templateItemId: item.templateItemId,
    title: item.title,
    helpText: item.helpText || undefined,
    isRequired: Boolean(item.isRequired),
    requiresEvidence: Boolean(item.requiresEvidence),
    allowNote: Boolean(item.allowNote),
    sortOrder: Number.isFinite(Number(item.sortOrder)) ? Number(item.sortOrder) : 0,
    isChecked: Boolean(item.isChecked),
    note: item.note || undefined,
    evidenceUrl: item.evidenceUrl || undefined,
    evidenceFileName: item.evidenceFileName || undefined,
    evidenceContentType: item.evidenceContentType || undefined,
    evidenceSizeBytes: Number.isFinite(Number(item.evidenceSizeBytes)) ? Number(item.evidenceSizeBytes) : undefined,
    checkedByUserId: item.checkedByUserId || undefined,
    checkedAtUtc: item.checkedAtUtc || undefined
  };
}

function mapChecklistHistory(item: ProviderChecklistHistoryApiItem): ProviderChecklistHistoryItem {
  return {
    id: item.id,
    templateItemId: item.templateItemId,
    itemTitle: item.itemTitle,
    previousIsChecked: typeof item.previousIsChecked === 'boolean' ? item.previousIsChecked : undefined,
    newIsChecked: Boolean(item.newIsChecked),
    previousNote: item.previousNote || undefined,
    newNote: item.newNote || undefined,
    previousEvidenceUrl: item.previousEvidenceUrl || undefined,
    newEvidenceUrl: item.newEvidenceUrl || undefined,
    actorUserId: item.actorUserId,
    actorRole: item.actorRole,
    occurredAtUtc: item.occurredAtUtc
  };
}

function mapAppointmentChecklist(payload: ProviderAppointmentChecklistApiResponse): ProviderAppointmentChecklist {
  return {
    appointmentId: payload.appointmentId,
    templateId: payload.templateId || undefined,
    templateName: payload.templateName || undefined,
    categoryName: payload.categoryName,
    isRequiredChecklist: Boolean(payload.isRequiredChecklist),
    requiredItemsCount: Number.isFinite(Number(payload.requiredItemsCount)) ? Number(payload.requiredItemsCount) : 0,
    requiredCompletedCount: Number.isFinite(Number(payload.requiredCompletedCount)) ? Number(payload.requiredCompletedCount) : 0,
    items: (payload.items || [])
      .map(mapChecklistItem)
      .sort((a, b) => a.sortOrder - b.sortOrder),
    history: (payload.history || [])
      .map(mapChecklistHistory)
      .sort((a, b) => new Date(b.occurredAtUtc).getTime() - new Date(a.occurredAtUtc).getTime())
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

function mapChatConversation(item: ProviderChatConversationsApiItem): ProviderChatConversationSummary {
  return {
    requestId: item.requestId,
    providerId: item.providerId,
    counterpartUserId: item.counterpartUserId,
    counterpartRole: item.counterpartRole,
    counterpartName: item.counterpartName,
    title: item.title,
    lastMessagePreview: item.lastMessagePreview,
    lastMessageAt: item.lastMessageAt,
    unreadMessages: Number.isFinite(Number(item.unreadMessages)) ? Number(item.unreadMessages) : 0,
    counterpartIsOnline: Boolean(item.counterpartIsOnline),
    providerStatus: item.providerStatus || undefined
  };
}

function mapChatMessage(item: ProviderChatMessageApiItem): ProviderChatMessage {
  return {
    id: item.id,
    requestId: item.requestId,
    providerId: item.providerId,
    senderId: item.senderId,
    senderName: item.senderName,
    senderRole: item.senderRole,
    text: item.text || undefined,
    createdAt: item.createdAt,
    attachments: (item.attachments || []).map((attachment) => ({
      id: attachment.id,
      fileUrl: attachment.fileUrl,
      fileName: attachment.fileName,
      contentType: attachment.contentType,
      sizeBytes: attachment.sizeBytes,
      mediaKind: attachment.mediaKind
    })),
    deliveredAt: item.deliveredAt || undefined,
    readAt: item.readAt || undefined
  };
}

function mapChatReceipt(item: ProviderChatReceiptApiItem): ProviderChatMessageReceipt {
  return {
    messageId: item.messageId,
    requestId: item.requestId,
    providerId: item.providerId,
    deliveredAt: item.deliveredAt || undefined,
    readAt: item.readAt || undefined
  };
}

function mapBusinessErrorMessage(errorCode?: string, fallbackMessage?: string): string | undefined {
  const normalized = String(errorCode || '').trim().toLowerCase();
  if (!normalized) {
    return fallbackMessage;
  }

  return (
    {
      invalid_state: 'A acao nao pode ser executada no estado atual do atendimento.',
      invalid_request: fallbackMessage || 'Dados invalidos para a operacao solicitada.',
      appointment_not_found: 'Agendamento nao encontrado para este prestador.',
      forbidden: 'Voce nao tem permissao para executar esta operacao.',
      mobile_provider_agenda_reject_reason_required: 'Informe o motivo da recusa para continuar.',
      checklist_not_configured: 'Checklist tecnico nao configurado para este atendimento.',
      evidence_required: 'Este item exige evidencia antes de salvar.',
      invalid_item: 'Item de checklist invalido.',
      item_not_found: 'Item de checklist nao encontrado.',
      invalid_note: 'Observacao invalida para este item.',
      invalid_evidence: 'Evidencia invalida. Verifique tipo e tamanho do arquivo.',
      mobile_provider_checklist_evidence_required: 'Selecione um arquivo de evidencia.',
      mobile_provider_checklist_evidence_too_large: 'Arquivo acima do limite de 25MB.',
      mobile_provider_checklist_evidence_invalid_extension: 'Formato nao permitido. Use JPG, PNG, WEBP, MP4, WEBM ou MOV.',
      mobile_provider_checklist_evidence_invalid_content_type: 'Tipo de arquivo nao permitido para evidencia.',
      mobile_provider_checklist_evidence_invalid_signature: 'Arquivo invalido ou corrompido.'
    }[normalized] || fallbackMessage
  );
}

async function tryReadApiError(response: Response): Promise<ParsedApiError> {
  const contentType = response.headers.get('content-type') || '';
  if (contentType.includes('application/json')) {
    try {
      const payload = await response.json();
      const message = typeof payload?.message === 'string' && payload.message.trim()
        ? payload.message.trim()
        : undefined;
      const errorCode = typeof payload?.errorCode === 'string' && payload.errorCode.trim()
        ? payload.errorCode.trim()
        : undefined;
      return { message, errorCode };
    } catch {
      return {};
    }
  }

  try {
    const text = await response.text();
    return { message: text?.trim() || undefined };
  } catch {
    return {};
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

  const apiError = await tryReadApiError(response);
  const apiMessage = mapBusinessErrorMessage(apiError.errorCode, apiError.message);
  const detailedErrorCode = apiError.errorCode ? ` businessCode=${apiError.errorCode}` : '';

  if (response.status === 401) {
    throw new MobileProviderError('CPM-PROV-REQ-401', apiMessage || 'Sessao expirada. Faca login novamente.', {
      httpStatus: response.status,
      detail: `401 ao chamar ${endpoint}.${detailedErrorCode}`
    });
  }

  if (response.status === 403) {
    throw new MobileProviderError('CPM-PROV-REQ-403', apiMessage || 'Acesso negado para este recurso.', {
      httpStatus: response.status,
      detail: `403 ao chamar ${endpoint}.${detailedErrorCode}`
    });
  }

  if (response.status === 404) {
    throw new MobileProviderError('CPM-PROV-REQ-404', apiMessage || 'Recurso nao encontrado.', {
      httpStatus: response.status,
      detail: `404 ao chamar ${endpoint}.${detailedErrorCode}`
    });
  }

  if (response.status === 409) {
    throw new MobileProviderError('CPM-PROV-REQ-409', apiMessage || 'Conflito de regra de negocio.', {
      httpStatus: response.status,
      detail: `409 ao chamar ${endpoint}.${detailedErrorCode}`
    });
  }

  if (response.status >= 500) {
    throw new MobileProviderError('CPM-PROV-REQ-5XX', apiMessage || 'Erro interno no servidor.', {
      httpStatus: response.status,
      detail: `Erro ${response.status} ao chamar ${endpoint}.${detailedErrorCode}`
    });
  }

  throw new MobileProviderError('CPM-PROV-REQ-4XX', apiMessage || 'Falha na requisicao.', {
    httpStatus: response.status,
    detail: `Erro ${response.status} ao chamar ${endpoint}.${detailedErrorCode}`
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
    const friendlyMessage = mapBusinessErrorMessage(payload?.errorCode || undefined, payload?.errorMessage || undefined);
    throw new MobileProviderError('CPM-PROV-REQ-4XX', friendlyMessage || 'Operacao de agenda nao concluida.', {
      detail: payload?.errorCode
        ? `Erro logico ao chamar ${endpoint}. businessCode=${payload.errorCode}`
        : `Erro logico ao chamar ${endpoint}.`
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

export async function markMobileProviderAgendaArrival(
  token: string,
  appointmentId: string,
  payload?: {
    latitude?: number;
    longitude?: number;
    accuracyMeters?: number;
    manualReason?: string;
  }): Promise<ProviderAgendaItem | undefined> {
  const endpoint = `/api/mobile/provider/agenda/${appointmentId}/arrive`;
  const response = await callProviderApi(token, endpoint, 'POST', {
    latitude: payload?.latitude,
    longitude: payload?.longitude,
    accuracyMeters: payload?.accuracyMeters,
    manualReason: payload?.manualReason
  });
  return parseAgendaOperation(response, endpoint);
}

export async function startMobileProviderAgendaExecution(
  token: string,
  appointmentId: string,
  reason?: string): Promise<ProviderAgendaItem | undefined> {
  const endpoint = `/api/mobile/provider/agenda/${appointmentId}/start`;
  const response = await callProviderApi(token, endpoint, 'POST', { reason });
  return parseAgendaOperation(response, endpoint);
}

export async function updateMobileProviderAgendaOperationalStatus(
  token: string,
  appointmentId: string,
  operationalStatus: string,
  reason?: string): Promise<ProviderAgendaItem | undefined> {
  const endpoint = `/api/mobile/provider/agenda/${appointmentId}/operational-status`;
  const response = await callProviderApi(token, endpoint, 'POST', {
    operationalStatus,
    reason
  });
  return parseAgendaOperation(response, endpoint);
}

export async function fetchMobileProviderAgendaChecklist(
  token: string,
  appointmentId: string): Promise<ProviderAppointmentChecklist> {
  const endpoint = `/api/mobile/provider/agenda/${appointmentId}/checklist`;
  const response = await callProviderApi(token, endpoint, 'GET');
  const ok = await ensureOk(response, endpoint);
  const payload = await ok.json() as ProviderAppointmentChecklistApiResponse;
  return mapAppointmentChecklist(payload);
}

export async function updateMobileProviderAgendaChecklistItem(
  token: string,
  appointmentId: string,
  payload: ProviderChecklistItemUpsertPayload): Promise<ProviderAppointmentChecklist> {
  const endpoint = `/api/mobile/provider/agenda/${appointmentId}/checklist/items`;
  const response = await callProviderApi(token, endpoint, 'POST', {
    templateItemId: payload.templateItemId,
    isChecked: payload.isChecked,
    note: payload.note,
    evidenceUrl: payload.evidenceUrl,
    evidenceFileName: payload.evidenceFileName,
    evidenceContentType: payload.evidenceContentType,
    evidenceSizeBytes: payload.evidenceSizeBytes,
    clearEvidence: payload.clearEvidence ?? false
  });
  const ok = await ensureOk(response, endpoint);
  const checklist = await ok.json() as ProviderAppointmentChecklistApiResponse;
  return mapAppointmentChecklist(checklist);
}

export async function uploadMobileProviderAgendaChecklistEvidence(
  token: string,
  appointmentId: string,
  file: File): Promise<ProviderChecklistEvidenceUploadResult> {
  const endpoint = '/api/mobile/provider/agenda/checklist-evidences/upload';
  const formData = new FormData();
  formData.append('appointmentId', appointmentId);
  formData.append('file', file);

  let response: Response;
  try {
    response = await fetch(`${getApiBaseUrl()}${endpoint}`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${token}`
      },
      body: formData
    });
  } catch {
    throw new MobileProviderError('CPM-PROV-REQ-001', 'Nao foi possivel enviar evidencia do checklist.', {
      detail: `Falha de rede/CORS/SSL ao chamar ${endpoint}.`
    });
  }

  const ok = await ensureOk(response, endpoint);
  const payload = await ok.json() as ProviderChecklistEvidenceUploadResult;
  return {
    fileUrl: payload.fileUrl,
    fileName: payload.fileName,
    contentType: payload.contentType,
    sizeBytes: payload.sizeBytes
  };
}

export async function fetchMobileProviderChatConversations(token: string): Promise<ProviderChatConversationSummary[]> {
  const endpoint = '/api/mobile/provider/chats';
  const response = await callProviderApi(token, endpoint, 'GET');
  const ok = await ensureOk(response, endpoint);
  const payload = await ok.json() as ProviderChatConversationsApiResponse;
  return (payload.conversations || [])
    .map(mapChatConversation)
    .sort((a, b) => new Date(b.lastMessageAt).getTime() - new Date(a.lastMessageAt).getTime());
}

export async function fetchMobileProviderChatMessages(
  token: string,
  requestId: string): Promise<ProviderChatMessage[]> {
  const endpoint = `/api/mobile/provider/chats/${encodeURIComponent(requestId)}/messages`;
  const response = await callProviderApi(token, endpoint, 'GET');
  const ok = await ensureOk(response, endpoint);
  const payload = await ok.json() as ProviderChatMessagesApiResponse;
  return (payload.messages || [])
    .map(mapChatMessage)
    .sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());
}

export async function sendMobileProviderChatMessage(
  token: string,
  requestId: string,
  text?: string,
  attachments?: Array<{
    fileUrl: string;
    fileName: string;
    contentType: string;
    sizeBytes: number;
  }>): Promise<ProviderChatMessage | null> {
  const endpoint = `/api/mobile/provider/chats/${encodeURIComponent(requestId)}/messages`;
  const response = await callProviderApi(token, endpoint, 'POST', {
    text,
    attachments: attachments || []
  });
  const ok = await ensureOk(response, endpoint);
  const payload = await ok.json() as ProviderSendChatMessageApiResponse;
  if (!payload.success) {
    throw new MobileProviderError('CPM-PROV-REQ-4XX', payload.errorMessage || 'Nao foi possivel enviar mensagem.', {
      detail: payload.errorCode || `Erro logico ao chamar ${endpoint}.`
    });
  }

  return payload.message ? mapChatMessage(payload.message) : null;
}

export async function markMobileProviderChatDelivered(
  token: string,
  requestId: string): Promise<ProviderChatMessageReceipt[]> {
  const endpoint = `/api/mobile/provider/chats/${encodeURIComponent(requestId)}/delivered`;
  const response = await callProviderApi(token, endpoint, 'POST');
  const ok = await ensureOk(response, endpoint);
  const payload = await ok.json() as ProviderChatReceiptOperationApiResponse;
  if (!payload.success) {
    throw new MobileProviderError('CPM-PROV-REQ-4XX', payload.errorMessage || 'Nao foi possivel atualizar entrega.', {
      detail: payload.errorCode || `Erro logico ao chamar ${endpoint}.`
    });
  }

  return (payload.receipts || []).map(mapChatReceipt);
}

export async function markMobileProviderChatRead(
  token: string,
  requestId: string): Promise<ProviderChatMessageReceipt[]> {
  const endpoint = `/api/mobile/provider/chats/${encodeURIComponent(requestId)}/read`;
  const response = await callProviderApi(token, endpoint, 'POST');
  const ok = await ensureOk(response, endpoint);
  const payload = await ok.json() as ProviderChatReceiptOperationApiResponse;
  if (!payload.success) {
    throw new MobileProviderError('CPM-PROV-REQ-4XX', payload.errorMessage || 'Nao foi possivel atualizar leitura.', {
      detail: payload.errorCode || `Erro logico ao chamar ${endpoint}.`
    });
  }

  return (payload.receipts || []).map(mapChatReceipt);
}

interface ProviderChatAttachmentUploadApiResponse {
  fileUrl: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
}

export async function uploadMobileProviderChatAttachments(
  token: string,
  requestId: string,
  files: File[]): Promise<Array<{
    fileUrl: string;
    fileName: string;
    contentType: string;
    sizeBytes: number;
  }>> {
  if (!requestId || files.length === 0) {
    return [];
  }

  const uploaded: Array<{
    fileUrl: string;
    fileName: string;
    contentType: string;
    sizeBytes: number;
  }> = [];

  for (const file of files) {
    const formData = new FormData();
    formData.append('requestId', requestId);
    formData.append('file', file);

    let response: Response;
    try {
      response = await fetch(`${getApiBaseUrl()}/api/mobile/provider/chat-attachments/upload`, {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${token}`
        },
        body: formData
      });
    } catch {
      throw new MobileProviderError('CPM-PROV-REQ-001', 'Nao foi possivel enviar anexo para o chat.');
    }

    if (!response.ok) {
      const parsed = await tryReadApiError(response);
      const message = mapBusinessErrorMessage(parsed.errorCode, parsed.message) || 'Falha ao enviar anexo para o chat.';
      throw new MobileProviderError('CPM-PROV-REQ-4XX', message, {
        httpStatus: response.status,
        detail: parsed.errorCode ? `businessCode=${parsed.errorCode}` : undefined
      });
    }

    const payload = await response.json() as ProviderChatAttachmentUploadApiResponse;
    uploaded.push({
      fileUrl: payload.fileUrl,
      fileName: payload.fileName,
      contentType: payload.contentType,
      sizeBytes: payload.sizeBytes
    });
  }

  return uploaded;
}

export function resolveMobileProviderChatAttachmentUrl(fileUrl: string): string {
  const trimmed = String(fileUrl || '').trim();
  if (!trimmed) {
    return '';
  }

  if (trimmed.startsWith('http://') || trimmed.startsWith('https://')) {
    return trimmed;
  }

  return `${getApiBaseUrl()}${trimmed.startsWith('/') ? '' : '/'}${trimmed}`;
}
