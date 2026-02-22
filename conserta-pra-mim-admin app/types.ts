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

export interface AdminMonitoringTopEndpointsResponse {
  items: AdminMonitoringTopEndpoint[];
}