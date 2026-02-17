import React from 'react';
import { ServiceCategory, ServiceRequest } from '../types';

interface Props {
  requests: ServiceRequest[];
  unreadNotificationsCount?: number;
  onNewRequest: () => void;
  onShowDetails: (request: ServiceRequest) => void;
  onOpenChatList: () => void;
  onViewAllCategories: () => void;
  onViewOrders: () => void;
  onViewProfile: () => void;
  onViewNotifications?: () => void;
}

const CATEGORIES: ServiceCategory[] = [
  { id: '1', name: 'Eletrica', icon: 'bolt', color: 'bg-primary/10' },
  { id: '2', name: 'Hidraulica', icon: 'water_drop', color: 'bg-primary/10' },
  { id: '3', name: 'Montagem', icon: 'construction', color: 'bg-primary/10' },
  { id: '4', name: 'Pintura', icon: 'format_paint', color: 'bg-primary/10' }
];

function getDescriptionPreview(description?: string): string {
  const normalized = (description || '').trim();
  if (!normalized) {
    return 'Sem descricao informada.';
  }

  if (normalized.length <= 100) {
    return normalized;
  }

  return `${normalized.slice(0, 100).trimEnd()}...`;
}

const Dashboard: React.FC<Props> = ({
  requests,
  unreadNotificationsCount = 0,
  onNewRequest,
  onShowDetails,
  onOpenChatList,
  onViewAllCategories,
  onViewOrders,
  onViewProfile,
  onViewNotifications
}) => {
  const activeRequests = requests.filter(req => req.status !== 'CONCLUIDO');

  return (
    <div className="flex flex-col h-screen bg-background-light overflow-hidden">
      <header className="flex items-center bg-white p-4 justify-between sticky top-0 z-10 border-b border-primary/10">
        <div
          onClick={onViewProfile}
          className="flex size-10 shrink-0 items-center overflow-hidden rounded-full border-2 border-primary/20 cursor-pointer"
        >
          <img src="https://i.pravatar.cc/150?u=joao" alt="Profile" className="h-full w-full object-cover" />
        </div>
        <h2 className="text-primary text-lg font-bold flex-1 ml-3">Conserta Pra Mim</h2>
        <button
          onClick={onViewNotifications}
          className="flex size-10 items-center justify-center rounded-xl bg-primary/10 text-primary relative active:scale-90 transition-transform"
        >
          <span className="material-symbols-outlined">notifications</span>
          {unreadNotificationsCount > 0 && (
            <span className="absolute -top-1 -right-1 size-5 bg-red-500 text-white text-[10px] font-bold flex items-center justify-center rounded-full border-2 border-white">
              {unreadNotificationsCount}
            </span>
          )}
        </button>
      </header>

      <div className="flex-1 overflow-y-auto no-scrollbar pb-24">
        <section className="px-4 pt-6 pb-2">
          <h1 className="text-[#101818] text-3xl font-bold">Ola, Joao!</h1>
          <p className="text-primary/70 text-sm font-medium mt-1">Como podemos te ajudar hoje?</p>
        </section>

        <section className="px-4 py-5">
          <button
            onClick={onNewRequest}
            className="flex w-full items-center justify-center gap-3 rounded-2xl h-16 bg-primary text-white text-lg font-bold shadow-xl shadow-primary/20 active:scale-[0.98] transition-all"
          >
            <span className="material-symbols-outlined material-symbols-fill">add_circle</span>
            <span>Pedir um Servico</span>
          </button>
        </section>

        <section className="py-4">
          <div className="flex items-center justify-between px-4 mb-4">
            <h3 className="text-[#101818] text-lg font-bold">Categorias</h3>
            <button onClick={onViewAllCategories} className="text-primary text-sm font-semibold hover:underline">Ver todas</button>
          </div>
          <div className="grid grid-cols-4 gap-4 px-4 pb-2">
            {CATEGORIES.map(cat => (
              <div key={cat.id} className="flex flex-col items-center gap-2 w-full">
                <button
                  onClick={onNewRequest}
                  className="size-16 rounded-2xl bg-white shadow-sm border border-primary/5 flex items-center justify-center text-primary active:bg-primary active:text-white transition-colors"
                >
                  <span className="material-symbols-outlined text-3xl">{cat.icon}</span>
                </button>
                <span className="text-xs font-semibold text-[#101818]">{cat.name}</span>
              </div>
            ))}
          </div>
        </section>

        <section className="px-4 py-4">
          <h3 className="text-[#101818] text-lg font-bold mb-4">Seus pedidos recentes</h3>
          {activeRequests.length === 0 ? (
            <div className="text-center py-8 bg-white rounded-xl border border-dashed border-primary/20">
              <p className="text-sm text-[#5e8d8d]">Voce nao tem pedidos ativos no momento.</p>
              <button onClick={onViewOrders} className="text-primary text-xs font-bold mt-2 hover:underline">Ver historico completo</button>
            </div>
          ) : (
            activeRequests.map(req => (
              <div key={req.id} className="bg-white rounded-xl p-4 border border-primary/5 shadow-sm mb-4">
                <div className="flex justify-between items-start mb-3">
                  <div className="flex items-center gap-3">
                    <div className="size-12 rounded-lg bg-primary/10 flex items-center justify-center text-primary">
                      <span className="material-symbols-outlined">{req.icon}</span>
                    </div>
                    <div>
                      <h4 className="font-bold text-[#101818]">{req.category || req.title}</h4>
                      <p className="text-xs text-primary/60 font-medium">{getDescriptionPreview(req.description)}</p>
                    </div>
                  </div>
                  <span className={`text-[10px] font-bold px-2 py-1 rounded-full uppercase ${
                    req.status === 'EM_ANDAMENTO' ? 'bg-orange-100 text-orange-600' :
                    req.status === 'AGUARDANDO' ? 'bg-blue-100 text-blue-600' :
                    'bg-gray-100 text-gray-600'
                  }`}>
                    {req.status.replace('_', ' ')}
                  </span>
                </div>
                <div className="flex items-center justify-between pt-3 border-t border-primary/5 mt-3">
                  <div className="flex items-center gap-2 text-[#5e8d8d]">
                    <span className="material-symbols-outlined text-sm">calendar_month</span>
                    <span className="text-xs font-medium">{req.date}</span>
                  </div>
                  <button
                    onClick={() => onShowDetails(req)}
                    className="text-primary text-sm font-bold flex items-center gap-1 hover:underline"
                  >
                    Detalhes
                    <span className="material-symbols-outlined text-sm">chevron_right</span>
                  </button>
                </div>
              </div>
            ))
          )}
        </section>
      </div>

      <nav className="fixed bottom-0 left-0 right-0 z-50 bg-white border-t border-primary/10 px-4 pb-4 pt-2 max-w-md mx-auto">
        <div className="flex items-center justify-between mb-2">
          <NavItem active icon="home" label="Inicio" />
          <NavItem icon="assignment" label="Pedidos" onClick={onViewOrders} />
          <NavItem icon="chat_bubble" label="Chat" badge onClick={onOpenChatList} />
          <NavItem icon="person" label="Perfil" onClick={onViewProfile} />
        </div>
        <p className="text-center text-[8px] font-bold text-primary/30 tracking-widest uppercase">
          Powered by DevCfrat Studio
        </p>
      </nav>
    </div>
  );
};

const NavItem: React.FC<{ icon: string; label: string; active?: boolean; badge?: boolean; onClick?: () => void }> = ({ icon, label, active, badge, onClick }) => (
  <button onClick={onClick} className={`flex flex-col items-center gap-1 ${active ? 'text-primary' : 'text-[#5e8d8d]'} active:scale-95 transition-transform`}>
    <div className="flex h-8 items-center justify-center relative">
      <span className={`material-symbols-outlined text-[28px] ${active ? 'material-symbols-fill' : ''}`}>{icon}</span>
      {badge && <span className="absolute top-0 -right-1 size-2 bg-red-500 rounded-full border-2 border-white"></span>}
    </div>
    <p className={`text-[10px] leading-normal tracking-wide ${active ? 'font-bold' : 'font-medium'}`}>{label}</p>
  </button>
);

export default Dashboard;
