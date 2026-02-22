import React, { useEffect, useState } from 'react';
import {
  AdminAuthApiError,
  AdminApiIssueCode,
  checkAdminApiHealth,
  getAdminApiBaseUrl,
  loginAdminWithEmailPassword
} from '../services/auth';
import type { AdminAuthSession } from '../types';

interface AuthProps {
  onLoginSuccess: (session: AdminAuthSession) => void;
}

type HealthState = 'checking' | 'available' | 'maintenance';

const DEFAULT_EMAIL = (import.meta.env.VITE_DEFAULT_LOGIN_EMAIL || 'admin@teste.com').trim();
const DEFAULT_PASSWORD = import.meta.env.VITE_DEFAULT_LOGIN_PASSWORD || 'SeedDev!2026';

function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError';
}

const Auth: React.FC<AuthProps> = ({ onLoginSuccess }) => {
  const [healthState, setHealthState] = useState<HealthState>('checking');
  const [healthCode, setHealthCode] = useState<AdminApiIssueCode | undefined>();
  const [healthDetail, setHealthDetail] = useState<string>('');
  const [email, setEmail] = useState(DEFAULT_EMAIL);
  const [password, setPassword] = useState(DEFAULT_PASSWORD);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorCode, setErrorCode] = useState<string>('');
  const [errorMessage, setErrorMessage] = useState<string>('');

  const runHealthCheck = async () => {
    setHealthState('checking');
    setHealthCode(undefined);
    setHealthDetail('');

    const health = await checkAdminApiHealth();
    if (health.available) {
      setHealthState('available');
      return;
    }

    setHealthCode(health.code);
    setHealthDetail(health.detail || 'Falha ao verificar disponibilidade da API.');
    setHealthState('maintenance');
  };

  useEffect(() => {
    void runHealthCheck();
  }, []);

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setErrorCode('');
    setErrorMessage('');

    if (healthState !== 'available') {
      setErrorCode(healthCode || 'CPM-ADMIN-API-001');
      setErrorMessage('Servico em manutencao. Tente novamente em instantes.');
      return;
    }

    if (!email.trim() || !password) {
      setErrorCode('CPM-ADMIN-AUTH-4XX');
      setErrorMessage('Informe e-mail e senha para continuar.');
      return;
    }

    setIsSubmitting(true);
    try {
      const session = await loginAdminWithEmailPassword(email, password);
      onLoginSuccess(session);
    } catch (error) {
      if (error instanceof AdminAuthApiError) {
        setErrorCode(error.code);
        setErrorMessage(error.message);
      } else if (isAbortError(error)) {
        setErrorCode('CPM-ADMIN-AUTH-002');
        setErrorMessage('Tempo limite excedido ao autenticar.');
      } else {
        setErrorCode('CPM-ADMIN-AUTH-001');
        setErrorMessage('Falha inesperada ao autenticar.');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  if (healthState === 'checking') {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-950 px-6 text-slate-100">
        <div className="w-full max-w-md rounded-3xl border border-slate-700 bg-slate-900/90 p-8 text-center shadow-2xl">
          <span className="material-symbols-outlined text-4xl text-blue-300">sync</span>
          <h1 className="mt-3 text-xl font-semibold">Verificando servicos</h1>
          <p className="mt-2 text-sm text-slate-300">Conferindo disponibilidade da API admin...</p>
          <p className="mt-3 text-xs text-slate-400">Base: {getAdminApiBaseUrl()}</p>
        </div>
      </div>
    );
  }

  if (healthState === 'maintenance') {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-950 px-6 text-slate-100">
        <div className="w-full max-w-md rounded-3xl border border-amber-700/40 bg-slate-900/90 p-8 shadow-2xl">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-amber-500/20 text-amber-300">
            <span className="material-symbols-outlined">build_circle</span>
          </div>
          <h1 className="mt-3 text-center text-xl font-semibold">API em manutencao</h1>
          <p className="mt-2 text-center text-sm text-slate-300">Nao foi possivel liberar o login no momento.</p>
          <div className="mt-5 rounded-xl border border-slate-700 bg-slate-950/60 p-4 text-sm text-slate-300">
            <p><span className="font-semibold">Codigo:</span> {healthCode || 'CPM-ADMIN-API-001'}</p>
            <p className="mt-1"><span className="font-semibold">Detalhe:</span> {healthDetail}</p>
          </div>
          <button
            type="button"
            onClick={() => void runHealthCheck()}
            className="mt-6 w-full rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white transition hover:bg-blue-500"
          >
            Tentar novamente
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-100 px-5 py-10 text-slate-900">
      <div className="w-full max-w-md rounded-3xl border border-slate-200 bg-white p-7 shadow-xl">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">ConsertaPraMim</p>
        <h1 className="mt-2 text-2xl font-semibold">Acesso administrativo</h1>
        <p className="mt-2 text-sm text-slate-600">Entre com uma conta de role `Admin` para continuar.</p>

        <form onSubmit={handleSubmit} className="mt-6 space-y-4">
          <label className="block space-y-2">
            <span className="text-sm font-semibold text-slate-700">E-mail</span>
            <input
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              className="h-12 w-full rounded-xl border border-slate-300 px-4 text-sm outline-none transition focus:border-blue-500 focus:ring-2 focus:ring-blue-200"
              autoComplete="email"
              required
            />
          </label>

          <label className="block space-y-2">
            <span className="text-sm font-semibold text-slate-700">Senha</span>
            <input
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              className="h-12 w-full rounded-xl border border-slate-300 px-4 text-sm outline-none transition focus:border-blue-500 focus:ring-2 focus:ring-blue-200"
              autoComplete="current-password"
              required
            />
          </label>

          {errorMessage ? (
            <div className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
              <p>{errorMessage}</p>
              {errorCode ? <p className="mt-1 font-semibold">Codigo: {errorCode}</p> : null}
            </div>
          ) : null}

          <button
            type="submit"
            disabled={isSubmitting}
            className="h-12 w-full rounded-xl bg-blue-600 px-4 text-sm font-semibold text-white transition hover:bg-blue-500 disabled:opacity-60"
          >
            {isSubmitting ? 'Autenticando...' : 'Entrar'}
          </button>
        </form>

        <p className="mt-5 text-xs text-slate-500">API: {getAdminApiBaseUrl()}</p>
      </div>
    </div>
  );
};

export default Auth;