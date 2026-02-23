import { getApiBaseUrl } from './auth';

const PROFILE_TIMEOUT_MS = 15000;
const MAX_PROFILE_PICTURE_SIZE_BYTES = 5_000_000;
const ALLOWED_PROFILE_PICTURE_TYPES = new Set([
  'image/jpeg',
  'image/png',
  'image/webp'
]);

export interface ClientProfileData {
  name: string;
  email: string;
  phone: string;
  role: string;
  clientProfileType: number;
  clientPjType?: number | null;
  clientBaseZipCode?: string | null;
  clientBaseStreet?: string | null;
  clientBaseCity?: string | null;
  clientBaseLatitude?: number | null;
  clientBaseLongitude?: number | null;
  profilePictureUrl?: string | null;
}

export interface ClientResolvedLocationData {
  zipCode: string;
  street: string;
  city: string;
  latitude: number;
  longitude: number;
}

export interface ClientProfileLegalTermsStatus {
  audience: string;
  activeVersion: number;
  title: string;
  htmlContent: string;
  publishedAtUtc: string;
  accepted: boolean;
  acceptedAtUtc?: string | null;
  acceptanceSource?: string | null;
}

export class ClientProfileApiError extends Error {
  public readonly code: string;
  public readonly httpStatus?: number;

  constructor(code: string, message: string, httpStatus?: number) {
    super(message);
    this.name = 'ClientProfileApiError';
    this.code = code;
    this.httpStatus = httpStatus;
  }
}

function normalizeBaseUrl(baseUrl: string): string {
  return baseUrl.replace(/\/+$/, '');
}

function createTimeoutController(timeoutMs: number): { controller: AbortController; timerId: number } {
  const controller = new AbortController();
  const timerId = window.setTimeout(() => controller.abort(), timeoutMs);
  return { controller, timerId };
}

function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError';
}

function buildAuthHeaders(token: string): HeadersInit {
  return {
    Authorization: `Bearer ${token}`,
    Accept: 'application/json'
  };
}

async function tryReadErrorMessage(response: Response, fallbackMessage: string): Promise<string> {
  const contentType = response.headers.get('content-type') || '';
  if (contentType.includes('application/json')) {
    try {
      const payload = await response.json();
      if (typeof payload?.message === 'string' && payload.message.trim()) {
        return payload.message;
      }
      if (typeof payload === 'string' && payload.trim()) {
        return payload;
      }
    } catch {
      return fallbackMessage;
    }
  }

  const text = await response.text();
  return text?.trim() || fallbackMessage;
}

export function resolveProfilePictureUrl(profilePictureUrl?: string | null): string {
  const normalized = String(profilePictureUrl || '').trim();
  if (!normalized) {
    return '';
  }

  if (/^https?:\/\//i.test(normalized)) {
    return normalized;
  }

  const baseUrl = normalizeBaseUrl(getApiBaseUrl());
  return normalized.startsWith('/') ? `${baseUrl}${normalized}` : `${baseUrl}/${normalized}`;
}

export async function fetchClientProfile(token: string): Promise<ClientProfileData> {
  const { controller, timerId } = createTimeoutController(PROFILE_TIMEOUT_MS);

  try {
    const response = await fetch(`${normalizeBaseUrl(getApiBaseUrl())}/api/profile`, {
      method: 'GET',
      headers: buildAuthHeaders(token),
      signal: controller.signal
    });

    if (!response.ok) {
      const message = await tryReadErrorMessage(response, 'Nao foi possivel carregar seu perfil.');
      throw new ClientProfileApiError('CPM-PROFILE-LOAD-HTTP', message, response.status);
    }

    return await response.json() as ClientProfileData;
  } catch (error) {
    if (error instanceof ClientProfileApiError) {
      throw error;
    }

    if (isAbortError(error)) {
      throw new ClientProfileApiError('CPM-PROFILE-LOAD-TIMEOUT', 'Timeout ao carregar dados do perfil.');
    }

    throw new ClientProfileApiError('CPM-PROFILE-LOAD-NET', 'Falha de conexao ao carregar o perfil.');
  } finally {
    window.clearTimeout(timerId);
  }
}

