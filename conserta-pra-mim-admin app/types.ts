export type AdminAppView = 'SPLASH' | 'AUTH' | 'HOME';

export type AdminHomeTab = 'dashboard' | 'monitoring' | 'support' | 'settings';

export type MonitoringRangePreset = '1h' | '24h' | '7d';

export interface AdminAuthSession {
  userId: string;
  token: string;
  userName: string;
  role: string;
  email: string;
  loggedInAtIso: string;
}

export interface AdminRecentEvent {
  type: string;
  referenceId: string;
  createdAt: string;
  title: string;
  description?: string | null;
}

export interface AdminDashboardData {
  totalUsers: number;
  totalProviders: number;
  totalClients: number;
  totalRequests: number;
  activeRequests: number;
  payingProviders: number;
  monthlySubscriptionRevenue: number;
  activeChatConversationsLast24h: number;
  recentEvents: AdminRecentEvent[];
}

export interface AdminMonitoringTimeseriesPoint {
  bucketUtc: string;
  value: number;
}

export interface AdminMonitoringLatencyTimeseriesPoint {
  bucketUtc: string;
  p50Ms: number;
  p95Ms: number;
  p99Ms: number;
}

export interface AdminMonitoringStatusDistribution {
  statusCode: number;
  count: number;
}

export interface AdminMonitoringTopError {
  errorKey: string;
  errorType: string;
  message: string;
  count: number;
  endpointTemplate?: string | null;
  statusCode?: number | null;
}

export interface AdminMonitoringOverviewData {
  totalRequests: number;
  errorRatePercent: number;
  p95LatencyMs: number;
  requestsPerMinute: number;
  topEndpoint: string;
  requestsSeries: AdminMonitoringTimeseriesPoint[];
  errorsSeries: AdminMonitoringTimeseriesPoint[];
  latencySeries: AdminMonitoringLatencyTimeseriesPoint[];
  statusDistribution: AdminMonitoringStatusDistribution[];
  topErrors: AdminMonitoringTopError[];
  apiUptimeSeconds: number;
  apiHealthStatus: string;
  databaseHealthStatus: string;
  clientPortalHealthStatus: string;
  providerPortalHealthStatus: string;
}

export interface AdminMonitoringTopEndpoint {
  method: string;
  endpointTemplate: string;
  hits: number;
  errorRatePercent: number;
  p95LatencyMs: number;
  p99LatencyMs: number;
  warningCount: number;
}

export interface AdminSupportTicketQueueIndicators {
  openCount: number;
  inProgressCount: number;
  waitingProviderCount: number;
  resolvedCount: number;
  closedCount: number;
  withoutFirstAdminResponseCount: number;
  overdueWithoutFirstResponseCount: number;
  unassignedCount: number;
}

export interface AdminSupportTicketSummary {
  id: string;
  providerId: string;
  providerName: string;
  providerEmail: string;
  assignedAdminUserId?: string | null;
  assignedAdminName?: string | null;
  subject: string;
  category: string;
  priority: string;
  status: string;
  openedAtUtc: string;
  lastInteractionAtUtc: string;
  firstAdminResponseAtUtc?: string | null;
  closedAtUtc?: string | null;
  messageCount: number;
  lastMessagePreview?: string | null;
  isOverdueFirstResponse: boolean;
}

export interface AdminSupportTicketAttachment {
  id: string;
  fileUrl: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  mediaKind: string;
}

export interface AdminSupportTicketMessage {
  id: string;
  authorUserId?: string | null;
  authorRole: string;
  authorName: string;
  messageType: string;
  messageText: string;
  isInternal: boolean;
  metadataJson?: string | null;
  attachments: AdminSupportTicketAttachment[];
  createdAtUtc: string;
}

export interface AdminSupportTicketDetails {
  ticket: AdminSupportTicketSummary;
  metadataJson?: string | null;
  messages: AdminSupportTicketMessage[];
}

export interface AdminSupportTicketsListResponse {
  items: AdminSupportTicketSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  indicators: AdminSupportTicketQueueIndicators;
}