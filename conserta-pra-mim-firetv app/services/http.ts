const DEFAULT_API_BASE_URL = 'https://api.consertapramim.com';

export function getApiBaseUrl(): string {
  const envValue = String(import.meta.env.VITE_API_BASE_URL || '').trim();
  return (envValue || DEFAULT_API_BASE_URL).replace(/\/$/, '');
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
