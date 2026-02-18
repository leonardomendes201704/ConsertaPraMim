export type ProviderAppState =
  'SPLASH'
  | 'AUTH'
  | 'DASHBOARD'
  | 'COVERAGE_MAP'
  | 'REQUEST_DETAILS'
  | 'PROPOSALS'
  | 'AGENDA'
  | 'CHAT_LIST'
  | 'CHAT'
  | 'PROFILE';

export interface ProviderAuthSession {
  userId: string;
  token: string;
  userName: string;
  role: string;
  email: string;
}

export interface ProviderProfileStatusOption {
  value: number;
  name: string;
  label: string;
  selected: boolean;
}

export interface ProviderProfileCategoryOption {
  value: number;
  name: string;
  label: string;
  icon: string;
  selected: boolean;
}

export interface ProviderProfileSettings {
  name: string;
  email: string;
  phone: string;
  role: string;
  profilePictureUrl?: string;
  plan: string;
  onboardingStatus: string;
  isOnboardingCompleted: boolean;
  rating: number;
  reviewCount: number;
  hasOperationalCompliancePending: boolean;
  operationalComplianceNotes?: string;
  radiusKm: number;
  baseZipCode?: string;
  baseLatitude?: number;
  baseLongitude?: number;
  planMaxRadiusKm: number;
  planMaxAllowedCategories: number;
  operationalStatuses: ProviderProfileStatusOption[];
  categories: ProviderProfileCategoryOption[];
}

export interface ProviderResolveZipResult {
  zipCode: string;
  latitude: number;
  longitude: number;
  address: string;
}

export interface ProviderProfileSettingsUpdatePayload {
  radiusKm: number;
  baseZipCode?: string;
  baseLatitude?: number;
  baseLongitude?: number;
  categories: number[];
  operationalStatus: number;
}

export interface ProviderProfileSettingsSaveResult {
  success: boolean;
  message?: string;
  settings?: ProviderProfileSettings;
  errorCode?: string;
  errorMessage?: string;
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

export interface ProviderChecklistItem {
  templateItemId: string;
  title: string;
  helpText?: string;
  isRequired: boolean;
  requiresEvidence: boolean;
  allowNote: boolean;
  sortOrder: number;
  isChecked: boolean;
  note?: string;
  evidenceUrl?: string;
  evidenceFileName?: string;
  evidenceContentType?: string;
  evidenceSizeBytes?: number;
  checkedByUserId?: string;
  checkedAtUtc?: string;
}

export interface ProviderChecklistHistoryItem {
  id: string;
  templateItemId: string;
  itemTitle: string;
  previousIsChecked?: boolean;
  newIsChecked: boolean;
  previousNote?: string;
  newNote?: string;
  previousEvidenceUrl?: string;
  newEvidenceUrl?: string;
  actorUserId: string;
  actorRole: string;
  occurredAtUtc: string;
}

export interface ProviderAppointmentChecklist {
  appointmentId: string;
  templateId?: string;
  templateName?: string;
  categoryName: string;
  isRequiredChecklist: boolean;
  requiredItemsCount: number;
  requiredCompletedCount: number;
  items: ProviderChecklistItem[];
  history: ProviderChecklistHistoryItem[];
}

export interface ProviderChecklistItemUpsertPayload {
  templateItemId: string;
  isChecked: boolean;
  note?: string;
  evidenceUrl?: string;
  evidenceFileName?: string;
  evidenceContentType?: string;
  evidenceSizeBytes?: number;
  clearEvidence?: boolean;
}

export interface ProviderChecklistEvidenceUploadResult {
  fileUrl: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
}

export interface ProviderDashboardData {
  providerName: string;
  kpis: ProviderDashboardKpis;
  nearbyRequests: ProviderRequestCard[];
  agendaHighlights: ProviderAgendaHighlight[];
}

export interface ProviderCoverageMapPin {
  requestId: string;
  category: string;
  categoryIcon: string;
  description: string;
  street: string;
  city: string;
  zip: string;
  createdAtUtc: string;
  latitude: number;
  longitude: number;
  distanceKm: number;
  isWithinInterestRadius: boolean;
  isCategoryMatch: boolean;
}

export interface ProviderCoverageMapData {
  hasBaseLocation: boolean;
  providerLatitude?: number;
  providerLongitude?: number;
  interestRadiusKm?: number;
  mapSearchRadiusKm?: number;
  baseZipCode?: string;
  appliedCategoryFilter?: string;
  appliedMaxDistanceKm?: number;
  pinPage: number;
  pinPageSize: number;
  totalPins: number;
  hasMorePins: boolean;
  pins: ProviderCoverageMapPin[];
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

export interface ProviderAppNotification {
  id: string;
  type: 'MESSAGE' | 'SYSTEM';
  title: string;
  description: string;
  timestamp: string;
  read: boolean;
  requestId?: string;
  providerId?: string;
  counterpartName?: string;
}

export interface ProviderChatAttachment {
  id?: string;
  fileUrl: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  mediaKind: string;
}

export interface ProviderChatMessage {
  id: string;
  requestId: string;
  providerId: string;
  senderId: string;
  senderName: string;
  senderRole: string;
  text?: string;
  createdAt: string;
  attachments: ProviderChatAttachment[];
  deliveredAt?: string;
  readAt?: string;
}

export interface ProviderChatMessageReceipt {
  messageId: string;
  requestId: string;
  providerId: string;
  deliveredAt?: string;
  readAt?: string;
}

export interface ProviderChatConversationSummary {
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
