import { AuthSession } from '../types';

import { Capacitor } from '@capacitor/core';
import { BiometricAuth, BiometryError, BiometryErrorType } from '@aparajita/capacitor-biometric-auth';
import { SecureStorage } from '@aparajita/capacitor-secure-storage';

const AUTH_STORAGE_KEY = 'conserta.auth.session';
const BIOMETRIC_ENABLED_KEY = 'conserta.auth.biometric.enabled';
const BIOMETRIC_SESSION_KEY = 'conserta.auth.biometric.session';
const HEALTH_TIMEOUT_MS = 5000;
const LOGIN_TIMEOUT_MS = 12000;

interface LoginApiResponse {
  userId: string;
  token: string;
  userName: string;
  role: string;
  email: string;
}

export interface BiometricLoginState {
  isNativeRuntime: boolean;
  isBiometryAvailable: boolean;
  isBiometricLoginEnabled: boolean;
  hasStoredBiometricSession: boolean;
}

export type ApiIssueCode =
  | 'CPM-API-001'
  | 'CPM-API-002'
  | 'CPM-API-003'
  | 'CPM-API-004'
  | 'CPM-API-005'
  | 'CPM-AUTH-001'
  | 'CPM-AUTH-002'
  | 'CPM-AUTH-003'
  | 'CPM-AUTH-4XX'
  | 'CPM-AUTH-401'
  | 'CPM-AUTH-403'
  | 'CPM-AUTH-5XX';

interface ApiIssueMeta {
  title: string;
  developerHint: string;
}

export const API_ISSUE_CATALOG: Record<ApiIssueCode, ApiIssueMeta> = {
  'CPM-API-001': {
    title: 'API indisponivel (rede/CORS/SSL)',
    developerHint: 'Falha de conexao. Verifique API online, CORS, certificado HTTPS e VITE_API_BASE_URL.'
  },
  'CPM-API-002': {
    title: 'Timeout no health-check',
    developerHint: 'A API nao respondeu no tempo esperado. Verifique latencia, carga ou travamento.'
  },
  'CPM-API-003': {
    title: 'Health-check retornou 5xx',
    developerHint: 'Erro interno da API. Inspecione logs e dependencias (DB, services, migrations).'
  },
  'CPM-API-004': {
    title: 'Health-check retornou 4xx',
    developerHint: 'Endpoint de health bloqueado ou caminho invalido. Verifique MapHealthChecks e proxy.'
  },
  'CPM-API-005': {
    title: 'Health-check invalido',
    developerHint: 'Resposta inesperada no endpoint de health. Confirme formato e status HTTP.'
  },
  'CPM-AUTH-001': {
    title: 'Falha de conexao no login',
    developerHint: 'Erro de rede/CORS ao chamar /api/auth/login.'
  },
  'CPM-AUTH-002': {
    title: 'Timeout no login',
    developerHint: 'A API nao respondeu no login dentro do timeout configurado.'
  },
  'CPM-AUTH-003': {
    title: 'Payload de login invalido',
    developerHint: 'A resposta da API nao trouxe token esperado.'
  },
  'CPM-AUTH-4XX': {
    title: 'Erro de requisicao no login',
    developerHint: 'Revise contrato da API, campos enviados e validacoes de entrada.'
  },
  'CPM-AUTH-401': {
    title: 'Credenciais invalidas',
    developerHint: 'Email/senha incorretos para usuario existente.'
  },
  'CPM-AUTH-403': {
    title: 'Perfil nao permitido',
    developerHint: 'O app aceita apenas role Client.'
  },
  'CPM-AUTH-5XX': {
    title: 'Erro interno no login',
    developerHint: 'Erro de servidor na autenticacao. Verificar logs da API.'
  }
};

export interface ApiHealthCheckResult {
  available: boolean;
  code?: ApiIssueCode;
  message: string;
  detail?: string;
  httpStatus?: number;
  developerHint?: string;
}

