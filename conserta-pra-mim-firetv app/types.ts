export type FireTvAppView = 'SPLASH' | 'AUTH' | 'DASHBOARD';

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
}

export interface FireTvBreakdownItem {
  label: string;
  count: number;
}

export interface FireTvHeatmapCell {
  row: number;
  column: number;
  hits: number;
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
  allowedRangeDays: number[];
  autoRefreshSeconds: number;
  generatedAtUtc: string;
  fromUtc: string;
  toUtc: string;
  kpis: FireTvDashboardKpi[];
  heatmapRows: number;
  heatmapColumns: number;
  heatmap: FireTvHeatmapCell[];
  topOrigins: FireTvBreakdownItem[];
  topLocalities: FireTvBreakdownItem[];
  recentSessions: FireTvRecentSession[];
}
