
export type AppState = 'SPLASH' | 'ONBOARDING' | 'AUTH' | 'DASHBOARD' | 'NEW_REQUEST' | 'REQUEST_DETAILS' | 'PROPOSAL_DETAILS' | 'CHAT_LIST' | 'CHAT' | 'CATEGORIES' | 'ORDERS' | 'PROFILE' | 'FINISH_SERVICE' | 'NOTIFICATIONS';

export interface AuthSession {
  userId: string;
  token: string;
  userName: string;
  role: string;
  email: string;
}

export interface ServiceCategory {
  id: string;
  name: string;
  icon: string;
  color: string;
}

export interface ServiceRequestCategoryOption {
  id: string;
  name: string;
  slug: string;
  legacyCategory: string;
  icon: string;
}

export interface Notification {
  id: string;
  type: 'STATUS' | 'MESSAGE' | 'PROMO' | 'SYSTEM';
  title: string;
  description: string;
  timestamp: string;
  read: boolean;
  requestId?: string;
  providerId?: string;
  providerName?: string;
}

export interface ServiceRequest {
  id: string;
  title: string;
  status: 'AGUARDANDO' | 'EM_ANDAMENTO' | 'CONCLUIDO' | 'CANCELADO';
  date: string;
  category: string;
  icon: string;
  description?: string;
  proposalCount?: number;
  provider?: {
    id?: string;
    name: string;
    avatar: string;
    rating: number;
    specialty: string;
  };
  aiDiagnosis?: {
    summary: string;
    possibleCauses: string[];
  };
  rating?: number;
  review?: string;
  paymentMethod?: string;
  paidAmount?: string;
}

export interface OrderFlowStep {
  step: number;
  title: string;
  completed: boolean;
  current: boolean;
}

export interface OrderTimelineEvent {
  eventCode: string;
  title: string;
  description: string;
  occurredAt: string;
  relatedEntityType?: string;
  relatedEntityId?: string;
}

export interface ServiceRequestDetailsData {
  order: ServiceRequest;
  flowSteps: OrderFlowStep[];
  timeline: OrderTimelineEvent[];
}

export interface OrderProposalDetailsData {
  order: ServiceRequest;
  proposal: {
    id: string;
    orderId: string;
    providerId: string;
    providerName: string;
    estimatedValue?: number;
    estimatedLeadTimeHours?: number;
    warrantyDays?: number;
    message?: string;
    accepted: boolean;
    invalidated: boolean;
    statusLabel: string;
    sentAt: string;
  };
  currentAppointment?: ProposalAppointmentSummary;
}

export type OrderProposalComparisonSortBy =
  | 'best_score'
  | 'lowest_price'
  | 'fastest_lead_time'
  | 'best_rating'
  | 'highest_warranty';

export interface OrderProposalComparisonItem {
  proposalId: string;
  orderId: string;
  providerId: string;
  providerName: string;
  estimatedValue?: number;
  estimatedLeadTimeHours?: number;
  warrantyDays?: number;
  providerRating: number;
  providerReviewCount: number;
  providerCompletedServices: number;
  responseTimeMinutes: number;
  accepted: boolean;
  invalidated: boolean;
  statusLabel: string;
  sentAt: string;
  comparisonScore: number;
}

export interface OrderProposalComparisonData {
  orderId: string;
  experimentGroup: string;
  sortBy: OrderProposalComparisonSortBy;
  availableSortOptions: OrderProposalComparisonSortBy[];
  summary: {
    totalProposals: number;
    lowestPrice?: number;
    highestPrice?: number;
    fastestLeadTimeHours?: number;
    highestWarrantyDays?: number;
  };
  proposals: OrderProposalComparisonItem[];
}

export interface ProposalAppointmentSummary {
  id: string;
  orderId: string;
  proposalId: string;
  providerId: string;
  providerName: string;
  status: string;
  statusLabel: string;
  windowStartUtc: string;
  windowEndUtc: string;
  windowLabel: string;
  createdAtUtc: string;
  updatedAtUtc?: string;
}

export interface ProposalScheduleSlot {
  windowStartUtc: string;
  windowEndUtc: string;
  label: string;
}

export interface ChatAttachment {
  id?: string;
  fileUrl: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  mediaKind: string;
}

export interface ChatMessage {
  id: string;
  requestId: string;
  providerId: string;
  senderId: string;
  senderName: string;
  senderRole: string;
  text?: string;
  createdAt: string;
  attachments: ChatAttachment[];
  deliveredAt?: string;
  readAt?: string;
}

export interface ChatMessageReceipt {
  messageId: string;
  requestId: string;
  providerId: string;
  deliveredAt?: string;
  readAt?: string;
}

export interface ChatConversationSummary {
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
  providerStatus?: string;
}
