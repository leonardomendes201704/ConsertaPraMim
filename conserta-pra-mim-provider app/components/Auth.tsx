import React, { useEffect, useState } from 'react';
import {
  getProviderActiveLegalTerms,
  ProviderActiveLegalTermsDocument,
  ProviderApiHealthCheckResult
} from '../services/auth';

interface RegisterPayload {
  name: string;
  email: string;
  password: string;
  phone: string;
  termsVersion: number;
  enableBiometricLogin: boolean;
}

interface Props {
  loading: boolean;
  error: string;
  healthStatus: ProviderApiHealthCheckResult | null;
  defaultEmail: string;
  defaultPassword: string;
  biometricAvailable: boolean;
  biometricEnabled: boolean;
  biometricHasStoredSession: boolean;
  onBiometricLogin: () => Promise<void>;
  onSubmit: (email: string, password: string, enableBiometricLogin: boolean) => Promise<void>;
  onRegister: (payload: RegisterPayload) => Promise<void>;
  onRetryHealth: () => Promise<void>;
}

type AuthMode = 'login' | 'register';

function normalizePhone(phone: string): string {
  return String(phone || '').replace(/\D/g, '');
}

function formatPhoneInput(value: string): string {
  const digits = normalizePhone(value).slice(0, 11);
  if (!digits) return '';
  if (digits.length <= 2) return `(${digits}`;
  if (digits.length <= 6) return `(${digits.slice(0, 2)}) ${digits.slice(2)}`;
  if (digits.length <= 10) return `(${digits.slice(0, 2)}) ${digits.slice(2, 6)}-${digits.slice(6)}`;
  return `(${digits.slice(0, 2)}) ${digits.slice(2, 7)}-${digits.slice(7)}`;
}

function isEmailValid(email: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(String(email || '').trim());
}

