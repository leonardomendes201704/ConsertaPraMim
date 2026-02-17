import React, { useState } from 'react';
import { ServiceRequest } from '../types';

interface Props {
  openOrders: ServiceRequest[];
  finalizedOrders: ServiceRequest[];
  isLoading?: boolean;
  errorMessage?: string;
  onRetry?: () => void;
  onBack: () => void;
  onShowDetails: (request: ServiceRequest) => void;
  onGoToHome: () => void;
  onGoToChat: () => void;
  onViewProfile?: () => void;
}

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

function getProposalBadgeText(proposalCount?: number): string {
  const normalized = Number(proposalCount ?? 0);
  if (!Number.isFinite(normalized) || normalized <= 0) {
    return '';
  }

  const count = Math.max(0, Math.trunc(normalized));
  return `${count} ${count === 1 ? 'proposta' : 'propostas'}`;
}

const OrdersList: React.FC<Props> = ({
  openOrders,
  finalizedOrders,
  isLoading = false,
  errorMessage,
  onRetry,
  onBack,
  onShowDetails,
  onGoToHome,
  onGoToChat,
  onViewProfile
}) => {
  const [activeTab, setActiveTab] = useState<'ATIVOS' | 'HISTORICO'>('ATIVOS');
  const currentList = activeTab === 'ATIVOS' ? openOrders : finalizedOrders;

  return (
    <div className="flex flex-col h-screen bg-background-light overflow-hidden">
      <header className="bg-white px-4 pt-6 pb-2 sticky top-0 z-20 border-b border-primary/10">
        <div className="flex items-center justify-between mb-4">
          <button onClick={onBack} className="p-2 -ml-2 text-primary hover:bg-primary/5 rounded-full transition-colors">
            <span className="material-symbols-outlined">arrow_back</span>
          </button>
          <h1 className="text-xl font-bold text-[#101818]">Meus Pedidos</h1>
          <div className="w-10"></div>
        </div>

        <div className="flex w-full">
          <button
            onClick={() => setActiveTab('ATIVOS')}
            className={`flex-1 py-3 text-sm font-bold border-b-2 transition-all ${
              activeTab === 'ATIVOS' ? 'border-primary text-primary' : 'border-transparent text-[#5e8d8d]'
            }`}
          >
            Ativos ({openOrders.length})
          </button>
          <button
            onClick={() => setActiveTab('HISTORICO')}
            className={`flex-1 py-3 text-sm font-bold border-b-2 transition-all ${
              activeTab === 'HISTORICO' ? 'border-primary text-primary' : 'border-transparent text-[#5e8d8d]'
            }`}
          >
            Historico ({finalizedOrders.length})
          </button>
        </div>
      </header>

      <div className="flex-1 overflow-y-auto no-scrollbar p-4 pb-24">
        {isLoading ? (
          <div className="flex flex-col items-center justify-center h-full text-center py-20">
            <span className="material-symbols-outlined text-5xl text-primary mb-3 animate-spin">progress_activity</span>
            <p className="text-base font-bold text-[#101818]">Carregando pedidos...</p>
          </div>
        ) : errorMessage && currentList.length === 0 ? (
          <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-center">
            <p className="text-sm text-amber-900 font-semibold">{errorMessage}</p>
            {onRetry ? (
              <button
                type="button"
                onClick={onRetry}
                className="mt-3 inline-flex h-10 px-4 items-center justify-center rounded-lg bg-primary text-white font-bold"
              >
                Tentar novamente
              </button>
            ) : null}
          </div>
        ) : currentList.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-full text-center opacity-40 py-20">
            <span className="material-symbols-outlined text-6xl mb-4">receipt_long</span>
            <p className="text-lg font-bold">Nenhum pedido encontrado</p>
            <p className="text-sm">Seus pedidos aparecerao listados aqui.</p>
          </div>
        ) : (
          <div className="space-y-4">
            {currentList.map((req) => (
              <div
                key={req.id}
                onClick={() => onShowDetails(req)}
                className="bg-white rounded-xl p-4 border border-primary/5 shadow-sm active:scale-[0.98] transition-all cursor-pointer"
              >
                <div className="flex justify-between items-start mb-3">
                  <div className="flex items-center gap-3">
                    <div className="size-12 rounded-lg bg-primary/10 flex items-center justify-center text-primary">
                      <span className="material-symbols-outlined">{req.icon}</span>
                    </div>
                    <div>
                      <h4 className="font-bold text-[#101818]">{req.category || req.title}</h4>
                      <p className="text-xs text-primary/60 font-medium">{getDescriptionPreview(req.description)}</p>
                      {req.proposalCount && req.proposalCount > 0 ? (
                        <span className="mt-1 inline-flex rounded-full bg-emerald-100 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide text-emerald-700">
                          {getProposalBadgeText(req.proposalCount)}
                        </span>
                      ) : null}
                    </div>
                  </div>
                  <span
                    className={`text-[10px] font-bold px-2 py-1 rounded-full uppercase ${
                      req.status === 'EM_ANDAMENTO'
                        ? 'bg-orange-100 text-orange-600'
                        : req.status === 'AGUARDANDO'
                        ? 'bg-blue-100 text-blue-600'
                        : req.status === 'CONCLUIDO'
                        ? 'bg-green-100 text-green-600'
                        : 'bg-gray-100 text-gray-600'
                    }`}
                  >
                    {req.status.replace('_', ' ')}
                  </span>
                </div>

                <div className="flex items-center justify-between pt-3 border-t border-primary/5 mt-3">
                  <div className="flex items-center gap-2 text-[#5e8d8d]">
                    <span className="material-symbols-outlined text-sm">calendar_month</span>
                    <span className="text-xs font-medium">{req.date}</span>
                  </div>
                  <div className="flex items-center gap-1 text-primary text-sm font-bold">
                    Detalhes
                    <span className="material-symbols-outlined text-sm">chevron_right</span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      <nav className="fixed bottom-0 left-0 right-0 z-50 bg-white border-t border-primary/10 px-4 pb-4 pt-2 max-w-md mx-auto">
        <div className="flex items-center justify-between mb-2">
          <NavItem icon="home" label="Inicio" onClick={onGoToHome} />
          <NavItem active icon="assignment" label="Pedidos" />
          <NavItem icon="chat_bubble" label="Chat" onClick={onGoToChat} />
          <NavItem icon="person" label="Perfil" onClick={onViewProfile} />
        </div>
        <p className="text-center text-[8px] font-bold text-primary/30 tracking-widest uppercase">Powered by DevCfrat Studio</p>
      </nav>
    </div>
  );
};

const NavItem: React.FC<{ icon: string; label: string; active?: boolean; onClick?: () => void }> = ({ icon, label, active, onClick }) => (
  <button onClick={onClick} className={`flex flex-col items-center gap-1 ${active ? 'text-primary' : 'text-[#5e8d8d]'} active:scale-95 transition-transform`}>
    <div className="flex h-8 items-center justify-center">
      <span className={`material-symbols-outlined text-[28px] ${active ? 'material-symbols-fill' : ''}`}>{icon}</span>
    </div>
    <p className={`text-[10px] leading-normal tracking-wide ${active ? 'font-bold' : 'font-medium'}`}>{label}</p>
  </button>
);

export default OrdersList;