export async function fetchClientProfileLegalTermsStatus(token: string): Promise<ClientProfileLegalTermsStatus> {
  const { controller, timerId } = createTimeoutController(PROFILE_TIMEOUT_MS);

  try {
    const response = await fetch(`${normalizeBaseUrl(getApiBaseUrl())}/api/profile/legal-terms`, {
      method: 'GET',
      headers: buildAuthHeaders(token),
      signal: controller.signal
    });

    if (!response.ok) {
      const message = await tryReadErrorMessage(response, 'Nao foi possivel carregar o termo de aceite.');
      throw new ClientProfileApiError('CPM-PROFILE-TERMS-LOAD-HTTP', message, response.status);
    }

    return await response.json() as ClientProfileLegalTermsStatus;
  } catch (error) {
    if (error instanceof ClientProfileApiError) {
      throw error;
    }

    if (isAbortError(error)) {
      throw new ClientProfileApiError('CPM-PROFILE-TERMS-LOAD-TIMEOUT', 'Timeout ao carregar o termo de aceite.');
    }

    throw new ClientProfileApiError('CPM-PROFILE-TERMS-LOAD-NET', 'Falha de conexao ao carregar o termo de aceite.');
  } finally {
    window.clearTimeout(timerId);
  }
}

export async function acceptClientProfileLegalTerms(
  token: string,
  source = 'mobile_client_profile'): Promise<ClientProfileLegalTermsStatus> {
  const { controller, timerId } = createTimeoutController(PROFILE_TIMEOUT_MS);

  try {
    const response = await fetch(`${normalizeBaseUrl(getApiBaseUrl())}/api/profile/legal-terms/accept`, {
      method: 'POST',
      headers: {
        ...buildAuthHeaders(token),
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        accepted: true,
        source
      }),
      signal: controller.signal
    });

    if (!response.ok) {
      const message = await tryReadErrorMessage(response, 'Nao foi possivel registrar o aceite do termo.');
      throw new ClientProfileApiError('CPM-PROFILE-TERMS-ACCEPT-HTTP', message, response.status);
    }

    return await response.json() as ClientProfileLegalTermsStatus;
  } catch (error) {
    if (error instanceof ClientProfileApiError) {
      throw error;
    }

    if (isAbortError(error)) {
      throw new ClientProfileApiError('CPM-PROFILE-TERMS-ACCEPT-TIMEOUT', 'Timeout ao registrar o aceite do termo.');
    }

    throw new ClientProfileApiError('CPM-PROFILE-TERMS-ACCEPT-NET', 'Falha de conexao ao registrar o aceite do termo.');
  } finally {
    window.clearTimeout(timerId);
  }
}

export interface UpdateClientProfilePayload {
  name: string;
  clientProfileType?: number;
  clientPjType?: number;
  clientBaseZipCode?: string;
  clientBaseStreet?: string;
  clientBaseCity?: string;
  clientBaseLatitude?: number;
  clientBaseLongitude?: number;
}

