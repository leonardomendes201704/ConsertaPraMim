import type { FireTvAuthSession } from '../types';
import { getApiBaseUrl } from './http';

const SESSION_STORAGE_KEY = 'cpm.firetv.auth.session';
const HEALTH_TIMEOUT_MS = 5000;
const LOGIN_TIMEOUT_MS = 12000;

interface LoginApiResponse {
  userId: string;
  token: string;
  userName: string;
  role: string;
  email: string;
}

export class FireTvAuthApiError extends Error {
  public readonly httpStatus?: number;

  constructor(message: string, options?: { httpStatus?: number }) {
    super(message);
    this.name = 'FireTvAuthApiError';
    this.httpStatus = options?.httpStatus;
  }
}

function createTimeoutController(timeoutMs: number): { controller: AbortController; timerId: number } {
  const controller = new AbortController();
  const timerId = window.setTimeout(() => controller.abort(), timeoutMs);
  return { controller, timerId };
}

function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError';
}

function decodeJwtPayload(token: string): { exp?: number } | null {
  const parts = token.split('.');
  if (parts.length < 2) {
    return null;
  }

  try {
    const normalized = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const padded = normalized.padEnd(Math.ceil(normalized.length / 4) * 4, '=');
    return JSON.parse(atob(padded)) as { exp?: number };
  } catch {
    return null;
  }
}

function isSessionExpired(token: string): boolean {
  const payload = decodeJwtPayload(token);
  if (!payload?.exp) {
    return false;
  }

  return payload.exp <= Math.floor(Date.now() / 1000) + 30;
}

export async function checkFireTvApiHealth(): Promise<boolean> {
  const { controller, timerId } = createTimeoutController(HEALTH_TIMEOUT_MS);

  try {
    const response = await fetch(`${getApiBaseUrl()}/health`, {
      method: 'GET',
      headers: { Accept: 'text/plain, application/json' },
      signal: controller.signal
    });

    return response.ok;
  } catch {
    return false;
  } finally {
    window.clearTimeout(timerId);
  }
}

export async function loginFireTvAdmin(email: string, password: string): Promise<FireTvAuthSession> {
  const { controller, timerId } = createTimeoutController(LOGIN_TIMEOUT_MS);

  try {
    const response = await fetch(`${getApiBaseUrl()}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: email.trim(), password }),
      signal: controller.signal
    });

    if (!response.ok) {
      if (response.status === 401) {
        throw new FireTvAuthApiError('E-mail ou senha invalidos.', { httpStatus: 401 });
      }

      if (response.status === 403) {
        throw new FireTvAuthApiError('Acesso restrito a administradores.', { httpStatus: 403 });
      }

      throw new FireTvAuthApiError('Nao foi possivel autenticar no momento.', { httpStatus: response.status });
    }

    const payload = await response.json() as LoginApiResponse;
    if ((payload.role || '').trim() !== 'Admin') {
      throw new FireTvAuthApiError('Esta aplicacao exige uma conta com role Admin.', { httpStatus: 403 });
    }

    return {
      userId: payload.userId,
      token: payload.token,
      userName: payload.userName,
      role: payload.role,
      email: payload.email,
      loggedInAtIso: new Date().toISOString()
    };
  } catch (error) {
    if (isAbortError(error)) {
      throw new FireTvAuthApiError('Tempo limite excedido ao autenticar.');
    }

    if (error instanceof FireTvAuthApiError) {
      throw error;
    }

    throw new FireTvAuthApiError('Falha de conexao com a API.');
  } finally {
    window.clearTimeout(timerId);
  }
}

export function loadFireTvSession(): FireTvAuthSession | null {
  try {
    const raw = window.localStorage.getItem(SESSION_STORAGE_KEY);
    if (!raw) {
      return null;
    }

    const parsed = JSON.parse(raw) as FireTvAuthSession;
    if (!parsed?.token || isSessionExpired(parsed.token)) {
      clearFireTvSession();
      return null;
    }

    return parsed;
  } catch {
    clearFireTvSession();
    return null;
  }
}

export function saveFireTvSession(session: FireTvAuthSession): void {
  window.localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session));
}

export function clearFireTvSession(): void {
  window.localStorage.removeItem(SESSION_STORAGE_KEY);
}
