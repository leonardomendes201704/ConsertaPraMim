import React, { useCallback, useEffect, useMemo, useState } from 'react';
import Dashboard from './Dashboard';
import MonitoringPanel from './MonitoringPanel';
import {
  fetchMobileAdminDashboard,
  fetchMobileAdminMonitoringOverview,
  fetchMobileAdminMonitoringTopEndpoints,
  MobileAdminError
} from '../services/mobileAdmin';
import type {
  AdminAuthSession,
  AdminDashboardData,
  AdminHomeTab,
  AdminMonitoringOverviewData,
  AdminMonitoringTopEndpoint,
  MonitoringRangePreset
} from '../types';

interface AppShellProps {
  session: AdminAuthSession;
  onLogout: () => void;
}

const TAB_ITEMS: Array<{ id: AdminHomeTab; label: string; icon: string; helper: string }> = [
  { id: 'dashboard', label: 'Painel', icon: 'dashboard', helper: 'KPIs e visao executiva' },
  { id: 'monitoring', label: 'Monitorar', icon: 'monitoring', helper: 'Saude da API e endpoints' },
  { id: 'support', label: 'Chamados', icon: 'support_agent', helper: 'Fila de atendimento admin' },
  { id: 'settings', label: 'Conta', icon: 'manage_accounts', helper: 'Sessao e configuracoes locais' }
];

function toErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof MobileAdminError) {
    return error.message;
  }

  if (error instanceof Error && error.message.trim()) {
    return error.message;
  }

  return fallback;
}

const AppShell: React.FC<AppShellProps> = ({ session, onLogout }) => {
  const [activeTab, setActiveTab] = useState<AdminHomeTab>('dashboard');

  const [dashboardData, setDashboardData] = useState<AdminDashboardData | null>(null);
  const [isDashboardLoading, setIsDashboardLoading] = useState(false);
  const [dashboardError, setDashboardError] = useState('');

  const [monitoringRange, setMonitoringRange] = useState<MonitoringRangePreset>('24h');
  const [monitoringOverview, setMonitoringOverview] = useState<AdminMonitoringOverviewData | null>(null);
  const [monitoringTopEndpoints, setMonitoringTopEndpoints] = useState<AdminMonitoringTopEndpoint[]>([]);
  const [isMonitoringLoading, setIsMonitoringLoading] = useState(false);
  const [monitoringError, setMonitoringError] = useState('');

  const activeTabConfig = useMemo(() => {
    return TAB_ITEMS.find((item) => item.id === activeTab) || TAB_ITEMS[0];
  }, [activeTab]);

  const refreshDashboard = useCallback(async () => {
    setIsDashboardLoading(true);
    setDashboardError('');

    try {
      const payload = await fetchMobileAdminDashboard(session.token);
      setDashboardData(payload);
    } catch (error) {
      const message = toErrorMessage(error, 'Nao foi possivel carregar o dashboard.');
      setDashboardError(message);
    } finally {
      setIsDashboardLoading(false);
    }
  }, [session.token]);

  const refreshMonitoring = useCallback(async () => {
    setIsMonitoringLoading(true);
    setMonitoringError('');

    try {
      const [overview, topEndpoints] = await Promise.all([
        fetchMobileAdminMonitoringOverview(session.token, monitoringRange),
        fetchMobileAdminMonitoringTopEndpoints(session.token, monitoringRange)
      ]);

      setMonitoringOverview(overview);
      setMonitoringTopEndpoints(topEndpoints);
    } catch (error) {
      const message = toErrorMessage(error, 'Nao foi possivel carregar monitoramento.');
      setMonitoringError(message);
    } finally {
      setIsMonitoringLoading(false);
    }
  }, [monitoringRange, session.token]);

  useEffect(() => {
    void refreshDashboard();
  }, [refreshDashboard]);

  useEffect(() => {
    void refreshMonitoring();
  }, [refreshMonitoring]);

  return (
    <div className="min-h-screen bg-slate-100 text-slate-900">
      <header className="sticky top-0 z-20 border-b border-slate-200 bg-white/95 px-4 py-4 backdrop-blur">
        <div className="mx-auto flex max-w-lg items-center justify-between gap-3">
          <div>
            <p className="text-xs uppercase tracking-[0.18em] text-slate-500">Admin Mobile</p>
            <h1 className="text-lg font-semibold">{activeTabConfig.label}</h1>
            <p className="text-xs text-slate-500">{activeTabConfig.helper}</p>
          </div>
          <button
            type="button"
            onClick={onLogout}
            className="rounded-xl border border-slate-300 px-3 py-2 text-xs font-semibold text-slate-700 transition hover:bg-slate-50"
          >
            Sair
          </button>
        </div>
      </header>

      <main className="mx-auto max-w-lg px-4 pb-28 pt-5">
        <section className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
          <p className="text-xs uppercase tracking-[0.12em] text-slate-400">Usuario</p>
          <h2 className="mt-1 text-base font-semibold">{session.userName || session.email}</h2>
          <p className="text-sm text-slate-600">{session.email}</p>
        </section>

        <section className="mt-4 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
          {activeTab === 'dashboard' ? (
            <Dashboard
              data={dashboardData}
              isLoading={isDashboardLoading}
              errorMessage={dashboardError}
              onRefresh={refreshDashboard}
              onOpenMonitoring={() => setActiveTab('monitoring')}
            />
          ) : null}

          {activeTab === 'monitoring' ? (
            <MonitoringPanel
              overview={monitoringOverview}
              topEndpoints={monitoringTopEndpoints}
              range={monitoringRange}
              isLoading={isMonitoringLoading}
              errorMessage={monitoringError}
              onRefresh={refreshMonitoring}
              onChangeRange={setMonitoringRange}
            />
          ) : null}

          {activeTab === 'support' ? (
            <div className="space-y-3 rounded-2xl border border-slate-200 bg-slate-50 p-4">
              <h2 className="text-base font-semibold">Central de atendimento</h2>
              <p className="text-sm text-slate-600">ST-031 vai habilitar fila, detalhe, resposta e atualizacao de status.</p>
            </div>
          ) : null}

          {activeTab === 'settings' ? (
            <div className="space-y-3 rounded-2xl border border-slate-200 bg-slate-50 p-4">
              <h2 className="text-base font-semibold">Sessao ativa</h2>
              <p className="text-sm text-slate-600">Role: {session.role}</p>
              <p className="text-sm text-slate-600">Login: {new Date(session.loggedInAtIso).toLocaleString('pt-BR')}</p>
            </div>
          ) : null}
        </section>
      </main>

      <nav className="fixed bottom-0 left-0 right-0 z-20 border-t border-slate-200 bg-white/95 px-2 py-2 backdrop-blur">
        <div className="mx-auto flex max-w-lg items-stretch justify-between gap-1">
          {TAB_ITEMS.map((item) => {
            const isActive = item.id === activeTab;
            return (
              <button
                key={item.id}
                type="button"
                onClick={() => setActiveTab(item.id)}
                className={`flex min-w-0 flex-1 flex-col items-center rounded-xl px-2 py-2 text-[11px] font-medium transition ${
                  isActive ? 'bg-blue-50 text-blue-700' : 'text-slate-500 hover:bg-slate-50'
                }`}
              >
                <span className={`material-symbols-outlined text-[20px] ${isActive ? 'material-symbols-fill' : ''}`}>
                  {item.icon}
                </span>
                <span className="truncate">{item.label}</span>
              </button>
            );
          })}
        </div>
      </nav>
    </div>
  );
};

export default AppShell;