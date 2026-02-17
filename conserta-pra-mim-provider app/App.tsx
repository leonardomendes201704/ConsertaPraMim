import React, { useCallback, useEffect, useMemo, useState } from 'react';
import Agenda from './components/Agenda';
import Auth from './components/Auth';
import Dashboard from './components/Dashboard';
import Profile from './components/Profile';
import Proposals from './components/Proposals';
import RequestDetails from './components/RequestDetails';
import SplashScreen from './components/SplashScreen';
import {
  checkProviderApiHealth,
  clearProviderAuthSession,
  loadProviderAuthSession,
  loginProviderWithEmailPassword,
  ProviderApiHealthCheckResult,
  ProviderAppApiError,
  saveProviderAuthSession
} from './services/auth';
import {
  confirmMobileProviderAgendaAppointment,
  createMobileProviderProposal,
  fetchMobileProviderDashboard,
  fetchMobileProviderAgenda,
  fetchMobileProviderProposals,
  fetchMobileProviderRequestDetails,
  MobileProviderError,
  rejectMobileProviderAgendaAppointment,
  respondMobileProviderAgendaReschedule
} from './services/mobileProvider';
import {
  ProviderAgendaData,
  ProviderAppState,
  ProviderAuthSession,
  ProviderCreateProposalPayload,
  ProviderDashboardData,
  ProviderProposalsData,
  ProviderRequestCard,
  ProviderRequestDetailsData
} from './types';

const DEFAULT_PROVIDER_EMAIL = 'prestador1@teste.com';
const DEFAULT_PROVIDER_PASSWORD = '123456';

function toAppErrorMessage(error: unknown): string {
  if (error instanceof ProviderAppApiError || error instanceof MobileProviderError) {
    return `${error.message} (${error.code})`;
  }

  if (error instanceof Error && error.message) {
    return error.message;
  }

  return 'Erro inesperado ao processar a requisicao.';
}

