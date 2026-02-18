import { ProviderAuthSession } from '../types';
import { Capacitor } from '@capacitor/core';
import { BiometricAuth, BiometryError, BiometryErrorType } from '@aparajita/capacitor-biometric-auth';
import { SecureStorage } from '@aparajita/capacitor-secure-storage';

const AUTH_STORAGE_KEY = 'conserta.provider.auth.session';
const BIOMETRIC_ENABLED_KEY = 'conserta.provider.auth.biometric.enabled';
const BIOMETRIC_SESSION_KEY = 'conserta.provider.auth.biometric.session';
const HEALTH_TIMEOUT_MS = 5000;
const LOGIN_TIMEOUT_MS = 12000;

interface LoginApiResponse {
  userId: string;
  token: string;
  userName: string;
  role: string;
  email: string;
}

export interface ProviderBiometricLoginState {
  isNativeRuntime: boolean;
  isBiometryAvailable: boolean;
  isBiometricLoginEnabled: boolean;
  hasStoredBiometricSession: boolean;
}

export type ProviderApiIssueCode =
  | 'CPM-PROV-API-001'
  | 'CPM-PROV-API-002'
  | 'CPM-PROV-API-003'
  | 'CPM-PROV-AUTH-001'
  | 'CPM-PROV-AUTH-002'
  | 'CPM-PROV-AUTH-003'
  | 'CPM-PROV-AUTH-401'
  | 'CPM-PROV-AUTH-403'
  | 'CPM-PROV-AUTH-4XX'
  | 'CPM-PROV-AUTH-5XX';

interface ProviderApiIssueMeta {
  title: string;
  developerHint: string;
}

const PROVIDER_API_ISSUE_CATALOG: Record<ProviderApiIssueCode, ProviderApiIssueMeta> = {
  'CPM-PROV-API-001': {
    title: 'Falha de conexao com API',
    developerHint: 'Verifique API online, CORS, SSL e VITE_API_BASE_URL.'
  },
  'CPM-PROV-API-002': {
    title: 'Timeout no health-check',
    developerHint: 'A API nao respondeu no prazo esperado para /health.'
  },
  'CPM-PROV-API-003': {
    title: 'API indisponivel',
    developerHint: 'Endpoint /health retornou erro. Inspecione logs da API.'
  },
  'CPM-PROV-AUTH-001': {
    title: 'Falha de conexao no login',
    developerHint: 'Erro de rede/CORS ao chamar /api/auth/login.'
  },
  'CPM-PROV-AUTH-002': {
    title: 'Timeout no login',
    developerHint: 'A API nao respondeu ao login dentro do timeout.'
  },
  'CPM-PROV-AUTH-003': {
    title: 'Payload de login invalido',
    developerHint: 'A resposta da autenticacao nao retornou token esperado.'
  },
  'CPM-PROV-AUTH-401': {
    title: 'Credenciais invalidas',
    developerHint: 'Email/senha invalidos para a conta do prestador.'
  },
  'CPM-PROV-AUTH-403': {
    title: 'Perfil nao permitido',
    developerHint: 'Este app aceita apenas role Provider.'
  },
  'CPM-PROV-AUTH-4XX': {
    title: 'Erro de requisicao no login',
    developerHint: 'Revise payload e validacoes do endpoint de login.'
  },
  'CPM-PROV-AUTH-5XX': {
    title: 'Erro interno no login',
    developerHint: 'Erro interno da API na autenticacao.'
  }
};

export interface ProviderApiHealthCheckResult {
  available: boolean;
  code?: ProviderApiIssueCode;
  message: string;
  detail?: string;
  developerHint?: string;
  httpStatus?: number;
}

export class ProviderAppApiError extends Error {
  public readonly code: ProviderApiIssueCode;
  public readonly detail?: string;
  public readonly httpStatus?: number;

  constructor(code: ProviderApiIssueCode, message: string, options?: { detail?: string; httpStatus?: number }) {
    super(message);
    this.name = 'ProviderAppApiError';
    this.code = code;
    this.detail = options?.detail;
    this.httpStatus = options?.httpStatus;
  }
}

export class ProviderBiometricAuthError extends Error {
  public readonly code: string;

