import React, { useCallback, useEffect, useRef, useState } from 'react';
import { AppState, AuthSession, ChatConversationSummary, Notification, OrderProposalDetailsData, ProposalScheduleSlot, ServiceRequest, ServiceRequestCategoryOption, ServiceRequestDetailsData } from './types';
import { clearAuthSession, loadAuthSession, saveAuthSession } from './services/auth';
import {
  acceptMobileClientOrderProposal,
  fetchMobileClientOrderDetails,
  fetchMobileClientOrderProposalDetails,
  fetchMobileClientOrderProposalSlots,
  fetchMobileClientOrders,
  MobileOrdersError,
  scheduleMobileClientOrderProposal
} from './services/mobileOrders';
import { fetchMobileServiceRequestCategories, MobileServiceRequestError } from './services/mobileServiceRequests';
import {
  extractRequestIdFromActionUrl,
  RealtimeNotificationPayload,
  startRealtimeNotificationConnection,
  stopRealtimeNotificationConnection
} from './services/realtimeNotifications';
import {
  ClientPushPayload,
  initializeClientPushNotifications,
  teardownClientPushNotifications,
  unregisterClientPushNotifications
} from './services/pushNotifications';
import {
  startRealtimeChatConnection,
  stopRealtimeChatConnection,
  subscribeToRealtimeChatEvents
} from './services/realtimeChat';
import SplashScreen from './components/SplashScreen';
import Onboarding from './components/Onboarding';
import Auth from './components/Auth';
import Dashboard from './components/Dashboard';
import ServiceRequestFlow from './components/ServiceRequestFlow';
import RequestDetails from './components/RequestDetails';
import ProposalDetails from './components/ProposalDetails';
import ChatList from './components/ChatList';
import Chat from './components/Chat';
import CategoryList from './components/CategoryList';
import OrdersList from './components/OrdersList';
import Profile from './components/Profile';
import ServiceCompletionFlow from './components/ServiceCompletionFlow';
import Notifications from './components/Notifications';

function splitOrdersByFinalization(items: ServiceRequest[]): { openOrders: ServiceRequest[]; finalizedOrders: ServiceRequest[] } {
  const openOrders: ServiceRequest[] = [];
  const finalizedOrders: ServiceRequest[] = [];

  for (const item of items) {
    if (item.status === 'CONCLUIDO' || item.status === 'CANCELADO') {
      finalizedOrders.push(item);
    } else {
      openOrders.push(item);
    }
  }

  return { openOrders, finalizedOrders };
}

function normalizeRequestId(value?: string | null): string | null {
  const normalized = String(value || '').trim();
  return normalized ? normalized.toLowerCase() : null;
}

function normalizeEntityId(value?: string | null): string {
  return String(value || '').trim().toLowerCase();
}

function toDisplayDateTime(value?: string): string {
  if (!value) {
    return '';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '';
  }

  const dd = String(date.getDate()).padStart(2, '0');
  const mm = String(date.getMonth() + 1).padStart(2, '0');
  const hh = String(date.getHours()).padStart(2, '0');
  const min = String(date.getMinutes()).padStart(2, '0');
  return `${dd}/${mm} ${hh}:${min}`;
}

function getTomorrowDateInputValue(): string {
  const tomorrow = new Date();
  tomorrow.setDate(tomorrow.getDate() + 1);

  const yyyy = tomorrow.getFullYear();
  const mm = String(tomorrow.getMonth() + 1).padStart(2, '0');
  const dd = String(tomorrow.getDate()).padStart(2, '0');
  return `${yyyy}-${mm}-${dd}`;
}

function resolveNotificationType(subject: string, message: string): Notification['type'] {
  const normalized = `${subject} ${message}`.toLowerCase();
  if (normalized.includes('proposta') || normalized.includes('status')) {
    return 'STATUS';
  }

  if (normalized.includes('mensagem') || normalized.includes('chat')) {
    return 'MESSAGE';
  }

  if (normalized.includes('cupom') || normalized.includes('promo')) {
    return 'PROMO';
  }

  return 'SYSTEM';
}

