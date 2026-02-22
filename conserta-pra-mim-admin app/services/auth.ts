import type { AdminAuthSession } from '../types';
import { getApiBaseUrl } from './http';

const SESSION_STORAGE_KEY = 'cpm.admin.auth.session';
const HEALTH_TIMEOUT_MS = 5000;
const LOGIN_TIMEOUT_MS = 12000;

interface LoginApiResponse {
  userId: string;
  token: string;
  userName: string;
  role: string;
  email: string;
}

export type AdminApiIssueCode =
  | 'CPM-ADMIN-API-001'
  | 'CPM-ADMIN-API-002'
  | 'CPM-ADMIN-API-003'
  | 'CPM-ADMIN-AUTH-001'
  | 'CPM-ADMIN-AUTH-002'
  | 'CPM-ADMIN-AUTH-003'
  | 'CPM-ADMIN-AUTH-401'
  | 'CPM-ADMIN-AUTH-403'
  | 'CPM-ADMIN-AUTH-4XX'
  | 'CPM-ADMIN-AUTH-5XX';

export interface AdminApiHealthCheckResult {
  available: boolean;
  code?: AdminApiIssueCode;
  message: string;
  detail?: string;
  httpStatus?: number;
}

export class AdminAuthApiError extends Error {
  public readonly code: AdminApiIssueCode;
  public readonly detail?: string;
  public readonly httpStatus?: number;

  constructor(code: AdminApiIssueCode, message: string, options?: { detail?: string; httpStatus?: number }) {
    super(message);
    this.name = 'AdminAuthApiError';
    this.code = code;
    this.detail = options?.detail;
    this.httpStatus = options?.httpStatus;
  }
}

export function getAdminApiBaseUrl(): string {
  return getApiBaseUrl();
}

function createTimeoutController(timeoutMs: number): { controller: AbortController; timerId: number } {
  const controller = new AbortController();
  const timerId = window.setTimeout(() => controller.abort(), timeoutMs);
  return { controller, timerId };
}

function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError';
}

async function readErrorMessage(response: Response): Promise<string> {
  const contentType = response.headers.get('content-type') || '';
  if (contentType.includes('application/json')) {
    try {
      const payload = await response.json();
      if (typeof payload?.message === 'string' && payload.message.trim()) {
        return payload.message;
      }
      if (typeof payload?.errorMessage === 'string' && payload.errorMessage.trim()) {
        return payload.errorMessage;
      }
      if (typeof payload === 'string' && payload.trim()) {
        return payload;
      }
    } catch {
      return 'Falha na autenticacao.';
    }
  }

  const text = await response.text();
  return text.trim() || 'Falha na autenticacao.';
}

function decodeJwtPayload(token: string): { exp?: number } | null {
  const parts = token.split('.');
  if (parts.length < 2) {
    return null;
  }

  try {
    const normalized = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const padded = normalized.padEnd(Math.ceil(normalized.length / 4) * 4, '=');
    const decoded = atob(padded);
    return JSON.parse(decoded) as { exp?: number };
  } catch {
    return null;
  }
}

function isSessionExpired(token: string): boolean {
  const payload = decodeJwtPayload(token);
  if (!payload?.exp) {
    return false;
  }

  const nowInSeconds = Math.floor(Date.now() / 1000);
  return payload.exp <= nowInSeconds + 30;
}

function isStoredSessionValid(session: AdminAuthSession): boolean {
  if (!session?.token || !session?.email || !session?.userId) {
    return false;
  }

  if ((session.role || '').trim() !== 'Admin') {
    return false;
  }

  return !isSessionExpired(session.token);
}