  constructor(code: string, message: string) {
    super(message);
    this.name = 'ProviderBiometricAuthError';
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

function isStoredSessionValid(session: ProviderAuthSession): boolean {
  if (!session?.token || !session?.email) {
    return false;
  }

  if ((session.role || '').trim() !== 'Provider') {
    return false;
  }

  return !isSessionExpired(session.token);
}

async function tryReadErrorMessage(response: Response): Promise<string> {
  const contentType = response.headers.get('content-type') || '';
  if (contentType.includes('application/json')) {
    try {
      const payload = await response.json();
      if (typeof payload?.message === 'string' && payload.message.trim()) {
        return payload.message;
      }
    } catch {
      return 'Falha ao autenticar.';
    }
  }

  const text = await response.text();
  return text?.trim() || 'Falha ao autenticar.';
}

export async function checkProviderApiHealth(): Promise<ProviderApiHealthCheckResult> {
  const { controller, timerId } = createTimeoutController(HEALTH_TIMEOUT_MS);

  try {
    const response = await fetch(`${getApiBaseUrl()}/health`, {
      method: 'GET',
      headers: { Accept: 'text/plain, application/json' },
      signal: controller.signal
    });

    if (!response.ok) {
      return {
        available: false,
        code: 'CPM-PROV-API-003',
        message: 'Desculpe o transtorno, estamos em manutencao no momento.',
        detail: `Health-check retornou HTTP ${response.status}.`,
        developerHint: PROVIDER_API_ISSUE_CATALOG['CPM-PROV-API-003'].developerHint,
        httpStatus: response.status
      };
    }

    return { available: true, message: 'API disponivel.' };
  } catch (error) {
    if (isAbortError(error)) {
      return {
        available: false,
        code: 'CPM-PROV-API-002',
        message: 'Desculpe o transtorno, estamos em manutencao no momento.',
        detail: 'Timeout ao verificar disponibilidade da API.',
        developerHint: PROVIDER_API_ISSUE_CATALOG['CPM-PROV-API-002'].developerHint
      };
    }

    return {
      available: false,
      code: 'CPM-PROV-API-001',
      message: 'Desculpe o transtorno, estamos em manutencao no momento.',
      detail: 'Falha de rede/CORS/SSL ao acessar o endpoint de health.',
      developerHint: PROVIDER_API_ISSUE_CATALOG['CPM-PROV-API-001'].developerHint
    };
  } finally {
    window.clearTimeout(timerId);
  }
}

export async function loginProviderWithEmailPassword(email: string, password: string): Promise<ProviderAuthSession> {
  const { controller, timerId } = createTimeoutController(LOGIN_TIMEOUT_MS);
  let response: Response;

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
      throw new ProviderAppApiError('CPM-PROV-AUTH-002', 'Nao foi possivel autenticar agora.', {
        detail: 'Timeout no endpoint /api/auth/login.'
      });
    }

    throw new ProviderAppApiError('CPM-PROV-AUTH-001', 'Nao foi possivel conectar ao servidor.', {
      detail: 'Falha de rede/CORS/SSL ao chamar /api/auth/login.'
    });
  } finally {
    window.clearTimeout(timerId);
  }

  if (!response.ok) {
    if (response.status === 401) {
      throw new ProviderAppApiError('CPM-PROV-AUTH-401', 'E-mail ou senha invalidos.', {
        httpStatus: 401,
        detail: 'Resposta 401 no login.'
      });
    }

    if (response.status >= 500) {
      throw new ProviderAppApiError('CPM-PROV-AUTH-5XX', 'Servico de autenticacao indisponivel.', {
        httpStatus: response.status,
        detail: `Resposta ${response.status} no login.`
      });
    }

    const message = await tryReadErrorMessage(response);
    throw new ProviderAppApiError('CPM-PROV-AUTH-4XX', message || 'Falha na autenticacao.', {
      httpStatus: response.status,
      detail: `Resposta ${response.status} no login.`
    });
  }

  const payload = await response.json() as LoginApiResponse;
  if (!payload?.token) {
    throw new ProviderAppApiError('CPM-PROV-AUTH-003', 'Resposta de autenticacao invalida.', {
      detail: 'Token ausente no payload.'
    });
  }

