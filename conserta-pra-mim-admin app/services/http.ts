const DEFAULT_API_BASE_URL = 'http://10.0.2.2:5193';

export function getApiBaseUrl(): string {
  const envValue = String(import.meta.env.VITE_API_BASE_URL || '').trim();
  if (!envValue) {
    return DEFAULT_API_BASE_URL;
  }

  return envValue.replace(/\/$/, '');
}

export function buildAuthHeaders(token?: string, extra?: HeadersInit): HeadersInit {
  const headers: Record<string, string> = {
    Accept: 'application/json',
    ...(extra as Record<string, string> || {})
  };

  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  return headers;
}