export async function checkAdminApiHealth(): Promise<AdminApiHealthCheckResult> {
  const { controller, timerId } = createTimeoutController(HEALTH_TIMEOUT_MS);

  try {
    const response = await fetch(`${getAdminApiBaseUrl()}/health`, {
      method: 'GET',
      headers: { Accept: 'text/plain, application/json' },
      signal: controller.signal
    });

    if (!response.ok) {
      return {
        available: false,
        code: 'CPM-ADMIN-API-003',
        message: 'API indisponivel no momento.',
        detail: `Health-check retornou HTTP ${response.status}.`,
        httpStatus: response.status
      };
    }

    return {
      available: true,
      message: 'API disponivel.'
    };
  } catch (error) {
    if (isAbortError(error)) {
      return {
        available: false,
        code: 'CPM-ADMIN-API-002',
        message: 'Timeout ao consultar health-check.',
        detail: 'A API nao respondeu dentro do tempo esperado.'
      };
    }

    return {
      available: false,
      code: 'CPM-ADMIN-API-001',
      message: 'Falha de conexao com a API.',
      detail: 'Erro de rede/CORS/SSL ao acessar /health.'
    };
  } finally {
    window.clearTimeout(timerId);
  }
}

export async function loginAdminWithEmailPassword(email: string, password: string): Promise<AdminAuthSession> {
  const { controller, timerId } = createTimeoutController(LOGIN_TIMEOUT_MS);
  let response: Response;

  try {
    response = await fetch(`${getAdminApiBaseUrl()}/api/auth/login`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        email: email.trim(),
        password
      }),
      signal: controller.signal
    });
  } catch (error) {
    if (isAbortError(error)) {
      throw new AdminAuthApiError('CPM-ADMIN-AUTH-002', 'Tempo limite excedido ao autenticar.', {
        detail: 'Timeout no endpoint /api/auth/login.'
      });
    }

    throw new AdminAuthApiError('CPM-ADMIN-AUTH-001', 'Falha de conexao ao autenticar.', {
      detail: 'Erro de rede/CORS/SSL no endpoint /api/auth/login.'
    });
  } finally {
    window.clearTimeout(timerId);
  }

  if (!response.ok) {
    if (response.status === 401) {
      throw new AdminAuthApiError('CPM-ADMIN-AUTH-401', 'E-mail ou senha invalidos.', {
        httpStatus: 401
      });
    }

    if (response.status >= 500) {
      throw new AdminAuthApiError('CPM-ADMIN-AUTH-5XX', 'Servico de autenticacao indisponivel.', {
        httpStatus: response.status
      });
    }

    const message = await readErrorMessage(response);
    throw new AdminAuthApiError('CPM-ADMIN-AUTH-4XX', message, {
      httpStatus: response.status
    });
  }

  const payload = await response.json() as LoginApiResponse;
  if (!payload?.token || !payload?.userId) {
    throw new AdminAuthApiError('CPM-ADMIN-AUTH-003', 'Payload de autenticacao invalido.', {
      detail: 'Token ou userId ausente no login.'
    });
  }

  if ((payload.role || '').trim() !== 'Admin') {
    throw new AdminAuthApiError('CPM-ADMIN-AUTH-403', 'Este app e exclusivo para administradores.', {
      detail: `Role recebida: ${payload.role || 'desconhecida'}.`
    });
  }

  return {
    userId: payload.userId,
    token: payload.token,
    userName: payload.userName,
    role: payload.role,
    email: payload.email,
    loggedInAtIso: new Date().toISOString()
  };
}

export function loadAdminAuthSession(): AdminAuthSession | null {
  try {
    const raw = window.localStorage.getItem(SESSION_STORAGE_KEY);
    if (!raw) {
      return null;
    }

    const parsed = JSON.parse(raw) as AdminAuthSession;
    if (!isStoredSessionValid(parsed)) {
      clearAdminAuthSession();
      return null;
    }

    return parsed;
  } catch {
    clearAdminAuthSession();
    return null;
  }
}

export function saveAdminAuthSession(session: AdminAuthSession): void {
  window.localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session));
}

export function clearAdminAuthSession(): void {
  window.localStorage.removeItem(SESSION_STORAGE_KEY);
}