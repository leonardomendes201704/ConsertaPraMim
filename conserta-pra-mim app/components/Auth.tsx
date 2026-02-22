import React, { useEffect, useState } from 'react';
import { AuthSession } from '../types';
import {
  AppApiError,
  AppBiometricError,
  BiometricLoginState,
  checkApiHealth,
  disableBiometricLogin,
  enableBiometricLoginForSession,
  getApiBaseUrl,
  getBiometricLoginState,
  loginWithBiometrics,
  loginWithEmailPassword,
  registerClientWithEmailPassword
} from '../services/auth';

interface Props {
  onLogin: (session: AuthSession) => void;
  onBack: () => void;
}

type ApiScreenState = 'checking' | 'available' | 'maintenance';
type AuthMode = 'login' | 'register';
type RegisterStep = 1 | 2 | 3;

interface MaintenanceInfo {
  code?: string;
  detail?: string;
  developerHint?: string;
}

interface RegisterStepMeta {
  step: RegisterStep;
  title: string;
  description: string;
}

const REGISTER_STEPS: RegisterStepMeta[] = [
  {
    step: 1,
    title: 'Dados pessoais',
    description: 'Nome completo e telefone'
  },
  {
    step: 2,
    title: 'Acesso',
    description: 'E-mail e senha'
  },
  {
    step: 3,
    title: 'Confirmacao',
    description: 'Revise e finalize'
  }
];

function normalizePhone(phone: string): string {
  return String(phone || '').replace(/\D/g, '');
}

function isEmailValid(email: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(String(email || '').trim());
}

function formatPhoneInput(value: string): string {
  const digits = normalizePhone(value).slice(0, 11);
  if (!digits) {
    return '';
  }

  if (digits.length <= 2) {
    return `(${digits}`;
  }

  if (digits.length <= 6) {
    return `(${digits.slice(0, 2)}) ${digits.slice(2)}`;
  }

  if (digits.length <= 10) {
    return `(${digits.slice(0, 2)}) ${digits.slice(2, 6)}-${digits.slice(6)}`;
  }

  return `(${digits.slice(0, 2)}) ${digits.slice(2, 7)}-${digits.slice(7)}`;
}

function getPasswordValidationMessage(password: string): string | null {
  const value = String(password || '');
  if (!value) {
    return 'Informe uma senha para continuar.';
  }

  if (value.length < 8) {
    return 'A senha deve ter no minimo 8 caracteres.';
  }

  if (!/[A-Z]/.test(value)) {
    return 'A senha deve conter pelo menos uma letra maiuscula.';
  }

  if (!/[a-z]/.test(value)) {
    return 'A senha deve conter pelo menos uma letra minuscula.';
  }

  if (!/\d/.test(value)) {
    return 'A senha deve conter pelo menos um numero.';
  }

  if (!/[^a-zA-Z0-9]/.test(value)) {
    return 'A senha deve conter pelo menos um caractere especial.';
  }

  return null;
}

function getNextRegisterStep(step: RegisterStep): RegisterStep {
  if (step === 1) {
    return 2;
  }

  return 3;
}

function getPreviousRegisterStep(step: RegisterStep): RegisterStep {
  if (step === 3) {
    return 2;
  }

  return 1;
}