export async function updateClientProfile(token: string, payload: UpdateClientProfilePayload): Promise<ClientProfileData> {
  const normalizedName = String(payload.name || '').trim();
  if (!normalizedName) {
    throw new ClientProfileApiError('CPM-PROFILE-NAME-VALIDATION', 'Informe um nome valido.');
  }

  const { controller, timerId } = createTimeoutController(PROFILE_TIMEOUT_MS);

  try {
    const response = await fetch(`${normalizeBaseUrl(getApiBaseUrl())}/api/profile`, {
      method: 'PUT',
      headers: {
        ...buildAuthHeaders(token),
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        name: normalizedName,
        clientProfileType: Number.isFinite(Number(payload.clientProfileType)) ? Number(payload.clientProfileType) : undefined,
        clientPjType: Number.isFinite(Number(payload.clientPjType)) ? Number(payload.clientPjType) : undefined,
        clientBaseZipCode: String(payload.clientBaseZipCode || '').trim() || undefined,
        clientBaseStreet: String(payload.clientBaseStreet || '').trim() || undefined,
        clientBaseCity: String(payload.clientBaseCity || '').trim() || undefined,
        clientBaseLatitude: Number.isFinite(Number(payload.clientBaseLatitude)) ? Number(payload.clientBaseLatitude) : undefined,
        clientBaseLongitude: Number.isFinite(Number(payload.clientBaseLongitude)) ? Number(payload.clientBaseLongitude) : undefined
      }),
      signal: controller.signal
    });

    if (!response.ok) {
      const message = await tryReadErrorMessage(response, 'Nao foi possivel salvar seu nome.');
      throw new ClientProfileApiError('CPM-PROFILE-NAME-HTTP', message, response.status);
    }

    return await response.json() as ClientProfileData;
  } catch (error) {
    if (error instanceof ClientProfileApiError) {
      throw error;
    }

    if (isAbortError(error)) {
      throw new ClientProfileApiError('CPM-PROFILE-NAME-TIMEOUT', 'Timeout ao salvar o nome.');
    }

    throw new ClientProfileApiError('CPM-PROFILE-NAME-NET', 'Falha de conexao ao salvar o nome.');
  } finally {
    window.clearTimeout(timerId);
  }
}

