import React, { useMemo, useState } from 'react';
import { ProviderChatConversationSummary } from '../types';

interface Props {
  conversations: ProviderChatConversationSummary[];
  loading: boolean;
  error: string;
  onBack: () => void;
  onRefresh: () => Promise<void>;
  onSelectConversation: (conversation: ProviderChatConversationSummary) => void;
  onGoHome: () => void;
  onGoProposals: () => void;
  onGoAgenda: () => void;
  onGoProfile: () => void;
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
  const sameDay = date.getDate() === now.getDate()
    && date.getMonth() === now.getMonth()
    && date.getFullYear() === now.getFullYear();

  if (sameDay) {
    const hh = String(date.getHours()).padStart(2, '0');
    const mm = String(date.getMinutes()).padStart(2, '0');
    return `${hh}:${mm}`;
  }

  const dd = String(date.getDate()).padStart(2, '0');
  const mm = String(date.getMonth() + 1).padStart(2, '0');
  return `${dd}/${mm}`;
}

const ChatList: React.FC<Props> = ({
  conversations,
  loading,
  error,
  onBack,
  onRefresh,
  onSelectConversation,
  onGoHome,
  onGoProposals,
  onGoAgenda,
  onGoProfile
}) => {
  const [searchTerm, setSearchTerm] = useState('');

  const filtered = useMemo(() => {
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
    <div className="min-h-screen bg-[#f4f7fb] pb-24">
      <header className="bg-white border-b border-[#e4e7ec] sticky top-0 z-10">
        <div className="max-w-md mx-auto px-4 py-4 flex items-center justify-between gap-2">
          <button type="button" onClick={onBack} className="text-sm font-semibold text-[#344054] flex items-center gap-1">
            <span className="material-symbols-outlined text-base">arrow_back</span>
            Voltar
          </button>
          <h1 className="text-lg font-bold text-[#101828]">Conversas</h1>
          <button type="button" onClick={() => void onRefresh()} className="text-sm font-semibold text-primary">Atualizar</button>
        </div>
        <div className="max-w-md mx-auto px-4 pb-4">
          <input
            type="text"
            value={searchTerm}
            onChange={(event) => setSearchTerm(event.target.value)}
            placeholder="Buscar por cliente ou pedido..."
            className="w-full rounded-xl border border-[#d0d5dd] px-3 py-2 text-sm"
          />
        </div>
      </header>

      <main className="max-w-md mx-auto px-4 py-4">
        {error ? (
          <div className="mb-4 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {error}
          </div>
        ) : null}

        {loading ? (
          <div className="rounded-xl bg-white border border-[#e4e7ec] p-4 text-sm text-[#667085]">
            Carregando conversas...
          </div>
        ) : filtered.length === 0 ? (
          <div className="rounded-xl bg-white border border-[#e4e7ec] p-4 text-sm text-[#667085]">
            Nenhuma conversa ativa encontrada.
          </div>
        ) : (
          <div className="space-y-3">
            {filtered.map((conversation) => (
              <button
                key={`${conversation.requestId}-${conversation.providerId}`}
                type="button"
                onClick={() => onSelectConversation(conversation)}
                className="w-full text-left rounded-xl border border-[#e4e7ec] bg-white p-3"
              >
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="font-semibold text-sm text-[#101828] truncate">{conversation.counterpartName}</p>
                    <p className="text-[11px] text-primary font-semibold mt-0.5 truncate">{conversation.title}</p>
                    <p className="text-xs text-[#667085] mt-1 line-clamp-2">{conversation.lastMessagePreview}</p>
                  </div>
                  <div className="flex flex-col items-end gap-1">
                    <span className="text-[10px] font-semibold text-[#667085]">{formatConversationTime(conversation.lastMessageAt)}</span>
                    {conversation.unreadMessages > 0 ? (
                      <span className="inline-flex min-w-5 h-5 px-1 items-center justify-center rounded-full bg-primary text-white text-[10px] font-bold">
                        {conversation.unreadMessages > 99 ? '99+' : conversation.unreadMessages}
                      </span>
                    ) : null}
                  </div>
                </div>
              </button>
            ))}
          </div>
        )}
      </main>

      <nav className="fixed bottom-0 left-0 right-0 bg-white border-t border-[#e4e7ec] max-w-md mx-auto px-4 py-3 flex justify-between z-20">
        <NavButton icon="home" label="Inicio" onClick={onGoHome} />
        <NavButton icon="description" label="Propostas" onClick={onGoProposals} />
        <NavButton icon="event" label="Agenda" onClick={onGoAgenda} />
        <NavButton icon="chat" label="Chat" active />
        <NavButton icon="person" label="Perfil" onClick={onGoProfile} />
      </nav>
    </div>
  );
};

const NavButton: React.FC<{ icon: string; label: string; active?: boolean; onClick?: () => void }> = ({ icon, label, active, onClick }) => (
  <button
    type="button"
    onClick={onClick}
    className={`flex flex-col items-center gap-1 ${active ? 'text-primary' : 'text-[#667085]'}`}
  >
    <span className={`material-symbols-outlined ${active ? 'material-symbols-fill' : ''}`}>{icon}</span>
    <span className="text-[10px] font-semibold">{label}</span>
  </button>
);

export default ChatList;
