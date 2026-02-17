import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import Agenda from './components/Agenda';
import Auth from './components/Auth';
import Chat from './components/Chat';
import ChatList from './components/ChatList';
import Dashboard from './components/Dashboard';
import Profile from './components/Profile';
import Proposals from './components/Proposals';
import RequestDetails from './components/RequestDetails';
import SplashScreen from './components/SplashScreen';
import {
  checkProviderApiHealth,
  clearProviderAuthSession,
  disableProviderBiometricLogin,
  enableProviderBiometricLoginForSession,
  getProviderBiometricLoginState,
  loadProviderAuthSession,
  loginProviderWithBiometrics,
  loginProviderWithEmailPassword,
  ProviderApiHealthCheckResult,
  ProviderAppApiError,
  ProviderBiometricAuthError,
  ProviderBiometricLoginState,
  saveProviderAuthSession
} from './services/auth';
import {
  confirmMobileProviderAgendaAppointment,
  createMobileProviderProposal,
  fetchMobileProviderAgendaChecklist,
  fetchMobileProviderChatConversations,
  fetchMobileProviderDashboard,
  fetchMobileProviderAgenda,
  fetchMobileProviderProposals,
  fetchMobileProviderRequestDetails,
  markMobileProviderAgendaArrival,
  MobileProviderError,
  markMobileProviderChatRead,
  rejectMobileProviderAgendaAppointment,
  respondMobileProviderAgendaReschedule,
  startMobileProviderAgendaExecution,
  updateMobileProviderAgendaChecklistItem,
  updateMobileProviderAgendaOperationalStatus,
  uploadMobileProviderAgendaChecklistEvidence
} from './services/mobileProvider';
import {
  startProviderRealtimeChatConnection,
  stopProviderRealtimeChatConnection,
  subscribeToProviderRealtimeChatEvents
} from './services/realtimeChat';
import {
  ProviderAgendaData,
  ProviderAppointmentChecklist,
  ProviderAppState,
  ProviderAuthSession,
  ProviderAppNotification,
  ProviderChatConversationSummary,
  ProviderChecklistEvidenceUploadResult,
  ProviderChecklistItemUpsertPayload,
  ProviderCreateProposalPayload,
  ProviderDashboardData,
  ProviderChatMessage,
  ProviderProposalsData,
  ProviderRequestCard,
  ProviderRequestDetailsData
} from './types';

const DEFAULT_PROVIDER_EMAIL = 'prestador1@teste.com';
const DEFAULT_PROVIDER_PASSWORD = '123456';

function toAppErrorMessage(error: unknown): string {
  if (error instanceof ProviderAppApiError || error instanceof ProviderBiometricAuthError || error instanceof MobileProviderError) {
    return `${error.message} (${error.code})`;
  }

  if (error instanceof Error && error.message) {
    return error.message;
  }

  return 'Erro inesperado ao processar a requisicao.';
}

function normalizeEntityId(value?: string | null): string {
  return String(value || '').trim().toLowerCase();
}

function formatNotificationTimestamp(value?: string): string {
  if (!value) {
    return 'Agora';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return 'Agora';
  }

  const dd = String(date.getDate()).padStart(2, '0');
  const mm = String(date.getMonth() + 1).padStart(2, '0');
  const hh = String(date.getHours()).padStart(2, '0');
  const min = String(date.getMinutes()).padStart(2, '0');
  return `${dd}/${mm} ${hh}:${min}`;
}

function buildChatNotificationDescription(messageText?: string, attachmentCount = 0): string {
  const normalizedText = String(messageText || '').trim();
  if (normalizedText) {
    return normalizedText.length > 120 ? `${normalizedText.slice(0, 120)}...` : normalizedText;
  }

  if (attachmentCount <= 0) {
    return 'Nova mensagem recebida.';
  }

  if (attachmentCount === 1) {
    return 'Novo anexo recebido.';
  }

  return `${attachmentCount} anexos recebidos.`;
}