function isProposalNotification(subject: string, message: string): boolean {
  const normalized = `${subject} ${message}`.toLowerCase();
  return normalized.includes('proposta');
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

function extractPushChatContext(actionUrl?: string): { requestId?: string; providerId?: string } {
  const normalized = String(actionUrl || '').trim();
  if (!normalized) {
    return {};
  }

  try {
    const parsed = new URL(normalized, window.location.origin);
    const requestId = parsed.searchParams.get('requestId') || undefined;
    const providerId = parsed.searchParams.get('providerId') || undefined;
    return { requestId, providerId };
  } catch {
    return {};
  }
}

const App: React.FC = () => {
  const [currentView, setCurrentView] = useState<AppState>('SPLASH');
  const [viewVisitToken, setViewVisitToken] = useState(0);
  const [authSession, setAuthSession] = useState<AuthSession | null>(null);
  const [selectedRequest, setSelectedRequest] = useState<ServiceRequest | null>(null);
  const [selectedRequestDetails, setSelectedRequestDetails] = useState<ServiceRequestDetailsData | null>(null);
  const [detailsLoading, setDetailsLoading] = useState(false);
  const [detailsError, setDetailsError] = useState('');
  const [selectedProposalId, setSelectedProposalId] = useState<string | null>(null);
  const [selectedProposalDetails, setSelectedProposalDetails] = useState<OrderProposalDetailsData | null>(null);
  const [proposalDetailsLoading, setProposalDetailsLoading] = useState(false);
  const [proposalDetailsError, setProposalDetailsError] = useState('');
  const [proposalAccepting, setProposalAccepting] = useState(false);
  const [proposalAcceptSuccess, setProposalAcceptSuccess] = useState('');
  const [proposalAcceptError, setProposalAcceptError] = useState('');
  const [proposalScheduleDate, setProposalScheduleDate] = useState(getTomorrowDateInputValue);
  const [proposalScheduleReason, setProposalScheduleReason] = useState('');
  const [proposalSlots, setProposalSlots] = useState<ProposalScheduleSlot[]>([]);
  const [proposalSlotsLoading, setProposalSlotsLoading] = useState(false);
  const [proposalSlotsError, setProposalSlotsError] = useState('');
  const [proposalSlotsSearched, setProposalSlotsSearched] = useState(false);
  const [proposalSchedulingSlotStartUtc, setProposalSchedulingSlotStartUtc] = useState<string | null>(null);
  const [proposalScheduleSuccess, setProposalScheduleSuccess] = useState('');
  const [proposalScheduleError, setProposalScheduleError] = useState('');
  const [selectedCategoryId, setSelectedCategoryId] = useState<string | null>(null);
  const [dashboardCategories, setDashboardCategories] = useState<ServiceRequestCategoryOption[]>([]);
  const [chatBackView, setChatBackView] = useState<AppState>('CHAT_LIST');

  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [toastNotification, setToastNotification] = useState<Notification | null>(null);
  const handleNotificationClickRef = useRef<(notification: Notification) => void>(() => {});

  const [requests, setRequests] = useState<ServiceRequest[]>([]);
  const [openOrders, setOpenOrders] = useState<ServiceRequest[]>([]);
  const [finalizedOrders, setFinalizedOrders] = useState<ServiceRequest[]>([]);
  const [ordersLoading, setOrdersLoading] = useState(false);
  const [ordersError, setOrdersError] = useState('');

  const syncOrdersState = useCallback((allOrders: ServiceRequest[]) => {
    const buckets = splitOrdersByFinalization(allOrders);
    setRequests(allOrders);
    setOpenOrders(buckets.openOrders);
    setFinalizedOrders(buckets.finalizedOrders);
  }, []);

  const upsertOrderInState = useCallback((updatedOrder: ServiceRequest) => {
    setRequests((previousOrders) => {
      let found = false;
      const mergedOrders = previousOrders.map((order) => {
        if (normalizeRequestId(order.id) !== normalizeRequestId(updatedOrder.id)) {
          return order;
        }

        found = true;
        return {
          ...order,
          ...updatedOrder,
          provider: updatedOrder.provider || order.provider
        };
      });

      const nextOrders = found ? mergedOrders : [updatedOrder, ...mergedOrders];
      const buckets = splitOrdersByFinalization(nextOrders);
      setOpenOrders(buckets.openOrders);
      setFinalizedOrders(buckets.finalizedOrders);
      return nextOrders;
    });
  }, []);

  const incrementProposalCountForRequest = useCallback((requestId: string) => {
    const normalizedTargetId = normalizeRequestId(requestId);
    if (!normalizedTargetId) {
      return;
    }

    setRequests((previousOrders) => {
      let changed = false;
      const updatedOrders = previousOrders.map((order) => {
        if (normalizeRequestId(order.id) !== normalizedTargetId) {
          return order;
        }

        changed = true;
        return {
          ...order,
          proposalCount: (order.proposalCount || 0) + 1
        };
      });

      if (!changed) {
        return previousOrders;
      }

      const buckets = splitOrdersByFinalization(updatedOrders);
      setOpenOrders(buckets.openOrders);
      setFinalizedOrders(buckets.finalizedOrders);

      setSelectedRequest((currentSelectedRequest) => {
        if (!currentSelectedRequest || normalizeRequestId(currentSelectedRequest.id) !== normalizedTargetId) {
          return currentSelectedRequest;
        }

        const updatedSelected = updatedOrders.find((item) => normalizeRequestId(item.id) === normalizedTargetId);
        return updatedSelected || currentSelectedRequest;
      });

      setSelectedRequestDetails((currentDetails) => {
        if (!currentDetails || normalizeRequestId(currentDetails.order.id) !== normalizedTargetId) {
          return currentDetails;
        }

        const updatedSelected = updatedOrders.find((item) => normalizeRequestId(item.id) === normalizedTargetId);
        if (!updatedSelected) {
          return currentDetails;
        }

        return {
          ...currentDetails,
          order: updatedSelected
        };
      });

      return updatedOrders;
    });
  }, []);

  const handleRealtimeNotificationReceived = useCallback((payload: RealtimeNotificationPayload) => {
    const subject = (payload.subject || 'Nova notificacao').trim() || 'Nova notificacao';
    const message = (payload.message || 'Voce recebeu uma atualizacao.').trim() || 'Voce recebeu uma atualizacao.';
    const requestId = extractRequestIdFromActionUrl(payload.actionUrl);

    const realtimeNotification: Notification = {
      id: `rt-${Date.now()}-${Math.random().toString(16).slice(2)}`,
      type: resolveNotificationType(subject, message),
      title: subject,
      description: message,
      timestamp: formatNotificationTimestamp(payload.timestamp),
      read: false,
      requestId: requestId || undefined
    };

    setNotifications((previousNotifications) => [realtimeNotification, ...previousNotifications].slice(0, 200));
    setToastNotification(realtimeNotification);

    if (requestId && isProposalNotification(subject, message)) {
      incrementProposalCountForRequest(requestId);
    }
  }, [incrementProposalCountForRequest]);

  const handleRealtimeChatMessageReceived = useCallback((payload: {
    id: string;
    requestId: string;
    providerId: string;
    senderId: string;
    senderName: string;
    text?: string;
    createdAt: string;
    attachments: Array<unknown>;
  }) => {
    if (!authSession?.userId) {
      return;
    }

    if (normalizeEntityId(payload.senderId) === normalizeEntityId(authSession.userId)) {
      return;
    }

    if (currentView === 'CHAT') {
      return;
    }

    const notification: Notification = {
      id: `rt-chat-${payload.id}-${Date.now()}`,
      type: 'MESSAGE',
      title: `Nova mensagem de ${payload.senderName || 'Prestador'}`,
      description: buildChatNotificationDescription(payload.text, payload.attachments?.length || 0),
      timestamp: formatNotificationTimestamp(payload.createdAt),
      read: false,
      requestId: payload.requestId,
      providerId: payload.providerId,
      providerName: payload.senderName || 'Prestador'
    };

    setNotifications((previous) => [notification, ...previous].slice(0, 200));
    setToastNotification(notification);
  }, [authSession?.userId, currentView]);

  const loadClientOrders = useCallback(async (session: AuthSession) => {
    setOrdersLoading(true);
    setOrdersError('');

    try {
      const result = await fetchMobileClientOrders(session.token);
      syncOrdersState([...result.openOrders, ...result.finalizedOrders]);
    } catch (error) {
      if (error instanceof MobileOrdersError && (error.code === 'CPM-ORDERS-401' || error.code === 'CPM-ORDERS-403')) {
        clearAuthSession();
        setAuthSession(null);
        setCurrentView('AUTH');
        setOrdersError(error.message);
        syncOrdersState([]);
        return;
      }

      setOrdersError('Nao foi possivel carregar seus pedidos agora.');
      syncOrdersState([]);
    } finally {
      setOrdersLoading(false);
    }
  }, [syncOrdersState]);

  const loadDashboardCategories = useCallback(async (session: AuthSession) => {
    try {
      const categories = await fetchMobileServiceRequestCategories(session.token);
      setDashboardCategories(categories);
    } catch (error) {
      if (error instanceof MobileServiceRequestError && (error.code === 'CPM-REQ-401' || error.code === 'CPM-REQ-403')) {
        clearAuthSession();
        setAuthSession(null);
        setCurrentView('AUTH');
        setDashboardCategories([]);
        return;
      }

      setDashboardCategories([]);
    }
  }, []);

  useEffect(() => {
    const existingSession = loadAuthSession();
    if (existingSession) {
      setAuthSession(existingSession);
      setCurrentView('DASHBOARD');
      return;
    }

    setCurrentView('AUTH');
  }, []);

  useEffect(() => {
    setViewVisitToken((previous) => previous + 1);
  }, [currentView]);

  useEffect(() => {
    if (!toastNotification) {
      return undefined;
    }

    const timer = window.setTimeout(() => setToastNotification(null), 6000);
    return () => window.clearTimeout(timer);
  }, [toastNotification]);

  useEffect(() => {
    if (!authSession?.token) {
      void stopRealtimeNotificationConnection();
      return undefined;
    }

    let disposed = false;

    const bootstrapRealtimeNotifications = async () => {
      try {
        await startRealtimeNotificationConnection(authSession.token, (payload) => {
          if (disposed) {
            return;
          }

          handleRealtimeNotificationReceived(payload);
        });
      } catch {
        // silent fallback; app keeps polling endpoints manually where applicable
      }
    };

    void bootstrapRealtimeNotifications();

    return () => {
      disposed = true;
      void stopRealtimeNotificationConnection();
    };
  }, [authSession?.token, handleRealtimeNotificationReceived]);

  useEffect(() => {
    if (!authSession?.token) {
      void stopRealtimeChatConnection();
      return undefined;
    }

    let disposed = false;
    const unsubscribe = subscribeToRealtimeChatEvents({
      onChatMessage: (message) => {
        if (disposed) {
          return;
        }

        handleRealtimeChatMessageReceived({
          id: message.id,
          requestId: message.requestId,
          providerId: message.providerId,
          senderId: message.senderId,
          senderName: message.senderName,
          text: message.text,
          createdAt: message.createdAt,
          attachments: message.attachments
        });
      }
    });

    const bootstrapRealtimeChat = async () => {
      try {
        await startRealtimeChatConnection(authSession.token);
      } catch {
        // silent fallback: chat screen will retry when user opens chat
      }
    };

    void bootstrapRealtimeChat();

    return () => {
      disposed = true;
      unsubscribe();
    };
  }, [authSession?.token, handleRealtimeChatMessageReceived]);

  const handleLoginSuccess = (session: AuthSession) => {
    saveAuthSession(session);
    setAuthSession(session);
    setCurrentView('DASHBOARD');
  };

  const handleLogout = () => {
    const currentToken = authSession?.token || '';
    clearAuthSession();
    void stopRealtimeChatConnection();
    if (currentToken) {
      void unregisterClientPushNotifications(currentToken);
    } else {
      void teardownClientPushNotifications();
    }
    setAuthSession(null);
    setSelectedRequest(null);
    setSelectedRequestDetails(null);
    setSelectedProposalId(null);
    setSelectedProposalDetails(null);
    setSelectedCategoryId(null);
    setDashboardCategories([]);
    setNotifications([]);
    setToastNotification(null);
    setOrdersError('');
    setDetailsError('');
    setProposalDetailsError('');
    setProposalAcceptSuccess('');
    setProposalAcceptError('');
    setProposalScheduleDate(getTomorrowDateInputValue());
    setProposalScheduleReason('');
    setProposalSlots([]);
    setProposalSlotsLoading(false);
    setProposalSlotsError('');
    setProposalSlotsSearched(false);
    setProposalSchedulingSlotStartUtc(null);
    setProposalScheduleSuccess('');
    setProposalScheduleError('');
    setChatBackView('CHAT_LIST');
    syncOrdersState([]);
    setCurrentView('AUTH');
  };

  const loadRequestDetails = useCallback(async (requestId: string) => {
    if (!authSession) {
      setDetailsError('Sessao invalida para carregar os detalhes do pedido.');
      return;
    }

    setDetailsLoading(true);
    setDetailsError('');
    setSelectedRequestDetails(null);

    try {
      const details = await fetchMobileClientOrderDetails(authSession.token, requestId);
      setSelectedRequestDetails(details);
      setSelectedRequest(details.order);
    } catch (error) {
      if (error instanceof MobileOrdersError && (error.code === 'CPM-ORDERS-401' || error.code === 'CPM-ORDERS-403')) {
        clearAuthSession();
        setAuthSession(null);
        setCurrentView('AUTH');
        return;
      }

      setDetailsError('Nao foi possivel carregar o historico deste pedido.');
    } finally {
      setDetailsLoading(false);
    }
  }, [authSession]);

  const loadProposalDetails = useCallback(async (orderId: string, proposalId: string) => {
    if (!authSession) {
      setProposalDetailsError('Sessao invalida para carregar os detalhes da proposta.');
      return;
    }

    setProposalDetailsLoading(true);
    setProposalDetailsError('');
    setProposalAcceptSuccess('');
    setProposalAcceptError('');
    setProposalScheduleSuccess('');
    setProposalScheduleError('');
    setProposalSlots([]);
    setProposalSlotsError('');
    setProposalSlotsSearched(false);
    setProposalSchedulingSlotStartUtc(null);
    setSelectedProposalDetails(null);

    try {
      const details = await fetchMobileClientOrderProposalDetails(authSession.token, orderId, proposalId);
      setSelectedProposalDetails(details);
      setSelectedRequest(details.order);
    } catch (error) {
      if (error instanceof MobileOrdersError && (error.code === 'CPM-ORDERS-401' || error.code === 'CPM-ORDERS-403')) {
        clearAuthSession();
        setAuthSession(null);
        setCurrentView('AUTH');
        return;
      }

      setProposalDetailsError('Nao foi possivel carregar os detalhes desta proposta.');
    } finally {
      setProposalDetailsLoading(false);
    }
  }, [authSession]);

  const handleViewDetails = (request: ServiceRequest) => {
    setSelectedRequest(request);
    setSelectedRequestDetails(null);
    setSelectedProposalId(null);
    setSelectedProposalDetails(null);
    setProposalDetailsError('');
    setProposalAcceptSuccess('');
    setProposalAcceptError('');
    setProposalScheduleDate(getTomorrowDateInputValue());
    setProposalScheduleReason('');
    setProposalSlots([]);
    setProposalSlotsLoading(false);
    setProposalSlotsError('');
    setProposalSlotsSearched(false);
    setProposalSchedulingSlotStartUtc(null);
    setProposalScheduleSuccess('');
    setProposalScheduleError('');
    setDetailsError('');
    setCurrentView('REQUEST_DETAILS');
  };

  const handleOpenProposalDetails = (proposalId: string) => {
    if (!selectedRequest) {
      return;
    }

    setSelectedProposalId(proposalId);
    setProposalAcceptSuccess('');
    setProposalAcceptError('');
    setProposalScheduleDate(getTomorrowDateInputValue());
    setProposalScheduleReason('');
    setProposalSlots([]);
    setProposalSlotsLoading(false);
    setProposalSlotsError('');
    setProposalSlotsSearched(false);
    setProposalSchedulingSlotStartUtc(null);
    setProposalScheduleSuccess('');
    setProposalScheduleError('');
    setCurrentView('PROPOSAL_DETAILS');
  };

  const handleOpenProposalChat = useCallback(() => {
    if (!selectedProposalDetails) {
      return;
    }

    const proposalProvider = selectedProposalDetails.proposal;
    const requestForChat: ServiceRequest = {
      ...(selectedRequest || selectedProposalDetails.order),
      provider: {
        id: proposalProvider.providerId,
        name: proposalProvider.providerName,
        avatar: `https://i.pravatar.cc/120?u=${proposalProvider.providerId}`,
        rating: selectedRequest?.provider?.rating || 5,
        specialty: selectedProposalDetails.order.category
      }
    };

    setSelectedRequest(requestForChat);
    setChatBackView('PROPOSAL_DETAILS');
    setCurrentView('CHAT');
  }, [selectedProposalDetails, selectedRequest]);

  const handleAcceptSelectedProposal = useCallback(async () => {
    if (!authSession || !selectedRequest || !selectedProposalId) {
      return;
    }

    setProposalAccepting(true);
    setProposalAcceptError('');
    setProposalAcceptSuccess('');

    try {
      const result = await acceptMobileClientOrderProposal(authSession.token, selectedRequest.id, selectedProposalId);
      const proposalProvider = result.details.proposal;
      const updatedOrder: ServiceRequest = {
        ...result.details.order,
        provider: {
          id: proposalProvider.providerId,
          name: proposalProvider.providerName,
          avatar: `https://i.pravatar.cc/120?u=${proposalProvider.providerId}`,
          rating: selectedRequest.provider?.rating || 5,
          specialty: result.details.order.category
        }
      };

      setSelectedProposalDetails(result.details);
      setSelectedRequest(updatedOrder);
      upsertOrderInState(updatedOrder);
      setSelectedRequestDetails((previousDetails) => previousDetails
        ? {
            ...previousDetails,
            order: updatedOrder
          }
        : previousDetails);
      setProposalAcceptSuccess(result.message || 'Proposta aceita com sucesso.');

      void loadRequestDetails(result.details.order.id);
    } catch (error) {
      if (error instanceof MobileOrdersError && (error.code === 'CPM-ORDERS-401' || error.code === 'CPM-ORDERS-403')) {
        clearAuthSession();
        setAuthSession(null);
        setCurrentView('AUTH');
        return;
      }

      setProposalAcceptError('Nao foi possivel aceitar esta proposta agora.');
    } finally {
      setProposalAccepting(false);
    }
  }, [authSession, loadRequestDetails, selectedProposalId, selectedRequest, upsertOrderInState]);

  const handleLoadProposalSlots = useCallback(async () => {
    if (!authSession || !selectedRequest || !selectedProposalId) {
      return;
    }

    if (selectedProposalDetails?.currentAppointment) {
      setProposalSlots([]);
      setProposalSlotsSearched(false);
      setProposalSlotsError('');
      setProposalScheduleError('Ja existe um agendamento solicitado para esta proposta. Aguarde a confirmacao do prestador.');
      return;
    }

    if (!proposalScheduleDate) {
      setProposalSlots([]);
      setProposalSlotsSearched(true);
      setProposalSlotsError('Selecione uma data para consultar os horarios disponiveis.');
      return;
    }

    setProposalSlotsLoading(true);
    setProposalSlotsError('');
    setProposalSlotsSearched(true);
    setProposalScheduleSuccess('');
    setProposalScheduleError('');

    try {
      const slots = await fetchMobileClientOrderProposalSlots(
        authSession.token,
        selectedRequest.id,
        selectedProposalId,
        proposalScheduleDate);

      setProposalSlots(slots);
    } catch (error) {
      if (error instanceof MobileOrdersError && (error.code === 'CPM-ORDERS-401' || error.code === 'CPM-ORDERS-403')) {
        clearAuthSession();
        setAuthSession(null);
        setCurrentView('AUTH');
        return;
      }

      setProposalSlots([]);
      setProposalSlotsError('Nao foi possivel buscar horarios disponiveis agora.');
    } finally {
      setProposalSlotsLoading(false);
    }
  }, [authSession, proposalScheduleDate, selectedProposalDetails?.currentAppointment, selectedProposalId, selectedRequest]);

  const handleScheduleSelectedProposalSlot = useCallback(async (slot: ProposalScheduleSlot) => {
    if (!authSession || !selectedRequest || !selectedProposalId) {
      return;
    }

    if (selectedProposalDetails?.currentAppointment) {
      setProposalScheduleError('Ja existe um agendamento solicitado para esta proposta. Aguarde a confirmacao do prestador.');
      return;
    }

    setProposalSchedulingSlotStartUtc(slot.windowStartUtc);
    setProposalScheduleError('');
    setProposalScheduleSuccess('');

    try {
      const result = await scheduleMobileClientOrderProposal(
        authSession.token,
        selectedRequest.id,
        selectedProposalId,
        {
          windowStartUtc: slot.windowStartUtc,
          windowEndUtc: slot.windowEndUtc,
          reason: proposalScheduleReason || undefined
        });

      const proposalProvider = result.details.proposal;
      const updatedOrder: ServiceRequest = {
        ...result.details.order,
        provider: {
          id: proposalProvider.providerId,
          name: proposalProvider.providerName,
          avatar: selectedRequest.provider?.avatar || `https://i.pravatar.cc/120?u=${proposalProvider.providerId}`,
          rating: selectedRequest.provider?.rating || 5,
          specialty: result.details.order.category
        }
      };

      setSelectedProposalDetails(result.details);
      setSelectedRequest(updatedOrder);
      upsertOrderInState(updatedOrder);
      setProposalScheduleSuccess(result.message || 'Agendamento solicitado com sucesso. Aguarde confirmacao do prestador.');
      setProposalSlots([]);
      setProposalSlotsSearched(false);

      void loadRequestDetails(updatedOrder.id);
    } catch (error) {
      if (error instanceof MobileOrdersError && (error.code === 'CPM-ORDERS-401' || error.code === 'CPM-ORDERS-403')) {
        clearAuthSession();
        setAuthSession(null);
        setCurrentView('AUTH');
        return;
      }

      setProposalScheduleError('Nao foi possivel solicitar o agendamento nesse horario.');
    } finally {
      setProposalSchedulingSlotStartUtc(null);
    }
  }, [
    authSession,
    loadRequestDetails,
    proposalScheduleReason,
    selectedProposalDetails?.currentAppointment,
    selectedProposalId,
    selectedRequest,
    upsertOrderInState
  ]);

  const handleOpenChat = (request: ServiceRequest, returnView: AppState = 'CHAT_LIST') => {
    setSelectedRequest(request);
    setChatBackView(returnView);
    setCurrentView('CHAT');
  };

  const buildRequestForChat = useCallback((params: {
    requestId: string;
    providerId: string;
    providerName?: string;
    title?: string;
    preview?: string;
    lastMessageAt?: string;
  }): ServiceRequest => {
    const requestFromState = requests.find((item) =>
      normalizeRequestId(item.id) === normalizeRequestId(params.requestId));

    return {
      ...(requestFromState || {
        id: params.requestId,
        title: params.title || 'Conversa',
        status: 'AGUARDANDO',
        date: toDisplayDateTime(params.lastMessageAt),
        category: 'Servico',
        icon: 'chat',
        description: params.preview
      }),
      provider: {
        id: params.providerId,
        name: params.providerName || requestFromState?.provider?.name || 'Prestador',
        avatar: requestFromState?.provider?.avatar || `https://i.pravatar.cc/120?u=${params.providerId}`,
        rating: requestFromState?.provider?.rating || 5,
        specialty: requestFromState?.provider?.specialty || requestFromState?.category || 'Prestador'
      }
    };
  }, [requests]);

  const handleOpenChatFromConversation = useCallback((conversation: ChatConversationSummary) => {
    const requestForChat = buildRequestForChat({
      requestId: conversation.requestId,
      providerId: conversation.providerId,
      providerName: conversation.counterpartName,
      title: conversation.title,
      preview: conversation.lastMessagePreview,
      lastMessageAt: conversation.lastMessageAt
    });

    handleOpenChat(requestForChat, 'CHAT_LIST');
  }, [buildRequestForChat]);

  const handleAddNewRequest = (newRequest: ServiceRequest) => {
    const updated = [newRequest, ...requests];
    syncOrdersState(updated);
    setSelectedCategoryId(null);
  };

  const handleCompleteService = (requestId: string, rating: number, review: string, paymentMethod: string, amount?: string) => {
    const updated = requests.map((req) =>
      req.id === requestId
        ? { ...req, status: 'CONCLUIDO', rating, review, paymentMethod, paidAmount: amount }
        : req);

    syncOrdersState(updated);
    setCurrentView('DASHBOARD');
  };

  const buildNotificationFromPushPayload = useCallback((payload: ClientPushPayload): Notification => {
    const pushChatContext = extractPushChatContext(payload.actionUrl);
    const requestId = normalizeRequestId(payload.requestId)
      || normalizeRequestId(pushChatContext.requestId)
      || extractRequestIdFromActionUrl(payload.actionUrl)
      || undefined;
    const providerId = normalizeEntityId(payload.providerId)
      || normalizeEntityId(pushChatContext.providerId)
      || undefined;
    const notificationType = String(payload.notificationType || '').trim().toLowerCase();

    return {
      id: `push-${Date.now()}-${Math.random().toString(16).slice(2)}`,
      type: notificationType.includes('chat')
        ? 'MESSAGE'
        : resolveNotificationType(payload.title, payload.body),
      title: payload.title,
      description: payload.body,
      timestamp: formatNotificationTimestamp(),
      read: false,
      requestId,
      providerId,
      providerName: payload.providerName
    };
  }, []);

  const handleNotificationClick = (notification: Notification) => {
    setNotifications((prev) => prev.map((n) => (n.id === notification.id ? { ...n, read: true } : n)));

    const normalizedRequestId = normalizeRequestId(notification.requestId);
    if (!normalizedRequestId) {
      return;
    }

    if (notification.type === 'MESSAGE') {
      const requestFromState = requests.find((item) => normalizeRequestId(item.id) === normalizedRequestId);
      const providerId = notification.providerId || requestFromState?.provider?.id;
      if (providerId) {
        const chatRequest = buildRequestForChat({
          requestId: notification.requestId!,
          providerId,
          providerName: notification.providerName || requestFromState?.provider?.name,
          title: requestFromState?.title || 'Conversa',
          preview: notification.description
        });

        handleOpenChat(chatRequest, currentView);
        return;
      }
    }

    const request = requests.find((item) => normalizeRequestId(item.id) === normalizedRequestId);
    if (request) {
      handleViewDetails(request);
    }
  };

  useEffect(() => {
    handleNotificationClickRef.current = handleNotificationClick;
  }, [handleNotificationClick]);

  useEffect(() => {
    if (!authSession?.token) {
      void teardownClientPushNotifications();
      return undefined;
    }

    let disposed = false;
    const accessToken = authSession.token;

    const initializePush = async () => {
      try {
        await initializeClientPushNotifications(accessToken, {
          onForegroundNotification: (payload) => {
            if (disposed) {
              return;
            }

            const notification = buildNotificationFromPushPayload(payload);
            setNotifications((previous) => [notification, ...previous].slice(0, 200));
            setToastNotification(notification);

            if (notification.requestId && isProposalNotification(notification.title, notification.description)) {
              incrementProposalCountForRequest(notification.requestId);
            }
          },
          onNotificationAction: (payload) => {
            if (disposed) {
              return;
            }

            const notification = buildNotificationFromPushPayload(payload);
            setNotifications((previous) => [notification, ...previous].slice(0, 200));
            window.setTimeout(() => {
              handleNotificationClickRef.current(notification);
            }, 0);
          },
          onError: (message) => {
            if (disposed) {
              return;
            }

            const notification: Notification = {
              id: `push-error-${Date.now()}-${Math.random().toString(16).slice(2)}`,
              type: 'SYSTEM',
              title: 'Notificacoes push indisponiveis',
              description: message,
              timestamp: formatNotificationTimestamp(),
              read: false
            };

            setNotifications((previous) => [notification, ...previous].slice(0, 200));
          }
        });
      } catch {
        // fallback silencioso: notificacoes realtime continuam disponiveis no app aberto
      }
    };

    void initializePush();

    return () => {
      disposed = true;
      void teardownClientPushNotifications();
    };
  }, [authSession?.token, buildNotificationFromPushPayload, incrementProposalCountForRequest]);

  useEffect(() => {
    if (!authSession) {
      return;
    }

    if (currentView === 'DASHBOARD') {
      void loadClientOrders(authSession);
      void loadDashboardCategories(authSession);
      return;
    }

    if (currentView === 'ORDERS') {
      void loadClientOrders(authSession);
      return;
    }

    if (currentView === 'REQUEST_DETAILS' && selectedRequest?.id) {
      void loadRequestDetails(selectedRequest.id);
      return;
    }

    if (currentView === 'PROPOSAL_DETAILS' && selectedRequest?.id && selectedProposalId) {
      void loadProposalDetails(selectedRequest.id, selectedProposalId);
    }
  }, [
    authSession,
    currentView,
    loadClientOrders,
    loadDashboardCategories,
    loadProposalDetails,
    loadRequestDetails,
    selectedProposalId,
    selectedRequest?.id,
    viewVisitToken
  ]);

  const renderView = () => {
    switch (currentView) {
      case 'SPLASH':
        return <SplashScreen />;
      case 'ONBOARDING':
        return <Onboarding onFinish={() => setCurrentView('AUTH')} />;
      case 'AUTH':
        return <Auth onLogin={handleLoginSuccess} onBack={() => setCurrentView('ONBOARDING')} />;
      case 'DASHBOARD':
        return (
          <Dashboard
            requests={requests}
            categories={dashboardCategories}
            unreadNotificationsCount={notifications.filter((n) => !n.read).length}
            onNewRequest={() => {
              setSelectedCategoryId(null);
              setCurrentView('NEW_REQUEST');
            }}
            onShowDetails={handleViewDetails}
            onOpenChatList={() => setCurrentView('CHAT_LIST')}
            onViewAllCategories={() => setCurrentView('CATEGORIES')}
            onSelectCategory={(categoryId) => {
              setSelectedCategoryId(categoryId);
              setCurrentView('NEW_REQUEST');
            }}
            onViewOrders={() => setCurrentView('ORDERS')}
            onViewProfile={() => setCurrentView('PROFILE')}
            onViewNotifications={() => setCurrentView('NOTIFICATIONS')}
          />
        );
      case 'NOTIFICATIONS':
        return (
          <Notifications
            notifications={notifications}
            onBack={() => setCurrentView('DASHBOARD')}
            onNotificationClick={handleNotificationClick}
            onClearAll={() => setNotifications([])}
          />
        );
      case 'ORDERS':
        return (
          <OrdersList
            openOrders={openOrders}
            finalizedOrders={finalizedOrders}
            isLoading={ordersLoading}
            errorMessage={ordersError}
            onRetry={() => {
              if (authSession) {
                void loadClientOrders(authSession);
              }
            }}
            onBack={() => setCurrentView('DASHBOARD')}
            onShowDetails={handleViewDetails}
            onGoToHome={() => setCurrentView('DASHBOARD')}
            onGoToChat={() => setCurrentView('CHAT_LIST')}
            onViewProfile={() => setCurrentView('PROFILE')}
          />
        );
      case 'PROFILE':
        return (
          <Profile
            userName={authSession?.userName}
            userEmail={authSession?.email}
            onBack={() => setCurrentView('DASHBOARD')}
            onLogout={handleLogout}
            onGoToHome={() => setCurrentView('DASHBOARD')}
            onGoToOrders={() => setCurrentView('ORDERS')}
            onGoToChat={() => setCurrentView('CHAT_LIST')}
          />
        );
      case 'NEW_REQUEST':
        return (
          <ServiceRequestFlow
            authSession={authSession}
            categoryId={selectedCategoryId}
            onCancel={() => setCurrentView('DASHBOARD')}
            onFinish={(newReq) => {
              if (newReq) {
                handleAddNewRequest(newReq);
              }
              setCurrentView('DASHBOARD');
            }}
          />
        );
      case 'CATEGORIES':
        return (
          <CategoryList
            authSession={authSession}
            onBack={() => setCurrentView('DASHBOARD')}
            onSelectCategory={(id) => {
              setSelectedCategoryId(id);
              setCurrentView('NEW_REQUEST');
            }}
          />
        );
      case 'REQUEST_DETAILS':
        return selectedRequest ? (
          <RequestDetails
            request={selectedRequestDetails?.order || selectedRequest}
            details={selectedRequestDetails}
            isLoadingDetails={detailsLoading}
            detailsError={detailsError}
            onRetryDetails={() => {
              if (selectedRequest?.id) {
                void loadRequestDetails(selectedRequest.id);
              }
            }}
            onBack={() => setCurrentView('DASHBOARD')}
            onOpenChat={() => handleOpenChat(selectedRequest, 'REQUEST_DETAILS')}
            onOpenProposalDetails={handleOpenProposalDetails}
            onFinishService={() => setCurrentView('FINISH_SERVICE')}
          />
        ) : null;
      case 'PROPOSAL_DETAILS':
        return (
          <ProposalDetails
            details={selectedProposalDetails}
            isLoading={proposalDetailsLoading}
            errorMessage={proposalDetailsError}
            onOpenChatWithProvider={handleOpenProposalChat}
            onAcceptProposal={handleAcceptSelectedProposal}
            isAcceptingProposal={proposalAccepting}
            acceptSuccessMessage={proposalAcceptSuccess}
            acceptErrorMessage={proposalAcceptError}
            scheduleDate={proposalScheduleDate}
            scheduleReason={proposalScheduleReason}
            onScheduleDateChange={setProposalScheduleDate}
            onScheduleReasonChange={setProposalScheduleReason}
            onLoadScheduleSlots={handleLoadProposalSlots}
            availableSlots={proposalSlots}
            hasSearchedSlots={proposalSlotsSearched}
            isLoadingSlots={proposalSlotsLoading}
            slotsErrorMessage={proposalSlotsError}
            onScheduleSlot={handleScheduleSelectedProposalSlot}
            schedulingSlotStartUtc={proposalSchedulingSlotStartUtc}
            scheduleSuccessMessage={proposalScheduleSuccess}
            scheduleErrorMessage={proposalScheduleError}
            onRetry={() => {
              if (selectedRequest?.id && selectedProposalId) {
                void loadProposalDetails(selectedRequest.id, selectedProposalId);
              }
            }}
            onBack={() => setCurrentView('REQUEST_DETAILS')}
          />
        );
      case 'FINISH_SERVICE':
        return selectedRequest ? (
          <ServiceCompletionFlow
            request={selectedRequest}
            onCancel={() => setCurrentView('REQUEST_DETAILS')}
            onFinish={handleCompleteService}
          />
        ) : null;
      case 'CHAT_LIST':
        return (
          <ChatList
            authSession={authSession}
            onBack={() => setCurrentView('DASHBOARD')}
            onSelectChat={handleOpenChatFromConversation}
            onGoToHome={() => setCurrentView('DASHBOARD')}
            onGoToOrders={() => setCurrentView('ORDERS')}
            onGoToProfile={() => setCurrentView('PROFILE')}
          />
        );
      case 'CHAT':
        return selectedRequest ? (
          <Chat
            request={selectedRequest}
            authSession={authSession}
            onBack={() => setCurrentView(chatBackView)}
          />
        ) : null;
      default:
        return <SplashScreen />;
    }
  };

  return (
    <>
      {toastNotification ? (
        <div className="fixed left-1/2 top-4 z-[120] w-[calc(100%-1.5rem)] max-w-md -translate-x-1/2">
          <button
            type="button"
            onClick={() => {
              handleNotificationClick(toastNotification);
              setToastNotification(null);
            }}
            className="w-full rounded-xl border border-primary/20 bg-white p-3 text-left shadow-xl"
          >
            <div className="flex items-center justify-between gap-2">
              <span className="text-xs font-bold uppercase tracking-wide text-primary">Nova notificacao</span>
              <span className="text-[10px] font-semibold text-primary/70">{toastNotification.timestamp}</span>
            </div>
            <p className="mt-1 text-sm font-bold text-[#101818]">{toastNotification.title}</p>
            <p className="mt-1 text-xs text-[#4a5e5e]">{toastNotification.description}</p>
          </button>
        </div>
      ) : null}

      <div className="min-h-screen bg-background-light dark:bg-background-dark max-w-md mx-auto shadow-2xl relative flex flex-col">
        {renderView()}
      </div>
    </>
  );
};

export default App;
