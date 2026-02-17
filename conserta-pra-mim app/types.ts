
export type AppState = 'SPLASH' | 'ONBOARDING' | 'AUTH' | 'DASHBOARD' | 'NEW_REQUEST' | 'REQUEST_DETAILS' | 'CHAT_LIST' | 'CHAT' | 'CATEGORIES' | 'ORDERS' | 'PROFILE' | 'FINISH_SERVICE' | 'NOTIFICATIONS';

export interface AuthSession {
  userId: string;
  token: string;
  userName: string;
  role: string;
  email: string;
}

export interface ServiceCategory {
  id: string;
  name: string;
  icon: string;
  color: string;
}

export interface ServiceRequestCategoryOption {
  id: string;
  name: string;
  slug: string;
  legacyCategory: string;
  icon: string;
}

export interface Notification {
  id: string;
  type: 'STATUS' | 'MESSAGE' | 'PROMO' | 'SYSTEM';
  title: string;
  description: string;
  timestamp: string;
  read: boolean;
  requestId?: string;
}

export interface ServiceRequest {
  id: string;
  title: string;
  status: 'AGUARDANDO' | 'EM_ANDAMENTO' | 'CONCLUIDO' | 'CANCELADO';
  date: string;
  category: string;
  icon: string;
  description?: string;
  provider?: {
    name: string;
    avatar: string;
    rating: number;
    specialty: string;
  };
  aiDiagnosis?: {
    summary: string;
    possibleCauses: string[];
  };
  rating?: number;
  review?: string;
  paymentMethod?: string;
  paidAmount?: string;
}

export interface OrderFlowStep {
  step: number;
  title: string;
  completed: boolean;
  current: boolean;
}

export interface OrderTimelineEvent {
  eventCode: string;
  title: string;
  description: string;
  occurredAt: string;
}

export interface ServiceRequestDetailsData {
  order: ServiceRequest;
  flowSteps: OrderFlowStep[];
  timeline: OrderTimelineEvent[];
}

export interface Message {
  role: 'user' | 'model';
  text: string;
  timestamp?: string;
}

export interface ChatPreview {
  id: string;
  request: ServiceRequest;
  lastMessage: string;
  timestamp: string;
  unreadCount: number;
}