const App: React.FC = () => {
  const [currentView, setCurrentView] = useState<ProviderAppState>('SPLASH');
  const [viewVisitToken, setViewVisitToken] = useState(0);

  const [healthStatus, setHealthStatus] = useState<ProviderApiHealthCheckResult | null>(null);

  const [authSession, setAuthSession] = useState<ProviderAuthSession | null>(null);
  const [authLoading, setAuthLoading] = useState(false);
  const [authError, setAuthError] = useState('');

  const [dashboard, setDashboard] = useState<ProviderDashboardData | null>(null);
  const [dashboardLoading, setDashboardLoading] = useState(false);
  const [dashboardError, setDashboardError] = useState('');

  const [selectedRequest, setSelectedRequest] = useState<ProviderRequestCard | null>(null);
  const [requestDetails, setRequestDetails] = useState<ProviderRequestDetailsData | null>(null);
  const [requestDetailsLoading, setRequestDetailsLoading] = useState(false);
  const [requestDetailsError, setRequestDetailsError] = useState('');
  const [requestSubmitLoading, setRequestSubmitLoading] = useState(false);
  const [requestSubmitError, setRequestSubmitError] = useState('');
  const [requestSubmitSuccess, setRequestSubmitSuccess] = useState('');

  const [proposals, setProposals] = useState<ProviderProposalsData | null>(null);
  const [proposalsLoading, setProposalsLoading] = useState(false);
  const [proposalsError, setProposalsError] = useState('');

  const [agenda, setAgenda] = useState<ProviderAgendaData | null>(null);
  const [agendaLoading, setAgendaLoading] = useState(false);
  const [agendaError, setAgendaError] = useState('');
  const [agendaActionLoadingKey, setAgendaActionLoadingKey] = useState<string | null>(null);

  const goToView = useCallback((view: ProviderAppState) => {
    setCurrentView(view);
    setViewVisitToken((current) => current + 1);
  }, []);

  const refreshHealth = useCallback(async () => {
    const status = await checkProviderApiHealth();
    setHealthStatus(status);
    return status;
  }, []);

  const refreshDashboard = useCallback(async (session: ProviderAuthSession) => {
    setDashboardLoading(true);
    setDashboardError('');

    try {
      const payload = await fetchMobileProviderDashboard(session.token);
      setDashboard(payload);
    } catch (error) {
      setDashboardError(toAppErrorMessage(error));
    } finally {
      setDashboardLoading(false);
    }
  }, []);

  const refreshProposals = useCallback(async (session: ProviderAuthSession) => {
    setProposalsLoading(true);
    setProposalsError('');

    try {
      const payload = await fetchMobileProviderProposals(session.token);
      setProposals(payload);
    } catch (error) {
      setProposalsError(toAppErrorMessage(error));
    } finally {
      setProposalsLoading(false);
    }
  }, []);

  const refreshAgenda = useCallback(async (session: ProviderAuthSession) => {
    setAgendaLoading(true);
    setAgendaError('');

    try {
      const payload = await fetchMobileProviderAgenda(session.token, { statusFilter: 'all', take: 60 });
      setAgenda(payload);
    } catch (error) {
      setAgendaError(toAppErrorMessage(error));
    } finally {
      setAgendaLoading(false);
    }
  }, []);

  const refreshRequestDetails = useCallback(async (session: ProviderAuthSession, requestId: string) => {
    setRequestDetailsLoading(true);
    setRequestDetailsError('');

    try {
      const payload = await fetchMobileProviderRequestDetails(session.token, requestId);
      setRequestDetails(payload);
    } catch (error) {
      setRequestDetailsError(toAppErrorMessage(error));
    } finally {
      setRequestDetailsLoading(false);
    }
  }, []);

  useEffect(() => {
    const initialize = async () => {
      await new Promise((resolve) => window.setTimeout(resolve, 700));
      await refreshHealth();

      const storedSession = loadProviderAuthSession();
      if (storedSession) {
        setAuthSession(storedSession);
        goToView('DASHBOARD');
        return;
      }

      goToView('AUTH');
    };

    void initialize();
  }, [goToView, refreshHealth]);

  useEffect(() => {
    if (!authSession) {
      return;
    }

    if (currentView === 'DASHBOARD') {
      void refreshDashboard(authSession);
      return;
    }

    if (currentView === 'PROPOSALS') {
      void refreshProposals(authSession);
      return;
    }

    if (currentView === 'AGENDA') {
      void refreshAgenda(authSession);
      return;
    }

    if (currentView === 'REQUEST_DETAILS' && selectedRequest?.id) {
      void refreshRequestDetails(authSession, selectedRequest.id);
    }
  }, [
    authSession,
    currentView,
    selectedRequest?.id,
    viewVisitToken,
    refreshDashboard,
    refreshAgenda,
    refreshProposals,
    refreshRequestDetails
  ]);

  const handleLogin = useCallback(async (email: string, password: string) => {
    setAuthError('');
    setAuthLoading(true);

    try {
      const status = await refreshHealth();
      if (!status.available) {
        setAuthError(status.message);
        return;
      }

      const session = await loginProviderWithEmailPassword(email, password);
      saveProviderAuthSession(session);
      setAuthSession(session);
      goToView('DASHBOARD');
    } catch (error) {
      setAuthError(toAppErrorMessage(error));
    } finally {
      setAuthLoading(false);
    }
  }, [goToView, refreshHealth]);

  const handleLogout = useCallback(() => {
    clearProviderAuthSession();
    setAuthSession(null);
    setDashboard(null);
    setProposals(null);
    setAgenda(null);
    setSelectedRequest(null);
    setRequestDetails(null);
    setRequestSubmitError('');
    setRequestSubmitSuccess('');
    setAgendaError('');
    setAgendaActionLoadingKey(null);
    goToView('AUTH');
  }, [goToView]);

  const handleOpenRequest = useCallback((request: ProviderRequestCard) => {
    setSelectedRequest(request);
    setRequestDetails(null);
    setRequestSubmitError('');
    setRequestSubmitSuccess('');
    goToView('REQUEST_DETAILS');
  }, [goToView]);

  const handleOpenRequestById = useCallback((requestId: string) => {
    setSelectedRequest({
      id: requestId,
      category: 'Servico',
      categoryIcon: 'build_circle',
      description: '',
      status: '',
      createdAtUtc: '',
      createdAtLabel: '',
      street: '',
      city: '',
      zip: '',
      alreadyProposed: false
    });
    setRequestDetails(null);
    setRequestSubmitError('');
    setRequestSubmitSuccess('');
    goToView('REQUEST_DETAILS');
  }, [goToView]);

  const handleConfirmAgenda = useCallback(async (appointmentId: string) => {
    if (!authSession) {
      return;
    }

    setAgendaActionLoadingKey(`${appointmentId}:confirm`);
    setAgendaError('');

    try {
      await confirmMobileProviderAgendaAppointment(authSession.token, appointmentId);
      await Promise.all([
        refreshAgenda(authSession),
        refreshDashboard(authSession)
      ]);
    } catch (error) {
      setAgendaError(toAppErrorMessage(error));
    } finally {
      setAgendaActionLoadingKey(null);
    }
  }, [authSession, refreshAgenda, refreshDashboard]);

  const handleRejectAgenda = useCallback(async (appointmentId: string, reason: string) => {
    if (!authSession) {
      return;
    }

    setAgendaActionLoadingKey(`${appointmentId}:reject`);
    setAgendaError('');

    try {
      await rejectMobileProviderAgendaAppointment(authSession.token, appointmentId, reason);
      await Promise.all([
        refreshAgenda(authSession),
        refreshDashboard(authSession)
      ]);
    } catch (error) {
      setAgendaError(toAppErrorMessage(error));
    } finally {
      setAgendaActionLoadingKey(null);
    }
  }, [authSession, refreshAgenda, refreshDashboard]);

  const handleRespondAgendaReschedule = useCallback(async (appointmentId: string, accept: boolean, reason?: string) => {
    if (!authSession) {
      return;
    }

    setAgendaActionLoadingKey(`${appointmentId}:reschedule`);
    setAgendaError('');

    try {
      await respondMobileProviderAgendaReschedule(authSession.token, appointmentId, accept, reason);
      await Promise.all([
        refreshAgenda(authSession),
        refreshDashboard(authSession)
      ]);
    } catch (error) {
      setAgendaError(toAppErrorMessage(error));
    } finally {
      setAgendaActionLoadingKey(null);
    }
  }, [authSession, refreshAgenda, refreshDashboard]);

  const handleSubmitProposal = useCallback(async (payload: ProviderCreateProposalPayload) => {
    if (!authSession || !selectedRequest?.id) {
      return;
    }

    setRequestSubmitLoading(true);
    setRequestSubmitError('');
    setRequestSubmitSuccess('');

    try {
      const proposal = await createMobileProviderProposal(authSession.token, selectedRequest.id, payload);
      setRequestSubmitSuccess(`Proposta enviada com sucesso (${proposal.statusLabel}).`);
      await Promise.all([
        refreshRequestDetails(authSession, selectedRequest.id),
        refreshDashboard(authSession),
        refreshProposals(authSession)
      ]);
    } catch (error) {
      setRequestSubmitError(toAppErrorMessage(error));
    } finally {
      setRequestSubmitLoading(false);
    }
  }, [authSession, selectedRequest?.id, refreshDashboard, refreshProposals, refreshRequestDetails]);

  const content = useMemo(() => {
    if (currentView === 'SPLASH') {
      return <SplashScreen />;
    }

    if (currentView === 'AUTH') {
      return (
        <Auth
          loading={authLoading}
          error={authError}
          healthStatus={healthStatus}
          defaultEmail={DEFAULT_PROVIDER_EMAIL}
          defaultPassword={DEFAULT_PROVIDER_PASSWORD}
          onSubmit={handleLogin}
          onRetryHealth={refreshHealth}
        />
      );
    }

    if (currentView === 'DASHBOARD') {
      return (
        <Dashboard
          dashboard={dashboard}
          loading={dashboardLoading}
          error={dashboardError}
          onRefresh={async () => {
            if (!authSession) return;
            await refreshDashboard(authSession);
          }}
          onOpenRequest={handleOpenRequest}
          onOpenAgenda={() => goToView('AGENDA')}
          onOpenProposals={() => goToView('PROPOSALS')}
          onOpenProfile={() => goToView('PROFILE')}
        />
      );
    }

    if (currentView === 'AGENDA') {
      return (
        <Agenda
          agenda={agenda}
          loading={agendaLoading}
          error={agendaError}
          actionLoadingKey={agendaActionLoadingKey}
          onBack={() => goToView('DASHBOARD')}
          onRefresh={async () => {
            if (!authSession) return;
            await refreshAgenda(authSession);
          }}
          onOpenRequest={handleOpenRequestById}
          onConfirm={handleConfirmAgenda}
          onReject={handleRejectAgenda}
          onRespondReschedule={handleRespondAgendaReschedule}
        />
      );
    }

    if (currentView === 'REQUEST_DETAILS') {
      return (
        <RequestDetails
          details={requestDetails}
          loading={requestDetailsLoading}
          error={requestDetailsError}
          submitting={requestSubmitLoading}
          submitError={requestSubmitError}
          submitSuccess={requestSubmitSuccess}
          onBack={() => goToView('DASHBOARD')}
          onRefresh={async () => {
            if (!authSession || !selectedRequest?.id) return;
            await refreshRequestDetails(authSession, selectedRequest.id);
          }}
          onSubmitProposal={handleSubmitProposal}
        />
      );
    }

    if (currentView === 'PROPOSALS') {
      return (
        <Proposals
          proposals={proposals}
          loading={proposalsLoading}
          error={proposalsError}
          onBack={() => goToView('DASHBOARD')}
          onRefresh={async () => {
            if (!authSession) return;
            await refreshProposals(authSession);
          }}
        />
      );
    }

    return (
      <Profile
        session={authSession}
        onBack={() => goToView('DASHBOARD')}
        onLogout={handleLogout}
      />
    );
  }, [
    agenda,
    agendaActionLoadingKey,
    agendaError,
    agendaLoading,
    authError,
    authLoading,
    authSession,
    currentView,
    dashboard,
    dashboardError,
    dashboardLoading,
    goToView,
    handleConfirmAgenda,
    handleLogin,
    handleLogout,
    handleOpenRequest,
    handleOpenRequestById,
    handleRejectAgenda,
    handleRespondAgendaReschedule,
    handleSubmitProposal,
    healthStatus,
    proposals,
    proposalsError,
    proposalsLoading,
    refreshDashboard,
    refreshAgenda,
    refreshHealth,
    refreshProposals,
    refreshRequestDetails,
    requestDetails,
    requestDetailsError,
    requestDetailsLoading,
    requestSubmitError,
    requestSubmitLoading,
    requestSubmitSuccess,
    selectedRequest?.id
  ]);

  return content;
};

export default App;