export class AppApiError extends Error {
  public readonly code: ApiIssueCode;
  public readonly detail?: string;
  public readonly httpStatus?: number;

  constructor(code: ApiIssueCode, message: string, options?: { detail?: string; httpStatus?: number }) {
    super(message);
    this.name = 'AppApiError';
    this.code = code;
    this.detail = options?.detail;
    this.httpStatus = options?.httpStatus;
  }
}

export class AppBiometricError extends Error {
  public readonly code: string;

  constructor(code: string, message: string) {
    super(message);
    this.name = 'AppBiometricError';
    this.code = code;
  }
}

function normalizeBaseUrl(baseUrl: string): string {
  return baseUrl.replace(/\/+$/, '');
}

function isNativeRuntime(): boolean {
  return Capacitor.getPlatform() !== 'web';
}

export function getApiBaseUrl(): string {
  const fromEnv = (import.meta.env.VITE_API_BASE_URL || '').trim();
  return normalizeBaseUrl(fromEnv || 'http://localhost:5193');
}

function createTimeoutController(timeoutMs: number): { controller: AbortController; timerId: number } {
  const controller = new AbortController();
  const timerId = window.setTimeout(() => controller.abort(), timeoutMs);
  return { controller, timerId };
}

function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError';
}

function buildCatalogHint(code: ApiIssueCode): string {
  return API_ISSUE_CATALOG[code]?.developerHint || 'Sem dica tecnica cadastrada.';
}

async function tryReadErrorMessage(response: Response): Promise<string> {
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
      return 'Falha na autenticacao.';
    }
  }

  const text = await response.text();
  return text?.trim() || 'Falha na autenticacao.';
}

export async function checkApiHealth(): Promise<ApiHealthCheckResult> {
  const { controller, timerId } = createTimeoutController(HEALTH_TIMEOUT_MS);

  try {
    const response = await fetch(`${getApiBaseUrl()}/health`, {
      method: 'GET',
      headers: {
        Accept: 'text/plain, application/json'
      },
      signal: controller.signal
    });

    if (!response.ok) {
      const code: ApiIssueCode = response.status >= 500 ? 'CPM-API-003' : 'CPM-API-004';
      return {
        available: false,
        code,
        message: 'Desculpe o transtorno, estamos em manutencao no momento.',
        detail: `Health-check retornou HTTP ${response.status}.`,
        httpStatus: response.status,
        developerHint: buildCatalogHint(code)
      };
    }

    return {
      available: true,
      message: 'API disponivel.'
    };
  } catch (error) {
    if (isAbortError(error)) {
      const code: ApiIssueCode = 'CPM-API-002';
      return {
        available: false,
        code,
        message: 'Desculpe o transtorno, estamos em manutencao no momento.',
        detail: 'Timeout ao verificar disponibilidade da API.',
        developerHint: buildCatalogHint(code)
      };
    }

    const code: ApiIssueCode = 'CPM-API-001';
    return {
      available: false,
      code,
      message: 'Desculpe o transtorno, estamos em manutencao no momento.',
      detail: 'Falha de rede/CORS/SSL ao acessar o endpoint de health.',
      developerHint: buildCatalogHint(code)
    };
  } finally {
    window.clearTimeout(timerId);
  }
}

