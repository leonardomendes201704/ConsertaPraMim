export type FireTvAppView =
  | 'SPLASH'
  | 'AUTH'
  | 'MENU'
  | 'LANDING_DASHBOARD'
  | 'OPERATIONS_DASHBOARD';

export interface FireTvAuthSession {
  userId: string;
  token: string;
  userName: string;
  role: string;
  email: string;
  loggedInAtIso: string;
}

export interface FireTvDashboardKpi {
  key: string;
  label: string;
  value: string;
  helperText?: string | null;
  tone: string;
  previousValue?: string | null;
  comparisonValue?: string | null;
  comparisonLabel?: string | null;
  comparisonTone?: string | null;
}

export interface FireTvBreakdownItem {
  label: string;
  count: number;
}

export interface FireTvDashboardFilterOption {
  value: string;
  label: string;
}

export interface FireTvHeatmapCell {
  row: number;
  column: number;
  hits: number;
}

export interface FireTvScrollmapBucket {
  milestonePercent: number;
  sessionsReached: number;
  sessionReachRatePercent: number;
}

export interface FireTvElementRankingItem {
  elementKey: string;
  label: string;
  href?: string | null;
  clicks: number;
  uniqueSessions: number;
  sessionRatePercent: number;
}

export interface FireTvRecentSession {
  sessionId: string;
  path: string;
  estimatedLocality: string;
  lastActivityLabel: string;
  leadStatusLabel: string;
  activeSeconds: number;
  maxScrollPercent: number;
}

export interface FireTvLandingDashboardData {
  enabled: boolean;
  appTitle: string;
  appSubtitle: string;
  selectedRangeDays: number;
  selectedOrigin: string;
  selectedComparisonMode: string;
  allowedRangeDays: number[];
  originOptions: FireTvDashboardFilterOption[];
  comparisonOptions: FireTvDashboardFilterOption[];
  showComparison: boolean;
  autoRefreshSeconds: number;
  generatedAtUtc: string;
  fromUtc: string;
  toUtc: string;
  comparisonFromUtc?: string | null;
  comparisonToUtc?: string | null;
  comparisonLabel?: string | null;
  kpis: FireTvDashboardKpi[];
  showHeatmap: boolean;
  heatmapRows: number;
  heatmapColumns: number;
  heatmap: FireTvHeatmapCell[];
  showScrollmap: boolean;
  scrollmap: FireTvScrollmapBucket[];
  showElementRanking: boolean;
  topElements: FireTvElementRankingItem[];
  topOrigins: FireTvBreakdownItem[];
  topLocalities: FireTvBreakdownItem[];
  recentSessions: FireTvRecentSession[];
}

export interface FireTvHealthTargetStatus {
  key: string;
  label: string;
  url: string;
  healthy: boolean;
  latencyMs?: number | null;
  statusLabel: string;
  detail?: string | null;
}

export interface FireTvOperationalCategory {
  category: string;
  count: number;
  percent: number;
}

export interface FireTvOperationalMapPoint {
  id: string;
  pointType: 'provider' | 'request' | string;
  label: string;
  subtitle: string;
  latitude: number;
  longitude: number;
  tone: string;
}

export interface FireTvOperationalDailySeriesItem {
  label: string;
  requests: number;
  attendances: number;
}

export interface FireTvOperationalRecentActivity {
  timeLabel: string;
  title: string;
  subtitle: string;
  tone: string;
}

export interface FireTvOperationsDashboardData {
  enabled: boolean;
  appTitle: string;
  appSubtitle: string;
  refreshSeconds: number;
  pulseSeconds: number;
  historyDays: number;
  generatedAtUtc: string;
  realtimeConnected: boolean;
  overallStatus: string;
  averageLatencyMs?: number | null;
  healthyTargets: number;
  totalTargets: number;
  healthTargets: FireTvHealthTargetStatus[];
  kpis: FireTvDashboardKpi[];
  categories: FireTvOperationalCategory[];
  providerPoints: FireTvOperationalMapPoint[];
  requestPoints: FireTvOperationalMapPoint[];
  dailySeries: FireTvOperationalDailySeriesItem[];
  recentActivity: FireTvOperationalRecentActivity[];
}
