import type {
  FireTvLandingDashboardData,
  FireTvOperationsDashboardData
} from '../types';
import { buildAuthHeaders, getApiBaseUrl } from './http';

export class FireTvDashboardApiError extends Error {
  public readonly httpStatus?: number;

  constructor(message: string, httpStatus?: number) {
    super(message);
    this.name = 'FireTvDashboardApiError';
    this.httpStatus = httpStatus;
  }
}

export interface FireTvDashboardQuery {
  rangeDays?: number;
  origin?: string;
  comparisonMode?: string;
}

async function executeGet<T>(url: URL, token: string, fallbackMessage: string): Promise<T> {
  const response = await fetch(url.toString(), {
    method: 'GET',
    headers: buildAuthHeaders(token),
    cache: 'no-store'
  });

  if (!response.ok) {
    if (response.status === 401) {
      throw new FireTvDashboardApiError('Sessao expirada. Faca login novamente.', 401);
    }

    throw new FireTvDashboardApiError(fallbackMessage, response.status);
  }

  return await response.json() as T;
}

export async function fetchFireTvLandingDashboard(token: string, query?: FireTvDashboardQuery): Promise<FireTvLandingDashboardData> {
  const url = new URL(`${getApiBaseUrl()}/api/admin/fire-tv/landing-dashboard`);

  if (query?.rangeDays) {
    url.searchParams.set('rangeDays', String(query.rangeDays));
  }

  if (query?.origin) {
    url.searchParams.set('origin', query.origin);
  }

  if (query?.comparisonMode) {
    url.searchParams.set('comparisonMode', query.comparisonMode);
  }

  return await executeGet<FireTvLandingDashboardData>(
    url,
    token,
    'Nao foi possivel carregar o dashboard da landing.');
}

export async function fetchFireTvOperationsDashboard(token: string): Promise<FireTvOperationsDashboardData> {
  const url = new URL(`${getApiBaseUrl()}/api/admin/fire-tv/operations-dashboard`);
  return await executeGet<FireTvOperationsDashboardData>(
    url,
    token,
    'Nao foi possivel carregar a visao operacional.');
}
