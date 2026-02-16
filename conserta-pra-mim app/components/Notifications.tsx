
import React from 'react';
import { Notification } from '../types';

interface Props {
  notifications: Notification[];
  onBack: () => void;
  onNotificationClick: (notification: Notification) => void;
  onClearAll: () => void;
}

const Notifications: React.FC<Props> = ({ notifications, onBack, onNotificationClick, onClearAll }) => {
  const getIcon = (type: string) => {
    switch (type) {
      case 'STATUS': return 'info';
      case 'MESSAGE': return 'chat_bubble';
      case 'PROMO': return 'local_offer';
      case 'SYSTEM': return 'settings';
      default: return 'notifications';
    }
  };

  const getColor = (type: string) => {
    switch (type) {
      case 'STATUS': return 'text-blue-500 bg-blue-50';
      case 'MESSAGE': return 'text-primary bg-primary/10';
      case 'PROMO': return 'text-orange-500 bg-orange-50';
      case 'SYSTEM': return 'text-gray-500 bg-gray-50';
      default: return 'text-primary bg-primary/10';
    }
  };

  return (
    <div className="flex flex-col h-screen bg-background-light overflow-hidden">
      {/* Header */}
      <header className="flex items-center p-4 border-b border-primary/5 sticky top-0 bg-white z-10">
        <button onClick={onBack} className="p-2 -ml-2 text-primary hover:bg-primary/5 rounded-full transition-colors">
          <span className="material-symbols-outlined">arrow_back</span>
        </button>
        <h2 className="text-[#101818] text-base font-bold flex-1 text-center">Notificações</h2>
        <button 
          onClick={onClearAll}
          className="text-xs font-bold text-primary hover:underline active:scale-95 transition-all"
        >
          Limpar
        </button>
      </header>

      <div className="flex-1 overflow-y-auto no-scrollbar p-4 space-y-3">
        {notifications.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-full opacity-40 text-center py-20">
            <span className="material-symbols-outlined text-6xl mb-4">notifications_off</span>
            <p className="font-bold">Nada por aqui</p>
            <p className="text-sm">Você não tem novas notificações.</p>
          </div>
        ) : (
          notifications.map((notif) => (
            <button
              key={notif.id}
              onClick={() => onNotificationClick(notif)}
              className={`w-full p-4 rounded-2xl border flex gap-4 text-left transition-all active:scale-[0.98] ${
                notif.read ? 'bg-white border-primary/5' : 'bg-white border-primary/20 shadow-sm'
              }`}
            >
              <div className={`size-12 rounded-xl flex items-center justify-center shrink-0 ${getColor(notif.type)}`}>
                <span className="material-symbols-outlined text-2xl">{getIcon(notif.type)}</span>
              </div>
              <div className="flex-1 min-w-0">
                <div className="flex justify-between items-start mb-1">
                  <h3 className={`text-sm font-bold truncate pr-2 ${notif.read ? 'text-[#101818]' : 'text-primary'}`}>
                    {notif.title}
                  </h3>
                  <span className="text-[10px] font-medium text-[#5e8d8d] whitespace-nowrap pt-0.5">
                    {notif.timestamp}
                  </span>
                </div>
                <p className="text-xs text-[#5e8d8d] leading-relaxed line-clamp-2">
                  {notif.description}
                </p>
                {!notif.read && (
                  <div className="mt-2 flex items-center gap-1.5">
                    <div className="size-1.5 bg-primary rounded-full"></div>
                    <span className="text-[10px] font-bold text-primary uppercase">Novo</span>
                  </div>
                )}
              </div>
            </button>
          ))
        )}
      </div>

      <div className="p-4 bg-white/50 border-t border-primary/5 mt-auto">
        <p className="text-center text-[8px] font-bold text-primary/30 tracking-widest uppercase">
          Powered by DevCfrat Studio
        </p>
      </div>
    </div>
  );
};

export default Notifications;