export async function loginWithEmailPassword(email: string, password: string): Promise<AuthSession> {
  let response: Response;
  const { controller, timerId } = createTimeoutController(LOGIN_TIMEOUT_MS);

  try {
    response = await fetch(`${getApiBaseUrl()}/api/auth/login`, {
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
      throw new AppApiError('CPM-AUTH-002', 'Nao foi possivel autenticar agora. Tente novamente em instantes.', {
        detail: 'Timeout no endpoint /api/auth/login.'
      });
    }

    throw new AppApiError('CPM-AUTH-001', 'Nao foi possivel conectar ao servidor no momento.', {
      detail: 'Falha de rede/CORS/SSL ao chamar /api/auth/login.'
    });
  } finally {
    window.clearTimeout(timerId);
  }

  if (!response.ok) {
    if (response.status === 401) {
      throw new AppApiError('CPM-AUTH-401', 'E-mail ou senha invalidos.', {
        httpStatus: 401,
        detail: 'Resposta 401 do endpoint de login.'
      });
    }

    if (response.status >= 500) {
      throw new AppApiError('CPM-AUTH-5XX', 'Servico de autenticacao indisponivel no momento.', {
        httpStatus: response.status,
        detail: `Resposta ${response.status} do endpoint de login.`
      });
    }

    const message = await tryReadErrorMessage(response);
    throw new AppApiError('CPM-AUTH-4XX', message || 'Falha na autenticacao.', {
      httpStatus: response.status,
      detail: `Resposta ${response.status} do endpoint de login.`
    });
  }

  const payload = await response.json() as LoginApiResponse;
  if (!payload?.token) {
    throw new AppApiError('CPM-AUTH-003', 'Resposta de autenticacao invalida.', {
      detail: 'Token ausente no payload do login.'
    });
  }

  if (payload.role !== 'Client') {
    throw new AppApiError('CPM-AUTH-403', 'Este app e exclusivo para clientes.', {
      detail: `Role recebida: ${payload.role || 'desconhecida'}.`
    });
  }

  return {
    userId: payload.userId,
    token: payload.token,
    userName: payload.userName,
    role: payload.role,
    email: payload.email
  };
}

async function getBiometricEnabledFlag(): Promise<boolean> {
  if (!isNativeRuntime()) {
    return false;
  }

  try {
    const value = await SecureStorage.getItem(BIOMETRIC_ENABLED_KEY);
    return value === '1';
  } catch {
    return false;
  }
}

async function hasBiometricSessionStored(): Promise<boolean> {
  if (!isNativeRuntime()) {
    return false;
  }

  try {
    const value = await SecureStorage.getItem(BIOMETRIC_SESSION_KEY);
    return Boolean(value?.trim());
  } catch {
    return false;
  }
}

async function loadBiometricStoredSession(): Promise<AuthSession | null> {
  if (!isNativeRuntime()) {
    return null;
  }

  let raw: string | null = null;
  try {
    raw = await SecureStorage.getItem(BIOMETRIC_SESSION_KEY);
  } catch {
    return null;
  }

  if (!raw) {
    return null;
  }

  try {
    const parsed = JSON.parse(raw) as AuthSession;
    if (!isStoredSessionValid(parsed)) {
      await SecureStorage.removeItem(BIOMETRIC_SESSION_KEY);
      return null;
    }

    return parsed;
  } catch {
    await SecureStorage.removeItem(BIOMETRIC_SESSION_KEY);
    return null;
  }
}

async function resolveBiometryAvailability(): Promise<boolean> {
  if (!isNativeRuntime()) {
    return false;
  }

  try {
    const check = await BiometricAuth.checkBiometry();
    return check.isAvailable;
  } catch {
    return false;
  }
}

export async function getBiometricLoginState(): Promise<BiometricLoginState> {
  if (!isNativeRuntime()) {
    return {
      isNativeRuntime: false,
      isBiometryAvailable: false,
      isBiometricLoginEnabled: false,
      hasStoredBiometricSession: false
    };
  }

  const [isBiometryAvailable, isBiometricLoginEnabled, hasStoredBiometricSession] = await Promise.all([
    resolveBiometryAvailability(),
    getBiometricEnabledFlag(),
    hasBiometricSessionStored()
  ]);

  return {
    isNativeRuntime: true,
    isBiometryAvailable,
    isBiometricLoginEnabled: isBiometryAvailable && isBiometricLoginEnabled,
    hasStoredBiometricSession: isBiometryAvailable && hasStoredBiometricSession
  };
}

export async function disableBiometricLogin(): Promise<void> {
  if (!isNativeRuntime()) {
    return;
  }

  await Promise.allSettled([
    SecureStorage.removeItem(BIOMETRIC_ENABLED_KEY),
    SecureStorage.removeItem(BIOMETRIC_SESSION_KEY)
  ]);
}

export async function enableBiometricLoginForSession(session: AuthSession): Promise<void> {
  if (!isNativeRuntime()) {
    return;
  }

  if (!isStoredSessionValid(session)) {
    throw new AppBiometricError('CPM-BIO-007', 'Sessao invalida para biometria.');
  }

  const check = await BiometricAuth.checkBiometry();
  if (!check.isAvailable) {
    throw new AppBiometricError('CPM-BIO-003', 'Biometria nao esta disponivel neste dispositivo.');
  }

  await SecureStorage.setItem(BIOMETRIC_ENABLED_KEY, '1');
  await SecureStorage.setItem(BIOMETRIC_SESSION_KEY, JSON.stringify(session));
}

export async function loginWithBiometrics(): Promise<AuthSession> {
  if (!isNativeRuntime()) {
    throw new AppBiometricError('CPM-BIO-001', 'Biometria disponivel apenas no app instalado.');
  }

  const isEnabled = await getBiometricEnabledFlag();
  if (!isEnabled) {
    throw new AppBiometricError('CPM-BIO-002', 'Biometria nao habilitada neste dispositivo.');
  }

  const check = await BiometricAuth.checkBiometry();
  if (!check.isAvailable) {
    throw new AppBiometricError('CPM-BIO-003', 'Biometria nao esta disponivel neste dispositivo.');
  }

  const storedSession = await loadBiometricStoredSession();
  if (!storedSession) {
    throw new AppBiometricError('CPM-BIO-004', 'Sessao biometrica indisponivel. Entre com e-mail e senha.');
  }

  try {
    await BiometricAuth.authenticate({
      reason: 'Confirme sua identidade para entrar no Conserta Pra Mim',
      cancelTitle: 'Cancelar',
      allowDeviceCredential: true,
      androidTitle: 'Entrar com biometria',
      androidSubtitle: 'Conserta Pra Mim'
    });
  } catch (error) {
    if (error instanceof BiometryError) {
      if (error.code === BiometryErrorType.userCancel || error.code === BiometryErrorType.systemCancel) {
        throw new AppBiometricError('CPM-BIO-005', 'Autenticacao biometrica cancelada.');
      }

      if (error.code === BiometryErrorType.biometryLockout) {
        throw new AppBiometricError('CPM-BIO-006', 'Biometria bloqueada temporariamente no dispositivo.');
      }

      throw new AppBiometricError('CPM-BIO-008', 'Nao foi possivel validar sua biometria.');
    }

    throw new AppBiometricError('CPM-BIO-008', 'Nao foi possivel validar sua biometria.');
  }

  return storedSession;
}

export function saveAuthSession(session: AuthSession): void {
  localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(session));
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
  const leewaySeconds = 30;
  return payload.exp <= nowInSeconds + leewaySeconds;
}

function isStoredSessionValid(session: AuthSession): boolean {
  if (!session?.token || !session?.email) {
    return false;
  }

  if ((session.role || '').trim() !== 'Client') {
    return false;
  }

  if (isSessionExpired(session.token)) {
    return false;
  }

  return true;
}

export function loadAuthSession(): AuthSession | null {
  const raw = localStorage.getItem(AUTH_STORAGE_KEY);
  if (!raw) return null;

  try {
    const parsed = JSON.parse(raw) as AuthSession;
    if (!isStoredSessionValid(parsed)) {
      clearAuthSession();
      return null;
    }
    return parsed;
  } catch {
    clearAuthSession();
    return null;
  }
}

export function clearAuthSession(): void {
  localStorage.removeItem(AUTH_STORAGE_KEY);
}
