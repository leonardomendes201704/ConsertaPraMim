import React, { useEffect, useState } from 'react';
import { ProviderApiHealthCheckResult } from '../services/auth';

interface Props {
  loading: boolean;
  error: string;
  healthStatus: ProviderApiHealthCheckResult | null;
  defaultEmail: string;
  defaultPassword: string;
  onSubmit: (email: string, password: string) => Promise<void>;
  onRetryHealth: () => Promise<void>;
}

const Auth: React.FC<Props> = ({
  loading,
  error,
  healthStatus,
  defaultEmail,
  defaultPassword,
  onSubmit,
  onRetryHealth
}) => {
  const [email, setEmail] = useState(defaultEmail);
  const [password, setPassword] = useState(defaultPassword);

  useEffect(() => {
    setEmail(defaultEmail);
    setPassword(defaultPassword);
  }, [defaultEmail, defaultPassword]);

  const maintenanceMode = healthStatus ? !healthStatus.available : false;

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (loading || maintenanceMode) {
      return;
    }

    await onSubmit(email, password);
  };

  return (
    <div className="min-h-screen bg-[#f4f7fb] flex items-center justify-center p-4">
      <div className="w-full max-w-md bg-white rounded-3xl shadow-xl border border-[#d9e5ff] p-6">
        <div className="text-center mb-6">
          <div className="mx-auto w-14 h-14 rounded-2xl bg-primary/10 text-primary flex items-center justify-center mb-3">
            <span className="material-symbols-outlined text-3xl material-symbols-fill">construction</span>
          </div>
          <h1 className="text-2xl font-bold text-[#101828]">Login do Prestador</h1>
          <p className="text-sm text-[#475467] mt-1">Acesse seu painel de atendimento no app.</p>
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

        {error && !maintenanceMode && (
          <div className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-red-700 text-sm mb-4">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-sm font-semibold text-[#344054] mb-1">E-mail</label>
            <input
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              className="w-full rounded-xl border border-[#d0d5dd] px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-primary/30"
              placeholder="prestador@teste.com"
              autoComplete="username"
            />
          </div>

          <div>
            <label className="block text-sm font-semibold text-[#344054] mb-1">Senha</label>
            <input
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              className="w-full rounded-xl border border-[#d0d5dd] px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-primary/30"
              placeholder="â€¢â€¢â€¢â€¢â€¢â€¢â€¢â€¢"
              autoComplete="current-password"
            />
          </div>

          <button
            type="submit"
            disabled={loading || maintenanceMode}
            className="w-full rounded-xl bg-primary text-white font-bold py-3 disabled:opacity-60"
          >
            {loading ? 'Entrando...' : 'Entrar'}
          </button>
        </form>
      </div>
    </div>
  );
};

export default Auth;