  if (payload.role !== 'Provider') {
    throw new ProviderAppApiError('CPM-PROV-AUTH-403', 'Este app e exclusivo para prestadores.', {
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

async function loadBiometricStoredSession(): Promise<ProviderAuthSession | null> {
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
    const parsed = JSON.parse(raw) as ProviderAuthSession;
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

export async function getProviderBiometricLoginState(): Promise<ProviderBiometricLoginState> {
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

export async function disableProviderBiometricLogin(): Promise<void> {
  if (!isNativeRuntime()) {
    return;
  }

  await Promise.allSettled([
    SecureStorage.removeItem(BIOMETRIC_ENABLED_KEY),
    SecureStorage.removeItem(BIOMETRIC_SESSION_KEY)
  ]);
}

export async function enableProviderBiometricLoginForSession(session: ProviderAuthSession): Promise<void> {
  if (!isNativeRuntime()) {
    return;
  }

  if (!isStoredSessionValid(session)) {
    throw new ProviderBiometricAuthError('CPM-PROV-BIO-007', 'Sessao invalida para biometria.');
  }

  const check = await BiometricAuth.checkBiometry();
  if (!check.isAvailable) {
    throw new ProviderBiometricAuthError('CPM-PROV-BIO-003', 'Biometria nao esta disponivel neste dispositivo.');
  }

  await SecureStorage.setItem(BIOMETRIC_ENABLED_KEY, '1');
  await SecureStorage.setItem(BIOMETRIC_SESSION_KEY, JSON.stringify(session));
}

export async function loginProviderWithBiometrics(): Promise<ProviderAuthSession> {
  if (!isNativeRuntime()) {
    throw new ProviderBiometricAuthError('CPM-PROV-BIO-001', 'Biometria disponivel apenas no app instalado.');
  }

  const isEnabled = await getBiometricEnabledFlag();
  if (!isEnabled) {
    throw new ProviderBiometricAuthError('CPM-PROV-BIO-002', 'Biometria nao habilitada neste dispositivo.');
  }

  const check = await BiometricAuth.checkBiometry();
  if (!check.isAvailable) {
    throw new ProviderBiometricAuthError('CPM-PROV-BIO-003', 'Biometria nao esta disponivel neste dispositivo.');
  }

  const storedSession = await loadBiometricStoredSession();
  if (!storedSession) {
    throw new ProviderBiometricAuthError('CPM-PROV-BIO-004', 'Sessao biometrica indisponivel. Entre com e-mail e senha.');
  }

  try {
    await BiometricAuth.authenticate({
      reason: 'Confirme sua identidade para entrar no app do prestador',
      cancelTitle: 'Cancelar',
      allowDeviceCredential: true,
      androidTitle: 'Entrar com biometria',
      androidSubtitle: 'Conserta Pra Mim Prestador'
    });
  } catch (error) {
    if (error instanceof BiometryError) {
      if (error.code === BiometryErrorType.userCancel || error.code === BiometryErrorType.systemCancel) {
        throw new ProviderBiometricAuthError('CPM-PROV-BIO-005', 'Autenticacao biometrica cancelada.');
      }

      if (error.code === BiometryErrorType.biometryLockout) {
        throw new ProviderBiometricAuthError('CPM-PROV-BIO-006', 'Biometria bloqueada temporariamente no dispositivo.');
      }

      throw new ProviderBiometricAuthError('CPM-PROV-BIO-008', 'Nao foi possivel validar sua biometria.');
    }

    throw new ProviderBiometricAuthError('CPM-PROV-BIO-008', 'Nao foi possivel validar sua biometria.');
  }

  return storedSession;
}

export function saveProviderAuthSession(session: ProviderAuthSession): void {
  localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(session));
}

export function loadProviderAuthSession(): ProviderAuthSession | null {
  const raw = localStorage.getItem(AUTH_STORAGE_KEY);
  if (!raw) {
    return null;
  }

  try {
    const parsed = JSON.parse(raw) as ProviderAuthSession;
    if (!isStoredSessionValid(parsed)) {
      clearProviderAuthSession();
      return null;
    }

    return parsed;
  } catch {
    clearProviderAuthSession();
    return null;
  }
}

export function clearProviderAuthSession(): void {
  localStorage.removeItem(AUTH_STORAGE_KEY);
}

export function getProviderIssueHint(code?: ProviderApiIssueCode): string {
  if (!code) {
    return '';
  }

  return PROVIDER_API_ISSUE_CATALOG[code]?.developerHint || '';
}
