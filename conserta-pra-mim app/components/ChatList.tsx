import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { AuthSession, ChatConversationSummary } from '../types';
import {
  getMyActiveConversations,
  subscribeToRealtimeChatEvents
} from '../services/realtimeChat';

interface Props {
  authSession: AuthSession | null;
  onBack: () => void;
  onSelectChat: (conversation: ChatConversationSummary) => void;
  onGoToHome: () => void;
  onGoToOrders?: () => void;
  onGoToProfile?: () => void;
}

function normalizeId(value?: string | null): string {
  return String(value || '').trim().toLowerCase();
}

function formatConversationTime(value?: string): string {
  if (!value) {
    return 'Agora';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return 'Agora';
  }

  const now = new Date();
  const isSameDay = date.getDate() === now.getDate()
    && date.getMonth() === now.getMonth()
    && date.getFullYear() === now.getFullYear();

  if (isSameDay) {
    const hh = String(date.getHours()).padStart(2, '0');
    const mm = String(date.getMinutes()).padStart(2, '0');
    return `${hh}:${mm}`;
  }

  const dd = String(date.getDate()).padStart(2, '0');
  const month = String(date.getMonth() + 1).padStart(2, '0');
  return `${dd}/${month}`;
}

function buildMessagePreview(conversation: ChatConversationSummary): string {
  const preview = String(conversation.lastMessagePreview || '').trim();
  if (!preview) {
    return 'Sem mensagens.';
  }

  if (preview.length <= 80) {
    return preview;
  }

  return `${preview.slice(0, 80)}...`;
}