const Auth: React.FC<Props> = ({ onLogin, onBack }) => {
  const defaultEmail = import.meta.env.VITE_DEFAULT_LOGIN_EMAIL || 'cliente2@teste.com';
  const defaultPassword = import.meta.env.VITE_DEFAULT_LOGIN_PASSWORD || 'SeedDev!2026';

  const [authMode, setAuthMode] = useState<AuthMode>('login');
  const [registerStep, setRegisterStep] = useState<RegisterStep>(1);

  const [email, setEmail] = useState(defaultEmail);
  const [password, setPassword] = useState(defaultPassword);
  const [confirmPassword, setConfirmPassword] = useState('');
  const [fullName, setFullName] = useState('');
  const [phone, setPhone] = useState('');
  const [acceptTerms, setAcceptTerms] = useState(false);

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');
  const [errorCode, setErrorCode] = useState<string | null>(null);
  const [apiState, setApiState] = useState<ApiScreenState>('checking');
  const [maintenanceInfo, setMaintenanceInfo] = useState<MaintenanceInfo>({});
  const [biometricState, setBiometricState] = useState<BiometricLoginState>({
    isNativeRuntime: false,
    isBiometryAvailable: false,
    isBiometricLoginEnabled: false,
    hasStoredBiometricSession: false
  });
  const [enableBiometricLogin, setEnableBiometricLogin] = useState(false);

  const probeApiHealth = async () => {
    setApiState('checking');
    setErrorMessage('');
    setErrorCode(null);

    const health = await checkApiHealth();
    if (health.available) {
      setApiState('available');
      setMaintenanceInfo({});
      return;
    }

    setMaintenanceInfo({
      code: health.code,
      detail: health.detail,
      developerHint: health.developerHint
    });
    setApiState('maintenance');
  };

  const refreshBiometricState = async () => {
    const state = await getBiometricLoginState();
    setBiometricState(state);
    setEnableBiometricLogin(state.isBiometryAvailable && (state.isBiometricLoginEnabled || !state.hasStoredBiometricSession));
  };

  useEffect(() => {
    probeApiHealth();
    void refreshBiometricState();
  }, []);

  const clearValidationErrors = () => {
    setErrorMessage('');
    setErrorCode(null);
  };

  const resetRegisterWizard = () => {
    setRegisterStep(1);
    setFullName('');
    setPhone('');
    setEmail('');
    setPassword('');
    setConfirmPassword('');
    setAcceptTerms(false);
  };

  const handleModeChange = (mode: AuthMode) => {
    setAuthMode(mode);
    clearValidationErrors();

    if (mode === 'register') {
      resetRegisterWizard();
      return;
    }

    setRegisterStep(1);
    setEmail(defaultEmail);
    setPassword(defaultPassword);
    setConfirmPassword('');
    setAcceptTerms(false);
  };

  const validateRegisterStep = (step: RegisterStep): boolean => {
    if (step === 1) {
      if (!fullName.trim()) {
        setErrorMessage('Informe seu nome completo para continuar.');
        setErrorCode('CPM-REG-4XX');
        return false;
      }

      const normalizedPhone = normalizePhone(phone);
      if (normalizedPhone.length < 10 || normalizedPhone.length > 11) {
        setErrorMessage('Informe um telefone valido com DDD.');
        setErrorCode('CPM-REG-4XX');
        return false;
      }

      return true;
    }

    if (step === 2) {
      if (!email.trim()) {
        setErrorMessage('Informe seu e-mail para continuar.');
        setErrorCode('CPM-REG-4XX');
        return false;
      }

      if (!isEmailValid(email)) {
        setErrorMessage('Informe um e-mail valido.');
        setErrorCode('CPM-REG-4XX');
        return false;
      }

      const passwordValidationMessage = getPasswordValidationMessage(password);
      if (passwordValidationMessage) {
        setErrorMessage(passwordValidationMessage);
        setErrorCode('CPM-REG-4XX');
        return false;
      }

      if (password !== confirmPassword) {
        setErrorMessage('As senhas informadas nao conferem.');
        setErrorCode('CPM-REG-4XX');
        return false;
      }

      return true;
    }

    if (!acceptTerms) {
      setErrorMessage('Voce precisa aceitar os termos para concluir o cadastro.');
      setErrorCode('CPM-REG-4XX');
      return false;
    }

    return true;
  };

  const handleRegisterNext = () => {
    clearValidationErrors();
    if (!validateRegisterStep(registerStep)) {
      return;
    }

    if (registerStep < 3) {
      setRegisterStep((currentStep) => getNextRegisterStep(currentStep));
    }
  };

  const handleRegisterBack = () => {
    clearValidationErrors();

    if (registerStep === 1) {
      handleModeChange('login');
      return;
    }

    setRegisterStep((currentStep) => getPreviousRegisterStep(currentStep));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (apiState !== 'available') {
      setErrorMessage('Desculpe o transtorno, estamos em manutencao no momento.');
      setErrorCode(maintenanceInfo.code || 'CPM-API-001');
      return;
    }

    clearValidationErrors();

    if (authMode === 'register' && registerStep < 3) {
      handleRegisterNext();
      return;
    }

    if (authMode === 'login' && (!email.trim() || !password)) {
      setErrorMessage('Informe e-mail e senha para continuar.');
      return;
    }

    if (authMode === 'register' && !validateRegisterStep(3)) {
      return;
    }

    setIsSubmitting(true);
    try {
      const session = authMode === 'register'
        ? await registerClientWithEmailPassword({
            name: fullName,
            email,
            password,
            phone
          })
        : await loginWithEmailPassword(email, password);

      if (biometricState.isNativeRuntime && biometricState.isBiometryAvailable) {
        if (enableBiometricLogin) {
          await enableBiometricLoginForSession(session);
        } else if (biometricState.isBiometricLoginEnabled || biometricState.hasStoredBiometricSession) {
          await disableBiometricLogin();
        }

        await refreshBiometricState();
      }

      onLogin(session);
    } catch (error) {
      if (error instanceof AppApiError) {
        setErrorMessage(error.message);
        setErrorCode(error.code);
      } else if (error instanceof AppBiometricError) {
        setErrorMessage(error.message);
        setErrorCode(error.code);
      } else {
        setErrorMessage('Falha ao autenticar.');
        setErrorCode('CPM-AUTH-001');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleBiometricLogin = async () => {
    if (apiState !== 'available') {
      setErrorMessage('Desculpe o transtorno, estamos em manutencao no momento.');
      setErrorCode(maintenanceInfo.code || 'CPM-API-001');
      return;
    }

    clearValidationErrors();
    setIsSubmitting(true);
    try {
      const session = await loginWithBiometrics();
      onLogin(session);
    } catch (error) {
      if (error instanceof AppBiometricError) {
        setErrorMessage(error.message);
        setErrorCode(error.code);
      } else if (error instanceof AppApiError) {
        setErrorMessage(error.message);
        setErrorCode(error.code);
      } else {
        setErrorMessage('Nao foi possivel autenticar com biometria.');
        setErrorCode('CPM-BIO-008');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const showBiometricControls = biometricState.isNativeRuntime && biometricState.isBiometryAvailable;
  const canLoginWithBiometrics =
    authMode === 'login' &&
    showBiometricControls &&
    biometricState.isBiometricLoginEnabled &&
    biometricState.hasStoredBiometricSession;

  if (apiState === 'checking') {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center p-6 bg-background-light overflow-y-auto">
        <div className="w-full max-w-[440px] bg-white rounded-2xl shadow-xl border border-primary/5 p-8 text-center space-y-4">
          <div className="mx-auto size-14 rounded-full bg-primary/10 text-primary flex items-center justify-center">
            <span className="material-symbols-outlined text-3xl">sync</span>
          </div>
          <h1 className="text-2xl font-bold text-[#101818]">Verificando servicos</h1>
          <p className="text-sm text-[#4a5e5e]">Validando disponibilidade da API antes do login...</p>
          <div className="text-xs text-[#5e8d8d]">Base: {getApiBaseUrl()}</div>
        </div>
      </div>
    );
  }

  if (apiState === 'maintenance') {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center p-6 bg-background-light overflow-y-auto">
        <div className="w-full max-w-[440px] bg-white rounded-2xl shadow-xl border border-amber-200 overflow-hidden max-h-[calc(100vh-2rem)] flex flex-col">
          <div className="bg-amber-50 border-b border-amber-200 px-6 py-4">
            <h2 className="text-amber-800 text-base font-bold">Status da plataforma</h2>
          </div>
          <div className="p-6 space-y-5 overflow-y-auto flex-1">
            <div className="text-center space-y-2">
              <div className="mx-auto size-14 rounded-full bg-amber-100 text-amber-700 flex items-center justify-center">
                <span className="material-symbols-outlined text-3xl">build_circle</span>
              </div>
              <h1 className="text-2xl font-bold text-[#101818]">Desculpe o transtorno</h1>
              <p className="text-[#4a5e5e]">Estamos em manutencao no momento. Tente novamente em alguns instantes.</p>
            </div>

            <div className="rounded-xl border border-[#dae7e7] bg-background-light p-4 space-y-2 text-sm">
              <div><span className="font-bold text-[#101818]">Codigo tecnico:</span> {maintenanceInfo.code || 'CPM-API-001'}</div>
              {maintenanceInfo.detail ? <div><span className="font-bold text-[#101818]">Detalhe:</span> {maintenanceInfo.detail}</div> : null}
              {maintenanceInfo.developerHint ? <div><span className="font-bold text-[#101818]">Dica DEV:</span> {maintenanceInfo.developerHint}</div> : null}
              <div><span className="font-bold text-[#101818]">Base API:</span> {getApiBaseUrl()}</div>
            </div>

            <div className="flex gap-3">
              <button
                type="button"
                onClick={onBack}
                className="flex-1 h-12 rounded-xl border border-[#dae7e7] text-[#101818] font-bold"
              >
                Voltar
              </button>
              <button
                type="button"
                onClick={probeApiHealth}
                className="flex-1 h-12 rounded-xl bg-primary text-white font-bold"
              >
                Tentar novamente
              </button>
            </div>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="flex min-h-screen flex-col items-center justify-start sm:justify-center p-4 bg-background-light overflow-y-auto">
      <div className="w-full max-w-[440px] bg-white rounded-2xl shadow-xl border border-primary/5 overflow-hidden max-h-[calc(100vh-2rem)] flex flex-col my-2">
        <div className="flex items-center p-4 border-b border-primary/5">
          <button onClick={onBack} className="text-primary hover:bg-primary/5 p-2 rounded-full transition-colors">
            <span className="material-symbols-outlined">arrow_back</span>
          </button>
          <h2 className="text-[#101818] text-base font-bold flex-1 text-center pr-10">Conserta Pra Mim</h2>
        </div>

        <div className="p-8 overflow-y-auto flex-1">
          <div className="mb-5 rounded-xl border border-[#dae7e7] p-1 grid grid-cols-2 gap-1 bg-background-light">
            <button
              type="button"
              onClick={() => handleModeChange('login')}
              className={`h-10 rounded-lg text-sm font-bold transition-colors ${
                authMode === 'login'
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
                authMode === 'register'
                  ? 'bg-white text-primary shadow-sm border border-primary/10'
                  : 'text-[#4a5e5e]'
              }`}
            >
              Criar conta
            </button>
          </div>

          <div className="mb-6">
            <h1 className="text-[#101818] text-3xl font-bold mb-3">
              {authMode === 'register' ? 'Cadastro de cliente' : 'Bem-vindo!'}
            </h1>
            <p className="text-[#4a5e5e] text-base font-normal leading-relaxed">
              {authMode === 'register'
                ? 'Preencha todas as etapas para criar sua conta de cliente.'
                : 'Entre com seu e-mail e senha para acessar sua conta de cliente.'}
            </p>
          </div>

          <form onSubmit={handleSubmit} className="space-y-6">
            {authMode === 'register' ? (
              <div className="rounded-xl border border-[#dae7e7] bg-background-light p-4">
                <div className="grid grid-cols-3 gap-2">
                  {REGISTER_STEPS.map((item) => {
                    const isDone = registerStep > item.step;
                    const isCurrent = registerStep === item.step;
                    return (
                      <div
                        key={item.step}
                        className={`rounded-lg border px-2 py-2 text-center transition-colors ${
                          isCurrent
                            ? 'border-primary bg-white'
                            : isDone
                              ? 'border-primary/40 bg-primary/5'
                              : 'border-[#dae7e7] bg-white/70'
                        }`}
                      >
                        <div className={`text-[10px] font-bold uppercase ${isCurrent || isDone ? 'text-primary' : 'text-[#7b9696]'}`}>
                          Etapa {item.step}
                        </div>
                        <div className="mt-1 text-xs font-semibold text-[#101818]">{item.title}</div>
                      </div>
                    );
                  })}
                </div>
                <div className="mt-3 text-xs text-[#5e8d8d]">
                  {REGISTER_STEPS.find((item) => item.step === registerStep)?.description}
                </div>
              </div>
            ) : null}

            {authMode === 'login' ? (
              <>
                <div className="space-y-2">
                  <label className="text-[#101818] text-sm font-semibold ml-1">E-mail</label>
                  <input
                    type="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    className="w-full h-14 rounded-xl border border-[#dae7e7] focus:ring-2 focus:ring-primary/20 px-4 text-base bg-white placeholder:text-[#5e8d8d]"
                    placeholder="voce@exemplo.com"
                    autoComplete="email"
                    required
                  />
                </div>

                <div className="space-y-2">
                  <label className="text-[#101818] text-sm font-semibold ml-1">Senha</label>
                  <input
                    type="password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    className="w-full h-14 rounded-xl border border-[#dae7e7] focus:ring-2 focus:ring-primary/20 px-4 text-base bg-white placeholder:text-[#5e8d8d]"
                    placeholder="Digite sua senha"
                    autoComplete="current-password"
                    required
                  />
                </div>

                {showBiometricControls ? (
                  <label className="flex items-start gap-3 rounded-xl border border-[#dae7e7] px-4 py-3 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={enableBiometricLogin}
                      onChange={(event) => setEnableBiometricLogin(event.target.checked)}
                      className="mt-1 h-4 w-4 rounded border-[#9fbaba] text-primary focus:ring-primary"
                    />
                    <div>
                      <div className="text-sm font-semibold text-[#101818]">Ativar login com biometria neste dispositivo</div>
                      <div className="text-xs text-[#5e8d8d]">No navegador o acesso continua somente com e-mail e senha.</div>
                    </div>
                  </label>
                ) : null}
              </>
            ) : null}

            {authMode === 'register' && registerStep === 1 ? (
              <>
                <div className="space-y-2">
                  <label className="text-[#101818] text-sm font-semibold ml-1">Nome completo</label>
                  <input
                    type="text"
                    value={fullName}
                    onChange={(e) => setFullName(e.target.value)}
                    className="w-full h-14 rounded-xl border border-[#dae7e7] focus:ring-2 focus:ring-primary/20 px-4 text-base bg-white placeholder:text-[#5e8d8d]"
                    placeholder="Seu nome completo"
                    autoComplete="name"
                    required
                  />
                </div>

                <div className="space-y-2">
                  <label className="text-[#101818] text-sm font-semibold ml-1">Telefone com DDD</label>
                  <input
                    type="tel"
                    value={phone}
                    onChange={(e) => setPhone(formatPhoneInput(e.target.value))}
                    className="w-full h-14 rounded-xl border border-[#dae7e7] focus:ring-2 focus:ring-primary/20 px-4 text-base bg-white placeholder:text-[#5e8d8d]"
                    placeholder="(11) 99999-9999"
                    autoComplete="tel-national"
                    required
                  />
                </div>
              </>
            ) : null}

            {authMode === 'register' && registerStep === 2 ? (
              <>
                <div className="space-y-2">
                  <label className="text-[#101818] text-sm font-semibold ml-1">E-mail</label>
                  <input
                    type="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    className="w-full h-14 rounded-xl border border-[#dae7e7] focus:ring-2 focus:ring-primary/20 px-4 text-base bg-white placeholder:text-[#5e8d8d]"
                    placeholder="voce@exemplo.com"
                    autoComplete="email"
                    required
                  />
                </div>

                <div className="space-y-2">
                  <label className="text-[#101818] text-sm font-semibold ml-1">Senha</label>
                  <input
                    type="password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    className="w-full h-14 rounded-xl border border-[#dae7e7] focus:ring-2 focus:ring-primary/20 px-4 text-base bg-white placeholder:text-[#5e8d8d]"
                    placeholder="Crie sua senha"
                    autoComplete="new-password"
                    required
                  />
                </div>

                <div className="space-y-2">
                  <label className="text-[#101818] text-sm font-semibold ml-1">Confirmar senha</label>
                  <input
                    type="password"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    className="w-full h-14 rounded-xl border border-[#dae7e7] focus:ring-2 focus:ring-primary/20 px-4 text-base bg-white placeholder:text-[#5e8d8d]"
                    placeholder="Repita sua senha"
                    autoComplete="new-password"
                    required
                  />
                </div>
              </>
            ) : null}

            {authMode === 'register' && registerStep === 3 ? (
              <div className="space-y-4">
                <div className="rounded-xl border border-[#dae7e7] bg-background-light p-4 space-y-3">
                  <div className="text-sm font-bold text-[#101818]">Revise seus dados</div>
                  <div className="text-sm text-[#4a5e5e]"><span className="font-semibold text-[#101818]">Nome:</span> {fullName || '-'}</div>
                  <div className="text-sm text-[#4a5e5e]"><span className="font-semibold text-[#101818]">Telefone:</span> {phone || '-'}</div>
                  <div className="text-sm text-[#4a5e5e]"><span className="font-semibold text-[#101818]">E-mail:</span> {email || '-'}</div>
                </div>

                <label className="flex items-start gap-3 rounded-xl border border-[#dae7e7] px-4 py-3 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={acceptTerms}
                    onChange={(event) => setAcceptTerms(event.target.checked)}
                    className="mt-1 h-4 w-4 rounded border-[#9fbaba] text-primary focus:ring-primary"
                  />
                  <div className="text-sm text-[#4a5e5e]">
                    Li e aceito os termos de uso e a politica de privacidade para concluir meu cadastro.
                  </div>
                </label>
              </div>
            ) : null}

            {errorMessage ? (
              <div className="rounded-xl border border-red-200 bg-red-50 text-red-700 text-sm px-4 py-3">
                <div>{errorMessage}</div>
                {errorCode ? <div className="mt-1 font-bold">Codigo: {errorCode}</div> : null}
              </div>
            ) : null}

            {authMode === 'login' ? (
              <>
                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="w-full flex items-center justify-center rounded-xl h-14 bg-primary hover:bg-primary/90 text-white font-bold transition-all shadow-lg shadow-primary/20 active:scale-[0.98] disabled:opacity-60"
                >
                  {isSubmitting ? 'Autenticando...' : 'Entrar'}
                </button>

                {canLoginWithBiometrics ? (
                  <button
                    type="button"
                    onClick={() => void handleBiometricLogin()}
                    disabled={isSubmitting}
                    className="w-full flex items-center justify-center gap-2 rounded-xl h-14 border border-primary text-primary font-bold transition-all hover:bg-primary/5 disabled:opacity-60"
                  >
                    <span className="material-symbols-outlined text-[20px]">fingerprint</span>
                    Entrar com biometria
                  </button>
                ) : null}
              </>
            ) : (
              <div className="flex gap-3">
                <button
                  type="button"
                  onClick={handleRegisterBack}
                  disabled={isSubmitting}
                  className="flex-1 flex items-center justify-center rounded-xl h-14 border border-[#dae7e7] text-[#101818] font-bold disabled:opacity-60"
                >
                  {registerStep === 1 ? 'Cancelar' : 'Voltar'}
                </button>

                {registerStep < 3 ? (
                  <button
                    type="button"
                    onClick={handleRegisterNext}
                    disabled={isSubmitting}
                    className="flex-1 flex items-center justify-center rounded-xl h-14 bg-primary hover:bg-primary/90 text-white font-bold transition-all shadow-lg shadow-primary/20 active:scale-[0.98] disabled:opacity-60"
                  >
                    Continuar
                  </button>
                ) : (
                  <button
                    type="submit"
                    disabled={isSubmitting}
                    className="flex-1 flex items-center justify-center rounded-xl h-14 bg-primary hover:bg-primary/90 text-white font-bold transition-all shadow-lg shadow-primary/20 active:scale-[0.98] disabled:opacity-60"
                  >
                    {isSubmitting ? 'Criando conta...' : 'Concluir cadastro'}
                  </button>
                )}
              </div>
            )}

            <div className="text-center text-sm text-[#5e8d8d]">
              {authMode === 'register' ? 'Ja possui conta?' : 'Ainda nao possui conta?'}{' '}
              <button
                type="button"
                onClick={() => handleModeChange(authMode === 'register' ? 'login' : 'register')}
                className="text-primary font-semibold underline"
              >
                {authMode === 'register' ? 'Entrar' : 'Criar conta'}
              </button>
            </div>
          </form>

          <div className="mt-8 text-[#5e8d8d] text-xs text-center">
            Ao continuar, voce concorda com nossos <a href="#" className="text-primary font-semibold underline">Termos</a> e <a href="#" className="text-primary font-semibold underline">Privacidade</a>.
          </div>
        </div>
      </div>

      <div className="mt-8 text-[#5e8d8d] text-sm flex items-center gap-2">
        <span className="material-symbols-outlined text-sm">verified_user</span>
        <span>Ambiente seguro e criptografado</span>
      </div>
    </div>
  );
};

export default Auth;
