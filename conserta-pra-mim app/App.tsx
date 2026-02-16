
import React, { useState, useEffect } from 'react';
import { AppState, ServiceRequest, Notification } from './types';
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

const App: React.FC = () => {
  const [currentView, setCurrentView] = useState<AppState>('SPLASH');
  const [selectedRequest, setSelectedRequest] = useState<ServiceRequest | null>(null);
  const [selectedCategoryId, setSelectedCategoryId] = useState<string | null>(null);
  
  const [notifications, setNotifications] = useState<Notification[]>([
    {
      id: '1',
      type: 'STATUS',
      title: 'Profissional a caminho!',
      description: 'Ricardo Silva iniciou o deslocamento para o seu endereço.',
      timestamp: 'Agora',
      read: false,
      requestId: '8429'
    },
    {
      id: '2',
      type: 'MESSAGE',
      title: 'Nova mensagem de Marcos',
      description: 'Pode me enviar uma foto do vazamento por favor?',
      timestamp: '15 min atrás',
      read: false,
      requestId: '9012'
    },
    {
      id: '3',
      type: 'PROMO',
      title: 'Cupom de 10% OFF',
      description: 'Use o código CONSERTA10 no seu próximo pedido de pintura.',
      timestamp: '2 horas atrás',
      read: true
    }
  ]);

  const [requests, setRequests] = useState<ServiceRequest[]>([
    { 
      id: '8429', 
      title: 'Troca de Resistência', 
      status: 'EM_ANDAMENTO', 
      date: '14 Out', 
      category: 'Elétrica', 
      icon: 'bolt',
      description: 'O chuveiro parou de esquentar de repente no meio do banho.',
      provider: {
        name: 'Ricardo Silva',
        avatar: 'https://i.pravatar.cc/150?u=ricardo',
        rating: 4.8,
        specialty: 'Eletricista Residencial'
      },
      aiDiagnosis: {
        summary: 'Provável queima da resistência devido a pico de tensão ou desgaste natural.',
        possibleCauses: ['Resistência rompida', 'Problema no disjuntor', 'Fiação oxidada']
      }
    },
    {
      id: '9012',
      title: 'Vazamento na Cozinha',
      status: 'CONCLUIDO',
      date: '10 Out',
      category: 'Hidráulica',
      icon: 'water_drop',
      provider: {
        name: 'Marcos Nunes',
        avatar: 'https://i.pravatar.cc/150?u=marcos',
        rating: 4.9,
        specialty: 'Encanador Master'
      }
    }
  ]);

  useEffect(() => {
    if (currentView === 'SPLASH') {
      const timer = setTimeout(() => setCurrentView('ONBOARDING'), 2500);
      return () => clearTimeout(timer);
    }
  }, [currentView]);

  const handleViewDetails = (request: ServiceRequest) => {
    setSelectedRequest(request);
    setCurrentView('REQUEST_DETAILS');
  };

  const handleOpenChat = (request: ServiceRequest) => {
    setSelectedRequest(request);
    setCurrentView('CHAT');
  };

  const handleAddNewRequest = (newRequest: ServiceRequest) => {
    setRequests(prev => [newRequest, ...prev]);
    setSelectedCategoryId(null); 
  };

  const handleCompleteService = (requestId: string, rating: number, review: string, paymentMethod: string, amount?: string) => {
    setRequests(prev => prev.map(req => 
      req.id === requestId 
        ? { ...req, status: 'CONCLUIDO', rating, review, paymentMethod, paidAmount: amount } 
        : req
    ));
    setCurrentView('DASHBOARD');
  };

  const handleNotificationClick = (notification: Notification) => {
    setNotifications(prev => prev.map(n => n.id === notification.id ? { ...n, read: true } : n));
    if (notification.requestId) {
      const req = requests.find(r => r.id === notification.requestId);
      if (req) handleViewDetails(req);
    }
  };

  const renderView = () => {
    switch (currentView) {
      case 'SPLASH':
        return <SplashScreen />;
      case 'ONBOARDING':
        return <Onboarding onFinish={() => setCurrentView('AUTH')} />;
      case 'AUTH':
        return <Auth onLogin={() => setCurrentView('DASHBOARD')} onBack={() => setCurrentView('ONBOARDING')} />;
      case 'DASHBOARD':
        return (
          <Dashboard 
            requests={requests}
            unreadNotificationsCount={notifications.filter(n => !n.read).length}
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
            requests={requests}
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
            onBack={() => setCurrentView('DASHBOARD')}
            onLogout={() => setCurrentView('AUTH')}
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
              if (newReq) handleAddNewRequest(newReq);
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
            chats={requests.filter(r => r.status !== 'CANCELADO')}
          />
        );
      case 'CHAT':
        return selectedRequest ? (
          <Chat 
            request={selectedRequest} 
            onBack={() => setCurrentView('CHAT_LIST')} 
          />
        ) : null;
      default:
        return <SplashScreen />;
    }
  };

  return (
    <div className="min-h-screen bg-background-light dark:bg-background-dark max-w-md mx-auto shadow-2xl relative flex flex-col">
      {renderView()}
    </div>
  );
};

export default App;
