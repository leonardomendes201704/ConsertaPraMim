import React, { useCallback, useEffect, useState } from 'react';
import { AppState, AuthSession, Notification, ServiceRequest } from './types';
import { clearAuthSession, loadAuthSession, saveAuthSession } from './services/auth';
import { fetchMobileClientOrders, MobileOrdersError } from './services/mobileOrders';
import SplashScreen from './components/SplashScreen';
import Onboarding from './components/Onboarding';
import Auth from './components/Auth';
import Dashboard from './components/Dashboard';
import ServiceRequestFlow from './components/ServiceRequestFlow';
import RequestDetails from './components/RequestDetails';
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

const App: React.FC = () => {
  const [currentView, setCurrentView] = useState<AppState>('SPLASH');
  const [authSession, setAuthSession] = useState<AuthSession | null>(null);
  const [selectedRequest, setSelectedRequest] = useState<ServiceRequest | null>(null);
  const [selectedCategoryId, setSelectedCategoryId] = useState<string | null>(null);

  const [notifications, setNotifications] = useState<Notification[]>([
    {
      id: '1',
      type: 'STATUS',
      title: 'Profissional a caminho!',
      description: 'Ricardo Silva iniciou o deslocamento para o seu endereco.',
      timestamp: 'Agora',
      read: false,
      requestId: '8429'
    },
    {
      id: '2',
      type: 'MESSAGE',
      title: 'Nova mensagem de Marcos',
      description: 'Pode me enviar uma foto do vazamento por favor?',
      timestamp: '15 min atras',
      read: false,
      requestId: '9012'
    },
    {
      id: '3',
      type: 'PROMO',
      title: 'Cupom de 10% OFF',
      description: 'Use o codigo CONSERTA10 no seu proximo pedido de pintura.',
      timestamp: '2 horas atras',
      read: true
    }
  ]);

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

  useEffect(() => {
    const existingSession = loadAuthSession();
    if (existingSession) {
      setAuthSession(existingSession);
      setCurrentView('DASHBOARD');
      void loadClientOrders(existingSession);
    }
  }, [loadClientOrders]);

  useEffect(() => {
    if (currentView === 'SPLASH' && !authSession) {
      const timer = setTimeout(() => setCurrentView('ONBOARDING'), 2500);
      return () => clearTimeout(timer);
    }
    return undefined;
  }, [currentView, authSession]);

  const handleLoginSuccess = (session: AuthSession) => {
    saveAuthSession(session);
    setAuthSession(session);
    setCurrentView('DASHBOARD');
    void loadClientOrders(session);
  };

  const handleLogout = () => {
    clearAuthSession();
    setAuthSession(null);
    setSelectedRequest(null);
    setSelectedCategoryId(null);
    setOrdersError('');
    syncOrdersState([]);
    setCurrentView('AUTH');
  };

  const handleViewDetails = (request: ServiceRequest) => {
    setSelectedRequest(request);
    setCurrentView('REQUEST_DETAILS');
  };

  const handleOpenChat = (request: ServiceRequest) => {
    setSelectedRequest(request);
    setCurrentView('CHAT');
  };

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

  const handleNotificationClick = (notification: Notification) => {
    setNotifications((prev) => prev.map((n) => (n.id === notification.id ? { ...n, read: true } : n)));
    if (notification.requestId) {
      const req = requests.find((r) => r.id === notification.requestId);
      if (req) {
        handleViewDetails(req);
      }
    }
  };

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
            unreadNotificationsCount={notifications.filter((n) => !n.read).length}
            onNewRequest={() => {
              setSelectedCategoryId(null);
              setCurrentView('NEW_REQUEST');
            }}
            onShowDetails={handleViewDetails}
            onOpenChatList={() => setCurrentView('CHAT_LIST')}
            onViewAllCategories={() => setCurrentView('CATEGORIES')}
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
            request={selectedRequest}
            onBack={() => setCurrentView('DASHBOARD')}
            onOpenChat={() => handleOpenChat(selectedRequest)}
            onFinishService={() => setCurrentView('FINISH_SERVICE')}
          />
        ) : null;
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
            onBack={() => setCurrentView('DASHBOARD')}
            onSelectChat={handleOpenChat}
            onGoToHome={() => setCurrentView('DASHBOARD')}
            onGoToOrders={() => setCurrentView('ORDERS')}
            onGoToProfile={() => setCurrentView('PROFILE')}
            chats={requests.filter((r) => r.status !== 'CANCELADO')}
          />
        );
      case 'CHAT':
        return selectedRequest ? <Chat request={selectedRequest} onBack={() => setCurrentView('CHAT_LIST')} /> : null;
      default:
        return <SplashScreen />;
    }
  };

  return <div className="min-h-screen bg-background-light dark:bg-background-dark max-w-md mx-auto shadow-2xl relative flex flex-col">{renderView()}</div>;
};

export default App;