const ChatList: React.FC<Props> = ({
  authSession,
  onBack,
  onSelectChat,
  onGoToHome,
  onGoToOrders,
  onGoToProfile
}) => {
  const [conversations, setConversations] = useState<ChatConversationSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState('');
  const [searchTerm, setSearchTerm] = useState('');

  const loadConversations = useCallback(async () => {
    if (!authSession?.token) {
      setConversations([]);
      setErrorMessage('Sessao invalida para carregar conversas.');
      setIsLoading(false);
      return;
    }

    setIsLoading(true);
    setErrorMessage('');

    try {
      const data = await getMyActiveConversations(authSession.token);
      setConversations(data);
    } catch {
      setErrorMessage('Nao foi possivel carregar suas conversas agora.');
    } finally {
      setIsLoading(false);
    }
  }, [authSession?.token]);

  useEffect(() => {
    void loadConversations();
  }, [loadConversations]);

  useEffect(() => {
    if (!authSession?.token) {
      return undefined;
    }

    const unsubscribe = subscribeToRealtimeChatEvents({
      onChatMessage: (message) => {
        const targetRequestId = normalizeId(message.requestId);
        const targetProviderId = normalizeId(message.providerId);

        setConversations((previous) => {
          const existingIndex = previous.findIndex((conversation) =>
            normalizeId(conversation.requestId) === targetRequestId
            && normalizeId(conversation.providerId) === targetProviderId);

          if (existingIndex < 0) {
            void loadConversations();
            return previous;
          }

          const current = previous[existingIndex];
          const senderIsCurrentUser = normalizeId(message.senderId) === normalizeId(authSession.userId);
          const messagePreview = message.text?.trim()
            || (message.attachments.length === 1
              ? 'Anexo enviado.'
              : `${message.attachments.length} anexos enviados.`);

          const updatedConversation: ChatConversationSummary = {
            ...current,
            lastMessagePreview: messagePreview,
            lastMessageAt: message.createdAt,
            unreadMessages: senderIsCurrentUser ? current.unreadMessages : current.unreadMessages + 1
          };

          const next = previous.filter((_, index) => index !== existingIndex);
          return [updatedConversation, ...next];
        });
      },
      onMessageReceiptUpdated: () => {
        void loadConversations();
      },
      onUserPresence: (presence) => {
        setConversations((previous) => previous.map((conversation) => {
          if (normalizeId(conversation.counterpartUserId) !== normalizeId(presence.userId)) {
            return conversation;
          }

          return {
            ...conversation,
            counterpartIsOnline: presence.isOnline
          };
        }));
      },
      onProviderStatus: (payload) => {
        setConversations((previous) => previous.map((conversation) => {
          if (normalizeId(conversation.providerId) !== normalizeId(payload.providerId)) {
            return conversation;
          }

          return {
            ...conversation,
            providerStatus: payload.status || undefined
          };
        }));
      }
    });

    return () => {
      unsubscribe();
    };
  }, [authSession?.token, authSession?.userId, loadConversations]);

  const filteredConversations = useMemo(() => {
    const term = searchTerm.trim().toLowerCase();
    if (!term) {
      return conversations;
    }

    return conversations.filter((conversation) => {
      const name = conversation.counterpartName.toLowerCase();
      const title = conversation.title.toLowerCase();
      const preview = conversation.lastMessagePreview.toLowerCase();
      return name.includes(term) || title.includes(term) || preview.includes(term);
    });
  }, [conversations, searchTerm]);

  return (
    <div className="flex flex-col h-screen bg-white overflow-hidden">
      <header className="flex flex-col bg-white px-4 pt-6 pb-4 sticky top-0 z-20 border-b border-primary/5">
        <div className="flex items-center justify-between mb-6">
          <button onClick={onBack} className="p-2 -ml-2 text-primary hover:bg-primary/5 rounded-full transition-colors">
            <span className="material-symbols-outlined">arrow_back</span>
          </button>
          <h1 className="text-2xl font-bold text-[#101818]">Conversas</h1>
          <button
            type="button"
            onClick={() => void loadConversations()}
            className="p-2 text-primary hover:bg-primary/5 rounded-full"
            title="Atualizar conversas"
          >
            <span className="material-symbols-outlined">refresh</span>
          </button>
        </div>

        <div className="relative">
          <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-[#5e8d8d] text-xl">search</span>
          <input
            type="text"
            value={searchTerm}
            onChange={(event) => setSearchTerm(event.target.value)}
            placeholder="Buscar por prestador ou servico..."
            className="w-full h-11 bg-background-light border-none rounded-xl pl-10 pr-4 text-sm focus:ring-2 focus:ring-primary/20 placeholder:text-[#5e8d8d]"
          />
        </div>
      </header>

      <div className="flex-1 overflow-y-auto no-scrollbar pb-24">
        {isLoading ? (
          <div className="flex flex-col items-center justify-center h-full p-8 text-center text-[#5e8d8d]">
            <span className="material-symbols-outlined text-5xl animate-spin">progress_activity</span>
            <p className="text-sm mt-3">Carregando conversas...</p>
          </div>
        ) : errorMessage ? (
          <div className="m-4 rounded-2xl border border-amber-200 bg-amber-50 p-4">
            <p className="text-sm text-amber-900">{errorMessage}</p>
            <button
              type="button"
              onClick={() => void loadConversations()}
              className="mt-3 inline-flex h-9 px-4 items-center justify-center rounded-lg bg-primary text-white text-sm font-bold"
            >
              Tentar novamente
            </button>
          </div>
        ) : filteredConversations.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-full p-8 text-center opacity-40">
            <span className="material-symbols-outlined text-7xl mb-4">forum</span>
            <p className="text-lg font-bold">Nenhuma conversa ativa</p>
            <p className="text-sm">As conversas entre voce e prestadores aparecerao aqui.</p>
          </div>
        ) : (
          <div className="divide-y divide-primary/5">
            {filteredConversations.map((conversation) => (
              <button
                key={`${conversation.requestId}-${conversation.providerId}`}
                onClick={() => onSelectChat(conversation)}
                className="w-full flex items-center gap-4 p-4 hover:bg-background-light active:bg-primary/5 transition-colors text-left"
              >
                <div className="relative flex-shrink-0">
                  <div className="size-14 rounded-full border-2 border-primary/10 overflow-hidden bg-background-light">
                    <img
                      src={`https://i.pravatar.cc/150?u=${conversation.counterpartUserId}`}
                      alt={conversation.counterpartName}
                      className="w-full h-full object-cover"
                    />
                  </div>
                  <div className={`absolute bottom-0 right-0 size-4 rounded-full border-2 border-white ${
                    conversation.counterpartIsOnline ? 'bg-green-500' : 'bg-[#96a7a7]'
                  }`}
                  />
                </div>

                <div className="flex-1 min-w-0">
                  <div className="flex justify-between items-start mb-1">
                    <h3 className="text-base font-bold text-[#101818] truncate pr-2">
                      {conversation.counterpartName}
                    </h3>
                    <span className="text-[10px] font-medium text-[#5e8d8d] whitespace-nowrap pt-1">
                      {formatConversationTime(conversation.lastMessageAt)}
                    </span>
                  </div>
                  <p className="text-[11px] font-bold text-primary mb-1 uppercase tracking-tight truncate">
                    {conversation.providerStatus ? `Status: ${conversation.providerStatus}` : conversation.title}
                  </p>
                  <p className="text-sm text-[#5e8d8d] truncate leading-tight">
                    {buildMessagePreview(conversation)}
                  </p>
                </div>

                {conversation.unreadMessages > 0 ? (
                  <div className="min-w-5 h-5 px-1 bg-primary rounded-full flex items-center justify-center">
                    <span className="text-[10px] text-white font-bold">{conversation.unreadMessages > 99 ? '99+' : conversation.unreadMessages}</span>
                  </div>
                ) : null}
              </button>
            ))}
          </div>
        )}
      </div>

      <nav className="fixed bottom-0 left-0 right-0 z-50 bg-white border-t border-primary/10 px-4 pb-4 pt-2 max-w-md mx-auto">
        <div className="flex items-center justify-between mb-2">
          <NavItem icon="home" label="Inicio" onClick={onGoToHome} />
          <NavItem icon="assignment" label="Pedidos" onClick={onGoToOrders} />
          <NavItem active icon="chat_bubble" label="Chat" />
          <NavItem icon="person" label="Perfil" onClick={onGoToProfile} />
        </div>
        <p className="text-center text-[8px] font-bold text-primary/30 tracking-widest uppercase">
          Powered by DevCraft Studio
        </p>
      </nav>
    </div>
  );
};

const NavItem: React.FC<{ icon: string; label: string; active?: boolean; onClick?: () => void }> = ({ icon, label, active, onClick }) => (
  <button
    onClick={onClick}
    className={`flex flex-col items-center gap-1 ${active ? 'text-primary' : 'text-[#5e8d8d]'} active:scale-95 transition-transform`}
  >
    <div className="flex h-8 items-center justify-center">
      <span className={`material-symbols-outlined text-[28px] ${active ? 'material-symbols-fill' : ''}`}>{icon}</span>
    </div>
    <p className={`text-[10px] leading-normal tracking-wide ${active ? 'font-bold' : 'font-medium'}`}>{label}</p>
  </button>
);

export default ChatList;
