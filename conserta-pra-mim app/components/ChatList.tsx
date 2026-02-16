
import React from 'react';
import { ServiceRequest } from '../types';

interface Props {
  chats: ServiceRequest[];
  onBack: () => void;
  onSelectChat: (request: ServiceRequest) => void;
  onGoToHome: () => void;
  onGoToOrders?: () => void;
  onGoToProfile?: () => void;
}

const ChatList: React.FC<Props> = ({ chats, onBack, onSelectChat, onGoToHome, onGoToOrders, onGoToProfile }) => {
  return (
    <div className="flex flex-col h-screen bg-white overflow-hidden">
      {/* Header */}
      <header className="flex flex-col bg-white px-4 pt-6 pb-4 sticky top-0 z-20 border-b border-primary/5">
        <div className="flex items-center justify-between mb-6">
          <button onClick={onBack} className="p-2 -ml-2 text-primary hover:bg-primary/5 rounded-full transition-colors">
            <span className="material-symbols-outlined">arrow_back</span>
          </button>
          <h1 className="text-2xl font-bold text-[#101818]">Conversas</h1>
          <button className="p-2 text-primary hover:bg-primary/5 rounded-full">
            <span className="material-symbols-outlined">edit_square</span>
          </button>
        </div>
        
        {/* Search Bar */}
        <div className="relative">
          <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-[#5e8d8d] text-xl">search</span>
          <input 
            type="text" 
            placeholder="Buscar por prestador ou serviço..."
            className="w-full h-11 bg-background-light border-none rounded-xl pl-10 pr-4 text-sm focus:ring-2 focus:ring-primary/20 placeholder:text-[#5e8d8d]"
          />
        </div>
      </header>

      {/* List Area */}
      <div className="flex-1 overflow-y-auto no-scrollbar pb-24">
        {chats.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-full p-8 text-center opacity-40">
            <span className="material-symbols-outlined text-7xl mb-4">forum</span>
            <p className="text-lg font-bold">Nenhuma conversa ativa</p>
            <p className="text-sm">Seus chats com prestadores aparecerão aqui.</p>
          </div>
        ) : (
          <div className="divide-y divide-primary/5">
            {chats.map((chat) => (
              <button 
                key={chat.id}
                onClick={() => onSelectChat(chat)}
                className="w-full flex items-center gap-4 p-4 hover:bg-background-light active:bg-primary/5 transition-colors text-left"
              >
                {/* Avatar with Status */}
                <div className="relative flex-shrink-0">
                  <div className="size-14 rounded-full border-2 border-primary/10 overflow-hidden bg-background-light">
                    <img src={chat.provider?.avatar || `https://i.pravatar.cc/150?u=${chat.id}`} alt={chat.provider?.name} className="w-full h-full object-cover" />
                  </div>
                  <div className="absolute bottom-0 right-0 size-4 bg-green-500 rounded-full border-2 border-white"></div>
                </div>

                {/* Content */}
                <div className="flex-1 min-w-0">
                  <div className="flex justify-between items-start mb-1">
                    <h3 className="text-base font-bold text-[#101818] truncate pr-2">
                      {chat.provider?.name || 'Buscando profissional...'}
                    </h3>
                    <span className="text-[10px] font-medium text-[#5e8d8d] whitespace-nowrap pt-1">
                      11:45
                    </span>
                  </div>
                  <p className="text-xs font-bold text-primary mb-1 uppercase tracking-tight">
                    {chat.title}
                  </p>
                  <p className="text-sm text-[#5e8d8d] truncate leading-tight">
                    {chat.status === 'AGUARDANDO' ? 'Aguardando confirmação do técnico...' : 'Olá! Em que posso ajudar?'}
                  </p>
                </div>

                {/* Unread Badge (Mock) */}
                {chat.id === '8429' && (
                  <div className="size-5 bg-primary rounded-full flex items-center justify-center">
                    <span className="text-[10px] text-white font-bold">1</span>
                  </div>
                )}
              </button>
            ))}
          </div>
        )}
      </div>

      {/* Navigation Footer - Fixed to bottom */}
      <nav className="fixed bottom-0 left-0 right-0 z-50 bg-white border-t border-primary/10 px-4 pb-4 pt-2 max-w-md mx-auto">
        <div className="flex items-center justify-between mb-2">
          <NavItem icon="home" label="Início" onClick={onGoToHome} />
          <NavItem icon="assignment" label="Pedidos" onClick={onGoToOrders} />
          <NavItem active icon="chat_bubble" label="Chat" />
          <NavItem icon="person" label="Perfil" onClick={onGoToProfile} />
        </div>
        <p className="text-center text-[8px] font-bold text-primary/30 tracking-widest uppercase">
          Powered by DevCfrat Studio
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