const App: React.FC = () => {
  const [currentView, setCurrentView] = useState<ProviderAppState>('SPLASH');
  const [viewVisitToken, setViewVisitToken] = useState(0);

  const [healthStatus, setHealthStatus] = useState<ProviderApiHealthCheckResult | null>(null);

  const [authSession, setAuthSession] = useState<ProviderAuthSession | null>(null);
  const [authLoading, setAuthLoading] = useState(false);
  const [authError, setAuthError] = useState('');
  const [biometricState, setBiometricState] = useState<ProviderBiometricLoginState>({
    isNativeRuntime: false,
    isBiometryAvailable: false,
    isBiometricLoginEnabled: false,
    hasStoredBiometricSession: false
  });

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
  const [agendaChecklists, setAgendaChecklists] = useState<Record<string, ProviderAppointmentChecklist | undefined>>({});

  const [chatConversations, setChatConversations] = useState<ProviderChatConversationSummary[]>([]);
  const [chatLoading, setChatLoading] = useState(false);
  const [chatError, setChatError] = useState('');
  const [selectedConversation, setSelectedConversation] = useState<ProviderChatConversationSummary | null>(null);
  const [chatBackView, setChatBackView] = useState<ProviderAppState>('CHAT_LIST');
  const [notifications, setNotifications] = useState<ProviderAppNotification[]>([]);
  const [toastNotification, setToastNotification] = useState<ProviderAppNotification | null>(null);

  const currentViewRef = useRef<ProviderAppState>('SPLASH');
  const selectedConversationRef = useRef<ProviderChatConversationSummary | null>(null);

  const goToView = useCallback((view: ProviderAppState) => {
    setCurrentView(view);
    setViewVisitToken((current) => current + 1);
  }, []);

  const unreadChatMessages = useMemo(
    () => chatConversations.reduce((sum, conversation) => sum + Math.max(0, conversation.unreadMessages || 0), 0),
    [chatConversations]
  );

  useEffect(() => {
    currentViewRef.current = currentView;
  }, [currentView]);

  useEffect(() => {
    selectedConversationRef.current = selectedConversation;
  }, [selectedConversation]);

  const mergeAndSortConversations = useCallback((items: ProviderChatConversationSummary[]) => {
    return [...items].sort((a, b) => new Date(b.lastMessageAt).getTime() - new Date(a.lastMessageAt).getTime());
  }, []);

  const refreshHealth = useCallback(async () => {
    const status = await checkProviderApiHealth();
    setHealthStatus(status);
    return status;
  }, []);

  const refreshBiometricState = useCallback(async () => {
    const state = await getProviderBiometricLoginState();
    setBiometricState(state);
    return state;
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

  const refreshChatConversations = useCallback(async (session: ProviderAuthSession) => {
    setChatLoading(true);
    setChatError('');

    try {
      const payload = await fetchMobileProviderChatConversations(session.token);
      setChatConversations(mergeAndSortConversations(payload));
    } catch (error) {
      setChatError(toAppErrorMessage(error));
    } finally {
      setChatLoading(false);
    }
  }, [mergeAndSortConversations]);

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
      await refreshBiometricState();

      const storedSession = loadProviderAuthSession();
      if (storedSession) {
        setAuthSession(storedSession);
        goToView('DASHBOARD');
        return;
      }

      goToView('AUTH');
    };

    void initialize();
  }, [goToView, refreshBiometricState, refreshHealth]);

  const markConversationAsReadLocal = useCallback((requestId: string, providerId: string) => {
    setChatConversations((previous) => previous.map((conversation) => {
      if (
        normalizeEntityId(conversation.requestId) !== normalizeEntityId(requestId)
        || normalizeEntityId(conversation.providerId) !== normalizeEntityId(providerId)
      ) {
        return conversation;
      }

      if ((conversation.unreadMessages || 0) <= 0) {
        return conversation;
      }

      return {
        ...conversation,
        unreadMessages: 0
      };
    }));
  }, []);

  const openConversation = useCallback((
    conversation: ProviderChatConversationSummary,
    backView: ProviderAppState = 'CHAT_LIST') => {
    setSelectedConversation(conversation);
    setChatBackView(backView);
    markConversationAsReadLocal(conversation.requestId, conversation.providerId);
    goToView('CHAT');
  }, [goToView, markConversationAsReadLocal]);

  const handleToastNotificationClick = useCallback((notification: ProviderAppNotification) => {
    setNotifications((current) => current.map((item) => (
      item.id === notification.id
        ? { ...item, read: true }
        : item
    )));

    if (!notification.requestId || !notification.providerId) {
      return;
    }

    const existing = chatConversations.find((conversation) =>
      normalizeEntityId(conversation.requestId) === normalizeEntityId(notification.requestId)
      && normalizeEntityId(conversation.providerId) === normalizeEntityId(notification.providerId));

    if (existing) {
      openConversation(existing, currentViewRef.current);
      return;
    }

    const fallback: ProviderChatConversationSummary = {
      requestId: notification.requestId,
      providerId: notification.providerId,
      counterpartUserId: '',
      counterpartRole: 'Client',
      counterpartName: notification.counterpartName || 'Cliente',
      title: `Pedido #${notification.requestId.slice(0, 8)}`,
      lastMessagePreview: notification.description,
      lastMessageAt: new Date().toISOString(),
      unreadMessages: 0,
      counterpartIsOnline: false
    };

    setChatConversations((previous) => mergeAndSortConversations([fallback, ...previous]));
    openConversation(fallback, currentViewRef.current);
  }, [chatConversations, mergeAndSortConversations, openConversation]);

  const handleRealtimeChatMessageReceived = useCallback((message: ProviderChatMessage) => {
    if (!authSession?.userId) {
      return;
    }

    const fromCurrentProvider = normalizeEntityId(message.senderId) === normalizeEntityId(authSession.userId);
    const activeConversation = selectedConversationRef.current;
    const isActiveConversation =
      currentViewRef.current === 'CHAT'
      && !!activeConversation
      && normalizeEntityId(activeConversation.requestId) === normalizeEntityId(message.requestId)
      && normalizeEntityId(activeConversation.providerId) === normalizeEntityId(message.providerId);

    let shouldNotify = false;
    let notificationCounterpartName = 'Cliente';
    const preview = buildChatNotificationDescription(message.text, message.attachments?.length || 0);

    setChatConversations((previous) => {
      const index = previous.findIndex((conversation) =>
        normalizeEntityId(conversation.requestId) === normalizeEntityId(message.requestId)
        && normalizeEntityId(conversation.providerId) === normalizeEntityId(message.providerId));

      const current = index >= 0 ? previous[index] : null;
      const incrementUnread = !fromCurrentProvider && !isActiveConversation;
      shouldNotify = incrementUnread;
      notificationCounterpartName = current?.counterpartName || (!fromCurrentProvider ? message.senderName : 'Cliente');

      const updated: ProviderChatConversationSummary = {
        requestId: message.requestId,
        providerId: message.providerId,
        counterpartUserId: current?.counterpartUserId || '',
        counterpartRole: current?.counterpartRole || 'Client',
        counterpartName: current?.counterpartName || (!fromCurrentProvider ? message.senderName : 'Cliente'),
        title: current?.title || `Pedido #${message.requestId.slice(0, 8)}`,
        lastMessagePreview: preview,
        lastMessageAt: message.createdAt || new Date().toISOString(),
        unreadMessages: Math.max(0, (current?.unreadMessages || 0) + (incrementUnread ? 1 : 0)),
        counterpartIsOnline: current?.counterpartIsOnline || false,
        providerStatus: current?.providerStatus
      };

      if (index < 0) {
        return mergeAndSortConversations([updated, ...previous]);
      }

      const next = [...previous];
      next[index] = updated;
      return mergeAndSortConversations(next);
    });

    if (!shouldNotify) {
      return;
    }

    const notification: ProviderAppNotification = {
      id: `provider-chat-${message.id}-${Date.now()}`,
      type: 'MESSAGE',
      title: `Nova mensagem de ${notificationCounterpartName}`,
      description: preview,
      timestamp: formatNotificationTimestamp(message.createdAt),
      read: false,
      requestId: message.requestId,
      providerId: message.providerId,
      counterpartName: notificationCounterpartName
    };

    setNotifications((current) => [notification, ...current].slice(0, 200));
    setToastNotification(notification);
  }, [authSession?.userId, mergeAndSortConversations]);

  useEffect(() => {
    if (!toastNotification) {
      return undefined;
    }

    const timer = window.setTimeout(() => setToastNotification(null), 6000);
    return () => window.clearTimeout(timer);
  }, [toastNotification]);

  useEffect(() => {
    if (!authSession?.token) {
      void stopProviderRealtimeChatConnection();
      return undefined;
    }

    let disposed = false;
    const unsubscribe = subscribeToProviderRealtimeChatEvents({
      onChatMessage: (message) => {
        if (disposed) {
          return;
        }

        handleRealtimeChatMessageReceived(message);
      }
    });

    const bootstrap = async () => {
      try {
        await startProviderRealtimeChatConnection(authSession.token);
      } catch {
        // conexao realtime e reestabelecida quando o usuario abrir o chat
      }
    };

    void bootstrap();

    return () => {
      disposed = true;
      unsubscribe();
      void stopProviderRealtimeChatConnection();
    };
  }, [authSession?.token, handleRealtimeChatMessageReceived]);

  useEffect(() => {
    if (!authSession) {
      return;
    }

    if (currentView === 'DASHBOARD') {
      void Promise.all([
        refreshDashboard(authSession),
        refreshChatConversations(authSession)
      ]);
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

    if (currentView === 'CHAT_LIST') {
      void refreshChatConversations(authSession);
      return;
    }

    if (currentView === 'CHAT' && selectedConversation?.requestId) {
      void markMobileProviderChatRead(authSession.token, selectedConversation.requestId);
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
    refreshChatConversations,
    refreshProposals,
    refreshRequestDetails
  ]);

  const handleLogin = useCallback(async (email: string, password: string, enableBiometricLogin: boolean) => {
    setAuthError('');
    setAuthLoading(true);

    try {
      const status = await refreshHealth();
      if (!status.available) {
        setAuthError(status.message);
        return;
      }

      const session = await loginProviderWithEmailPassword(email, password);

      if (biometricState.isNativeRuntime && biometricState.isBiometryAvailable) {
        if (enableBiometricLogin) {
          await enableProviderBiometricLoginForSession(session);
        } else if (biometricState.isBiometricLoginEnabled || biometricState.hasStoredBiometricSession) {
          await disableProviderBiometricLogin();
        }
      }

      saveProviderAuthSession(session);
      setAuthSession(session);
      await refreshBiometricState();
      goToView('DASHBOARD');
    } catch (error) {
      setAuthError(toAppErrorMessage(error));
    } finally {
      setAuthLoading(false);
    }
  }, [biometricState, goToView, refreshBiometricState, refreshHealth]);

  const handleBiometricLogin = useCallback(async () => {
    setAuthError('');
    setAuthLoading(true);

    try {
      const status = await refreshHealth();
      if (!status.available) {
        setAuthError(status.message);
        return;
      }

      const session = await loginProviderWithBiometrics();
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
    void stopProviderRealtimeChatConnection();
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
    setAgendaChecklists({});
    setChatConversations([]);
    setChatError('');
    setSelectedConversation(null);
    setChatBackView('CHAT_LIST');
    setNotifications([]);
    setToastNotification(null);
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

  const handleSelectConversation = useCallback((conversation: ProviderChatConversationSummary) => {
    openConversation(conversation, 'CHAT_LIST');
  }, [openConversation]);

  const handleOpenRequestChat = useCallback(() => {
    if (!authSession?.userId || !selectedRequest?.id) {
      return;
    }

    const existing = chatConversations.find((conversation) =>
      normalizeEntityId(conversation.requestId) === normalizeEntityId(selectedRequest.id)
      && normalizeEntityId(conversation.providerId) === normalizeEntityId(authSession.userId));

    if (existing) {
      openConversation(existing, 'REQUEST_DETAILS');
      return;
    }

    const fallbackConversation: ProviderChatConversationSummary = {
      requestId: selectedRequest.id,
      providerId: authSession.userId,
      counterpartUserId: '',
      counterpartRole: 'Client',
      counterpartName: 'Cliente',
      title: selectedRequest.category || `Pedido #${selectedRequest.id.slice(0, 8)}`,
      lastMessagePreview: 'Conversa iniciada pelo prestador.',
      lastMessageAt: new Date().toISOString(),
      unreadMessages: 0,
      counterpartIsOnline: false
    };

    setChatConversations((previous) => mergeAndSortConversations([fallbackConversation, ...previous]));
    openConversation(fallbackConversation, 'REQUEST_DETAILS');
  }, [
    authSession?.userId,
    chatConversations,
    mergeAndSortConversations,
    openConversation,
    selectedRequest?.category,
    selectedRequest?.id
  ]);

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

  const handleMarkAgendaArrival = useCallback(async (
    appointmentId: string,
    payload?: {
      latitude?: number;
      longitude?: number;
      accuracyMeters?: number;
      manualReason?: string;
    }) => {
    if (!authSession) {
      return;
    }

    setAgendaActionLoadingKey(`${appointmentId}:arrive`);
    setAgendaError('');

    try {
      await markMobileProviderAgendaArrival(authSession.token, appointmentId, payload);
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

  const handleStartAgendaExecution = useCallback(async (appointmentId: string, reason?: string) => {
    if (!authSession) {
      return;
    }

    setAgendaActionLoadingKey(`${appointmentId}:start`);
    setAgendaError('');

    try {
      await startMobileProviderAgendaExecution(authSession.token, appointmentId, reason);
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

  const handleUpdateAgendaOperationalStatus = useCallback(async (
    appointmentId: string,
    operationalStatus: string,
    reason?: string) => {
    if (!authSession) {
      return;
    }

    setAgendaActionLoadingKey(`${appointmentId}:operational-status`);
    setAgendaError('');

    try {
      await updateMobileProviderAgendaOperationalStatus(
        authSession.token,
        appointmentId,
        operationalStatus,
        reason);
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

  const handleLoadAgendaChecklist = useCallback(async (appointmentId: string) => {
    if (!authSession) {
      return;
    }

    setAgendaActionLoadingKey(`${appointmentId}:checklist:load`);
    setAgendaError('');

    try {
      const checklist = await fetchMobileProviderAgendaChecklist(authSession.token, appointmentId);
      setAgendaChecklists((current) => ({
        ...current,
        [normalizeEntityId(appointmentId)]: checklist
      }));
    } catch (error) {
      setAgendaError(toAppErrorMessage(error));
    } finally {
      setAgendaActionLoadingKey(null);
    }
  }, [authSession]);

  const handleUpdateAgendaChecklistItem = useCallback(async (
    appointmentId: string,
    payload: ProviderChecklistItemUpsertPayload) => {
    if (!authSession) {
      return;
    }

    setAgendaActionLoadingKey(`${appointmentId}:checklist:item:${payload.templateItemId}`);
    setAgendaError('');

    try {
      const checklist = await updateMobileProviderAgendaChecklistItem(authSession.token, appointmentId, payload);
      setAgendaChecklists((current) => ({
        ...current,
        [normalizeEntityId(appointmentId)]: checklist
      }));
    } catch (error) {
      setAgendaError(toAppErrorMessage(error));
    } finally {
      setAgendaActionLoadingKey(null);
    }
  }, [authSession]);

  const handleUploadAgendaChecklistEvidence = useCallback(async (
    appointmentId: string,
    file: File): Promise<ProviderChecklistEvidenceUploadResult> => {
    if (!authSession) {
      throw new Error('Sessao invalida.');
    }

    setAgendaActionLoadingKey(`${appointmentId}:checklist:upload`);
    setAgendaError('');

    try {
      return await uploadMobileProviderAgendaChecklistEvidence(authSession.token, appointmentId, file);
    } catch (error) {
      setAgendaError(toAppErrorMessage(error));
      throw error;
    } finally {
      setAgendaActionLoadingKey(null);
    }
  }, [authSession]);

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

  const content = (() => {
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
          biometricAvailable={biometricState.isNativeRuntime && biometricState.isBiometryAvailable}
          biometricEnabled={biometricState.isBiometricLoginEnabled}
          biometricHasStoredSession={biometricState.hasStoredBiometricSession}
          onBiometricLogin={handleBiometricLogin}
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
          unreadChatMessages={unreadChatMessages}
          onRefresh={async () => {
            if (!authSession) return;
            await Promise.all([
              refreshDashboard(authSession),
              refreshChatConversations(authSession)
            ]);
          }}
          onOpenRequest={handleOpenRequest}
          onOpenAgenda={() => goToView('AGENDA')}
          onOpenChatList={() => goToView('CHAT_LIST')}
          onOpenProposals={() => goToView('PROPOSALS')}
          onOpenProfile={() => goToView('PROFILE')}
        />
      );
    }

    if (currentView === 'AGENDA') {
      return (
        <Agenda
          agenda={agenda}
          checklists={agendaChecklists}
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
          onMarkArrival={handleMarkAgendaArrival}
          onStartExecution={handleStartAgendaExecution}
          onUpdateOperationalStatus={handleUpdateAgendaOperationalStatus}
          onLoadChecklist={handleLoadAgendaChecklist}
          onUpdateChecklistItem={handleUpdateAgendaChecklistItem}
          onUploadChecklistEvidence={handleUploadAgendaChecklistEvidence}
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
          onOpenChat={handleOpenRequestChat}
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

    if (currentView === 'CHAT_LIST') {
      return (
        <ChatList
          conversations={chatConversations}
          loading={chatLoading}
          error={chatError}
          onBack={() => goToView('DASHBOARD')}
          onRefresh={async () => {
            if (!authSession) return;
            await refreshChatConversations(authSession);
          }}
          onSelectConversation={handleSelectConversation}
          onGoHome={() => goToView('DASHBOARD')}
          onGoProposals={() => goToView('PROPOSALS')}
          onGoAgenda={() => goToView('AGENDA')}
          onGoProfile={() => goToView('PROFILE')}
        />
      );
    }

    if (currentView === 'CHAT') {
      if (!selectedConversation) {
        return (
          <ChatList
            conversations={chatConversations}
            loading={chatLoading}
            error={chatError}
            onBack={() => goToView('DASHBOARD')}
            onRefresh={async () => {
              if (!authSession) return;
              await refreshChatConversations(authSession);
            }}
            onSelectConversation={handleSelectConversation}
            onGoHome={() => goToView('DASHBOARD')}
            onGoProposals={() => goToView('PROPOSALS')}
            onGoAgenda={() => goToView('AGENDA')}
            onGoProfile={() => goToView('PROFILE')}
          />
        );
      }

      return (
        <Chat
          authSession={authSession}
          conversation={selectedConversation}
          onBack={() => goToView(chatBackView)}
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
  })();

  return (
    <>
      {toastNotification ? (
        <div className="fixed left-1/2 top-4 z-[120] w-[calc(100%-1.5rem)] max-w-md -translate-x-1/2">
          <button
            type="button"
            onClick={() => {
              handleToastNotificationClick(toastNotification);
              setToastNotification(null);
            }}
            className="w-full rounded-xl border border-primary/20 bg-white p-3 text-left shadow-xl"
          >
            <div className="flex items-center justify-between gap-2">
              <span className="text-xs font-bold uppercase tracking-wide text-primary">Nova mensagem</span>
              <span className="text-[10px] font-semibold text-primary/70">{toastNotification.timestamp}</span>
            </div>
            <p className="mt-1 text-sm font-bold text-[#101818]">{toastNotification.title}</p>
            <p className="mt-1 text-xs text-[#4a5e5e]">{toastNotification.description}</p>
          </button>
        </div>
      ) : null}

      <div className="min-h-screen bg-background-light dark:bg-background-dark max-w-md mx-auto shadow-2xl relative flex flex-col">
        {content}
      </div>
    </>
  );
};

export default App;