const Auth: React.FC<Props> = ({
  loading,
  error,
  healthStatus,
  defaultEmail,
  defaultPassword,
  biometricAvailable,
  biometricEnabled,
  biometricHasStoredSession,
  onBiometricLogin,
  onSubmit,
  onRegister,
  onRetryHealth
}) => {
  const [mode, setMode] = useState<AuthMode>('login');
  const [email, setEmail] = useState(defaultEmail);
  const [password, setPassword] = useState(defaultPassword);
  const [enableBiometricLogin, setEnableBiometricLogin] = useState(false);

  const [fullName, setFullName] = useState('');
  const [phone, setPhone] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [acceptTerms, setAcceptTerms] = useState(false);
  const [localError, setLocalError] = useState('');
  const [terms, setTerms] = useState<ProviderActiveLegalTermsDocument | null>(null);
  const [termsLoading, setTermsLoading] = useState(false);
  const [termsError, setTermsError] = useState('');

  useEffect(() => {
    setEmail(defaultEmail);
    setPassword(defaultPassword);
  }, [defaultEmail, defaultPassword]);

  useEffect(() => {
    if (!biometricAvailable) {
      setEnableBiometricLogin(false);
      return;
    }

    setEnableBiometricLogin(biometricEnabled || !biometricHasStoredSession);
  }, [biometricAvailable, biometricEnabled, biometricHasStoredSession]);

  const loadTerms = async () => {
    setTermsLoading(true);
    setTermsError('');
    try {
      const active = await getProviderActiveLegalTerms('provider');
      setTerms(active);
    } catch (loadError) {
      if (loadError instanceof Error) {
        setTermsError(loadError.message);
      } else {
        setTermsError('Nao foi possivel carregar o termo de cadastro.');
      }
      setTerms(null);
    } finally {
      setTermsLoading(false);
    }
  };

  const resetRegisterForm = () => {
    setFullName('');
    setPhone('');
    setEmail('');
    setPassword('');
    setConfirmPassword('');
    setAcceptTerms(false);
    setTerms(null);
    setTermsError('');
    setLocalError('');
  };

  const handleModeChange = (nextMode: AuthMode) => {
    setMode(nextMode);
    setLocalError('');

    if (nextMode === 'register') {
      resetRegisterForm();
      void loadTerms();
      return;
    }

    setEmail(defaultEmail);
    setPassword(defaultPassword);
    setConfirmPassword('');
    setAcceptTerms(false);
  };

  const maintenanceMode = healthStatus ? !healthStatus.available : false;

  const handleLoginSubmit = async () => {
    if (!email.trim() || !password) {
      setLocalError('Informe e-mail e senha para continuar.');
      return;
    }

    setLocalError('');
    await onSubmit(email, password, biometricAvailable && enableBiometricLogin);
  };

  const handleRegisterSubmit = async () => {
    setLocalError('');

    if (!fullName.trim()) {
      setLocalError('Informe seu nome completo para continuar.');
      return;
    }

    const normalizedPhone = normalizePhone(phone);
    if (normalizedPhone.length < 10 || normalizedPhone.length > 11) {
      setLocalError('Informe um telefone valido com DDD.');
      return;
    }

    if (!isEmailValid(email)) {
      setLocalError('Informe um e-mail valido.');
      return;
    }

    if (!password || password.length < 8) {
      setLocalError('A senha deve ter no minimo 8 caracteres.');
      return;
    }

    if (password !== confirmPassword) {
      setLocalError('As senhas informadas nao conferem.');
      return;
    }

    if (!terms) {
      setLocalError('Nao foi possivel carregar o termo de cadastro.');
      return;
    }

    if (!acceptTerms) {
      setLocalError('Voce precisa aceitar o termo para concluir o cadastro.');
      return;
    }

    await onRegister({
      name: fullName,
      email,
      password,
      phone,
      termsVersion: terms.version,
      enableBiometricLogin: biometricAvailable && enableBiometricLogin
    });
  };

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (loading || maintenanceMode) {
      return;
    }

    if (mode === 'register') {
      await handleRegisterSubmit();
      return;
    }

    await handleLoginSubmit();
  };

  const displayError = localError || error;

  return (
    <div className="min-h-screen bg-[#f4f7fb] flex items-center justify-center p-4">
      <div className="w-full max-w-[560px] bg-white rounded-3xl shadow-xl border border-[#d9e5ff] p-6">
        <div className="text-center mb-6">
          <div className="mx-auto w-14 h-14 rounded-2xl bg-primary/10 text-primary flex items-center justify-center mb-3">
            <span className="material-symbols-outlined text-3xl material-symbols-fill">construction</span>
          </div>
          <h1 className="text-2xl font-bold text-[#101828]">
            {mode === 'register' ? 'Cadastro de Prestador' : 'Login do Prestador'}
          </h1>
          <p className="text-sm text-[#475467] mt-1">
            {mode === 'register'
              ? 'Crie sua conta para acessar o painel de atendimento.'
              : 'Acesse seu painel de atendimento no app.'}
          </p>
        </div>

        <div className="mb-4 rounded-xl border border-[#dae7e7] p-1 grid grid-cols-2 gap-1 bg-[#f8fafc]">
          <button
            type="button"
            onClick={() => handleModeChange('login')}
            className={`h-10 rounded-lg text-sm font-bold transition-colors ${
              mode === 'login'
                ? 'bg-white text-primary shadow-sm border border-primary/10'
                : 'text-[#4a5e5e]'
            }`}
          >
            Entrar
          </button>
          <button
            type="button"
            onClick={() => handleModeChange('register')}
            className={`h-10 rounded-lg text-sm font-bold transition-colors ${
              mode === 'register'
                ? 'bg-white text-primary shadow-sm border border-primary/10'
                : 'text-[#4a5e5e]'
            }`}
          >
            Criar conta
          </button>
        </div>

        {maintenanceMode && (
          <div className="rounded-2xl border border-amber-300 bg-amber-50 p-4 text-amber-900 text-sm mb-4">
            <h2 className="font-bold text-base mb-1">Desculpe o transtorno</h2>
            <p>Estamos em manutencao no momento. Tente novamente em instantes.</p>
            <div className="mt-3 text-xs space-y-1 bg-white/70 border border-amber-200 rounded-xl p-3">
              <p><span className="font-semibold">Codigo tecnico:</span> {healthStatus?.code || '-'}</p>
              <p><span className="font-semibold">Detalhe:</span> {healthStatus?.detail || '-'}</p>
              <p><span className="font-semibold">Dica DEV:</span> {healthStatus?.developerHint || '-'}</p>
            </div>
            <button
              type="button"
              onClick={() => void onRetryHealth()}
              className="mt-3 w-full rounded-xl bg-amber-600 text-white font-semibold py-2"
            >
              Tentar novamente
            </button>
          </div>
        )}

        {displayError && !maintenanceMode && (
          <div className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-red-700 text-sm mb-4">
            {displayError}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          {mode === 'register' ? (
            <>
              <div>
                <label className="block text-sm font-semibold text-[#344054] mb-1">Nome completo</label>
                <input
                  type="text"
                  value={fullName}
                  onChange={(event) => setFullName(event.target.value)}
                  className="w-full rounded-xl border border-[#d0d5dd] px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-primary/30"
                  placeholder="Seu nome completo"
                  autoComplete="name"
                />
              </div>

              <div>
                <label className="block text-sm font-semibold text-[#344054] mb-1">Telefone com DDD</label>
                <input
                  type="tel"
                  value={phone}
                  onChange={(event) => setPhone(formatPhoneInput(event.target.value))}
                  className="w-full rounded-xl border border-[#d0d5dd] px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-primary/30"
                  placeholder="(11) 99999-9999"
                  autoComplete="tel-national"
                />
              </div>
            </>
          ) : null}

          <div>
            <label className="block text-sm font-semibold text-[#344054] mb-1">E-mail</label>
            <input
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              className="w-full rounded-xl border border-[#d0d5dd] px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-primary/30"
              placeholder="prestador@teste.com"
              autoComplete={mode === 'register' ? 'email' : 'username'}
            />
          </div>

          <div>
            <label className="block text-sm font-semibold text-[#344054] mb-1">Senha</label>
            <input
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              className="w-full rounded-xl border border-[#d0d5dd] px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-primary/30"
              placeholder="********"
              autoComplete={mode === 'register' ? 'new-password' : 'current-password'}
            />
          </div>

          {mode === 'register' ? (
            <div>
              <label className="block text-sm font-semibold text-[#344054] mb-1">Confirmar senha</label>
              <input
                type="password"
                value={confirmPassword}
                onChange={(event) => setConfirmPassword(event.target.value)}
                className="w-full rounded-xl border border-[#d0d5dd] px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-primary/30"
                placeholder="Repita sua senha"
                autoComplete="new-password"
              />
            </div>
          ) : null}

          {biometricAvailable ? (
            <label className="flex items-start gap-3 rounded-xl border border-[#d0d5dd] px-3 py-3 cursor-pointer">
              <input
                type="checkbox"
                checked={enableBiometricLogin}
                onChange={(event) => setEnableBiometricLogin(event.target.checked)}
                className="mt-1 h-4 w-4 rounded border-[#98a2b3] text-primary focus:ring-primary"
              />
              <div>
                <div className="text-sm font-semibold text-[#101828]">Ativar login com biometria neste dispositivo</div>
                <div className="text-xs text-[#667085]">No navegador o acesso continua por e-mail e senha.</div>
              </div>
            </label>
          ) : null}

          {mode === 'register' ? (
            <>
              {termsLoading ? (
                <div className="rounded-xl border border-[#d0d5dd] bg-[#f8fafc] px-3 py-3 text-sm text-[#475467]">
                  Carregando termo de cadastro...
                </div>
              ) : null}

              {termsError ? (
                <div className="rounded-xl border border-amber-300 bg-amber-50 px-3 py-3 text-sm text-amber-900 space-y-3">
                  <div>{termsError}</div>
                  <button
                    type="button"
                    onClick={() => void loadTerms()}
                    className="rounded-lg bg-amber-600 text-white px-3 py-2 text-xs font-bold"
                  >
                    Tentar novamente
                  </button>
                </div>
              ) : null}

              {terms ? (
                <div className="rounded-xl border border-[#d0d5dd] bg-white px-3 py-3 space-y-3">
                  <div className="text-sm font-semibold text-[#101828]">
                    {terms.title} (v{terms.version})
                  </div>
                  <div className="max-h-56 overflow-y-auto rounded-lg border border-[#d0d5dd] bg-[#f8fafc] p-3 text-xs text-[#344054]">
                    <div dangerouslySetInnerHTML={{ __html: terms.htmlContent }} />
                  </div>
                </div>
              ) : null}

              <label className="flex items-start gap-3 rounded-xl border border-[#d0d5dd] px-3 py-3 cursor-pointer">
                <input
                  type="checkbox"
                  checked={acceptTerms}
                  onChange={(event) => setAcceptTerms(event.target.checked)}
                  disabled={!terms || termsLoading}
                  className="mt-1 h-4 w-4 rounded border-[#98a2b3] text-primary focus:ring-primary"
                />
                <div className="text-sm text-[#475467]">
                  Li e aceito integralmente o termo acima, incluindo a clausula de isencao de responsabilidade da plataforma.
                </div>
              </label>
            </>
          ) : null}

          <button
            type="submit"
            disabled={loading || maintenanceMode || (mode === 'register' && (!terms || termsLoading))}
            className="w-full rounded-xl bg-primary text-white font-bold py-3 disabled:opacity-60"
          >
            {loading
              ? mode === 'register' ? 'Criando conta...' : 'Entrando...'
              : mode === 'register' ? 'Criar conta' : 'Entrar'}
          </button>

          {mode === 'login' && biometricAvailable && biometricEnabled && biometricHasStoredSession ? (
            <button
              type="button"
              onClick={() => void onBiometricLogin()}
              disabled={loading || maintenanceMode}
              className="w-full rounded-xl border border-primary text-primary font-bold py-3 disabled:opacity-60 flex items-center justify-center gap-2"
            >
              <span className="material-symbols-outlined text-[20px]">fingerprint</span>
              Entrar com biometria
            </button>
          ) : null}
        </form>
      </div>
    </div>
  );
};

export default Auth;
