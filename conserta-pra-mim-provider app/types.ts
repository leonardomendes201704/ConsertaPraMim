export type ProviderAppState = 'SPLASH' | 'AUTH' | 'DASHBOARD' | 'REQUEST_DETAILS' | 'PROPOSALS' | 'AGENDA' | 'PROFILE';

export interface ProviderAuthSession {
  userId: string;
  token: string;
  userName: string;
  role: string;
  email: string;
}

export interface ProviderDashboardKpis {
  nearbyRequestsCount: number;
  activeProposalsCount: number;
  acceptedProposalsCount: number;
  pendingAppointmentsCount: number;
  upcomingConfirmedVisitsCount: number;
}

export interface ProviderRequestCard {
  id: string;
  category: string;
  categoryIcon: string;
  description: string;
  status: string;
  createdAtUtc: string;
  createdAtLabel: string;
  street: string;
  city: string;
  zip: string;
  distanceKm?: number;
  estimatedValue?: number;
  alreadyProposed: boolean;
}

export interface ProviderAgendaHighlight {
  appointmentId: string;
  serviceRequestId: string;
  status: string;
  statusLabel: string;
  windowStartUtc: string;
  windowEndUtc: string;
  category?: string;
  clientName?: string;
  windowLabel: string;
}

export interface ProviderAgendaData {
  pendingItems: ProviderAgendaItem[];
  upcomingItems: ProviderAgendaItem[];
  pendingCount: number;
  upcomingCount: number;
}

export interface ProviderAgendaItem {
  appointmentId: string;
  serviceRequestId: string;
  appointmentStatus: string;
  appointmentStatusLabel: string;
  windowStartUtc: string;
  windowEndUtc: string;
  windowLabel: string;
  category?: string;
  description?: string;
  clientName?: string;
  street?: string;
  city?: string;
  zip?: string;
  canConfirm: boolean;
  canReject: boolean;
  canRespondReschedule: boolean;
}

export interface ProviderDashboardData {
  providerName: string;
  kpis: ProviderDashboardKpis;
  nearbyRequests: ProviderRequestCard[];
  agendaHighlights: ProviderAgendaHighlight[];
}

export interface ProviderProposalSummary {
  id: string;
  requestId: string;
  estimatedValue?: number;
  message?: string;
  accepted: boolean;
  invalidated: boolean;
  statusLabel: string;
  createdAtUtc: string;
  createdAtLabel: string;
}

export interface ProviderRequestDetailsData {
  request: ProviderRequestCard;
  existingProposal?: ProviderProposalSummary;
  canSubmitProposal: boolean;
}

export interface ProviderProposalsData {
  items: ProviderProposalSummary[];
  totalCount: number;
  acceptedCount: number;
  openCount: number;
}

export interface ProviderApiIssue {
  code: string;
  title: string;
  message: string;
  detail?: string;
  developerHint?: string;
  httpStatus?: number;
}

export interface ProviderCreateProposalPayload {
  estimatedValue?: number;
  message?: string;
}