export async function uploadClientProfilePicture(token: string, file: File): Promise<string> {
  if (!file || file.size <= 0) {
    throw new ClientProfileApiError('CPM-PROFILE-PICTURE-EMPTY', 'Selecione uma imagem valida.');
  }

  if (file.size > MAX_PROFILE_PICTURE_SIZE_BYTES) {
    throw new ClientProfileApiError('CPM-PROFILE-PICTURE-SIZE', 'A imagem deve ter no maximo 5MB.');
  }

  const contentType = String(file.type || '').toLowerCase();
  if (!ALLOWED_PROFILE_PICTURE_TYPES.has(contentType)) {
    throw new ClientProfileApiError('CPM-PROFILE-PICTURE-TYPE', 'Use apenas JPG, PNG ou WEBP.');
  }

  const formData = new FormData();
  formData.append('Folder', 'profiles');
  formData.append('File', file, file.name);

  const { controller, timerId } = createTimeoutController(PROFILE_TIMEOUT_MS);

  try {
    const response = await fetch(`${normalizeBaseUrl(getApiBaseUrl())}/api/files/upload`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${token}`
      },
      body: formData,
      signal: controller.signal
    });

    if (!response.ok) {
      const message = await tryReadErrorMessage(response, 'Nao foi possivel enviar a imagem.');
      throw new ClientProfileApiError('CPM-PROFILE-PICTURE-UPLOAD-HTTP', message, response.status);
    }

    const payload = await response.json() as { relativeUrl?: string; absoluteUrl?: string };
    const rawUrl = String(payload.absoluteUrl || payload.relativeUrl || '').trim();
    if (!rawUrl) {
      throw new ClientProfileApiError('CPM-PROFILE-PICTURE-UPLOAD-PAYLOAD', 'A API nao retornou a URL da imagem.');
    }

    return rawUrl;
  } catch (error) {
    if (error instanceof ClientProfileApiError) {
      throw error;
    }

    if (isAbortError(error)) {
      throw new ClientProfileApiError('CPM-PROFILE-PICTURE-UPLOAD-TIMEOUT', 'Timeout ao enviar a imagem.');
    }

    throw new ClientProfileApiError('CPM-PROFILE-PICTURE-UPLOAD-NET', 'Falha de conexao ao enviar a imagem.');
  } finally {
    window.clearTimeout(timerId);
  }
}

export async function updateClientProfilePicture(token: string, imageUrl: string): Promise<void> {
  const { controller, timerId } = createTimeoutController(PROFILE_TIMEOUT_MS);

  try {
    const response = await fetch(`${normalizeBaseUrl(getApiBaseUrl())}/api/profile/picture`, {
      method: 'PUT',
      headers: {
        ...buildAuthHeaders(token),
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        imageUrl: String(imageUrl || '').trim()
      }),
      signal: controller.signal
    });

    if (!response.ok) {
      const message = await tryReadErrorMessage(response, 'Nao foi possivel atualizar a foto.');
      throw new ClientProfileApiError('CPM-PROFILE-PICTURE-SAVE-HTTP', message, response.status);
    }
  } catch (error) {
    if (error instanceof ClientProfileApiError) {
      throw error;
    }

    if (isAbortError(error)) {
      throw new ClientProfileApiError('CPM-PROFILE-PICTURE-SAVE-TIMEOUT', 'Timeout ao salvar a foto.');
    }

    throw new ClientProfileApiError('CPM-PROFILE-PICTURE-SAVE-NET', 'Falha de conexao ao salvar a foto.');
  } finally {
    window.clearTimeout(timerId);
  }
}

function onlyDigits(value: string): string {
  return String(value || '').replace(/\D/g, '');
}

export async function resolveClientProfileZip(token: string, zipCode: string): Promise<ClientResolvedLocationData> {
  const normalizedZip = onlyDigits(zipCode);
  const { controller, timerId } = createTimeoutController(PROFILE_TIMEOUT_MS);

  try {
    const response = await fetch(
      `${normalizeBaseUrl(getApiBaseUrl())}/api/mobile/client/service-requests/zip-resolution?zipCode=${encodeURIComponent(normalizedZip)}`,
      {
        method: 'GET',
        headers: buildAuthHeaders(token),
        signal: controller.signal
      });

    if (!response.ok) {
      const message = await tryReadErrorMessage(response, 'Nao foi possivel localizar esse CEP.');
      throw new ClientProfileApiError('CPM-PROFILE-ZIP-HTTP', message, response.status);
    }

    const payload = await response.json() as ClientResolvedLocationData;
    return {
      zipCode: String(payload.zipCode || '').trim(),
      street: String(payload.street || '').trim(),
      city: String(payload.city || '').trim(),
      latitude: Number(payload.latitude),
      longitude: Number(payload.longitude)
    };
  } catch (error) {
    if (error instanceof ClientProfileApiError) {
      throw error;
    }

    if (isAbortError(error)) {
      throw new ClientProfileApiError('CPM-PROFILE-ZIP-TIMEOUT', 'Timeout ao consultar o CEP.');
    }

    throw new ClientProfileApiError('CPM-PROFILE-ZIP-NET', 'Falha de conexao ao consultar o CEP.');
  } finally {
    window.clearTimeout(timerId);
  }
}

export async function resolveClientProfileCurrentLocation(
  token: string,
  latitude: number,
  longitude: number): Promise<ClientResolvedLocationData> {
  const { controller, timerId } = createTimeoutController(PROFILE_TIMEOUT_MS);

  try {
    const response = await fetch(
      `${normalizeBaseUrl(getApiBaseUrl())}/api/mobile/client/service-requests/current-location-resolution?latitude=${encodeURIComponent(String(latitude))}&longitude=${encodeURIComponent(String(longitude))}`,
      {
        method: 'GET',
        headers: buildAuthHeaders(token),
        signal: controller.signal
      });

    if (!response.ok) {
      const message = await tryReadErrorMessage(response, 'Nao foi possivel resolver sua localizacao atual.');
      throw new ClientProfileApiError('CPM-PROFILE-CURRENT-LOCATION-HTTP', message, response.status);
    }

    const payload = await response.json() as ClientResolvedLocationData;
    return {
      zipCode: String(payload.zipCode || '').trim(),
      street: String(payload.street || '').trim(),
      city: String(payload.city || '').trim(),
      latitude: Number(payload.latitude),
      longitude: Number(payload.longitude)
    };
  } catch (error) {
    if (error instanceof ClientProfileApiError) {
      throw error;
    }

    if (isAbortError(error)) {
      throw new ClientProfileApiError('CPM-PROFILE-CURRENT-LOCATION-TIMEOUT', 'Timeout ao obter localizacao atual.');
    }

    throw new ClientProfileApiError('CPM-PROFILE-CURRENT-LOCATION-NET', 'Falha de conexao ao obter localizacao atual.');
  } finally {
    window.clearTimeout(timerId);
  }
}
