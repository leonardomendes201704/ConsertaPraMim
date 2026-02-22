import React, { useCallback, useEffect, useMemo, useState } from 'react';
import Dashboard from './Dashboard';
import MonitoringPanel from './MonitoringPanel';
import SupportTicketDetails from './SupportTicketDetails';
import SupportTickets from './SupportTickets';
import {
  addMobileAdminSupportTicketMessage,
  assignMobileAdminSupportTicket,
  fetchMobileAdminDashboard,
  fetchMobileAdminMonitoringOverview,
  fetchMobileAdminMonitoringTopEndpoints,
  fetchMobileAdminSupportTicketDetails,
  fetchMobileAdminSupportTickets,
  MobileAdminError,
  updateMobileAdminSupportTicketStatus
} from '../services/mobileAdmin';
import type {
  AdminAuthSession,
  AdminDashboardData,
  AdminHomeTab,
  AdminMonitoringOverviewData,
  AdminMonitoringTopEndpoint,
  AdminSupportTicketDetails,
  AdminSupportTicketsListResponse,
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

  const [supportStatusFilter, setSupportStatusFilter] = useState('all');
  const [supportListPayload, setSupportListPayload] = useState<AdminSupportTicketsListResponse | null>(null);
  const [isSupportListLoading, setIsSupportListLoading] = useState(false);
  const [supportListError, setSupportListError] = useState('');

  const [selectedSupportTicketId, setSelectedSupportTicketId] = useState<string | null>(null);
  const [selectedSupportTicketDetails, setSelectedSupportTicketDetails] = useState<AdminSupportTicketDetails | null>(null);
  const [isSupportDetailsLoading, setIsSupportDetailsLoading] = useState(false);
  const [supportDetailsError, setSupportDetailsError] = useState('');
  const [isSupportActionLoading, setIsSupportActionLoading] = useState(false);

  const activeTabConfig = useMemo(() => {
    return TAB_ITEMS.find((item) => item.id === activeTab) || TAB_ITEMS[0];
  }, [activeTab]);

  const isUnauthorizedError = useCallback((error: unknown): boolean => {
    return error instanceof MobileAdminError && error.httpStatus === 401;
  }, []);

  const refreshDashboard = useCallback(async () => {
    setIsDashboardLoading(true);
    setDashboardError('');

    try {
      const payload = await fetchMobileAdminDashboard(session.token);
      setDashboardData(payload);
    } catch (error) {
      if (isUnauthorizedError(error)) {
        onLogout();
        return;
      }

      setDashboardError(toErrorMessage(error, 'Nao foi possivel carregar o dashboard.'));
    } finally {
      setIsDashboardLoading(false);
    }
  }, [isUnauthorizedError, onLogout, session.token]);

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
      if (isUnauthorizedError(error)) {
        onLogout();
        return;
      }

      setMonitoringError(toErrorMessage(error, 'Nao foi possivel carregar monitoramento.'));
    } finally {
      setIsMonitoringLoading(false);
    }
  }, [isUnauthorizedError, monitoringRange, onLogout, session.token]);

  const refreshSupportTicketsList = useCallback(async () => {
    setIsSupportListLoading(true);
    setSupportListError('');

    try {
      const payload = await fetchMobileAdminSupportTickets(session.token, {
        status: supportStatusFilter,
        page: 1,
        pageSize: 20
      });

      setSupportListPayload(payload);
    } catch (error) {
      if (isUnauthorizedError(error)) {
        onLogout();
        return;
      }

      setSupportListError(toErrorMessage(error, 'Nao foi possivel carregar fila de chamados.'));
    } finally {
      setIsSupportListLoading(false);
    }
  }, [isUnauthorizedError, onLogout, session.token, supportStatusFilter]);

  const refreshSelectedSupportTicket = useCallback(async (ticketId: string) => {
    setIsSupportDetailsLoading(true);
    setSupportDetailsError('');

    try {
      const details = await fetchMobileAdminSupportTicketDetails(session.token, ticketId);
      setSelectedSupportTicketDetails(details);
    } catch (error) {
      if (isUnauthorizedError(error)) {
        onLogout();
        return;
      }

      setSupportDetailsError(toErrorMessage(error, 'Nao foi possivel carregar detalhe do chamado.'));
    } finally {
      setIsSupportDetailsLoading(false);
    }
  }, [isUnauthorizedError, onLogout, session.token]);

  const handleOpenSupportTicket = useCallback((ticketId: string) => {
    setSelectedSupportTicketId(ticketId);
  }, []);

  const handleCloseSupportTicket = useCallback(() => {
    setSelectedSupportTicketId(null);
    setSelectedSupportTicketDetails(null);
    setSupportDetailsError('');
  }, []);

  const handleSupportSendMessage = useCallback(async (message: string, isInternal: boolean) => {
    if (!selectedSupportTicketId) {
      return;
    }

    setIsSupportActionLoading(true);
    try {
      const updated = await addMobileAdminSupportTicketMessage(session.token, selectedSupportTicketId, {
        message,
        isInternal
      });

      setSelectedSupportTicketDetails(updated);
      setSupportDetailsError('');
      await refreshSupportTicketsList();
    } catch (error) {
      if (isUnauthorizedError(error)) {
        onLogout();
        return;
      }

      setSupportDetailsError(toErrorMessage(error, 'Nao foi possivel enviar mensagem no chamado.'));
    } finally {
      setIsSupportActionLoading(false);
    }
  }, [isUnauthorizedError, onLogout, refreshSupportTicketsList, selectedSupportTicketId, session.token]);

  const handleSupportAssignToMe = useCallback(async () => {
    if (!selectedSupportTicketId) {
      return;
    }

    setIsSupportActionLoading(true);
    try {
      const updated = await assignMobileAdminSupportTicket(session.token, selectedSupportTicketId, session.userId);
      setSelectedSupportTicketDetails(updated);
      setSupportDetailsError('');
      await refreshSupportTicketsList();
    } catch (error) {
      if (isUnauthorizedError(error)) {
        onLogout();
        return;
      }

      setSupportDetailsError(toErrorMessage(error, 'Nao foi possivel atribuir o chamado para seu usuario.'));
    } finally {
      setIsSupportActionLoading(false);
    }
  }, [isUnauthorizedError, onLogout, refreshSupportTicketsList, selectedSupportTicketId, session.token, session.userId]);

  const handleSupportUpdateStatus = useCallback(async (status: string) => {
    if (!selectedSupportTicketId) {
      return;
    }

    setIsSupportActionLoading(true);
    try {
      const updated = await updateMobileAdminSupportTicketStatus(session.token, selectedSupportTicketId, status);
      setSelectedSupportTicketDetails(updated);
      setSupportDetailsError('');
      await refreshSupportTicketsList();
    } catch (error) {
      if (isUnauthorizedError(error)) {
        onLogout();
        return;
      }

      setSupportDetailsError(toErrorMessage(error, 'Nao foi possivel atualizar status do chamado.'));
    } finally {
      setIsSupportActionLoading(false);
    }
  }, [isUnauthorizedError, onLogout, refreshSupportTicketsList, selectedSupportTicketId, session.token]);

  useEffect(() => {
    void refreshDashboard();
  }, [refreshDashboard]);

  useEffect(() => {
    void refreshMonitoring();
  }, [refreshMonitoring]);

  useEffect(() => {
    if (activeTab === 'support') {
      void refreshSupportTicketsList();
    }
  }, [activeTab, refreshSupportTicketsList]);

  useEffect(() => {
    if (selectedSupportTicketId) {
      void refreshSelectedSupportTicket(selectedSupportTicketId);
    }
  }, [selectedSupportTicketId, refreshSelectedSupportTicket]);

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
            selectedSupportTicketId ? (
              <SupportTicketDetails
                details={selectedSupportTicketDetails}
                isLoading={isSupportDetailsLoading}
                isActionLoading={isSupportActionLoading}
                errorMessage={supportDetailsError}
                onBack={handleCloseSupportTicket}
                onRefresh={() => {
                  if (selectedSupportTicketId) {
                    void refreshSelectedSupportTicket(selectedSupportTicketId);
                  }
                }}
                onSendMessage={handleSupportSendMessage}
                onAssignToMe={handleSupportAssignToMe}
                onUpdateStatus={handleSupportUpdateStatus}
              />
            ) : (
              <SupportTickets
                items={supportListPayload?.items || []}
                indicators={supportListPayload?.indicators || null}
                statusFilter={supportStatusFilter}
                isLoading={isSupportListLoading}
                errorMessage={supportListError}
                onStatusFilterChange={setSupportStatusFilter}
                onRefresh={refreshSupportTicketsList}
                onOpenTicket={(ticketId) => {
                  handleOpenSupportTicket(ticketId);
                }}
              />
            )
          ) : null}

          {activeTab === 'settings' ? (
            <div className="space-y-3 rounded-2xl border border-slate-200 bg-slate-50 p-4">
              <h2 className="text-base font-semibold">Sessao ativa</h2>
              <p className="text-sm text-slate-600">Role: {session.role}</p>
              <p className="text-sm text-slate-600">Login: {new Date(session.loggedInAtIso).toLocaleString('pt-BR')}</p>
              <p className="text-sm text-slate-600">ID: {session.userId}</p>
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
                onClick={() => {
                  setActiveTab(item.id);
                  if (item.id !== 'support') {
                    handleCloseSupportTicket();
                  }
                }}
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
