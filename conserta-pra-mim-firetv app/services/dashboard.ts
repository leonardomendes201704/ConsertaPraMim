import type { FireTvLandingDashboardData } from '../types';
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

  const response = await fetch(url.toString(), {
    method: 'GET',
    headers: buildAuthHeaders(token),
    cache: 'no-store'
  });

  if (!response.ok) {
    if (response.status === 401) {
      throw new FireTvDashboardApiError('Sessao expirada. Faca login novamente.', 401);
    }

    throw new FireTvDashboardApiError('Nao foi possivel carregar o dashboard da TV.', response.status);
  }

  return await response.json() as FireTvLandingDashboardData;
}
